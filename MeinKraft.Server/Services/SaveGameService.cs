using MeinKraft;
using MemoryPack;
using Microsoft.Extensions.Options;
using OpenTK.Mathematics;

/// <summary>
/// Owns the lifetime of a save session: which file is active, serialising and
/// deserialising <see cref="MeinKraftSave"/>, and flushing dirty chunks to the
/// chunk database. Nothing outside this class needs to know the save path.
/// </summary>
public class SaveGameService : ISaveGameService
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IChunkDbCompressed _chunkDb;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly ServerConfig _config;
    private readonly ISessionConfig _sessionConfig;
    private readonly GameTimer _gameTimer;
    private readonly IGameLogger _gameLogger;

    // Needed by LoadDatabase to reset per-client chunk visibility after a world switch.
    public Dictionary<int, ServerPlayer> Clients { get; set; } = [];

    // ── Session state — set once by InitialiseSession, never changes ──────────

    private SaveTarget? _target;

    // ── Server state the service needs to read when saving ───────────────────
    // These are injected or set via ISaveGameSession before the first Save call.

    public int Seed { get; set; }
    public long SimulationCurrentFrame { get; set; }
    public int LastMonsterId { get; set; }
    public Dictionary<string, PacketServerPlayerStats> PlayerStats { get; set; }
    public Dictionary<string, byte[]> ModData { get; set; }
    public Dictionary<string, Inventory> Inventory { get; set; }

    /// <summary>
    /// Callbacks registered by server subsystems that must flush their state
    /// into the save before <see cref="Save"/> serialises it.
    /// Replaces <c>Server.OnSave</c>.
    /// </summary>
    public List<Action> OnSave { get; } = [];

    // ── Constructor ───────────────────────────────────────────────────────────

    public SaveGameService(
        IChunkDbCompressed chunkDb, 
        IServerMapStorage serverMapStorage,
        IOptions<ServerConfig> options,
        ISessionConfig sessionConfig,
        IGameLogger gameLogger,
        GameTimer gameTimer)
    {
        _chunkDb = chunkDb;
        _serverMapStorage = serverMapStorage;
        _config = options.Value;
        _gameLogger = gameLogger;
        _sessionConfig = sessionConfig;
        _gameTimer = gameTimer;
    }

    // ── ISaveGameService ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public byte[] Save()
    {
        // Let all subsystems flush pending state before we snapshot.
        for (int i = 0; i < OnSave.Count; i++)
            OnSave[i]();

        MeinKraftSave save = new()
        {
            Seed = Seed,
            SimulationCurrentFrame = SimulationCurrentFrame,
            TimeOfDay = _gameTimer.Time.Ticks,
            LastMonsterId = LastMonsterId,
            PlayerStats = PlayerStats,
            ModData = ModData,
        };

        SaveAllLoadedChunks();

        if (!_config.IsCreative)
            save.Inventory = Inventory;

        return MemoryPackSerializer.Serialize(save);
    }

    /// <inheritdoc/>
    public void SaveGlobalData() => _chunkDb.SetGlobalData(Save());

    /// <inheritdoc/>
    public void Load()
    {
        _chunkDb.Open(ResolvedPath);
        byte[] globalData = _chunkDb.GetGlobalData();

        if (globalData == null)
        {
            // First load of this world — sentinel file exists (created by POST
            // /api/worlds) but no chunk data written yet.
            Seed = _config.RandomSeed
                ? new Random().Next()
                : _config.Seed;

            _chunkDb.SetGlobalData(Save());
            return;
        }
        
        MeinKraftSave save = MemoryPackSerializer.Deserialize<MeinKraftSave>(globalData);
        Seed = save.Seed; ;
        _serverMapStorage.Reset(
            _serverMapStorage.MapSizeX,
            _serverMapStorage.MapSizeY,
            _serverMapStorage.MapSizeZ);

        Inventory = _config.IsCreative
            ? new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase)
            : save.Inventory;

        PlayerStats = save.PlayerStats;
        SimulationCurrentFrame = (int)save.SimulationCurrentFrame;
        LastMonsterId = save.LastMonsterId;
        ModData = save.ModData;
    }

    /// <inheritdoc/>
    public void SaveAll()
    {
        int chunksX = _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize;
        int chunksY = _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize;
        int chunksZ = _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize;

        for (int x = 0; x < chunksX; x++)
            for (int y = 0; y < chunksY; y++)
                for (int z = 0; z < chunksZ; z++)
                {
                    ServerChunk chunk = _serverMapStorage.GetChunkValid(x, y, z);
                    if (chunk != null)
                        DoSaveChunk(x, y, z, chunk);
                }

        SaveGlobalData();
    }

    /// <inheritdoc/>
    public void DoSaveChunk(int x, int y, int z, ServerChunk chunk)
        => ChunkDbHelper.SetChunk(_chunkDb, x, y, z, MemoryPackSerializer.Serialize(chunk));

    /// <inheritdoc/>
    public bool LoadDatabase(string filename)
    {
        SaveAll();

        _chunkDb.InnerChunkDb.ClearTemporaryChunks();
        _serverMapStorage.Clear();
        Load();

        foreach (KeyValuePair<int, ServerPlayer> k in Clients)
        {
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            k.Value.chunksseenTime.Clear();
        }

        return true;
    }

    /// <inheritdoc/>
    public bool BackupDatabase(string backupFilename)
    {
        if (!GameStorePath.IsValidName(backupFilename))
        {
            return false;
        }

        if (!Directory.Exists(GameStorePath.gamepathbackup))
            Directory.CreateDirectory(GameStorePath.gamepathbackup);

        string finalFilename = Path.Combine(
            GameStorePath.gamepathbackup,
            $"{backupFilename}{FileConstatns.DbFileExtension}");

        _chunkDb.Backup(finalFilename);
        return true;
    }

    /// <inheritdoc/>
    public void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename)
    {
        if (!GameStorePath.IsValidName(filename))
        {
            Console.WriteLine("Invalid backup filename: " + filename);
            return;
        }

        if (!Directory.Exists(GameStorePath.gamepathbackup))
            Directory.CreateDirectory(GameStorePath.gamepathbackup);

        string finalFilename = Path.Combine(
            GameStorePath.gamepathbackup,
            $"{filename}{FileConstatns.DbFileExtension}");

        List<DbChunk> dbChunks = [];
        foreach (Vector3i pos in chunkPositions)
        {
            ushort[] data = GetChunk(pos.X, pos.Y, pos.Z);
            if (data == null)
                continue;

            dbChunks.Add(new DbChunk
            {
                Position = new Vector3i(
                    pos.X / GameConstants.ServerChunkSize,
                    pos.Y / GameConstants.ServerChunkSize,
                    pos.Z / GameConstants.ServerChunkSize),
                Chunk = MemoryPackSerializer.Serialize(new ServerChunk { Data = data }),
            });
        }

        if (dbChunks.Count != 0)
        {
            _chunkDb.SetChunksToFile(dbChunks, finalFilename);
            Console.WriteLine($"Saved {dbChunks.Count} chunk(s) to database.");
        }
        else
        {
            Console.WriteLine("0 chunks selected. Nothing to do.");
        }
    }

    /// <inheritdoc/>
    public ushort[] GetChunk(int x, int y, int z)
    {
        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
            return null;

        return _serverMapStorage
            .GetChunkValid(
                x / GameConstants.ServerChunkSize,
                y / GameConstants.ServerChunkSize,
                z / GameConstants.ServerChunkSize)
            ?.Data;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Flushes all chunks that have been modified since the last save to the
    /// chunk database, batching writes in groups of 200 to avoid large
    /// single transactions.
    /// </summary>
    private void SaveAllLoadedChunks()
    {
        int chunksX = _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize;
        int chunksY = _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize;
        int chunksZ = _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize;

        List<DbChunk> toSave = [];

        for (int cx = 0; cx < chunksX; cx++)
            for (int cy = 0; cy < chunksY; cy++)
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    ServerChunk chunk = _serverMapStorage.GetChunkValid(cx, cy, cz);
                    if (chunk == null || !chunk.DirtyForSaving)
                        continue;

                    chunk.DirtyForSaving = false;
                    toSave.Add(new DbChunk
                    {
                        Position = new Vector3i(cx, cy, cz),
                        Chunk = MemoryPackSerializer.Serialize(chunk),
                    });

                    if (toSave.Count > 200)
                    {
                        _chunkDb.SetChunks(toSave);
                        toSave.Clear();
                    }
                }

        // Flush any remaining chunks below the batch threshold.
        _chunkDb.SetChunks(toSave);
    }

    private string ResolvedPath
   => GameStorePath.WorldSavePath(_sessionConfig.SavePath, _sessionConfig.WorldName);
}

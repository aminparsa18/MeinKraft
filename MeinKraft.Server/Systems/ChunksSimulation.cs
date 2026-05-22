using MeinKraft;
using Microsoft.Extensions.Options;
using OpenTK.Mathematics;

public class ServerSystemChunksSimulation : ServerSystem
{
    private Random _rnd;
    private IBlockRegistry _blockRegistry;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly ServerConfig _config;
    private readonly ISaveGameService _saveGameService;
    private readonly IClientRegistry _serverClientService;

    public ServerSystemChunksSimulation(IBlockRegistry blockRegistry, IServerMapStorage serverMapStorage, IModEvents modEvents,
        IOptions<ServerConfig> options, ISaveGameService saveGameService, IClientRegistry serverClientService) : base(modEvents)
    {
        _blockRegistry = blockRegistry;
        _serverMapStorage = serverMapStorage;
        _config = options.Value;
        _saveGameService = saveGameService;
        _serverClientService = serverClientService;
    }

    public int[] MonsterTypesUnderground = [1, 2];
    public int[] MonsterTypesOnGround = [0, 3, 4];

    private const int ChunksSimulated = 1;
    private const int MonsterSpawnMaxTries = 500;
    private const int MinMonstersPerChunk = 1;

    private int simulationInterval = -1;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override void Initialize()
    {
        // Frames between full chunk updates: once per 10 minutes of simulation time
        simulationInterval = (int)(1f / GameConstants.SIMULATION_STEP_LENGTH) * 60 * 10;
        _rnd = new Random();
    }

    protected override void OnUpdate(ServerGameService server, float dt)
    {
        unchecked
        {
            for (int i = 0; i < ChunksSimulated; i++)
            {
                SimulateNextChunk(server);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk simulation
    // -------------------------------------------------------------------------

    private void SimulateNextChunk(ServerGameService server)
    {
        unchecked
        {
            foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
            {
                Vector3i playerPos = server.PlayerBlockPosition(k.Value);

                long oldestTime = long.MaxValue;
                Vector3i oldestPos = default;

                foreach (Vector3i chunkPos in ChunksAroundPlayer(server, playerPos))
                {
                    if (!VectorUtils.IsValidPos(_serverMapStorage, chunkPos.X, chunkPos.Y, chunkPos.Z))
                    {
                        continue;
                    }

                    ServerChunk chunk = _serverMapStorage.GetChunkValid(
                        server.InvertChunk(chunkPos.X),
                        server.InvertChunk(chunkPos.Y),
                        server.InvertChunk(chunkPos.Z));

                    if (chunk?.Data == null)
                    {
                        continue;
                    }

                    // Guard against future timestamps
                    if (chunk.LastUpdate > _saveGameService.SimulationCurrentFrame)
                    {
                        chunk.LastUpdate = _saveGameService.SimulationCurrentFrame;
                    }

                    if (chunk.LastUpdate < oldestTime)
                    {
                        oldestTime = chunk.LastUpdate;
                        oldestPos = chunkPos;
                    }

                    if (!chunk.IsPopulated)
                    {
                        chunk.IsPopulated = true;
                        PopulateChunk(server, chunkPos);
                    }
                }

                if (_saveGameService.SimulationCurrentFrame - oldestTime > simulationInterval)
                {
                    UpdateChunk(server, oldestPos);

                    ServerChunk chunk = _serverMapStorage.GetChunkValid(
                        server.InvertChunk(oldestPos.X),
                        server.InvertChunk(oldestPos.Y),
                        server.InvertChunk(oldestPos.Z));

                    chunk.LastUpdate = _saveGameService.SimulationCurrentFrame;
                    return;
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk population
    // -------------------------------------------------------------------------

    private void PopulateChunk(ServerGameService server, Vector3i chunkPos)
    {
        unchecked
        {
            int x = (int)(chunkPos.X * server.InvertedChunkSize);
            int y = (int)(chunkPos.Y * server.InvertedChunkSize);
            int z = (int)(chunkPos.Z * server.InvertedChunkSize);
            ModEvents.RaisePopulateChunk(x, y, z);
        }
    }

    // -------------------------------------------------------------------------
    // Chunk update (block ticks + monsters)
    // -------------------------------------------------------------------------

    private void UpdateChunk(ServerGameService server, Vector3i chunkPos)
    {
        unchecked
        {
            if (_config.Monsters)
            {
                AddMonsters(server, chunkPos);
            }

            for (int xx = 0; xx < GameConstants.ServerChunkSize; xx++)
            {
                int px = xx + chunkPos.X;
                for (int yy = 0; yy < GameConstants.ServerChunkSize; yy++)
                {
                    int py = yy + chunkPos.Y;
                    for (int zz = 0; zz < GameConstants.ServerChunkSize; zz++)
                    {
                        ModEvents.RaiseBlockUpdate(px, py, zz + chunkPos.Z);
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk enumeration
    // -------------------------------------------------------------------------

    private IEnumerable<Vector3i> ChunksAroundPlayer(ServerGameService server, Vector3i playerPos)
    {
        int zDrawDistance = server.InvertChunk(_serverMapStorage.MapSizeZ);
        unchecked
        {
            for (int x = -server.ChunkDrawDistance; x <= server.ChunkDrawDistance; x++)
            {
                for (int y = -server.ChunkDrawDistance; y <= server.ChunkDrawDistance; y++)
                {
                    for (int z = 0; z < zDrawDistance; z++)
                    {
                        Vector3i p = new(
                            playerPos.X + (x * GameConstants.ServerChunkSize),
                            playerPos.Y + (y * GameConstants.ServerChunkSize),
                            z * GameConstants.ServerChunkSize);

                        if (VectorUtils.IsValidPos(_serverMapStorage, p.X, p.Y, p.Z))
                        {
                            yield return p;
                        }
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Monster spawning
    // -------------------------------------------------------------------------

    public void AddMonsters(ServerGameService server, Vector3i chunkPos)
    {
        ServerChunk chunk = _serverMapStorage.GetChunkValid(
            chunkPos.X / GameConstants.ServerChunkSize,
            chunkPos.Y / GameConstants.ServerChunkSize,
            chunkPos.Z / GameConstants.ServerChunkSize);

        for (int tries = 0; chunk.Monsters.Count < MinMonstersPerChunk && tries < MonsterSpawnMaxTries; tries++)
        {
            int px = chunkPos.X + _rnd.Next(GameConstants.ServerChunkSize);
            int py = chunkPos.Y + _rnd.Next(GameConstants.ServerChunkSize);
            int pz = chunkPos.Z + _rnd.Next(GameConstants.ServerChunkSize);

            if (!IsValidSpawnPosition(px, py, pz))
            {
                continue;
            }

            int monsterType = ChooseMonsterType(px, py, pz);

            if (IsOpenGround(px, py, pz))
            {
                chunk.Monsters.Add(new Monster
                {
                    X = px,
                    Y = py,
                    Z = pz,
                    Id = _saveGameService.LastMonsterId++,
                    Health = 20,
                    MonsterType = monsterType
                });
            }
        }
    }

    private bool IsValidSpawnPosition(int px, int py, int pz)
        => VectorUtils.IsValidPos(_serverMapStorage, px, py, pz) &&
        VectorUtils.IsValidPos(_serverMapStorage, px, py, pz + 1) &&
        VectorUtils.IsValidPos(_serverMapStorage, px, py, pz - 1);

    private int ChooseMonsterType(int px, int py, int pz)
    {
        int surfaceHeight = VectorUtils.BlockHeight(_serverMapStorage, 0, px, py);
        return pz >= surfaceHeight
            ? MonsterTypesOnGround[_rnd.Next(MonsterTypesOnGround.Length)]
            : MonsterTypesUnderground[_rnd.Next(MonsterTypesUnderground.Length)];
    }

    private bool IsOpenGround(int px, int py, int pz)
        => _serverMapStorage.GetBlock(px, py, pz) == 0 &&
        _serverMapStorage.GetBlock(px, py, pz + 1) == 0 &&
        _serverMapStorage.GetBlock(px, py, pz - 1) != 0 &&
        !_blockRegistry.BlockTypes[_serverMapStorage.GetBlock(px, py, pz - 1)].IsFluid();
}
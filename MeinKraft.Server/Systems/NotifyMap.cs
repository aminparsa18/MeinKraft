using MeinKraft;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// The main system for loading, unloading, and streaming map chunks to players.
/// Each tick it finds the nearest unseen chunk for every connected client and
/// sends it, continuing until either all dirty chunks are resolved or 10 ms of
/// wall time has elapsed to avoid blocking the server loop.
/// </summary>
public class ServerSystemNotifyMap : ServerSystem
{
    private readonly ICompression _compression;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly IClientRegistry _serverClientService;
    private readonly ISaveGameService _saveGameService;
    private readonly IServerPacketService _serverPacketService;

    public ServerSystemNotifyMap(IModEvents modEvents, ICompression compression, IServerMapStorage serverMapStorage, IClientRegistry serverClientService, 
        ISaveGameService saveGameService, IServerPacketService serverPacketService) : base(modEvents)
    {
        _compression = compression;
        _serverMapStorage = serverMapStorage;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
        _saveGameService = saveGameService;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(ServerGameService server, float dt)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool sentAny = true;

        while (sentAny && stopwatch.ElapsedMilliseconds < 10)
        {
            sentAny = false;
            foreach ((int clientId, ServerPlayer? client) in _serverClientService.Clients)
            {
                if (client.State == ClientStateOnServer.Connecting)
                {
                    continue;
                }

                Vector3i playerPos = server.PlayerBlockPosition(client);
                Vector3i? nearest = FindNearestDirtyChunk(server, clientId, playerPos);

                if (nearest == null)
                {
                    continue;
                }

                LoadAndSendChunk(server, clientId, nearest.Value, stopwatch);
                sentAny = true;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Searches the area around <paramref name="playerPos"/> for the nearest
    /// chunk that has not yet been sent to <paramref name="clientId"/>.
    /// The search area is bounded by the configured draw distance.
    /// </summary>
    /// <returns>
    /// The chunk-space coordinates of the nearest unseen chunk,
    /// or <c>null</c> if all chunks in range have already been sent.
    /// </returns>
    private  Vector3i? FindNearestDirtyChunk(ServerGameService server, int clientId, Vector3i playerPos)
    {
        int px = playerPos.X / GameConstants.ServerChunkSize;
        int py = playerPos.Y / GameConstants.ServerChunkSize;
        int pz = playerPos.Z / GameConstants.ServerChunkSize;

        int halfXY = MapAreaSize(server) / GameConstants.ServerChunkSize / 2;
        int halfZ = MapAreaSizeZ(server) / GameConstants.ServerChunkSize / 2;
        int startX = Math.Max(0, px - halfXY);
        int startY = Math.Max(0, py - halfXY);
        int startZ = Math.Max(0, pz - halfZ);

        int mapSizeX = _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize;
        int mapSizeY = _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize;
        int mapSizeZ = _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize;

        int endX = Math.Min(mapSizeX - 1, px + halfXY);
        int endY = Math.Min(mapSizeY - 1, py + halfXY);
        int endZ = Math.Min(mapSizeZ - 1, pz + halfZ);

        int nearestDist = int.MaxValue;
        Vector3i? nearest = null;

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    if (server.ClientSeenChunk(clientId, x, y, z))
                    {
                        continue;
                    }

                    int dx = px - x;
                    int dy = py - y;
                    int dz = pz - z;
                    int dist = (dx * dx) + (dy * dy) + (dz * dz);

                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = new Vector3i(x, y, z);
                    }
                }
            }
        }

        return nearest;
    }

    // -------------------------------------------------------------------------
    // Chunk loading and sending
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the chunk at chunk-space position <paramref name="chunkPos"/> and
    /// sends it to <paramref name="clientId"/> if it has not already been sent.
    /// </summary>
    private void LoadAndSendChunk(ServerGameService server, int clientId, Vector3i chunkPos, Stopwatch stopwatch)
    {
        _serverMapStorage.LoadChunk(chunkPos.X, chunkPos.Y, chunkPos.Z);

        if (!server.ClientSeenChunk(clientId, chunkPos.X, chunkPos.Y, chunkPos.Z))
        {
            Vector3i globalPos = new(
                chunkPos.X * GameConstants.ServerChunkSize,
                chunkPos.Y * GameConstants.ServerChunkSize,
                chunkPos.Z * GameConstants.ServerChunkSize);

            SendChunk(server, clientId, globalPos, chunkPos);
        }
    }

    /// <summary>
    /// Sends a chunk and its associated heightmap data to a client.
    /// <list type="bullet">
    ///   <item>Empty chunks (solid air) are skipped — only the heightmap is sent.</item>
    ///   <item>Non-empty chunks are compressed and split into 1 KB parts before sending.</item>
    ///   <item>A final <c>Chunk_</c> packet is always sent to signal chunk completion.</item>
    /// </list>
    /// </summary>
    /// <param name="server">The running server instance.</param>
    /// <param name="clientId">The client to send the chunk to.</param>
    /// <param name="globalPos">Block-space origin of the chunk.</param>
    /// <param name="chunkPos">Chunk-space coordinates of the chunk.</param>
    private void SendChunk(ServerGameService server, int clientId, Vector3i globalPos, Vector3i chunkPos)
    {
        ServerPlayer client = _serverClientService.Clients[clientId];
        ServerChunk chunk = _serverMapStorage.GetChunk(globalPos.X, globalPos.Y, globalPos.Z);
        server.ClientSeenChunkSet(clientId, chunkPos.X, chunkPos.Y, chunkPos.Z, (int)_saveGameService.SimulationCurrentFrame);

        bool isSolid = IsSolidChunk(chunk.Data);
        int firstBlock = chunk.Data?[0] ?? -1;
        int nonZero = chunk.Data?.Count(b => b != 0) ?? -1;

        byte[] compressedChunk = null;

        if (!IsSolidChunk(chunk.Data) || chunk.Data[0] != 0)
        {
            // Compress and queue block data for sending
            compressedChunk = server.CompressChunkNetwork(chunk.Data);

            // Send heightmap for this column
            ReadOnlySpan<byte> heightmapBytes = MemoryMarshal.AsBytes(
                _serverMapStorage.Heightmap.GetOrAllocChunk(globalPos.X, globalPos.Y).AsSpan());

            Packet_ServerHeightmapChunk heightmapPacket = new()
            {
                X = globalPos.X,
                Y = globalPos.Y,
                SizeX = GameConstants.ServerChunkSize,
                SizeY = GameConstants.ServerChunkSize,
                CompressedHeightmap = _compression.Compress(heightmapBytes)
            };
            _serverPacketService.SendPacket(clientId, server.Serialize(new Packet_Server
            {
                Id = Packet_ServerIdEnum.HeightmapChunk,
                HeightmapChunk = heightmapPacket
            }));
            client.heightmapchunksseen[new Vector2i(globalPos.X, globalPos.Y)] = (int)_saveGameService.SimulationCurrentFrame;
        }

        // Send block data in 1 KB parts
        if (compressedChunk != null)
        {
            foreach (byte[] part in server.Parts(compressedChunk, 1024))
            {
                _serverPacketService.SendPacket(clientId, server.Serialize(new Packet_Server
                {
                    Id = Packet_ServerIdEnum.ChunkPart,
                    ChunkPart = new Packet_ServerChunkPart { CompressedChunkPart = part }
                }));
            }
        }

        // Signal chunk completion
        _serverPacketService.SendPacket(clientId, server.Serialize(new Packet_Server
        {
            Id = Packet_ServerIdEnum.Chunk_,
            Chunk_ = new Packet_ServerChunk
            {
                X = globalPos.X,
                Y = globalPos.Y,
                Z = globalPos.Z,
                SizeX = GameConstants.ServerChunkSize,
                SizeY = GameConstants.ServerChunkSize,
                SizeZ = GameConstants.ServerChunkSize
            }
        }));
    }

    // -------------------------------------------------------------------------
    // Area size helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns the XY map streaming area size in blocks based on the configured draw distance.</summary>
    public static int MapAreaSize(ServerGameService server) => server.ChunkDrawDistance * GameConstants.ServerChunkSize * 2;

    /// <summary>Returns the Z map streaming area size in blocks. Currently mirrors the XY area.</summary>
    public static int MapAreaSizeZ(ServerGameService server) => MapAreaSize(server);

    private static bool IsSolidChunk(ushort[] chunk)
    {
        for (int i = 0; i <= chunk.GetUpperBound(0); i++)
        {
            if (chunk[i] != chunk[0])
            {
                return false;
            }
        }

        return true;
    }
}
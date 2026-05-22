using MemoryPack;
using OpenTK.Mathematics;
using PointG = System.Drawing.Point;

public partial class ServerGameService
{
    // generates a new spawn near initial spawn if initial spawn is in water
    public Vector3i DontSpawnPlayerInWater(Vector3i initialSpawn)
    {
        if (IsPlayerPositionDry(initialSpawn.X, initialSpawn.Y, initialSpawn.Z))
        {
            return initialSpawn;
        }

        //find shore
        //bonus +10 because player shouldn't be spawned too close to shore.
        bool bonusset = false;
        int bonus = -1;
        Vector3i pos = initialSpawn;
        for (int i = 0; i < (playerareasize / 4) - 5; i++)
        {
            if (IsPlayerPositionDry(pos.X, pos.Y, pos.Z))
            {
                if (!bonusset)
                {
                    bonus = 10;
                    bonusset = true;
                }
            }

            if (bonusset && bonus-- < 0)
            {
                break;
            }

            pos.X++;
            int newblockheight = VectorUtils.BlockHeight(_serverMapStorage, 0, pos.X, pos.Y);
            pos.Z = newblockheight + 1;
        }

        return pos;
    }

    private bool IsPlayerPositionDry(int x, int y, int z)
    {
        for (int i = 0; i < 4; i++)
        {
            if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z - i))
            {
                int blockUnderPlayer = _serverMapStorage.GetBlock(x, y, z - i);
                if (_blockRegistry.BlockTypes[blockUnderPlayer].IsFluid())
                {
                    return false;
                }
            }
        }

        return true;
    }

    public int playerareasize = 256;
    public int centerareasize = 128;

    private PointG PlayerArea(int playerId) => VectorUtils.PlayerArea(playerareasize, centerareasize, PlayerBlockPosition(_serverClientService.Clients[playerId]));

    private IEnumerable<Vector3i> PlayerAreaChunks(int playerId)
    {
        PointG p = PlayerArea(playerId);
        for (int x = 0; x < playerareasize / GameConstants.ServerChunkSize; x++)
        {
            for (int y = 0; y < playerareasize / GameConstants.ServerChunkSize; y++)
            {
                for (int z = 0; z < _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize; z++)
                {
                    Vector3i v = new(p.X + (x * GameConstants.ServerChunkSize), p.Y + (y * GameConstants.ServerChunkSize), z * GameConstants.ServerChunkSize);
                    if (VectorUtils.IsValidPos(_serverMapStorage, v.X, v.Y, v.Z))
                    {
                        yield return v;
                    }
                }
            }
        }
    }

    // Interfaces to manipulate server's map.
    public void SetBlock(int x, int y, int z, int blocktype)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            SetBlockAndNotify(x, y, z, blocktype);
        }
    }

    public int GetBlock(int x, int y, int z)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            return _serverMapStorage.GetBlock(x, y, z);
        }

        return 0;
    }

    public int GetHeight(int x, int y) => VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);

    public void SetChunk(int x, int y, int z, ushort[] data)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            x /= GameConstants.ServerChunkSize;
            y /= GameConstants.ServerChunkSize;
            z /= GameConstants.ServerChunkSize;
            ServerChunk c = _serverMapStorage.GetChunkValid(x, y, z);
            c ??= new ServerChunk();
            c.Data = data;
            c.DirtyForSaving = true;
            _serverMapStorage.SetChunkValid(x, y, z, c);
            // update related chunk at clients
            foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public void SetChunks(Dictionary<Vector3i, ushort[]> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Vector3i, ushort[]> k in chunks)
        {
            if (k.Value == null)
            {
                continue;
            }

            // TODO: check bounds.
            ServerChunk c = _serverMapStorage.GetChunkValid(k.Key.X, k.Key.Y, k.Key.Z);
            c ??= new ServerChunk();
            c.Data = k.Value;
            c.DirtyForSaving = true;
            _serverMapStorage.SetChunkValid(k.Key.X, k.Key.Y, k.Key.Z, c);
        }

        // update related chunk at clients
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            //TODO wrong
            //k.Value.chunksseen.Clear();
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
        }
    }

    public void SetChunks(int offsetX, int offsetY, int offsetZ, Dictionary<Vector3i, ushort[]> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Vector3i, ushort[]> k in chunks)
        {
            if (k.Value == null)
            {
                continue;
            }

            // TODO: check bounds.
            ServerChunk c = _serverMapStorage.GetChunkValid(k.Key.X + offsetX, k.Key.Y + offsetY, k.Key.Z + offsetZ);
            c ??= new ServerChunk();
            c.Data = k.Value;
            c.DirtyForSaving = true;
            _serverMapStorage.SetChunkValid(k.Key.X + offsetX, k.Key.Y + offsetY, k.Key.Z + offsetZ, c);
        }

        // update related chunk at clients
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            //TODO wrong
            //k.Value.chunksseen.Clear();
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
        }
    }

    public ushort[] GetChunk(int x, int y, int z)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            x /= GameConstants.ServerChunkSize;
            y /= GameConstants.ServerChunkSize;
            z /= GameConstants.ServerChunkSize;
            return _serverMapStorage.GetChunkValid(x, y, z).Data;
        }

        return null;
    }

    public void DeleteChunk(int x, int y, int z)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            x /= GameConstants.ServerChunkSize;
            y /= GameConstants.ServerChunkSize;
            z /= GameConstants.ServerChunkSize;
            ChunkDbHelper.DeleteChunk(_chunkDb, x, y, z);
            _serverMapStorage.SetChunkValid(x, y, z, null);
            // update related chunk at clients
            foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public void DeleteChunks(List<Vector3i> chunkPositions)
    {
        List<Vector3i> chunks = [];
        foreach (Vector3i pos in chunkPositions)
        {
            if (VectorUtils.IsValidPos(_serverMapStorage, pos.X, pos.Y, pos.Z))
            {
                int x = pos.X / GameConstants.ServerChunkSize;
                int y = pos.Y / GameConstants.ServerChunkSize;
                int z = pos.Z / GameConstants.ServerChunkSize;
                _serverMapStorage.SetChunkValid(x, y, z, null);
                chunks.Add(new Vector3i() { X = x, Y = y, Z = z });
            }
        }

        if (chunks.Count != 0)
        {
            global::ChunkDbHelper.DeleteChunks(_chunkDb, chunks);
            // force to update chunks at clients
            foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public int[] GetMapSize() => [_serverMapStorage.MapSizeX, _serverMapStorage.MapSizeY, _serverMapStorage.MapSizeZ];

    public ushort[] GetChunkFromDatabase(int x, int y, int z, string filename)
    {
        if (VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
        {
            if (!GameStorePath.IsValidName(filename))
            {
                Console.WriteLine($"Invalid backup filename: {filename}");
                return null;
            }

            if (!Directory.Exists(GameStorePath.gamepathbackup))
            {
                Directory.CreateDirectory(GameStorePath.gamepathbackup);
            }

            string finalFilename = Path.Combine(GameStorePath.gamepathbackup, $"{filename}{FileConstatns.DbFileExtension}");

            x /= GameConstants.ServerChunkSize;
            y /= GameConstants.ServerChunkSize;
            z /= GameConstants.ServerChunkSize;

            byte[] serializedChunk = global::ChunkDbHelper.GetChunkFromFile(_chunkDb, x, y, z, finalFilename);
            if (serializedChunk != null)
            {
                ServerChunk c = DeserializeChunk(serializedChunk);
                return c.Data;
            }
        }

        return null;
    }

    public Dictionary<Vector3i, ushort[]> GetChunksFromDatabase(List<Vector3i> chunks, string filename)
    {
        if (chunks == null)
        {
            return null;
        }

        if (!GameStorePath.IsValidName(filename))
        {
            Console.WriteLine("Invalid backup filename: " + filename);
            return null;
        }

        if (!Directory.Exists(GameStorePath.gamepathbackup))
        {
            Directory.CreateDirectory(GameStorePath.gamepathbackup);
        }

        string finalFilename = Path.Combine(GameStorePath.gamepathbackup, $"{filename}{FileConstatns.DbFileExtension}");

        Dictionary<Vector3i, ushort[]> deserializedChunks = [];
        Dictionary<Vector3i, byte[]> serializedChunks = global::ChunkDbHelper.GetChunksFromFile(_chunkDb, chunks, finalFilename);

        foreach (KeyValuePair<Vector3i, byte[]> k in serializedChunks)
        {
            ServerChunk c = null;
            if (k.Value != null)
            {
                c = DeserializeChunk(k.Value);
            }

            deserializedChunks.Add(k.Key, c.Data);
        }

        return deserializedChunks;
    }

    private ServerChunk DeserializeChunk(byte[] serializedChunk)
    {
        ServerChunk c = MemoryPackSerializer.Deserialize<ServerChunk>(serializedChunk);
        //convert savegame to new format
        if (c.DataOld != null)
        {
            c.Data = new ushort[GameConstants.ServerChunkSize * GameConstants.ServerChunkSize * GameConstants.ServerChunkSize];
            for (int i = 0; i < c.DataOld.Length; i++)
            {
                c.Data[i] = c.DataOld[i];
            }

            c.DataOld = null;
        }

        return c;
    }

    public void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename)
    {
        if (!GameStorePath.IsValidName(filename))
        {
            Console.WriteLine("Invalid backup filename: " + filename);
            return;
        }

        if (!Directory.Exists(GameStorePath.gamepathbackup))
        {
            Directory.CreateDirectory(GameStorePath.gamepathbackup);
        }

        string finalFilename = Path.Combine(GameStorePath.gamepathbackup, filename + FileConstatns.DbFileExtension);

        List<DbChunk> dbchunks = [];
        foreach (Vector3i pos in chunkPositions)
        {
            int dx = pos.X / GameConstants.ServerChunkSize;
            int dy = pos.Y / GameConstants.ServerChunkSize;
            int dz = pos.Z / GameConstants.ServerChunkSize;

            ServerChunk cc = new() { Data = GetChunk(pos.X, pos.Y, pos.Z) };
            dbchunks.Add(new DbChunk() { Position = new Vector3i() { X = dx, Y = dy, Z = dz }, Chunk = MemoryPackSerializer.Serialize(cc) });
        }

        if (dbchunks.Count != 0)
        {
            _chunkDb.SetChunksToFile(dbchunks, finalFilename);
        }
        else
        {
            Console.WriteLine(string.Format("0 chunks selected. Nothing to do."));
        }

        Console.WriteLine(string.Format("Saved {0} chunk(s) to database.", dbchunks.Count));
    }
}
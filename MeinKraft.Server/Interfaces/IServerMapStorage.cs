public interface IServerMapStorage : IMapStorage
{
    int MapSizeX { get;}
    int MapSizeY { get; }
    int MapSizeZ { get; }

    void Reset(int sizex, int sizey, int sizez);
    ServerChunk GetChunkValid(int cx, int cy, int cz);
    void Clear();
    void SetBlockNotMakingDirty(int x, int y, int z, int tileType);
    ServerChunk GetChunk(int x, int y, int z);
    void SetChunkValid(int cx, int cy, int cz, ServerChunk chunk);
    ServerChunk GetChunkAt(int chunkx, int chunky, int chunkz);
    void LoadChunk(int cx, int cy, int cz);
    ChunkedMap2d<ushort> Heightmap { get; set; }
}
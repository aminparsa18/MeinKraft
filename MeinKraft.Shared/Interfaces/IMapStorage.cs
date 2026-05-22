public interface IMapStorage
{
    int MapSizeX { get; }
    int MapSizeY { get; }
    int MapSizeZ { get; }

    int GetBlock(int x, int y, int z);
}
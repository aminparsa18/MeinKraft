public class ServerEntityId
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int ChunkZ { get; set; }
    public int Id { get; set; }

    public ServerEntityId Clone()
    {
        return new()
        {
            ChunkX = ChunkX,
            ChunkY = ChunkY,
            ChunkZ = ChunkZ,
            Id = Id
        };
    }
}

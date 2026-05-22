public interface IChunkDbCompressed : IChunkDb 
{
    IChunkDbRegion InnerChunkDb { get; }
}
using MeinKraft;
using MessagePipe;

/// <summary>
/// Wraps the raw block-write path to publish a BlockChangedEvent after every
/// block placement or removal.  Inject this wherever SetTileAndUpdate is called
/// instead of calling the voxel map directly.
///
/// This is the single choke-point that feeds ALL block-change subscribers
/// (lighting, audio, particle effects, achievements, etc.) without any of
/// them polling or scanning.
/// </summary>
public interface IBlockChangeNotifier
{
    /// <summary>
    /// Writes <paramref name="newBlockId"/> at the given world position and
    /// publishes a <see cref="BlockChangedEvent"/> with both old and new IDs.
    /// Does nothing and does not publish if the position is out of bounds.
    /// </summary>
    void SetTileAndUpdate(int worldX, int worldY, int worldZ, int newBlockId);
}

public sealed class BlockChangeNotifier : IBlockChangeNotifier
{
    private readonly IVoxelMap _voxelMap;
    private readonly IPublisher<BlockChangedEvent> _publisher;

    public BlockChangeNotifier(
        IVoxelMap voxelMap,
        IPublisher<BlockChangedEvent> publisher)
    {
        _voxelMap = voxelMap;
        _publisher = publisher;
    }

    /// <summary>
    /// Writes <paramref name="newBlockId"/> at the given world position and
    /// publishes a <see cref="BlockChangedEvent"/> with both old and new IDs.
    /// Does nothing and does not publish if the position is out of bounds.
    /// </summary>
    public void SetTileAndUpdate(int worldX, int worldY, int worldZ, int newBlockId)
    {
        if (!_voxelMap.IsValidPos(worldX, worldY, worldZ)) return;

        int oldBlockId = _voxelMap.GetBlock(worldX, worldY, worldZ);
        if (oldBlockId == newBlockId) return;   // no-op — nothing changed

        _voxelMap.SetBlockRaw(worldX, worldY, worldZ, newBlockId);

        _publisher.Publish(new BlockChangedEvent(
            worldX, worldY, worldZ,
            oldBlockId, newBlockId));
    }
}
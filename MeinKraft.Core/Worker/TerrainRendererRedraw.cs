namespace MeinKraft.Worker;

/// <summary>
/// Carries tessellated geometry for one chunk from a worker thread
/// </summary>
public readonly record struct TerrainRendererRedraw(
    Chunk Chunk,
    VerticesIndicesToLoad[] Data,
    int DataCount,
    bool DataRented);

//Block definition:
//
//      Z
//      |
//      |
//      |
//      +----- X
//     /
//    /
//   Y
//

using MeinKraft.Worker;

public interface ITerrainChunkTesselator
{
    float BlockShadow { get; set; }
    bool DarkenBlockSidesOption { get; set; }
    int TerrainTexturesPerAtlas { get; set; }
    int[] TerrainTextures1d { get; set; }
    Dictionary<int, int[]> TextureId { get; set; }
    bool ENABLE_TEXTURE_TILING { get; set; }
    bool EnableSmoothLight { get; set; }
    void RefreshBlockTypeCache();
    ChunkTessellationContext CreateContext();
    VerticesIndicesToLoad[] MakeChunk(int x, int y, int z, float[] lightLevels, ChunkTessellationContext ctx, out int retCount);
    void Start();
    void OnAtlasReady(int texturesPerAtlas);
}
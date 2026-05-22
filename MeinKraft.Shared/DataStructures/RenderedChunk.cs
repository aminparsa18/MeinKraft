
/// <summary>
/// Holds the GPU-side rendering state for a single chunk.
/// A chunk is marked <see cref="Dirty"/> on creation and whenever its visual state needs rebuilding.
/// </summary>
public class RenderedChunk
{
    /// <summary>
    /// Indicates that the chunk needs to be re-tessellated or its lighting recomputed.
    /// </summary>
    public bool Dirty { get; set; }

    /// <summary>
    /// GPU buffer identifiers (e.g. VBO/IBO handles).
    /// Null when the chunk has not been uploaded.
    /// </summary>
    public int[]? Ids { get; set; }

    /// <summary>
    /// Number of valid entries in <see cref="Ids"/>.
    /// </summary>
    public int IdsCount { get; set; }

    /// <summary>
    /// Per-vertex or per-block light data used for rendering.
    /// May be null if lighting has not been computed.
    /// </summary>
    public byte[]? Light { get; set; }

    /// <summary>
    /// True when <see cref="Light"/> was rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/>
    /// by <c>ModDrawTerrain.CalculateShadows</c> and must be returned to the pool
    /// rather than simply nulled when this chunk is unloaded.
    /// </summary>
    public bool LightRented { get; set; }

    /// <summary>
    /// Releases pooled resources held by this chunk (if any).
    /// Safe to call multiple times.
    /// </summary>
    public void ReleaseLight()
    {
        if (Light != null && LightRented)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(Light);
        }

        Light = null;
        LightRented = false;
    }
}
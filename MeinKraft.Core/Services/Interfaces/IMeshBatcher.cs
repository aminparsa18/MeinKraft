//This class is a render batch manager — it groups 3D models by texture so the GPU doesn't have to switch textures 
//constantly (which is expensive). You register models with Add, they get a slot ID, and every frame Draw renders
//everything in two passes: solid geometry first (to fill the depth buffer), then transparent geometry 
//(water, glass, etc.) on top with back-face culling disabled so both sides of surfaces show.

/// <summary>
/// Manages a pool of renderable 3D models, batching draw calls by texture
/// and separating solid from transparent geometry for correct render ordering.
/// </summary>
/// <remarks>
/// Models are stored in a fixed-size slot array. Freed slots are tracked in a
/// <see cref="Stack{T}"/> free-list so new additions reuse slots before growing
/// the active count. Call <see cref="Add"/> to register a model and
/// <see cref="Remove"/> to release it. Call <see cref="Draw"/> once per frame.
/// </remarks>
public interface IMeshBatcher
{
    /// <summary>
    /// Registers a model in the batch and returns its assigned slot ID.
    /// The ID is stable until <see cref="Remove"/> is called with it.
    /// </summary>
    /// <param name="modelData">The geometry data used to create the GPU model.</param>
    /// <param name="transparent">
    ///     <c>true</c> if this model should be rendered in the transparent pass
    ///     (after all solid geometry); <c>false</c> for the solid pass.
    /// </param>
    /// <param name="texture">OpenGL texture handle to associate with this model.</param>
    /// <param name="centerX">World-space X centre of the model's bounding sphere.</param>
    /// <param name="centerY">World-space Y centre of the model's bounding sphere.</param>
    /// <param name="centerZ">World-space Z centre of the model's bounding sphere.</param>
    /// <param name="radius">Radius of the model's bounding sphere, used for frustum culling.</param>
    /// <returns>The slot ID representing this model. Pass it to <see cref="Remove"/> later.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more than <see cref="MaxTextures"/> distinct textures are registered.
    /// </exception>
    int Add(GeometryModel modelData, bool transparent, int texture, float centerX, float centerY, float centerZ, float radius);

    /// <summary>
    /// Removes all active models from the batch, releasing their GPU resources
    /// and resetting the free-list. This also frees all GPU memory — it is not
    /// a lightweight reset.
    /// </summary>
    void Clear();

    /// <summary>
    /// Renders all registered models for the current frame.
    /// Solid models are drawn first (to populate the depth buffer), followed by
    /// transparent models (with back-face culling disabled) to ensure correct blending.
    /// </summary>
    /// <param name="playerPositionX">Player world-space X coordinate.</param>
    /// <param name="playerPositionY">Player world-space Y coordinate.</param>
    /// <param name="playerPositionZ">Player world-space Z coordinate.</param>
    void Draw(float playerPositionX, float playerPositionY, float playerPositionZ);

    /// <summary>
    /// Releases the model at the given slot ID, freeing its GPU resources
    /// and returning the slot to the free-list for reuse.
    /// </summary>
    /// <param name="id">The slot ID previously returned by <see cref="Add"/>.</param>
    void Remove(int id);

    /// <summary>
    /// Returns the total number of triangles currently active in the batch.
    /// Each triangle is represented by 3 indices.
    /// </summary>
    int TotalTriangleCount();

    // Called from worker thread — no OpenGL, just enqueues
    void StageChunk(Chunk chunk, VerticesIndicesToLoad[] meshes, int meshCount, bool dataRented);

    void StageUnload(Chunk chunk);

    // Called from main thread at start of each frame — drains staged ops, uploads to GPU
    void FlushPendingUploads(int maxUploadsPerFrame = 512);
}
using OpenTK.Mathematics;

/// <summary>
/// Abstracts the software matrix stack and draw-call dispatch used throughout the renderer.
/// Replaces the fixed-function OpenGL matrix pipeline with explicit push/pop/multiply
/// operations backed by <see cref="Stack{Matrix4}"/> and forwarded to the shader via
/// <see cref="SetMatrixUniformProjection"/> and the implicit model-view upload inside
/// <see cref="DrawModel"/>, <see cref="DrawModelData"/>, and <see cref="DrawModels"/>.
/// </summary>
public interface IMeshDrawer
{
    // ── Matrix stacks ─────────────────────────────────────────────────────────

    /// <summary>
    /// Model-view matrix stack. The top of the stack is the active transform
    /// applied to every subsequent draw call.
    /// </summary>
    Stack<Matrix4> mvMatrix { get; set; }

    /// <summary>
    /// Projection matrix stack. Manipulated via <see cref="GLMatrixModeProjection"/>
    /// together with the push / pop / load operations.
    /// </summary>
    Stack<Matrix4> pMatrix { get; set; }

    // ── Draw calls ────────────────────────────────────────────────────────────

    /// <summary>
    /// Uploads the current model-view matrix uniform and issues a draw call for
    /// <paramref name="model"/>.
    /// </summary>
    void DrawModel(GeometryModel model);

    /// <summary>
    /// Uploads the current model-view matrix uniform and draws raw interleaved
    /// geometry from <paramref name="data"/> without going through a registered model slot.
    /// </summary>
    void DrawModelData(GeometryModel data);

    /// <summary>
    /// Uploads the current model-view matrix uniform and dispatches draw calls
    /// for the first <paramref name="count"/> entries in <paramref name="models"/>
    /// in a single batched operation.
    /// </summary>
    void DrawModels(List<GeometryModel> models, int count);

    // ── Matrix stack operations ───────────────────────────────────────────────

    /// <summary>
    /// Pushes a copy of the current active matrix onto the active stack,
    /// saving the current transform so it can be restored with <see cref="GLPopMatrix"/>.
    /// </summary>
    void GLPushMatrix();

    /// <summary>
    /// Pops the top matrix from the active stack, restoring the transform
    /// saved by the most recent <see cref="GLPushMatrix"/>. No-op if the stack
    /// has only one entry.
    /// </summary>
    void GLPopMatrix();

    /// <summary>Replaces the top of the active matrix stack with <paramref name="m"/>.</summary>
    void GLLoadMatrix(Matrix4 m);

    /// <summary>Replaces the top of the active matrix stack with the identity matrix.</summary>
    void GLLoadIdentity();

    // ── Transform helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Multiplies the active matrix by a rotation of <paramref name="angle"/> degrees
    /// around the axis (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    void GLRotate(float angle, float x, float y, float z);

    /// <summary>
    /// Multiplies the active matrix by a translation of
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    void GLTranslate(float x, float y, float z);

    /// <summary>
    /// Multiplies the active matrix by a non-uniform scale of
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    void GLScale(float x, float y, float z);

    // ── Matrix mode ───────────────────────────────────────────────────────────

    /// <summary>
    /// Switches the active stack to the model-view stack so that subsequent
    /// push / pop / transform calls affect <see cref="mvMatrix"/>.
    /// </summary>
    void GLMatrixModeModelView();

    /// <summary>
    /// Switches the active stack to the projection stack so that subsequent
    /// push / pop / transform calls affect <see cref="pMatrix"/>.
    /// </summary>
    void GLMatrixModeProjection();

    // ── Projection helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Pushes an orthographic projection sized to <paramref name="width"/> ×
    /// <paramref name="height"/> onto the projection stack and resets the
    /// model-view stack to identity, preparing for 2-D HUD rendering.
    /// Call <see cref="PerspectiveMode"/> to restore the previous state.
    /// </summary>
    void OrthoMode(int width, int height);

    /// <summary>
    /// Pops the orthographic projection pushed by <see cref="OrthoMode"/> and
    /// restores the perspective projection and model-view transform that were
    /// active before the 2-D pass.
    /// </summary>
    void PerspectiveMode();

    /// <summary>
    /// Uploads the top of <see cref="pMatrix"/> to the shader's projection
    /// matrix uniform. Must be called after any change to the projection stack
    /// that should take effect for the next draw call.
    /// </summary>
    void SetMatrixUniformProjection();
}
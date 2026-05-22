using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Drawing;

// ─────────────────────────────────────────────────────────────────────────────
// OpenGL
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstracts all direct OpenGL calls used by the renderer.
/// Covers viewport and clear operations, depth and culling state,
/// fog and ambient lighting uniforms, texture lifetime, shader initialisation,
/// matrix uniform uploads, and VAO-backed model lifecycle (create / update / draw / delete).
/// </summary>
public interface IOpenGlService
{
    // ── Viewport and clear ────────────────────────────────────────────────────

    /// <summary>Sets the GL viewport rectangle.</summary>
    void GlViewport(int x, int y, int width, int height);

    /// <summary>Clears both the colour buffer and the depth buffer.</summary>
    void GlClearColorBufferAndDepthBuffer();

    /// <summary>Clears only the depth buffer, leaving colour intact.</summary>
    void GlClearDepthBuffer();

    /// <summary>Sets the clear colour used by subsequent <see cref="GlClearColorBufferAndDepthBuffer"/> calls.</summary>
    void GlClearColorRgbaf(float r, float g, float b, float a);

    // ── Depth test ────────────────────────────────────────────────────────────

    /// <summary>Enables the depth test (<c>GL_DEPTH_TEST</c>).</summary>
    void GlEnableDepthTest();

    /// <summary>Disables the depth test.</summary>
    void GlDisableDepthTest();

    /// <summary>
    /// Controls depth buffer writes. Pass <see langword="false"/> to make the
    /// depth buffer read-only while still performing depth testing.
    /// </summary>
    void GlDepthMask(bool flag);

    // ── Face culling ──────────────────────────────────────────────────────────

    /// <summary>Enables back-face culling (<c>GL_CULL_FACE</c>).</summary>
    void GlEnableCullFace();

    /// <summary>Disables face culling so both sides of every polygon are rasterised.</summary>
    void GlDisableCullFace();

    /// <summary>Sets the culled face to back-faces (<c>GL_BACK</c>).</summary>
    void GlCullFaceBack();

    // ── Rasterisation state ───────────────────────────────────────────────────

    /// <summary>Sets the width (in pixels) used when drawing line primitives.</summary>
    void GLLineWidth(int width);

    // ── Ambient lighting ──────────────────────────────────────────────────────

    /// <summary>
    /// Sets the ambient light colour uniform (<c>uAmbientLight</c>) in the active shader.
    /// Each component is in the range [0, 255] and is normalised to [0, 1] before upload.
    /// </summary>
    void GlLightModelAmbient(int r, int g, int b);

    // ── Fog ───────────────────────────────────────────────────────────────────

    /// <summary>Enables exponential-squared fog in the fragment shader (<c>uFogEnabled = 1</c>).</summary>
    void GlEnableFog();

    /// <summary>Disables fog in the fragment shader (<c>uFogEnabled = 0</c>).</summary>
    void GlDisableFog();

    /// <summary>
    /// Sets the fog colour uniform (<c>uFogColor</c>).
    /// Each component is in the range [0, 255] and is normalised to [0, 1] before upload.
    /// </summary>
    void GlFogFogColor(int r, int g, int b, int a);

    /// <summary>
    /// Sets the fog density uniform (<c>uFogDensity</c>).
    /// Higher values produce thicker, shorter-range fog.
    /// </summary>
    void GlFogFogDensity(float density);

    // ── Textures ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds <paramref name="texture"/> to the <c>GL_TEXTURE_2D</c> target and
    /// updates the <c>uUseTexture</c> shader uniform accordingly
    /// (1 when a non-zero handle is bound, 0 otherwise).
    /// </summary>
    void BindTexture2d(int texture);

    /// <summary>Deletes the GPU texture identified by <paramref name="id"/>.</summary>
    void GLDeleteTexture(int id);

    /// <summary>Returns the maximum texture dimension supported by the GPU.</summary>
    int GlGetMaxTextureSize();

    /// <summary>
    /// Uploads <paramref name="bmp"/> to a new GPU texture and returns its OpenGL handle.
    /// Non-power-of-two bitmaps are resized when <c>ALLOW_NON_POWER_OF_TWO</c> is false.
    /// Mipmaps are generated when <c>ENABLE_MIPMAPS</c> is true.
    /// </summary>
    int LoadTextureFromBitmap(Bitmap bmp);

    /// <summary>
    /// Uploads a raw RGBA byte buffer directly to a GL texture, bypassing the
    /// Bitmap path. SkiaSharp already produces RGBA — no channel swap needed.
    /// </summary>
    int LoadTextureRgba(byte[] rgba, int width, int height);

    // ── Model lifecycle ───────────────────────────────────────────────────────

    /// <summary>
    /// Allocates a VAO and four VBOs (positions, colours, UVs, indices) on the GPU,
    /// uploads the data from <paramref name="modelData"/>, and stores the resulting
    /// handles back into the same object before returning it.
    /// </summary>
    GeometryModel CreateModel(GeometryModel modelData);

    /// <summary>
    /// Streams updated vertex data into the existing VBOs of <paramref name="data"/>
    /// using <c>glBufferSubData</c>. Falls back to <see cref="CreateModel"/> if the
    /// VAO has not been allocated yet.
    /// </summary>
    void UpdateModel(GeometryModel data);

    /// <summary>
    /// Draws <paramref name="model"/> using its VAO. Equivalent to
    /// <see cref="DrawModelData"/> but accepts a named model reference.
    /// </summary>
    void DrawModel(GeometryModel model);

    /// <summary>
    /// Binds <paramref name="data"/>'s VAO and issues a <c>glDrawElements</c> call.
    /// The primitive type is determined by <see cref="GeometryModel.Mode"/>.
    /// </summary>
    void DrawModelData(GeometryModel data);

    /// <summary>
    /// Calls <see cref="DrawModelData"/> for the first <paramref name="count"/>
    /// entries in <paramref name="models"/>.
    /// </summary>
    void DrawModels(List<GeometryModel> models, int count);

    /// <summary>
    /// Deletes the VAO and all four VBOs associated with <paramref name="model"/>,
    /// freeing the GPU memory.
    /// </summary>
    void DeleteModel(GeometryModel model);

    // ── Shaders and uniforms ──────────────────────────────────────────────────

    /// <summary>
    /// Compiles the built-in vertex and fragment shaders, links them into a program,
    /// and caches all uniform locations. Must be called once before any draw or
    /// uniform-upload method is used.
    /// </summary>
    void InitShaders();

    /// <summary>
    /// Uploads <paramref name="pMatrix"/> to the <c>uProjection</c> shader uniform
    /// and caches it for subsequent draw calls that re-upload per draw.
    /// </summary>
    void SetMatrixUniformProjection(ref Matrix4 pMatrix);

    /// <summary>
    /// Uploads <paramref name="mvMatrix"/> to the <c>uModelView</c> shader uniform
    /// and caches it for subsequent draw calls that re-upload per draw.
    /// </summary>
    void SetMatrixUniformModelView(ref Matrix4 mvMatrix);
}
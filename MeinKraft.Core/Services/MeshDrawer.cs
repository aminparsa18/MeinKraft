using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

/// <inheritdoc/>
public sealed class MeshDrawer : IMeshDrawer
{
    /// <inheritdoc/>
    public Stack<Matrix4> mvMatrix { get; set; }

    /// <inheritdoc/>
    public Stack<Matrix4> pMatrix { get; set; }

    private bool _projectionModeActive;
    private Stack<Matrix4> ActiveMatrix => _projectionModeActive ? pMatrix : mvMatrix;

    private readonly IOpenGlService _openGlService;

    public MeshDrawer(IOpenGlService openGlService)
    {
        _openGlService = openGlService;
        mvMatrix = new Stack<Matrix4>();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix = new Stack<Matrix4>();
        pMatrix.Push(Matrix4.Identity);
    }

    // ── Draw calls ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void DrawModel(GeometryModel model)
    {
        SetMatrixUniformModelView();
        _openGlService.DrawModel(model);
    }

    /// <inheritdoc/>
    public void DrawModelData(GeometryModel data)
    {
        SetMatrixUniformModelView();
        _openGlService.DrawModelData(data);
    }

    /// <inheritdoc/>
    public void DrawModels(List<GeometryModel> models, int count)
    {
        SetMatrixUniformModelView();
        _openGlService.DrawModels(models, count);
    }

    // ── Matrix stack operations ───────────────────────────────────────────────

    /// <inheritdoc/>
    public void GLPushMatrix() => ActiveMatrix.Push(ActiveMatrix.Peek());

    /// <inheritdoc/>
    public void GLPopMatrix()
    {
        if (ActiveMatrix.Count > 1)
        {
            ActiveMatrix.Pop();
        }
    }

    /// <inheritdoc/>
    public void GLLoadIdentity()
    {
        if (ActiveMatrix.Count > 0)
        {
            ActiveMatrix.Pop();
        }

        ActiveMatrix.Push(Matrix4.Identity);
    }

    /// <inheritdoc/>
    public void GLLoadMatrix(Matrix4 m)
    {
        if (ActiveMatrix.Count > 0)
        {
            ActiveMatrix.Pop();
        }

        ActiveMatrix.Push(m);
    }

    // ── Transform helpers ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GLScale(float x, float y, float z)
    {
        Matrix4.CreateScale(x, y, z, out Matrix4 scale);
        MultiplyActiveMatrix(scale);
    }

    /// <inheritdoc/>
    public void GLRotate(float angle, float x, float y, float z)
    {
        float radians = angle / 360f * 2f * MathF.PI;
        Matrix4.CreateFromAxisAngle(new Vector3(x, y, z), radians, out Matrix4 rotation);
        MultiplyActiveMatrix(rotation);
    }

    /// <inheritdoc/>
    public void GLTranslate(float x, float y, float z)
    {
        Matrix4.CreateTranslation(x, y, z, out Matrix4 translation);
        MultiplyActiveMatrix(translation);
    }

    // ── Matrix mode ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GLMatrixModeModelView() => _projectionModeActive = false;

    /// <inheritdoc/>
    public void GLMatrixModeProjection() => _projectionModeActive = true;

    // ── Projection helpers ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void OrthoMode(int width, int height)
    {
        GLMatrixModeProjection();
        GLPushMatrix();
        GLLoadIdentity();
        SetOrtho(0, width, height, 0, 0, 1);
        SetMatrixUniformProjection();

        GLMatrixModeModelView();
        GLPushMatrix();
        GLLoadIdentity();
        SetMatrixUniformModelView();
    }

    /// <inheritdoc/>
    public void PerspectiveMode()
    {
        GLMatrixModeProjection();
        GLPopMatrix();
        SetMatrixUniformProjection();

        GLMatrixModeModelView();
        GLPopMatrix();
        SetMatrixUniformModelView();
    }

    /// <inheritdoc/>
    public void SetMatrixUniformProjection()
    {
        Matrix4 p = pMatrix.Peek();
        _openGlService.SetMatrixUniformProjection(ref p);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Uploads the top of <see cref="mvMatrix"/> to the shader's model-view uniform.</summary>
    private void SetMatrixUniformModelView()
    {
        Matrix4 mv = mvMatrix.Peek();
        _openGlService.SetMatrixUniformModelView(ref mv);
    }

    /// <summary>
    /// Builds an orthographic projection matrix and replaces the top of
    /// <see cref="pMatrix"/> with it. Only valid when projection mode is active.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called while model-view mode is active.</exception>
    private void SetOrtho(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        if (!_projectionModeActive)
        {
            throw new InvalidOperationException(
                $"{nameof(SetOrtho)} must be called while in projection matrix mode.");
        }

        Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar, out Matrix4 ortho);
        pMatrix.Pop();
        pMatrix.Push(ortho);
    }

    /// <summary>Pre-multiplies the active matrix by <paramref name="transform"/>.</summary>
    private void MultiplyActiveMatrix(Matrix4 transform)
    {
        Matrix4 current = ActiveMatrix.Peek();
        ActiveMatrix.Pop();
        ActiveMatrix.Push(transform * current);
    }
}
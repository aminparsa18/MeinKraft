using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = System.Buffer;
using PixelFormat = OpenTK.Graphics.ES30.PixelFormat;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

/// <inheritdoc/>
public sealed class OpenGlService : IOpenGlService
{
    /// <summary>
    /// When <see langword="false"/>, non-power-of-two bitmaps are resized to the
    /// next power of two before upload. Set to <see langword="true"/> only if the
    /// target GPU advertises <c>GL_ARB_texture_non_power_of_two</c>.
    /// </summary>
    public bool AllowNonPowerOfTwo { get; set; } = false;

    /// <summary>When <see langword="true"/>, mipmaps are generated for every uploaded texture.</summary>
    public bool EnableMipmaps { get; set; } = true;

    /// <summary>When <see langword="true"/>, alpha blending is enabled after texture upload.</summary>
    public bool EnableTransparency { get; set; } = true;

    private Matrix4 _projectionMatrix;
    private Matrix4 _modelViewMatrix;

    /// <summary>OpenGL shader program handle; -1 until <see cref="InitShaders"/> is called.</summary>
    private int _shaderProgram = -1;

    // Cached uniform locations — populated by InitShaders.
    private int _uUseTexture;
    private int _uProjection;
    private int _uModelView;
    private int _uAmbientLight;
    private int _uFogEnabled;
    private int _uFogColor;
    private int _uFogDensity;

    private Vector3 _ambientLight = Vector3.One;
    private Vector4 _fogColor = Vector4.One;
    private float _fogDensity = 0.003f;

    private readonly IGameLogger _logger;

    public OpenGlService(IGameLogger gameLogger)
    {
        _logger = gameLogger;
    }

    // ── Viewport and clear ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GlViewport(int x, int y, int width, int height)
        => GL.Viewport(x, y, width, height);

    /// <inheritdoc/>
    public void GlClearColorBufferAndDepthBuffer()
        => GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

    /// <inheritdoc/>
    public void GlClearDepthBuffer()
        => GL.Clear(ClearBufferMask.DepthBufferBit);

    /// <inheritdoc/>
    public void GlClearColorRgbaf(float r, float g, float b, float a)
        => GL.ClearColor(r, g, b, a);

    // ── Depth test ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GlEnableDepthTest() => GL.Enable(EnableCap.DepthTest);

    /// <inheritdoc/>
    public void GlDisableDepthTest() => GL.Disable(EnableCap.DepthTest);

    /// <inheritdoc/>
    public void GlDepthMask(bool flag) => GL.DepthMask(flag);

    // ── Face culling ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GlEnableCullFace() => GL.Enable(EnableCap.CullFace);

    /// <inheritdoc/>
    public void GlDisableCullFace() => GL.Disable(EnableCap.CullFace);

    /// <inheritdoc/>
    public void GlCullFaceBack() => GL.CullFace(TriangleFace.Back);

    // ── Rasterisation state ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GLLineWidth(int width) => GL.LineWidth(width);

    // ── Ambient lighting ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GlLightModelAmbient(int r, int g, int b)
    {
        _ambientLight = new Vector3(r / 255f, g / 255f, b / 255f);
        if (_shaderProgram != -1)
        {
            GL.Uniform3(_uAmbientLight, _ambientLight.X, _ambientLight.Y, _ambientLight.Z);
        }
    }

    // ── Fog ───────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GlEnableFog()
    {
        if (_shaderProgram != -1)
        {
            GL.Uniform1(_uFogEnabled, 1);
        }
    }

    /// <inheritdoc/>
    public void GlDisableFog()
    {
        if (_shaderProgram != -1)
        {
            GL.Uniform1(_uFogEnabled, 0);
        }
    }

    /// <inheritdoc/>
    public void GlFogFogColor(int r, int g, int b, int a)
    {
        _fogColor = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        if (_shaderProgram != -1)
        {
            GL.Uniform4(_uFogColor, _fogColor);
        }
    }

    /// <inheritdoc/>
    public void GlFogFogDensity(float density)
    {
        _fogDensity = density;
        if (_shaderProgram != -1)
        {
            GL.Uniform1(_uFogDensity, _fogDensity);
        }
    }

    // ── Textures ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void BindTexture2d(int texture)
    {
        GL.BindTexture(TextureTarget.Texture2D, texture);
        if (_shaderProgram != -1)
        {
            GL.Uniform1(_uUseTexture, texture != 0 ? 1 : 0);
        }
    }

    /// <inheritdoc/>
    public void GLDeleteTexture(int id) => GL.DeleteTexture(id);

    /// <inheritdoc/>
    public int GlGetMaxTextureSize()
    {
        GL.GetInteger(GetPName.MaxTextureSize, out int size);
        return size;
    }

    /// <inheritdoc/>
    public int LoadTextureFromBitmap(Bitmap bmp) => LoadTexture(bmp, linearMag: false);

    // ── Model lifecycle ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public GeometryModel CreateModel(GeometryModel data)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        // attribute 0 — positions (XYZ)
        int vertexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Xyz.Length * sizeof(float), data.Xyz, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(0);

        // attribute 1 — colours (RGBA, normalised byte)
        int colorVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Rgba.Length * sizeof(byte), data.Rgba, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, 0, 0);
        GL.EnableVertexAttribArray(1);

        // attribute 2 — texture coordinates (UV)
        int uvVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, uvVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Uv.Length * sizeof(float), data.Uv, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(2);

        // element buffer — triangle indices
        int indexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexVbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, data.Indices.Length * sizeof(int), data.Indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);

        data.VaoId = vao;
        data.VertexVboId = vertexVbo;
        data.ColorVboId = colorVbo;
        data.UvVboId = uvVbo;
        data.IndexVboId = indexVbo;
        return data;
    }

    /// <inheritdoc/>
    public void UpdateModel(GeometryModel data)
    {
        if (data.VaoId == 0)
        {
            CreateModel(data);
            return;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.VertexVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Xyz.Length * sizeof(float), data.Xyz);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.ColorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Rgba.Length * sizeof(byte), data.Rgba);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.UvVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Uv.Length * sizeof(float), data.Uv);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, data.IndexVboId);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero,
            data.Indices.Length * sizeof(int), data.Indices);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    /// <inheritdoc/>
    public void DrawModel(GeometryModel model) => DrawModelData(model);

    /// <inheritdoc/>
    public void DrawModelData(GeometryModel data)
    {
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
        GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
        GL.BindVertexArray(data.VaoId);

        PrimitiveType primitive = data.Mode == (int)DrawMode.Triangles
            ? PrimitiveType.Triangles
            : PrimitiveType.Lines;
        GL.DrawElements(primitive, data.IndicesCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    /// <inheritdoc/>
    public void DrawModels(List<GeometryModel> models, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawModelData(models[i]);
        }
    }

    /// <inheritdoc/>
    public void DeleteModel(GeometryModel model)
    {
        GL.DeleteVertexArray(model.VaoId);
        GL.DeleteBuffer(model.VertexVboId);
        GL.DeleteBuffer(model.ColorVboId);
        GL.DeleteBuffer(model.UvVboId);
        GL.DeleteBuffer(model.IndexVboId);
    }

    // ── Shaders and uniforms ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public void InitShaders()
    {
        const string vertexSource = """
    #version 300 es

    layout(location = 0) in vec3 aPosition;
    layout(location = 1) in vec4 aColor;
    layout(location = 2) in vec2 aUv;

    uniform mat4 uProjection;
    uniform mat4 uModelView;

    out vec4 vColor;
    out vec2 vUv;
    out float vFogDepth;

    void main()
    {
        vec4 viewPos = uModelView * vec4(aPosition, 1.0);
        gl_Position  = uProjection * viewPos;
        vColor    = aColor;
        vUv       = aUv;
        vFogDepth = abs(viewPos.z);
    }
    """;

        const string fragmentSource = """
    #version 300 es
    precision mediump float;

    in vec4  vColor;
    in vec2  vUv;
    in float vFogDepth;

    uniform sampler2D uTexture;
    uniform vec3      uAmbientLight;
    uniform vec4      uFogColor;
    uniform float     uFogDensity;
    uniform int      uFogEnabled;
    uniform int      uUseTexture;

    out vec4 fragColor;

    void main()
    {
        fragColor = uUseTexture != 0 ? texture(uTexture, vUv) * vColor : vColor;

        if (uUseTexture != 0 && fragColor.a < 0.5)
        {
            discard;
        }
        
        fragColor.rgb *= uAmbientLight;

        if (uFogEnabled != 0)
        {
            float fogFactor = clamp(
                exp(-uFogDensity * uFogDensity * vFogDepth * vFogDepth),
                0.0, 1.0);
            fragColor.rgb = mix(uFogColor.rgb, fragColor.rgb, fogFactor);
        }
    }
    """;

        int vert = CompileShader(ShaderType.VertexShader, vertexSource);
        int frag = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vert);
        GL.AttachShader(_shaderProgram, frag);
        GL.LinkProgram(_shaderProgram);
        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            throw new Exception($"Shader link error: {GL.GetProgramInfoLog(_shaderProgram)}");
        }

        // Shaders are baked into the program — intermediate objects can be released.
        GL.DetachShader(_shaderProgram, vert);
        GL.DetachShader(_shaderProgram, frag);
        GL.DeleteShader(vert);
        GL.DeleteShader(frag);

        // Cache uniform locations.
        GL.UseProgram(_shaderProgram);
        _uUseTexture = GL.GetUniformLocation(_shaderProgram, "uUseTexture");
        _uProjection = GL.GetUniformLocation(_shaderProgram, "uProjection");
        _uModelView = GL.GetUniformLocation(_shaderProgram, "uModelView");
        _uAmbientLight = GL.GetUniformLocation(_shaderProgram, "uAmbientLight");
        _uFogEnabled = GL.GetUniformLocation(_shaderProgram, "uFogEnabled");
        _uFogColor = GL.GetUniformLocation(_shaderProgram, "uFogColor");
        _uFogDensity = GL.GetUniformLocation(_shaderProgram, "uFogDensity");

        // Push initial uniform values.
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uTexture"), 0);
        GL.Uniform3(_uAmbientLight, 1f, 1f, 1f);
        GL.Uniform1(_uFogEnabled, 0);
        GL.Uniform1(_uFogDensity, _fogDensity);
        GL.Uniform4(_uFogColor, _fogColor);
    }

    /// <inheritdoc/>
    public void SetMatrixUniformProjection(ref Matrix4 pMatrix)
    {
        _projectionMatrix = pMatrix;
        if (_shaderProgram != -1)
        {
            GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
        }
    }

    /// <inheritdoc/>
    public void SetMatrixUniformModelView(ref Matrix4 mvMatrix)
    {
        _modelViewMatrix = mvMatrix;
        if (_shaderProgram != -1)
        {
            GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a single shader stage and returns its handle.
    /// </summary>
    /// <exception cref="Exception">Thrown when compilation fails, with the info log attached.</exception>
    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            if (string.IsNullOrEmpty(log))
                log = "(no info log returned by driver)";
            throw new Exception($"{type} compile error: {log}");
        }

        return shader;
    }

    /// <summary>
    /// Uploads <paramref name="bmpArg"/> to the GPU and returns an OpenGL texture handle.
    /// Non-power-of-two bitmaps are resized when <see cref="AllowNonPowerOfTwo"/> is false.
    /// </summary>
    private int LoadTexture(Bitmap bmpArg, bool linearMag)
    {
        // ── 1. Ensure power-of-two dimensions ────────────────────────────────────
        Bitmap bmp = bmpArg;
        bool converted = false;

        if (!AllowNonPowerOfTwo &&
            !(BitOperations.IsPow2((uint)bmp.Width) && BitOperations.IsPow2((uint)bmp.Height)))
        {
            int w = (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Width);
            int h = (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Height);
            bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmpArg, 0, 0, w, h);
            }
            converted = true;
        }

        // ── 2. Allocate and bind texture ──────────────────────────────────────────
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);

        // ── 3. Set filtering — before upload ──────────────────────────────────────
        if (!EnableMipmaps)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Nearest);
        }
        else
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.NearestMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4);
        }

        // ── 4. Upload base level ──────────────────────────────────────────────────
        BitmapData bmpData = bmp.LockBits(
             new Rectangle(0, 0, bmp.Width, bmp.Height),
             ImageLockMode.ReadOnly,
             System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        // GDI+ gives us BGRA on Windows — ES3 only guarantees RGBA.
        // Swap B↔R in a temporary buffer before upload.
        int byteCount = bmpData.Width * bmpData.Height * 4;
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(bmpData.Scan0, pixels, 0, byteCount);
        bmp.UnlockBits(bmpData);   // data is on the GPU — safe to unlock

        for (int i = 0; i < byteCount; i += 4)
        {
            // BGRA → RGBA: swap index 0 (B) with index 2 (R)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }

        GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba,
            bmp.Width, bmp.Height, 0,
            PixelFormat.Rgba,          // ← was Bgra
            PixelType.UnsignedByte,
            pixels);

        // ── 5. Generate mipmaps from the uploaded GPU texture ─────────────────────
        if (EnableMipmaps)
            GL.GenerateMipmap(TextureTarget.Texture2D);

        // ── 6. Blending ───────────────────────────────────────────────────────────
        if (EnableTransparency)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        }

        // ── 7. Cleanup ────────────────────────────────────────────────────────────
        if (converted)
            bmp.Dispose();

        GL.BindTexture(TextureTarget.Texture2D, 0);   // leave no texture bound

        return id;
    }

    /// <inheritdoc/>
    public int LoadTextureRgba(byte[] rgba, int width, int height)
        => LoadTextureFromBytes(rgba, width, height, linearMag: false);

    private int LoadTextureFromBytes(byte[] pixels, int width, int height, bool linearMag)
    {
        // ── 1. Ensure power-of-two dimensions ────────────────────────────────────
        bool needsResize = !AllowNonPowerOfTwo &&
                           !(BitOperations.IsPow2((uint)width) &&
                             BitOperations.IsPow2((uint)height));

        if (needsResize)
        {
            int w2 = (int)BitOperations.RoundUpToPowerOf2((uint)width);
            int h2 = (int)BitOperations.RoundUpToPowerOf2((uint)height);

            // Blit into a zeroed (transparent) power-of-two buffer.
            byte[] padded = new byte[w2 * h2 * 4];
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(pixels, row * width * 4,
                                 padded, row * w2 * 4,
                                 width * 4);

            pixels = padded;
            width = w2;
            height = h2;
        }

        // ── 2-6. Identical to LoadTexture from here on ───────────────────────────
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);

        if (!EnableMipmaps)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Nearest);
        }
        else
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.NearestMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4);
        }

        GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba,
            width, height, 0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels);

        if (EnableMipmaps)
            GL.GenerateMipmap(TextureTarget.Texture2D);

        if (EnableTransparency)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        }

        GL.BindTexture(TextureTarget.Texture2D, 0);
        return id;
    }
}
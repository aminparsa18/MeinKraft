/// <summary>
/// Provides a factory method for generating UV-sphere <see cref="GeometryModel"/>,
/// parameterised by radius, height, segment and ring counts.
/// </summary>
public static class Sphere
{
    /// <summary>
    /// Builds a UV-sphere <see cref="GeometryModel"/> with the given dimensions and tessellation.
    /// </summary>
    /// <remarks>
    /// Topology:
    /// <list type="bullet">
    ///   <item>1 vertex at the north pole</item>
    ///   <item><paramref name="rings"/> latitudinal rings of <paramref name="segments"/> vertices each</item>
    ///   <item>1 vertex at the south pole</item>
    /// </list>
    /// Seam vertices are shared via index wrapping (<c>(x+1) % segments</c>) — no duplicate
    /// column. Trig is pre-computed per ring and per segment to avoid redundant calls.
    /// All vertices are initialised with full white vertex colour (255, 255, 255, 255).
    /// </remarks>
    /// <param name="radius">Horizontal radius of the sphere.</param>
    /// <param name="height">Vertical scale (use same as <paramref name="radius"/> for a true sphere).</param>
    /// <param name="segments">Subdivisions around the equator (longitude). Minimum 3.</param>
    /// <param name="rings">Latitudinal rings between the poles (not counting poles). Minimum 1.</param>
    /// <returns>A <see cref="GeometryModel"/> representing the UV-sphere.</returns>
    public static GeometryModel Create(float radius, float height, int segments, int rings)
    {
        // Vertex layout:
        //   [0]                      — north pole
        //   [1 .. rings*segments]    — body rings
        //   [rings*segments + 1]     — south pole
        int bodyVertices = rings * segments;
        int vertexCount = bodyVertices + 2;
        int northPoleIdx = 0;
        int southPoleIdx = vertexCount - 1;

        // Index layout:
        //   north fan : segments triangles  → segments * 3
        //   body quads: (rings-1)*segments  → (rings-1) * segments * 6
        //   south fan : segments triangles  → segments * 3
        int indexCount = (segments * 3) + ((rings - 1) * segments * 6) + (segments * 3);

        float[] xyz = new float[vertexCount * 3];
        float[] uv = new float[vertexCount * 2];
        byte[] rgba = new byte[vertexCount * 4];
        int[] indices = new int[indexCount];

        // ── Pre-compute trig tables ───────────────────────────────────────────
        // Avoids recomputing sin/cos for each vertex in the inner loop.

        float[] sinTheta = new float[segments];
        float[] cosTheta = new float[segments];
        for (int x = 0; x < segments; x++)
        {
            float theta = x / (float)segments * 2f * MathF.PI;
            sinTheta[x] = MathF.Sin(theta);
            cosTheta[x] = MathF.Cos(theta);
        }

        float[] sinPhi = new float[rings];
        float[] cosPhi = new float[rings];
        for (int y = 0; y < rings; y++)
        {
            // phi in (0, π) exclusive — poles are separate vertices
            float phi = (y + 1) / (float)(rings + 1) * MathF.PI;
            sinPhi[y] = MathF.Sin(phi);
            cosPhi[y] = MathF.Cos(phi);
        }

        // ── North pole ────────────────────────────────────────────────────────
        WriteVertex(xyz, uv, rgba, northPoleIdx, 0f, height, 0f, 0.5f, 0f);

        // ── Body rings ────────────────────────────────────────────────────────
        for (int y = 0; y < rings; y++)
        {
            float sp = sinPhi[y];
            float cp = cosPhi[y];
            float vv = (y + 1) / (float)(rings + 1); // v in (0,1) exclusive

            for (int x = 0; x < segments; x++)
            {
                float vx = radius * sp * cosTheta[x];
                float vy = height * cp;
                float vz = radius * sp * sinTheta[x];
                float uu = x / (float)segments; // u in [0,1) — seam is shared via index wrap

                int vi = 1 + y * segments + x;
                WriteVertex(xyz, uv, rgba, vi, vx, vy, vz, uu, vv);
            }
        }

        // ── South pole ────────────────────────────────────────────────────────
        WriteVertex(xyz, uv, rgba, southPoleIdx, 0f, -height, 0f, 0.5f, 1f);

        // ── Index buffer ──────────────────────────────────────────────────────
        int ii = 0;

        // North fan — pole to first ring
        for (int x = 0; x < segments; x++)
        {
            int curr = 1 + x;
            int next = 1 + (x + 1) % segments;
            indices[ii++] = northPoleIdx;
            indices[ii++] = curr;
            indices[ii++] = next;
        }

        // Body quads — ring y to ring y+1
        for (int y = 0; y < rings - 1; y++)
        {
            int ringBase = 1 + y * segments;
            int nextRingBase = 1 + (y + 1) * segments;

            for (int x = 0; x < segments; x++)
            {
                int nextX = (x + 1) % segments;

                int bl = ringBase + x;
                int br = ringBase + nextX;
                int tl = nextRingBase + x;
                int tr = nextRingBase + nextX;

                indices[ii++] = bl;
                indices[ii++] = tl;
                indices[ii++] = tr;

                indices[ii++] = tr;
                indices[ii++] = br;
                indices[ii++] = bl;
            }
        }

        // South fan — last ring to pole
        int lastRingBase = 1 + (rings - 1) * segments;
        for (int x = 0; x < segments; x++)
        {
            int curr = lastRingBase + x;
            int next = lastRingBase + (x + 1) % segments;
            indices[ii++] = southPoleIdx;
            indices[ii++] = next;
            indices[ii++] = curr;
        }

        return new GeometryModel
        {
            VerticesCount = vertexCount,
            IndicesCount = indexCount,
            Xyz = xyz,
            Uv = uv,
            Rgba = rgba,
            Indices = indices,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a single vertex into the flat SoA arrays at the given vertex index.
    /// </summary>
    private static void WriteVertex(
        float[] xyz, float[] uv, byte[] rgba,
        int vi, float x, float y, float z, float u, float v)
    {
        xyz[(vi * 3) + 0] = x;
        xyz[(vi * 3) + 1] = y;
        xyz[(vi * 3) + 2] = z;

        uv[(vi * 2) + 0] = u;
        uv[(vi * 2) + 1] = v;

        rgba[(vi * 4) + 0] = 255;
        rgba[(vi * 4) + 1] = 255;
        rgba[(vi * 4) + 2] = 255;
        rgba[(vi * 4) + 3] = 255;
    }
}
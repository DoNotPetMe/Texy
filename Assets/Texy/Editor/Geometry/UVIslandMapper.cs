using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Rasterizes a mesh's UV triangles into masks in texture space. Two outputs matter:
    ///   * a coverage mask (1 where the avatar actually uses UV space) so generated detail and
    ///     decals only land on real surface and seams get padding, and
    ///   * a per-island gradient that lets the procedural engine vary tone/pattern per body part.
    /// This is the bridge between "geometry" and "pixels" that makes Texy geometry-aware.
    /// </summary>
    public static class UVIslandMapper
    {
        public class UVMaps
        {
            public int Size;
            public float[] Coverage;   // 0..1 occupancy
            public float[] IslandTone; // 0..1 pseudo-random tone per island region

            public float SampleCoverage(int x, int y) => Coverage[y * Size + x];
            public float SampleTone(int x, int y) => IslandTone[y * Size + x];
        }

        public static UVMaps Build(MeshContext ctx, int size)
        {
            var maps = new UVMaps
            {
                Size = size,
                Coverage = new float[size * size],
                IslandTone = new float[size * size]
            };

            if (ctx == null || ctx.UV == null || ctx.UV.Length == 0 || ctx.Triangles == null)
                return maps;

            var uv = ctx.UV;
            var tris = ctx.Triangles;

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int ia = tris[i], ib = tris[i + 1], ic = tris[i + 2];
                if (ia >= uv.Length || ib >= uv.Length || ic >= uv.Length) continue;

                Vector2 a = Wrap(uv[ia]);
                Vector2 b = Wrap(uv[ib]);
                Vector2 c = Wrap(uv[ic]);

                // Per-triangle tone derived from a representative vertex index, stable across runs.
                float tone = Hash01(ia + ib * 7 + ic * 13);
                RasterizeTriangle(maps, a, b, c, tone);
            }

            // A light dilation closes seam gaps so decals/effects don't show hard UV cracks.
            Dilate(maps, 2);
            return maps;
        }

        private static void RasterizeTriangle(UVMaps maps, Vector2 a, Vector2 b, Vector2 c, float tone)
        {
            int size = maps.Size;
            Vector2 pa = a * size, pb = b * size, pc = c * size;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))), 0, size - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))), 0, size - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))), 0, size - 1);

            float area = Edge(pa, pb, pc);
            if (Mathf.Abs(area) < 1e-6f) return;
            float inv = 1f / area;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = Edge(pb, pc, p) * inv;
                float w1 = Edge(pc, pa, p) * inv;
                float w2 = Edge(pa, pb, p) * inv;
                bool inside = (w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0);
                if (!inside) continue;

                int idx = y * size + x;
                maps.Coverage[idx] = 1f;
                maps.IslandTone[idx] = tone;
            }
        }

        private static void Dilate(UVMaps maps, int radius)
        {
            int size = maps.Size;
            var srcCov = (float[])maps.Coverage.Clone();
            var srcTone = (float[])maps.IslandTone.Clone();

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (srcCov[idx] > 0f) continue;

                bool found = false;
                for (int dy = -radius; dy <= radius && !found; dy++)
                for (int dx = -radius; dx <= radius && !found; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    int nIdx = ny * size + nx;
                    if (srcCov[nIdx] > 0f)
                    {
                        maps.Coverage[idx] = srcCov[nIdx] * 0.85f;
                        maps.IslandTone[idx] = srcTone[nIdx];
                        found = true;
                    }
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        {
            return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        }

        private static Vector2 Wrap(Vector2 uv)
        {
            return new Vector2(Frac(uv.x), Frac(uv.y));
        }

        private static float Frac(float v)
        {
            v -= Mathf.Floor(v);
            return v < 0 ? v + 1f : v;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return m / 2147483647f;
            }
        }
    }
}

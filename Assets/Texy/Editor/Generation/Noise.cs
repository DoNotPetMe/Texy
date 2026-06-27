using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Deterministic, tileable noise primitives used by the procedural engine. Everything here is
    /// seeded and (where it matters) seamlessly tileable so generated textures wrap on avatar UVs.
    /// </summary>
    public static class Noise
    {
        /// <summary>Hash-based value noise sample in [0,1], tileable over <paramref name="period"/>.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            float v00 = Hash(xi, yi, period, seed);
            float v10 = Hash(xi + 1, yi, period, seed);
            float v01 = Hash(xi, yi + 1, period, seed);
            float v11 = Hash(xi + 1, yi + 1, period, seed);

            float u = Fade(xf);
            float v = Fade(yf);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, u), Mathf.Lerp(v01, v11, u), v);
        }

        /// <summary>Fractal Brownian motion (layered value noise). Returns [0,1].</summary>
        public static float FBM(float x, float y, int basePeriod, int octaves, float lacunarity, float gain, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float ampSum = 0f;
            int period = basePeriod;
            float freq = 1f;

            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(x * freq, y * freq, period, seed + o * 1013);
                ampSum += amp;
                amp *= gain;
                freq *= lacunarity;
                period = Mathf.Max(1, Mathf.RoundToInt(period * lacunarity));
            }
            return ampSum > 0f ? sum / ampSum : 0f;
        }

        /// <summary>
        /// Tileable Worley / cellular noise. Returns distance to the nearest feature point in [0,1],
        /// useful for scales, cells, voronoi camo and cracked surfaces.
        /// </summary>
        public static float Worley(float x, float y, int period, int seed, out float cellId)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            float minDist = 10f;
            cellId = 0f;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = xi + ox;
                int cy = yi + oy;
                float fx = Hash(cx, cy, period, seed);
                float fy = Hash(cx, cy, period, seed + 9173);
                Vector2 feature = new Vector2(ox + fx, oy + fy);
                Vector2 diff = feature - new Vector2(xf, yf);
                float d = diff.sqrMagnitude;
                if (d < minDist)
                {
                    minDist = d;
                    cellId = Hash(cx, cy, period, seed + 4421);
                }
            }
            return Mathf.Clamp01(Mathf.Sqrt(minDist));
        }

        /// <summary>Directional ridged noise (good for fabric weave, brushed metal, hair strands).</summary>
        public static float Ridged(float x, float y, int period, int octaves, int seed)
        {
            float n = FBM(x, y, period, octaves, 2f, 0.5f, seed);
            return 1f - Mathf.Abs(2f * n - 1f);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Integer hash to [0,1], wrapped on <paramref name="period"/> so the field tiles.</summary>
        private static float Hash(int x, int y, int period, int seed)
        {
            if (period > 0)
            {
                x = Mod(x, period);
                y = Mod(y, period);
            }
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }

        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}

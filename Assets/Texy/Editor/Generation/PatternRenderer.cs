using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Renders one normalized surface point for a parsed <see cref="PromptParser.TextureStyle"/>.
    /// Returns both a color-ramp coordinate (t) and a height value, so a single pass feeds both the
    /// albedo and the height-derived maps (normal/AO). Every pattern is built from tileable
    /// primitives, so outputs wrap seamlessly across avatar UVs.
    /// </summary>
    public static class PatternRenderer
    {
        public struct Sample
        {
            public float T;      // 0..1 position along the palette ramp
            public float Height; // 0..1 surface height for normal/AO derivation
        }

        public static Sample Evaluate(PromptParser.TextureStyle style, float u, float v, int seed)
        {
            float scale = Mathf.Max(0.1f, style.DetailScale);
            switch (style.Pattern)
            {
                case PromptParser.Pattern.Solid: return Solid(u, v, seed);
                case PromptParser.Pattern.Noise: return NoisePat(u, v, seed, scale);
                case PromptParser.Pattern.Stripes: return Stripes(u, v, seed, scale);
                case PromptParser.Pattern.Plaid: return Plaid(u, v, seed, scale);
                case PromptParser.Pattern.Camo: return Camo(u, v, seed, scale);
                case PromptParser.Pattern.Dots: return Dots(u, v, seed, scale);
                case PromptParser.Pattern.Hexagons: return Hexagons(u, v, seed, scale);
                case PromptParser.Pattern.Scales: return Scales(u, v, seed, scale);
                case PromptParser.Pattern.Cells: return Cells(u, v, seed, scale);
                case PromptParser.Pattern.Stars: return Stars(u, v, seed, scale);
                case PromptParser.Pattern.Galaxy: return Galaxy(u, v, seed, scale);
                case PromptParser.Pattern.Circuit: return Circuit(u, v, seed, scale);
                case PromptParser.Pattern.Floral: return Floral(u, v, seed, scale);
                case PromptParser.Pattern.Gradient: return Gradient(u, v, seed, scale);
                case PromptParser.Pattern.Marble: return Marble(u, v, seed, scale);
                case PromptParser.Pattern.Denim: return Denim(u, v, seed, scale);
                case PromptParser.Pattern.Weave: return Weave(u, v, seed, scale);
                default: return NoisePat(u, v, seed, scale);
            }
        }

        // Base detail frequency in "cells per tile". Kept integer-friendly for seamless wrap.
        private static int Freq(float scale, int baseFreq) => Mathf.Max(1, Mathf.RoundToInt(baseFreq * scale));

        private static Sample Solid(float u, float v, int seed)
        {
            float n = Noise.FBM(u * 4f, v * 4f, 4, 4, 2f, 0.5f, seed);
            return new Sample { T = 0f, Height = 0.4f + n * 0.15f };
        }

        private static Sample NoisePat(float u, float v, int seed, float scale)
        {
            int p = Freq(scale, 6);
            float n = Noise.FBM(u * p, v * p, p, 5, 2f, 0.55f, seed);
            float detail = Noise.FBM(u * p * 3f, v * p * 3f, p * 3, 3, 2f, 0.5f, seed + 77);
            float h = Mathf.Clamp01(n * 0.8f + detail * 0.2f);
            return new Sample { T = h, Height = h };
        }

        private static Sample Stripes(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 8);
            float wobble = Noise.FBM(u * 4f, v * 4f, 4, 3, 2f, 0.5f, seed) * 0.05f;
            float s = Mathf.Sin((v + wobble) * count * Mathf.PI * 2f);
            float t = s > 0f ? 0f : 1f;
            float edge = Mathf.SmoothStep(0f, 0.15f, Mathf.Abs(s));
            return new Sample { T = t, Height = 0.35f + edge * 0.3f };
        }

        private static Sample Plaid(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 6);
            float su = Mathf.Sin(u * count * Mathf.PI * 2f);
            float sv = Mathf.Sin(v * count * Mathf.PI * 2f);
            float band = (su > 0f ? 0.5f : 0f) + (sv > 0f ? 0.5f : 0f);
            float h = 0.3f + (Mathf.Abs(su) + Mathf.Abs(sv)) * 0.2f;
            return new Sample { T = band, Height = h };
        }

        private static Sample Camo(float u, float v, int seed, float scale)
        {
            int p = Freq(scale, 4);
            float a = Noise.FBM(u * p, v * p, p, 4, 2f, 0.55f, seed);
            float b = Noise.FBM(u * p + 5f, v * p + 5f, p, 4, 2f, 0.55f, seed + 31);
            float t;
            if (a < 0.45f) t = 0f;
            else if (b < 0.5f) t = 0.5f;
            else t = 1f;
            return new Sample { T = t, Height = 0.45f + a * 0.1f };
        }

        private static Sample Dots(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 8);
            float fu = Frac(u * count) - 0.5f;
            float fv = Frac(v * count) - 0.5f;
            float d = Mathf.Sqrt(fu * fu + fv * fv);
            float dot = Mathf.SmoothStep(0.35f, 0.25f, d);
            return new Sample { T = dot, Height = 0.35f + dot * 0.3f };
        }

        private static Sample Hexagons(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 7);
            float x = u * count;
            float y = v * count * 1.1547f; // hex vertical spacing
            float row = Mathf.Floor(y);
            if (((int)row & 1) == 1) x += 0.5f;
            float fx = Frac(x) - 0.5f;
            float fy = Frac(y) - 0.5f;
            float hex = Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fx) * 0.5f + Mathf.Abs(fy));
            float edge = Mathf.SmoothStep(0.45f, 0.5f, hex);
            float id = Hash01((int)Mathf.Floor(x) * 31 + (int)row * 17 + seed);
            return new Sample { T = Mathf.Lerp(0.2f, 0.8f, id), Height = 0.5f - edge * 0.35f };
        }

        private static Sample Scales(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 10);
            float y = v * count;
            float row = Mathf.Floor(y);
            float x = u * count + (((int)row & 1) == 1 ? 0.5f : 0f);
            float fx = Frac(x) - 0.5f;
            float fy = Frac(y);
            float d = Mathf.Sqrt(fx * fx + (fy - 0.5f) * (fy - 0.5f) * 0.6f);
            float scaleMask = Mathf.SmoothStep(0.5f, 0.1f, d);
            float id = Hash01((int)Mathf.Floor(x) + (int)row * 53 + seed);
            return new Sample { T = Mathf.Lerp(0.1f, 0.9f, id * 0.6f + 0.2f), Height = 0.3f + scaleMask * 0.5f };
        }

        private static Sample Cells(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 6);
            float d = Noise.Worley(u * count, v * count, count, seed, out float id);
            float edge = Mathf.SmoothStep(0.0f, 0.08f, d);
            return new Sample { T = id, Height = 0.3f + edge * 0.5f };
        }

        private static Sample Stars(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 24);
            float fu = Frac(u * count);
            float fv = Frac(v * count);
            int cx = (int)Mathf.Floor(u * count);
            int cy = (int)Mathf.Floor(v * count);
            float present = Hash01(cx * 13 + cy * 7 + seed);
            float star = 0f;
            if (present > 0.93f)
            {
                float d = Vector2.Distance(new Vector2(fu, fv), new Vector2(0.5f, 0.5f));
                star = Mathf.SmoothStep(0.2f, 0.0f, d);
            }
            return new Sample { T = star, Height = 0.5f };
        }

        private static Sample Galaxy(float u, float v, int seed, float scale)
        {
            int p = Freq(scale, 4);
            float clouds = Noise.FBM(u * p, v * p, p, 6, 2f, 0.6f, seed);
            var star = Stars(u, v, seed + 999, scale * 1.5f);
            float t = Mathf.Clamp01(clouds * 0.7f + star.T);
            return new Sample { T = t, Height = 0.5f };
        }

        private static Sample Circuit(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 12);
            float fx = Frac(u * count);
            float fy = Frac(v * count);
            float line = 0f;
            if (fx < 0.08f || fy < 0.08f) line = 1f;
            int cx = (int)Mathf.Floor(u * count);
            int cy = (int)Mathf.Floor(v * count);
            float node = (Hash01(cx * 7 + cy * 3 + seed) > 0.85f &&
                          Vector2.Distance(new Vector2(fx, fy), new Vector2(0.04f, 0.04f)) < 0.12f) ? 1f : 0f;
            float t = Mathf.Max(line, node);
            return new Sample { T = t, Height = 0.4f + t * 0.2f };
        }

        private static Sample Floral(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 5);
            float y = v * count;
            float row = Mathf.Floor(y);
            float x = u * count + (((int)row & 1) == 1 ? 0.5f : 0f);
            float fx = Frac(x) - 0.5f;
            float fy = Frac(y) - 0.5f;
            float ang = Mathf.Atan2(fy, fx);
            float r = Mathf.Sqrt(fx * fx + fy * fy);
            float petals = 0.25f + 0.12f * Mathf.Cos(ang * 6f);
            float flower = Mathf.SmoothStep(petals + 0.05f, petals - 0.05f, r);
            return new Sample { T = flower, Height = 0.4f + flower * 0.2f };
        }

        private static Sample Gradient(float u, float v, int seed, float scale)
        {
            float n = Noise.FBM(u * 3f, v * 3f, 3, 3, 2f, 0.5f, seed) * 0.1f;
            float t = Mathf.Clamp01(v + n);
            return new Sample { T = t, Height = 0.5f };
        }

        private static Sample Marble(float u, float v, int seed, float scale)
        {
            int p = Freq(scale, 4);
            float turb = Noise.FBM(u * p, v * p, p, 6, 2f, 0.6f, seed);
            float veins = Mathf.Sin((u + v + turb * 2f) * Mathf.PI * 4f);
            float t = Mathf.Abs(veins);
            return new Sample { T = 1f - t, Height = 0.45f + (1f - t) * 0.2f };
        }

        private static Sample Denim(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 200);
            float diag = Mathf.Sin((u * count + v * count) * Mathf.PI);
            float weave = Noise.FBM(u * 16f, v * 16f, 16, 3, 2f, 0.5f, seed) * 0.3f;
            float t = 0.2f + Mathf.Abs(diag) * 0.2f + weave;
            return new Sample { T = Mathf.Clamp01(t), Height = 0.4f + Mathf.Abs(diag) * 0.2f };
        }

        private static Sample Weave(float u, float v, int seed, float scale)
        {
            int count = Freq(scale, 64);
            float su = Mathf.Sin(u * count * Mathf.PI * 2f);
            float sv = Mathf.Sin(v * count * Mathf.PI * 2f);
            float over = su * sv;
            float fiber = Noise.FBM(u * count, v * count, count, 2, 2f, 0.5f, seed) * 0.15f;
            float h = 0.4f + over * 0.25f + fiber;
            return new Sample { T = Mathf.Clamp01(0.4f + over * 0.2f), Height = Mathf.Clamp01(h) };
        }

        private static float Frac(float v) { v -= Mathf.Floor(v); return v; }

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

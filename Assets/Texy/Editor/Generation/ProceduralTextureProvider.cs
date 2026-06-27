using System;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// The fully-offline generation engine. Needs no network, no API key, no model download — it
    /// runs entirely on the CPU inside the Unity editor. It parses the prompt, renders an albedo
    /// from tileable patterns, and derives every other map (normal, emission, decal, PBR) so that
    /// the whole set is mutually consistent. With no prompt it falls back to geometry-driven
    /// auto-generation via <see cref="MeshContext"/>.
    /// </summary>
    public class ProceduralTextureProvider : ITextureProvider
    {
        public string Name => "Procedural (offline)";

        public bool IsReady(out string reason)
        {
            reason = null;
            return true; // always available
        }

        public void Generate(TextureRequest request, Action<float, string> onProgress, Action<TextureResult> onComplete)
        {
            try
            {
                var style = PromptParser.Parse(request.Prompt, request.Mesh);
                int effectiveSeed = ResolveSeed(request, style);

                onProgress?.Invoke(0.05f, "Interpreting prompt...");

                Texture2D tex;
                switch (request.MapType)
                {
                    case MapType.Albedo: tex = GenerateAlbedo(request, style, effectiveSeed, onProgress); break;
                    case MapType.Normal: tex = GenerateNormal(request, style, effectiveSeed, onProgress); break;
                    case MapType.Emission: tex = GenerateEmission(request, style, effectiveSeed, onProgress); break;
                    case MapType.Decal: tex = GenerateDecal(request, style, effectiveSeed, onProgress); break;
                    case MapType.Height: tex = GenerateHeight(request, style, effectiveSeed, onProgress); break;
                    case MapType.AmbientOcclusion: tex = GenerateAO(request, style, effectiveSeed, onProgress); break;
                    case MapType.Roughness: tex = GenerateRoughness(request, style, effectiveSeed, onProgress, invert: false); break;
                    case MapType.Smoothness: tex = GenerateRoughness(request, style, effectiveSeed, onProgress, invert: true); break;
                    case MapType.Metallic: tex = GenerateMetallic(request, style, effectiveSeed, onProgress); break;
                    default: tex = GenerateAlbedo(request, style, effectiveSeed, onProgress); break;
                }

                onProgress?.Invoke(1f, "Done");
                onComplete?.Invoke(TextureResult.Ok(tex, request.MapType, Name));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(TextureResult.Fail(e.Message, request.MapType, Name));
            }
        }

        private static int ResolveSeed(TextureRequest req, PromptParser.TextureStyle style)
        {
            int s = req.Seed;
            if (req.Mesh != null) s ^= req.Mesh.GeometrySeed;
            if (!string.IsNullOrEmpty(req.Prompt)) s ^= req.Prompt.GetHashCode();
            s ^= (int)style.Pattern * 7919 + (int)style.Material * 104729;
            return s & 0x7fffffff;
        }

        // ---- Shared height field (reused for normal/AO/height so derived maps match the albedo) ----
        private float[] BuildHeightField(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            int w = req.Width, h = req.Height;

            // "Derive from existing" path: if the user supplied an albedo, height comes from its luminance.
            if (req.SourceAlbedo != null)
            {
                onProgress?.Invoke(0.2f, "Reading source albedo...");
                return MapPostProcessor.AlbedoToHeight(req.SourceAlbedo, w, h);
            }

            var height = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                if ((y & 63) == 0) onProgress?.Invoke(0.1f + 0.4f * y / h, "Building surface height...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    height[y * w + x] = PatternRenderer.Evaluate(style, u, v, seed).Height;
                }
            }
            return height;
        }

        private Texture2D GenerateAlbedo(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            int w = req.Width, h = req.Height;
            var pixels = new Color[w * h];

            UVIslandMapper.UVMaps uvMaps = null;
            if (req.Mesh != null)
            {
                onProgress?.Invoke(0.1f, "Mapping UV islands...");
                uvMaps = UVIslandMapper.Build(req.Mesh, Mathf.Min(512, w));
            }

            for (int y = 0; y < h; y++)
            {
                if ((y & 31) == 0) onProgress?.Invoke(0.2f + 0.7f * y / h, "Painting albedo...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    var s = PatternRenderer.Evaluate(style, u, v, seed);

                    Color c = SampleRamp(style, ApplyContrast(s.T, style.Contrast));

                    // Soft form shading from height gives generated flats a sense of relief.
                    float shade = Mathf.Lerp(0.82f, 1.08f, s.Height);
                    c *= shade;

                    if (style.Iridescent)
                        c = ApplyIridescence(c, s.Height + u * 0.3f);

                    // Geometry awareness: vary tone subtly per UV island so parts read distinctly.
                    if (uvMaps != null)
                    {
                        int mx = Mathf.Clamp((int)(u * uvMaps.Size), 0, uvMaps.Size - 1);
                        int my = Mathf.Clamp((int)(v * uvMaps.Size), 0, uvMaps.Size - 1);
                        float tone = uvMaps.SampleTone(mx, my);
                        c *= Mathf.Lerp(0.94f, 1.06f, tone);
                    }

                    c.a = 1f;
                    pixels[y * w + x] = ClampColor(c);
                }
            }

            return BuildTexture(pixels, w, h, linear: false);
        }

        private Texture2D GenerateNormal(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            var height = BuildHeightField(req, style, seed, onProgress);
            onProgress?.Invoke(0.7f, "Computing normals...");
            var pixels = MapPostProcessor.HeightToNormal(height, req.Width, req.Height, req.Strength);
            return BuildTexture(pixels, req.Width, req.Height, linear: true);
        }

        private Texture2D GenerateHeight(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            var height = BuildHeightField(req, style, seed, onProgress);
            var pixels = MapPostProcessor.HeightToGrayscale(height, req.Width, req.Height);
            return BuildTexture(pixels, req.Width, req.Height, linear: true);
        }

        private Texture2D GenerateAO(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            var height = BuildHeightField(req, style, seed, onProgress);
            onProgress?.Invoke(0.7f, "Baking ambient occlusion...");
            var pixels = MapPostProcessor.HeightToAO(height, req.Width, req.Height, req.Strength);
            return BuildTexture(pixels, req.Width, req.Height, linear: true);
        }

        private Texture2D GenerateRoughness(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress, bool invert)
        {
            int w = req.Width, h = req.Height;
            var pixels = new Color[w * h];
            float baseRough = Mathf.Clamp01(style.Roughness);

            for (int y = 0; y < h; y++)
            {
                if ((y & 63) == 0) onProgress?.Invoke(0.2f + 0.7f * y / h, invert ? "Building smoothness..." : "Building roughness...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    var s = PatternRenderer.Evaluate(style, u, v, seed);
                    // Crevices (low height) read rougher; raised areas a touch smoother.
                    float r = Mathf.Clamp01(baseRough + (0.5f - s.Height) * 0.3f);
                    float val = invert ? 1f - r : r;
                    pixels[y * w + x] = new Color(val, val, val, 1f);
                }
            }
            return BuildTexture(pixels, w, h, linear: true);
        }

        private Texture2D GenerateMetallic(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            int w = req.Width, h = req.Height;
            float metal = Mathf.Clamp01(style.Metallic);

            if (metal <= 0.001f)
            {
                onProgress?.Invoke(0.5f, "Building metallic mask...");
                return BuildTexture(MapPostProcessor.ConstantGray(w, h, 0f), w, h, linear: true);
            }

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                if ((y & 63) == 0) onProgress?.Invoke(0.2f + 0.7f * y / h, "Building metallic mask...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    var s = PatternRenderer.Evaluate(style, u, v, seed);
                    float m = Mathf.Clamp01(metal * Mathf.Lerp(0.85f, 1f, s.Height));
                    pixels[y * w + x] = new Color(m, m, m, 1f);
                }
            }
            return BuildTexture(pixels, w, h, linear: true);
        }

        private Texture2D GenerateEmission(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            int w = req.Width, h = req.Height;
            var pixels = new Color[w * h];
            Color glow = style.AccentColor;
            // Brighten the accent so it actually emits.
            Color.RGBToHSV(glow, out float gh, out float gs, out float gv);
            glow = Color.HSVToRGB(gh, Mathf.Clamp01(gs * 1.1f), 1f);

            bool fullyEmissive = style.Material == PromptParser.Material.Neon || style.EmissiveHint;

            for (int y = 0; y < h; y++)
            {
                if ((y & 63) == 0) onProgress?.Invoke(0.2f + 0.7f * y / h, "Generating emission...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    var s = PatternRenderer.Evaluate(style, u, v, seed);

                    // The pattern's "high" regions emit (circuit lines, neon stripes, runes, stars...).
                    float mask = fullyEmissive ? Mathf.SmoothStep(0.45f, 0.75f, s.T)
                                               : Mathf.SmoothStep(0.8f, 0.95f, s.T);
                    mask *= req.Strength;
                    Color c = glow * mask;
                    c.a = 1f;
                    pixels[y * w + x] = ClampColor(c);
                }
            }
            return BuildTexture(pixels, w, h, linear: false);
        }

        private Texture2D GenerateDecal(TextureRequest req, PromptParser.TextureStyle style, int seed, Action<float, string> onProgress)
        {
            int w = req.Width, h = req.Height;
            var pixels = new Color[w * h];
            Color ink = style.PrimaryColor;

            UVIslandMapper.UVMaps uvMaps = null;
            if (req.Mesh != null)
            {
                onProgress?.Invoke(0.1f, "Mapping placement surface...");
                uvMaps = UVIslandMapper.Build(req.Mesh, Mathf.Min(512, w));
            }

            // A decal/tattoo is a localized motif on transparent backing. We build line-work from
            // ridged noise so it reads as flowing tribal/ornamental strokes, then confine it with a
            // soft radial falloff (and the UV coverage mask when geometry is known).
            for (int y = 0; y < h; y++)
            {
                if ((y & 63) == 0) onProgress?.Invoke(0.2f + 0.7f * y / h, "Inking decal...");
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;

                    float ridged = Noise.Ridged(u * 6f * style.DetailScale, v * 6f * style.DetailScale, 6, 4, seed);
                    float lines = Mathf.SmoothStep(0.72f, 0.9f, ridged);

                    // Confine to a centered oval so it behaves like a placed decal, not a full wrap.
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float radial = Mathf.SmoothStep(1.0f, 0.5f, Mathf.Sqrt(dx * dx + dy * dy));

                    float alpha = lines * radial * req.Strength;

                    if (uvMaps != null)
                    {
                        int mx = Mathf.Clamp((int)(u * uvMaps.Size), 0, uvMaps.Size - 1);
                        int my = Mathf.Clamp((int)(v * uvMaps.Size), 0, uvMaps.Size - 1);
                        alpha *= uvMaps.SampleCoverage(mx, my);
                    }

                    Color c = ink;
                    c.a = Mathf.Clamp01(alpha);
                    pixels[y * w + x] = c;
                }
            }
            return BuildTexture(pixels, w, h, linear: false, alpha: true);
        }

        // ---- color helpers ----
        private static Color SampleRamp(PromptParser.TextureStyle style, float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.5f) return Color.Lerp(style.PrimaryColor, style.SecondaryColor, t * 2f);
            return Color.Lerp(style.SecondaryColor, style.AccentColor, (t - 0.5f) * 2f);
        }

        private static float ApplyContrast(float t, float contrast)
        {
            return Mathf.Clamp01((t - 0.5f) * contrast + 0.5f);
        }

        private static Color ApplyIridescence(Color c, float phase)
        {
            Color.RGBToHSV(c, out float hHue, out float s, out float v);
            hHue = Frac(hHue + phase * 0.5f);
            return Color.HSVToRGB(hHue, Mathf.Clamp01(s + 0.2f), v);
        }

        private static Color ClampColor(Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }

        private static float Frac(float v) { v -= Mathf.Floor(v); return v; }

        private static Texture2D BuildTexture(Color[] pixels, int w, int h, bool linear, bool alpha = false)
        {
            var tex = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, linear);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Turns a free-text prompt into a structured <see cref="TextureStyle"/> the procedural engine can
    /// render deterministically: a color palette, a material family, a surface pattern and modifiers.
    /// This is intentionally a curated, transparent keyword model (not a black box) so results are
    /// predictable and easy to extend. When the AI engine is active this still runs, to drive the
    /// post-processors (normal/emission/PBR) consistently with the AI albedo.
    /// </summary>
    public static class PromptParser
    {
        public enum Material { Generic, Skin, Fabric, Metal, Leather, Wood, Stone, Plastic, Scales, Fur, Glass, Neon, Latex }
        public enum Pattern { Auto, Solid, Noise, Stripes, Plaid, Camo, Dots, Hexagons, Scales, Cells, Stars, Galaxy, Circuit, Floral, Gradient, Marble, Denim, Weave }

        public class TextureStyle
        {
            public List<Color> Palette = new List<Color>();
            public Material Material = Material.Generic;
            public Pattern Pattern = Pattern.Auto;

            public float Roughness = 0.6f;   // 0 = mirror, 1 = matte
            public float Metallic = 0f;
            public float DetailScale = 1f;   // multiplies pattern frequency
            public float Contrast = 1f;
            public bool Glossy;
            public bool Metallicness;
            public bool EmissiveHint;        // prompt mentions glow/neon/led/light
            public bool Iridescent;

            public Color PrimaryColor => Palette.Count > 0 ? Palette[0] : new Color(0.6f, 0.6f, 0.62f);
            public Color SecondaryColor => Palette.Count > 1 ? Palette[1] : PrimaryColor * 0.7f;
            public Color AccentColor => Palette.Count > 2 ? Palette[2] : SecondaryColor;
        }

        // ---- Curated color vocabulary (extend freely) ----
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "red", Hex("E23A3A") }, { "crimson", Hex("B11226") }, { "scarlet", Hex("FF2400") },
            { "orange", Hex("F08D24") }, { "amber", Hex("FFBF00") }, { "gold", Hex("D4AF37") },
            { "yellow", Hex("F2D335") }, { "lime", Hex("9ACD32") }, { "green", Hex("3FA34D") },
            { "emerald", Hex("2ECC71") }, { "teal", Hex("1ABC9C") }, { "cyan", Hex("22D3EE") },
            { "blue", Hex("3B82F6") }, { "navy", Hex("1E3A8A") }, { "azure", Hex("4F9DDE") },
            { "indigo", Hex("4338CA") }, { "purple", Hex("8B5CF6") }, { "violet", Hex("7C3AED") },
            { "magenta", Hex("D946EF") }, { "pink", Hex("EC4899") }, { "rose", Hex("F43F5E") },
            { "white", Hex("F5F5F5") }, { "black", Hex("141414") }, { "gray", Hex("808080") },
            { "grey", Hex("808080") }, { "silver", Hex("C0C0C0") }, { "brown", Hex("8B5A2B") },
            { "tan", Hex("D2B48C") }, { "beige", Hex("E8D8B0") }, { "cream", Hex("F3E9D2") },
            { "mint", Hex("98FF98") }, { "lavender", Hex("C4B5FD") }, { "turquoise", Hex("40E0D0") },
            { "maroon", Hex("800000") }, { "olive", Hex("808000") }, { "coral", Hex("FF7F50") },
            { "peach", Hex("FFCBA4") }, { "ivory", Hex("FFFFF0") }, { "charcoal", Hex("36454F") },
            { "bronze", Hex("CD7F32") }, { "copper", Hex("B87333") }, { "platinum", Hex("E5E4E2") },
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>
        {
            { "skin", Material.Skin }, { "flesh", Material.Skin }, { "body", Material.Skin },
            { "fabric", Material.Fabric }, { "cloth", Material.Fabric }, { "cotton", Material.Fabric },
            { "denim", Material.Fabric }, { "wool", Material.Fabric }, { "silk", Material.Fabric }, { "shirt", Material.Fabric },
            { "metal", Material.Metal }, { "steel", Material.Metal }, { "iron", Material.Metal }, { "chrome", Material.Metal },
            { "gold", Material.Metal }, { "armor", Material.Metal }, { "brushed", Material.Metal },
            { "leather", Material.Leather }, { "hide", Material.Leather },
            { "wood", Material.Wood }, { "plank", Material.Wood }, { "bark", Material.Wood },
            { "stone", Material.Stone }, { "rock", Material.Stone }, { "marble", Material.Stone }, { "concrete", Material.Stone },
            { "plastic", Material.Plastic }, { "rubber", Material.Plastic },
            { "scale", Material.Scales }, { "scales", Material.Scales }, { "reptile", Material.Scales }, { "dragon", Material.Scales }, { "fish", Material.Scales },
            { "fur", Material.Fur }, { "hair", Material.Fur }, { "fluff", Material.Fur },
            { "glass", Material.Glass }, { "crystal", Material.Glass },
            { "neon", Material.Neon }, { "led", Material.Neon }, { "glow", Material.Neon },
            { "latex", Material.Latex }, { "vinyl", Material.Latex }, { "pvc", Material.Latex },
        };

        private static readonly Dictionary<string, Pattern> Patterns = new Dictionary<string, Pattern>
        {
            { "solid", Pattern.Solid }, { "plain", Pattern.Solid },
            { "noise", Pattern.Noise }, { "grunge", Pattern.Noise }, { "grungy", Pattern.Noise }, { "rough", Pattern.Noise },
            { "stripe", Pattern.Stripes }, { "stripes", Pattern.Stripes }, { "striped", Pattern.Stripes },
            { "plaid", Pattern.Plaid }, { "tartan", Pattern.Plaid }, { "checker", Pattern.Plaid }, { "checkered", Pattern.Plaid },
            { "camo", Pattern.Camo }, { "camouflage", Pattern.Camo }, { "military", Pattern.Camo },
            { "dots", Pattern.Dots }, { "polka", Pattern.Dots }, { "dotted", Pattern.Dots }, { "spotted", Pattern.Dots },
            { "hex", Pattern.Hexagons }, { "hexagon", Pattern.Hexagons }, { "honeycomb", Pattern.Hexagons }, { "tech", Pattern.Hexagons }, { "cyber", Pattern.Hexagons },
            { "scale", Pattern.Scales }, { "scales", Pattern.Scales }, { "scaled", Pattern.Scales },
            { "cells", Pattern.Cells }, { "cellular", Pattern.Cells }, { "voronoi", Pattern.Cells }, { "cracked", Pattern.Cells },
            { "star", Pattern.Stars }, { "stars", Pattern.Stars }, { "starry", Pattern.Stars },
            { "galaxy", Pattern.Galaxy }, { "space", Pattern.Galaxy }, { "nebula", Pattern.Galaxy }, { "cosmic", Pattern.Galaxy },
            { "circuit", Pattern.Circuit }, { "circuitry", Pattern.Circuit }, { "wire", Pattern.Circuit },
            { "floral", Pattern.Floral }, { "flower", Pattern.Floral }, { "flowers", Pattern.Floral },
            { "gradient", Pattern.Gradient }, { "ombre", Pattern.Gradient }, { "fade", Pattern.Gradient },
            { "marble", Pattern.Marble }, { "marbled", Pattern.Marble }, { "swirl", Pattern.Marble },
            { "denim", Pattern.Denim }, { "jeans", Pattern.Denim },
            { "weave", Pattern.Weave }, { "woven", Pattern.Weave }, { "knit", Pattern.Weave },
        };

        public static TextureStyle Parse(string prompt, MeshContext mesh)
        {
            var style = new TextureStyle();
            string p = (prompt ?? string.Empty).ToLowerInvariant();
            var tokens = Tokenize(p);

            // --- Colors (ordered by appearance so "blue and white" => primary blue) ---
            foreach (var t in tokens)
                if (Colors.TryGetValue(t, out var c) && !ContainsColor(style.Palette, c))
                    style.Palette.Add(c);

            // --- Material ---
            foreach (var t in tokens)
                if (Materials.TryGetValue(t, out var m)) { style.Material = m; break; }

            // --- Pattern ---
            foreach (var t in tokens)
                if (Patterns.TryGetValue(t, out var pat)) { style.Pattern = pat; break; }

            // --- Modifiers ---
            style.Glossy = ContainsAny(p, "glossy", "shiny", "wet", "polished", "metallic", "chrome", "latex", "vinyl");
            style.Metallicness = ContainsAny(p, "metal", "metallic", "chrome", "steel", "gold", "silver", "iron", "bronze", "copper");
            style.EmissiveHint = ContainsAny(p, "glow", "glowing", "neon", "led", "emissive", "light", "luminous", "bioluminescent", "rune");
            style.Iridescent = ContainsAny(p, "iridescent", "holographic", "holo", "rainbow", "opal", "pearlescent");
            if (ContainsAny(p, "matte", "flat", "dull")) style.Roughness = 0.85f;
            if (ContainsAny(p, "detailed", "intricate", "fine")) style.DetailScale = 1.6f;
            if (ContainsAny(p, "large", "big", "bold")) style.DetailScale = 0.6f;
            if (ContainsAny(p, "high contrast", "vivid", "bright")) style.Contrast = 1.3f;
            if (ContainsAny(p, "soft", "pastel", "subtle")) style.Contrast = 0.75f;

            ApplyMaterialDefaults(style);
            ResolvePalette(style, prompt, mesh);
            ResolvePattern(style, mesh);
            return style;
        }

        private static void ApplyMaterialDefaults(TextureStyle s)
        {
            switch (s.Material)
            {
                case Material.Skin: s.Roughness = 0.55f; break;
                case Material.Fabric: s.Roughness = 0.85f; break;
                case Material.Metal: s.Roughness = 0.25f; s.Metallic = 1f; s.Glossy = true; break;
                case Material.Leather: s.Roughness = 0.7f; break;
                case Material.Wood: s.Roughness = 0.75f; break;
                case Material.Stone: s.Roughness = 0.9f; break;
                case Material.Plastic: s.Roughness = 0.4f; break;
                case Material.Scales: s.Roughness = 0.45f; break;
                case Material.Fur: s.Roughness = 0.95f; break;
                case Material.Glass: s.Roughness = 0.05f; s.Glossy = true; break;
                case Material.Neon: s.Roughness = 0.3f; s.EmissiveHint = true; break;
                case Material.Latex: s.Roughness = 0.12f; s.Glossy = true; break;
            }
            if (s.Glossy) s.Roughness = Mathf.Min(s.Roughness, 0.3f);
            if (s.Metallicness) s.Metallic = Mathf.Max(s.Metallic, 0.8f);
        }

        /// <summary>Guarantee a usable palette: fill from material defaults / geometry when the prompt named no colors.</summary>
        private static void ResolvePalette(TextureStyle s, string prompt, MeshContext mesh)
        {
            if (s.Palette.Count == 0)
            {
                Color baseColor = MaterialBaseColor(s.Material, mesh);
                s.Palette.Add(baseColor);
            }

            // Derive complementary secondary/accent so single-color prompts still produce depth.
            while (s.Palette.Count < 3)
            {
                Color baseC = s.Palette[s.Palette.Count - 1];
                Color.RGBToHSV(baseC, out float h, out float sat, out float v);
                if (s.Palette.Count == 1)
                    s.Palette.Add(Color.HSVToRGB(Frac(h + 0.04f), Mathf.Clamp01(sat * 0.9f), Mathf.Clamp01(v * 0.75f)));
                else
                    s.Palette.Add(Color.HSVToRGB(Frac(h + 0.5f), Mathf.Clamp01(sat * 0.8f + 0.1f), Mathf.Clamp01(v * 1.1f)));
            }
        }

        private static void ResolvePattern(TextureStyle s, MeshContext mesh)
        {
            if (s.Pattern != Pattern.Auto) return;

            // No explicit pattern: pick something tasteful from the material, then the geometry.
            switch (s.Material)
            {
                case Material.Skin: s.Pattern = Pattern.Noise; s.Contrast = 0.6f; return;
                case Material.Fabric: s.Pattern = Pattern.Weave; return;
                case Material.Metal: s.Pattern = Pattern.Noise; return;
                case Material.Scales: s.Pattern = Pattern.Scales; return;
                case Material.Fur: s.Pattern = Pattern.Noise; s.DetailScale = 2f; return;
                case Material.Stone: s.Pattern = Pattern.Marble; return;
                case Material.Wood: s.Pattern = Pattern.Stripes; return;
                case Material.Neon: s.Pattern = Pattern.Circuit; return;
            }

            if (mesh != null)
            {
                switch (mesh.Guess)
                {
                    case MeshContext.SurfaceGuess.Clothing: s.Pattern = Pattern.Weave; return;
                    case MeshContext.SurfaceGuess.HardSurface: s.Pattern = Pattern.Hexagons; return;
                    case MeshContext.SurfaceGuess.HairOrFur: s.Pattern = Pattern.Noise; s.DetailScale = 2f; return;
                    case MeshContext.SurfaceGuess.BodySkin: s.Pattern = Pattern.Noise; s.Contrast = 0.6f; return;
                }
            }
            s.Pattern = Pattern.Noise;
        }

        private static Color MaterialBaseColor(Material m, MeshContext mesh)
        {
            switch (m)
            {
                case Material.Skin: return Hex("E8B89B");
                case Material.Metal: return Hex("9AA0A6");
                case Material.Leather: return Hex("6B4226");
                case Material.Wood: return Hex("8B5A2B");
                case Material.Stone: return Hex("8C8C84");
                case Material.Fur: return Hex("B89B72");
                case Material.Scales: return Hex("2E8B57");
                case Material.Glass: return Hex("CFE8F0");
                case Material.Neon: return Hex("19D3FF");
                case Material.Latex: return Hex("1A1A1A");
                case Material.Fabric: return Hex("4A6FA5");
                default:
                    // No prompt, generic material: derive a stable hue from geometry so each part differs.
                    if (mesh != null)
                    {
                        float h = (mesh.GeometrySeed % 360) / 360f;
                        return Color.HSVToRGB(h, 0.45f, 0.8f);
                    }
                    return Hex("9AA0A6");
            }
        }

        // ---- helpers ----
        private static List<string> Tokenize(string p)
        {
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            foreach (char ch in p)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var n in needles)
                if (text.Contains(n)) return true;
            return false;
        }

        private static bool ContainsColor(List<Color> palette, Color c)
        {
            foreach (var p in palette)
                if (Vector4.Distance(p, c) < 0.001f) return true;
            return false;
        }

        private static float Frac(float v) { v -= Mathf.Floor(v); return v; }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}

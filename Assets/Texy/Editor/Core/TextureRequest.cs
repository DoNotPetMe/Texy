using UnityEngine;

namespace Texy
{
    /// <summary>
    /// A fully resolved description of a single texture to generate. The UI builds one of
    /// these, the active <see cref="ITextureProvider"/> consumes it, and the import pipeline
    /// uses it to decide color space / importer settings. Keeping every knob on one struct
    /// makes generation deterministic and easy to reproduce from a saved seed.
    /// </summary>
    public class TextureRequest
    {
        public string Prompt = string.Empty;
        public MapType MapType = MapType.Albedo;

        public int Width = 2048;
        public int Height = 2048;

        /// <summary>Deterministic seed. Same seed + same prompt + same map = identical output.</summary>
        public int Seed = 0;

        /// <summary>0..1 strength applied to normal/emission/decal effects.</summary>
        public float Strength = 1f;

        /// <summary>Whether the result should tile seamlessly (true for body/clothing atlases).</summary>
        public bool Tileable = true;

        /// <summary>
        /// Optional geometry context. When present, the procedural engine seeds and masks
        /// generation from the avatar's mesh & UV layout, enabling no-prompt auto generation.
        /// </summary>
        public MeshContext Mesh;

        /// <summary>
        /// Optional source albedo. Derived maps (Normal, Emission, AO, Height...) are produced
        /// from this when present so that, e.g., a normal map matches the albedo the user already has.
        /// </summary>
        public Texture2D SourceAlbedo;

        public TextureRequest Clone()
        {
            return new TextureRequest
            {
                Prompt = Prompt,
                MapType = MapType,
                Width = Width,
                Height = Height,
                Seed = Seed,
                Strength = Strength,
                Tileable = Tileable,
                Mesh = Mesh,
                SourceAlbedo = SourceAlbedo
            };
        }
    }
}

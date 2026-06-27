namespace Texy
{
    /// <summary>
    /// The kinds of texture maps Texy can generate. Each map type carries enough
    /// metadata (via <see cref="MapTypeInfo"/>) for the import pipeline to apply
    /// the correct color space and importer settings.
    /// </summary>
    public enum MapType
    {
        Albedo,
        Normal,
        Emission,
        Decal,
        Metallic,
        Roughness,
        AmbientOcclusion,
        Height,
        Smoothness
    }

    /// <summary>
    /// Static metadata describing how each <see cref="MapType"/> should be authored,
    /// imported and assigned to a material. Centralizing this keeps the color-space
    /// rules (a very common source of VRChat texture bugs) in exactly one place.
    /// </summary>
    public static class MapTypeInfo
    {
        public static string DisplayName(MapType type)
        {
            switch (type)
            {
                case MapType.Albedo: return "Albedo (Base Color)";
                case MapType.Normal: return "Normal Map";
                case MapType.Emission: return "Emission";
                case MapType.Decal: return "Decal / Tattoo";
                case MapType.Metallic: return "Metallic";
                case MapType.Roughness: return "Roughness";
                case MapType.AmbientOcclusion: return "Ambient Occlusion";
                case MapType.Height: return "Height";
                case MapType.Smoothness: return "Smoothness";
                default: return type.ToString();
            }
        }

        public static string FileSuffix(MapType type)
        {
            switch (type)
            {
                case MapType.Albedo: return "Albedo";
                case MapType.Normal: return "Normal";
                case MapType.Emission: return "Emission";
                case MapType.Decal: return "Decal";
                case MapType.Metallic: return "Metallic";
                case MapType.Roughness: return "Roughness";
                case MapType.AmbientOcclusion: return "AO";
                case MapType.Height: return "Height";
                case MapType.Smoothness: return "Smoothness";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// True when the texture stores perceptual color (gamma / sRGB). Albedo,
        /// emission and decals are sRGB; data maps (normal, roughness, metallic,
        /// AO, height, smoothness) must be linear or VRChat lighting will be wrong.
        /// </summary>
        public static bool IsColorData(MapType type)
        {
            switch (type)
            {
                case MapType.Albedo:
                case MapType.Emission:
                case MapType.Decal:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsNormalMap(MapType type)
        {
            return type == MapType.Normal;
        }

        /// <summary>Decals/tattoos carry meaningful alpha and must not be premultiplied away.</summary>
        public static bool UsesAlpha(MapType type)
        {
            return type == MapType.Decal;
        }
    }
}

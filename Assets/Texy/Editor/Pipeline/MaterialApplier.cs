using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Assigns a generated map to the right slot on a material, enabling the keywords the shader needs.
    /// Supports the Unity Standard shader and Poiyomi (the most common VRChat avatar shader) by probing
    /// for known property names, and degrades gracefully when a property is absent.
    /// </summary>
    public static class MaterialApplier
    {
        public static bool Apply(Material material, MapType mapType, Texture2D texture)
        {
            if (material == null || texture == null) return false;

            Undo.RecordObject(material, "Texy Apply Texture");
            bool applied = false;

            switch (mapType)
            {
                case MapType.Albedo:
                    applied = SetFirst(material, texture, "_MainTex", "_BaseMap", "_BaseColorMap");
                    break;

                case MapType.Normal:
                    applied = SetFirst(material, texture, "_BumpMap", "_NormalMap", "_NormalTex");
                    if (applied)
                    {
                        material.EnableKeyword("_NORMALMAP");
                        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
                    }
                    break;

                case MapType.Emission:
                    applied = SetFirst(material, texture, "_EmissionMap", "_EmissionColorMap");
                    if (applied)
                    {
                        material.EnableKeyword("_EMISSION");
                        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white);
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        if (material.HasProperty("_EmissionMap0")) material.SetTexture("_EmissionMap0", texture); // Poiyomi
                    }
                    break;

                case MapType.Decal:
                    // Poiyomi exposes numbered decal slots; Standard has none, so we report what we did.
                    applied = SetFirst(material, texture, "_DecalTexture", "_DecalMask", "_Decal0Texture");
                    if (!applied)
                        TexyLog.Warn("Material has no decal slot. The decal PNG was saved — assign it to a decal layer (Poiyomi) or blend it onto your albedo.");
                    break;

                case MapType.Metallic:
                    applied = SetFirst(material, texture, "_MetallicGlossMap", "_MetallicGlossMap0", "_MetallicMap");
                    if (applied) material.EnableKeyword("_METALLICGLOSSMAP");
                    break;

                case MapType.Roughness:
                case MapType.Smoothness:
                    applied = SetFirst(material, texture, "_SpecGlossMap", "_RoughnessMap", "_SmoothnessMap");
                    break;

                case MapType.AmbientOcclusion:
                    applied = SetFirst(material, texture, "_OcclusionMap", "_OcclusionMap0", "_AOMap");
                    break;

                case MapType.Height:
                    applied = SetFirst(material, texture, "_ParallaxMap", "_HeightMap");
                    if (applied) material.EnableKeyword("_PARALLAXMAP");
                    break;
            }

            if (applied)
            {
                EditorUtility.SetDirty(material);
                TexyLog.Info($"Applied {MapTypeInfo.DisplayName(mapType)} to material '{material.name}'.");
            }
            return applied;
        }

        private static bool SetFirst(Material material, Texture2D tex, params string[] propertyNames)
        {
            foreach (var prop in propertyNames)
            {
                if (material.HasProperty(prop))
                {
                    material.SetTexture(prop, tex);
                    return true;
                }
            }
            return false;
        }
    }
}

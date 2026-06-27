using System.IO;
using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Writes a generated <see cref="Texture2D"/> to the project as a PNG and applies the correct
    /// importer settings for its <see cref="MapType"/>. This is where the most common VRChat texture
    /// mistakes are prevented: wrong sRGB/linear flags, normal maps not imported as normal maps, and
    /// missing tiling. Returns the imported asset so it can be assigned to a material.
    /// </summary>
    public static class TextureImportPipeline
    {
        public static Texture2D SaveAndImport(Texture2D texture, MapType mapType, string baseName, string outputFolder)
        {
            EnsureFolder(outputFolder);

            string safeName = MakeSafe(baseName);
            string fileName = $"{safeName}_{MapTypeInfo.FileSuffix(mapType)}.png";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputFolder, fileName).Replace("\\", "/"));

            byte[] png = texture.EncodeToPNG();
            File.WriteAllBytes(assetPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            ConfigureImporter(assetPath, mapType, texture.width);

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            TexyLog.Info($"Saved {MapTypeInfo.DisplayName(mapType)} → {assetPath}");
            return imported;
        }

        private static void ConfigureImporter(string assetPath, MapType mapType, int width)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = MapTypeInfo.IsNormalMap(mapType)
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            // sRGB only for perceptual color maps; data maps stay linear.
            importer.sRGBTexture = MapTypeInfo.IsColorData(mapType);
            importer.alphaSource = MapTypeInfo.UsesAlpha(mapType)
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = MapTypeInfo.UsesAlpha(mapType);

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.maxTextureSize = Mathf.Clamp(NextPow2(width), 256, 4096);
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.anisoLevel = 4;

            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string MakeSafe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Texy";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Replace(' ', '_');
            return name.Length > 40 ? name.Substring(0, 40) : name;
        }

        private static int NextPow2(int v)
        {
            int p = 256;
            while (p < v && p < 4096) p <<= 1;
            return p;
        }
    }
}

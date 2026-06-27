using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Converts between map representations. Used to derive normal/AO/height/roughness/metallic/
    /// emission maps either from a procedurally generated height field or from an existing albedo
    /// the user already has on their avatar. All operations are tileable (edges wrap) so derived
    /// maps stay seamless.
    /// </summary>
    public static class MapPostProcessor
    {
        /// <summary>Tangent-space normal map (RGB) from a height field, Sobel-filtered, seamless.</summary>
        public static Color[] HeightToNormal(float[] height, int w, int h, float strength)
        {
            var result = new Color[w * h];
            float scale = Mathf.Lerp(1f, 8f, Mathf.Clamp01(strength));

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float hl = height[y * w + Wrap(x - 1, w)];
                float hr = height[y * w + Wrap(x + 1, w)];
                float hd = height[Wrap(y - 1, h) * w + x];
                float hu = height[Wrap(y + 1, h) * w + x];

                float dx = (hl - hr) * scale;
                float dy = (hd - hu) * scale;
                Vector3 n = new Vector3(dx, dy, 1f).normalized;

                result[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return result;
        }

        /// <summary>Cheap large-radius ambient occlusion: darken where the blurred height sits below the local height.</summary>
        public static Color[] HeightToAO(float[] height, int w, int h, float strength)
        {
            var blurred = BoxBlur(height, w, h, Mathf.Max(1, Mathf.RoundToInt(w / 256f)));
            var result = new Color[w * h];
            for (int i = 0; i < result.Length; i++)
            {
                float diff = height[i] - blurred[i];
                float ao = Mathf.Clamp01(1f + diff * Mathf.Lerp(1f, 4f, strength));
                result[i] = new Color(ao, ao, ao, 1f);
            }
            return result;
        }

        public static Color[] HeightToGrayscale(float[] height, int w, int h)
        {
            var result = new Color[w * h];
            for (int i = 0; i < result.Length; i++)
            {
                float v = Mathf.Clamp01(height[i]);
                result[i] = new Color(v, v, v, 1f);
            }
            return result;
        }

        public static Color[] ConstantGray(int w, int h, float value)
        {
            var result = new Color[w * h];
            var c = new Color(value, value, value, 1f);
            for (int i = 0; i < result.Length; i++) result[i] = c;
            return result;
        }

        /// <summary>Extract a perceptual-luminance height field from an albedo texture (for "derive from existing").</summary>
        public static float[] AlbedoToHeight(Texture2D albedo, int w, int h)
        {
            var pixels = ReadResized(albedo, w, h);
            var height = new float[w * h];
            for (int i = 0; i < height.Length; i++)
            {
                Color c = pixels[i];
                height[i] = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            }
            return height;
        }

        /// <summary>Read a texture into a CPU array at an arbitrary size, even if it's not marked readable.</summary>
        public static Color[] ReadResized(Texture2D source, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var temp = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            temp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            temp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = temp.GetPixels();
            Object.DestroyImmediate(temp);
            return pixels;
        }

        private static float[] BoxBlur(float[] src, int w, int h, int radius)
        {
            // Separable two-pass box blur, wrapping at edges to stay seamless.
            var tmp = new float[w * h];
            var dst = new float[w * h];
            float inv = 1f / (radius * 2 + 1);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += src[y * w + Wrap(x + k, w)];
                tmp[y * w + x] = sum * inv;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += tmp[Wrap(y + k, h) * w + x];
                dst[y * w + x] = sum * inv;
            }
            return dst;
        }

        private static int Wrap(int v, int n)
        {
            v %= n;
            return v < 0 ? v + n : v;
        }
    }
}

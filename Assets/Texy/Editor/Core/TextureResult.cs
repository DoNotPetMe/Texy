using UnityEngine;

namespace Texy
{
    /// <summary>Outcome of a generation call. Either <see cref="Texture"/> is set, or <see cref="Error"/> explains why not.</summary>
    public class TextureResult
    {
        public Texture2D Texture;
        public MapType MapType;
        public string Error;
        public string ProviderName;

        public bool Success => Texture != null && string.IsNullOrEmpty(Error);

        public static TextureResult Ok(Texture2D tex, MapType type, string provider)
        {
            return new TextureResult { Texture = tex, MapType = type, ProviderName = provider };
        }

        public static TextureResult Fail(string error, MapType type, string provider)
        {
            return new TextureResult { Error = error, MapType = type, ProviderName = provider };
        }
    }
}

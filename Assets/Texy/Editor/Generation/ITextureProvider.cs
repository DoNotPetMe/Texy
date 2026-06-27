using System;

namespace Texy
{
    /// <summary>
    /// A texture generation backend. Procedural and AI engines both implement this so the UI and
    /// pipeline never care which one is active. Generation may be async (AI network calls), so the
    /// contract is callback-based and always resolves on the Unity main thread.
    /// </summary>
    public interface ITextureProvider
    {
        string Name { get; }

        /// <summary>True when the provider is configured well enough to run (e.g. AI has an endpoint + key).</summary>
        bool IsReady(out string reason);

        /// <summary>
        /// Generate one map. <paramref name="onProgress"/> reports 0..1 with a status label.
        /// <paramref name="onComplete"/> is invoked exactly once, on the main thread.
        /// </summary>
        void Generate(TextureRequest request, Action<float, string> onProgress, Action<TextureResult> onComplete);
    }
}

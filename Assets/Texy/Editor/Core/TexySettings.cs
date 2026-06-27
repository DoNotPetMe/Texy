using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Persistent, project-local settings for Texy. Stored as a single JSON blob in
    /// EditorPrefs (keyed per project) so nothing secret is ever committed to the repo.
    /// The AI endpoint/key live here; the procedural engine needs none of it.
    /// </summary>
    [System.Serializable]
    public class TexySettings
    {
        public enum Engine { Procedural, AI }

        public Engine ActiveEngine = Engine.Procedural;

        // ---- Output ----
        public int DefaultResolution = 2048;
        public string OutputFolder = "Assets/Texy/Generated";
        public bool ApplyToMaterialOnGenerate = true;

        // ---- AI provider ----
        // Defaults describe an OpenAI-compatible images endpoint, but any text-to-image
        // REST service returning base64 or a URL can be configured here (SD-WebUI, ComfyUI proxy, etc.).
        public string AiEndpoint = "https://api.openai.com/v1/images/generations";
        public string AiModel = "gpt-image-1";
        public string AiApiKey = string.Empty;
        public AiResponseFormat AiResponse = AiResponseFormat.OpenAI;
        public int AiTimeoutSeconds = 180;

        public enum AiResponseFormat
        {
            /// <summary>{ "data": [ { "b64_json": "..." } ] } or { "data": [ { "url": "..." } ] }</summary>
            OpenAI,
            /// <summary>Automatic1111 / SD-WebUI: { "images": [ "base64..." ] }</summary>
            SDWebUI
        }

        private const string PrefKey = "Texy.Settings.v1";

        private static TexySettings _instance;
        public static TexySettings Instance => _instance ?? (_instance = Load());

        private static string ProjectScopedKey => PrefKey + ":" + Application.dataPath.GetHashCode();

        public static TexySettings Load()
        {
            var json = EditorPrefs.GetString(ProjectScopedKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new TexySettings();

            try
            {
                return JsonUtility.FromJson<TexySettings>(json) ?? new TexySettings();
            }
            catch
            {
                return new TexySettings();
            }
        }

        public void Save()
        {
            EditorPrefs.SetString(ProjectScopedKey, JsonUtility.ToJson(this));
        }
    }
}

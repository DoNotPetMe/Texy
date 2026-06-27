using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Texy's main editor window: a VRChat-focused avatar texture generator. Pick an avatar mesh
    /// and/or material, type a prompt (or leave it empty for geometry-driven auto generation),
    /// choose which maps to create, and Texy generates, imports with correct VRChat settings, and
    /// applies them. Works fully offline via the procedural engine; an optional AI engine adds
    /// prompt-to-image generation.
    /// </summary>
    public class TexyWindow : EditorWindow
    {
        [MenuItem("Tools/Texy/Texture Generator %#t")]
        public static void ShowWindow()
        {
            var win = GetWindow<TexyWindow>("Texy");
            win.minSize = new Vector2(380, 560);
            win.Show();
        }

        // ---- target ----
        private GameObject _targetObject;
        private Material _targetMaterial;
        private int _materialSlot;
        private MeshContext _meshContext;

        // ---- request params ----
        private string _prompt = string.Empty;
        private int _resolution = 2048;
        private int _seed = 12345;
        private float _strength = 1f;
        private bool _tileable = true;
        private bool _useExistingAlbedo;

        /// <summary>Optional explicit source art (PNG/PSD) used as the base for retexture / deriving maps.</summary>
        private Texture2D _sourceOverride;

        private readonly Dictionary<MapType, bool> _selectedMaps = new Dictionary<MapType, bool>();

        // ---- settings / engine ----
        private TexySettings _settings;
        private bool _showAiSettings;
        private bool _showAdvanced;

        // Installed ControlNet models, fetched on demand from the SD-WebUI API.
        private string[] _cnModels;
        private string _cnStatus;

        private static readonly string[] CnModules =
        {
            "tile_resample", "lineart_realistic", "lineart_standard", "canny", "none"
        };

        // ---- runtime ----
        private bool _isGenerating;
        private float _progress;
        private string _status = string.Empty;
        private readonly Queue<MapType> _queue = new Queue<MapType>();
        private readonly List<GeneratedEntry> _results = new List<GeneratedEntry>();
        private Vector2 _scroll;

        private class GeneratedEntry
        {
            public MapType MapType;
            public Texture2D Asset;
            public string Provider;
        }

        private static readonly MapType[] AllMaps =
        {
            MapType.Albedo, MapType.Normal, MapType.Emission, MapType.Decal,
            MapType.Metallic, MapType.Roughness, MapType.AmbientOcclusion, MapType.Height, MapType.Smoothness
        };

        private static readonly int[] Resolutions = { 512, 1024, 2048, 4096 };

        private void OnEnable()
        {
            _settings = TexySettings.Instance;
            _resolution = _settings.DefaultResolution;
            if (_selectedMaps.Count == 0)
                foreach (var m in AllMaps) _selectedMaps[m] = m == MapType.Albedo;
        }

        private void OnDisable()
        {
            // Persist engine/endpoint/output preferences between sessions.
            if (_settings != null) _settings.Save();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Texy — VRChat Avatar Texture Generator", TexyStyles.Header);
            EditorGUILayout.LabelField("Unity 2022 (VRChat) • Albedo · Normal · Emission · Decal · PBR", TexyStyles.Hint);

            DrawTargetSection();
            DrawPromptSection();
            DrawMapSelection();
            DrawAdvanced();
            DrawEngineSection();
            DrawActions();
            DrawResults();

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- target

        private void DrawTargetSection()
        {
            EditorGUILayout.BeginVertical(TexyStyles.Card);
            GUILayout.Label("1 · Target", TexyStyles.SectionTitle);

            EditorGUI.BeginChangeCheck();
            _targetObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Avatar Mesh", "A GameObject with a Skinned Mesh or Mesh Renderer. Drives geometry-aware generation."),
                _targetObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                AutoBindFromTarget();

            using (new EditorGUI.DisabledScope(_targetObject == null))
            {
                if (GUILayout.Button("Analyze Geometry"))
                    Analyze();
            }

            if (_meshContext != null)
                DrawMeshInfo();

            _targetMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Material", "Generated maps are assigned here. Auto-filled from the mesh when possible."),
                _targetMaterial, typeof(Material), false);

            EditorGUILayout.EndVertical();
        }

        private void DrawMeshInfo()
        {
            var c = _meshContext;
            EditorGUILayout.HelpBox(
                $"{c.Name}: {c.VertexCount:N0} verts · {c.TriangleCount:N0} tris\n" +
                $"UV coverage {(c.UVCoverage * 100f):F0}% · {c.IslandCount} islands · guess: {c.Guess}\n" +
                $"Geometry seed {c.GeometrySeed}" +
                (c.ExistingTextureSize > 0 ? $" · existing texture {c.ExistingTextureSize}px" : ""),
                MessageType.None);

            if (c.ExistingTextureSize > 0 && GUILayout.Button($"Match existing resolution ({c.ExistingTextureSize}px)"))
                _resolution = Mathf.Clamp(c.ExistingTextureSize, 256, 4096);
        }

        private void AutoBindFromTarget()
        {
            _meshContext = null;
            if (_targetObject == null) return;

            var renderer = _targetObject.GetComponent<Renderer>() ?? _targetObject.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var mats = renderer.sharedMaterials;
                if (mats != null && mats.Length > 0)
                {
                    _materialSlot = Mathf.Clamp(_materialSlot, 0, mats.Length - 1);
                    if (_targetMaterial == null) _targetMaterial = mats[_materialSlot];
                }
            }
        }

        private void Analyze()
        {
            if (MeshAnalyzer.TryAnalyze(_targetObject, _materialSlot, out var ctx))
            {
                _meshContext = ctx;
                if (ctx.ExistingTextureSize > 0)
                    _resolution = Mathf.Clamp(ctx.ExistingTextureSize, 256, 4096);
                TexyLog.Info($"Analyzed '{ctx.Name}': guess {ctx.Guess}, seed {ctx.GeometrySeed}.");
            }
            else
            {
                EditorUtility.DisplayDialog("Texy", "No Skinned Mesh / Mesh Renderer with a readable mesh was found on the selected object.", "OK");
            }
        }

        // ---------------------------------------------------------------- prompt

        private void DrawPromptSection()
        {
            EditorGUILayout.BeginVertical(TexyStyles.Card);
            GUILayout.Label("2 · Prompt", TexyStyles.SectionTitle);
            EditorGUILayout.LabelField("Describe the texture. Leave empty to auto-generate from geometry.", TexyStyles.Hint);

            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.MinHeight(48));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Examples:", TexyStyles.Hint, GUILayout.Width(56));
            if (GUILayout.Button("cyber neon hex", EditorStyles.miniButton)) _prompt = "glowing cyan neon hexagon tech pattern on black, cyber";
            if (GUILayout.Button("dragon scales", EditorStyles.miniButton)) _prompt = "iridescent emerald green dragon scales, detailed";
            if (GUILayout.Button("denim", EditorStyles.miniButton)) _prompt = "blue denim fabric, woven";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- maps

        private void DrawMapSelection()
        {
            EditorGUILayout.BeginVertical(TexyStyles.Card);
            GUILayout.Label("3 · Maps to generate", TexyStyles.SectionTitle);

            int col = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (var map in AllMaps)
            {
                if (col == 3) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); col = 0; }
                _selectedMaps[map] = GUILayout.Toggle(_selectedMaps[map], MapTypeInfo.FileSuffix(map), EditorStyles.miniButton, GUILayout.Width(110));
                col++;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Full PBR Set", EditorStyles.miniButton))
                SetMaps(MapType.Albedo, MapType.Normal, MapType.Metallic, MapType.Roughness, MapType.AmbientOcclusion);
            if (GUILayout.Button("Albedo Only", EditorStyles.miniButton))
                SetMaps(MapType.Albedo);
            if (GUILayout.Button("None", EditorStyles.miniButton))
                SetMaps();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SetMaps(params MapType[] on)
        {
            var set = new HashSet<MapType>(on);
            foreach (var m in AllMaps) _selectedMaps[m] = set.Contains(m);
        }

        // ---------------------------------------------------------------- advanced

        private void DrawAdvanced()
        {
            EditorGUILayout.BeginVertical(TexyStyles.Card);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "4 · Options", true);
            if (_showAdvanced)
            {
                int resIndex = Mathf.Max(0, Array.IndexOf(Resolutions, _resolution));
                resIndex = EditorGUILayout.Popup("Resolution", resIndex, ResolutionLabels());
                _resolution = Resolutions[Mathf.Clamp(resIndex, 0, Resolutions.Length - 1)];

                EditorGUILayout.BeginHorizontal();
                _seed = EditorGUILayout.IntField(new GUIContent("Seed", "Same seed + prompt = identical result."), _seed);
                if (GUILayout.Button("🎲", GUILayout.Width(28))) _seed = UnityEngine.Random.Range(0, int.MaxValue);
                EditorGUILayout.EndHorizontal();

                _strength = EditorGUILayout.Slider(new GUIContent("Effect Strength", "Normal depth / emission brightness / decal opacity."), _strength, 0f, 1f);
                _tileable = EditorGUILayout.Toggle(new GUIContent("Tileable", "Seamless wrapping for body/clothing atlases."), _tileable);
                _useExistingAlbedo = EditorGUILayout.Toggle(new GUIContent("Derive from existing albedo", "Build normal/AO/height from the material's current main texture."), _useExistingAlbedo);

                _sourceOverride = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent("Source texture (optional)", "Drag the avatar's original PNG/PSD here to use it as the base for Retexture / deriving maps — higher quality than the in-game texture. Must match this material's UV layout. Empty = use the material's current main texture."),
                    _sourceOverride, typeof(Texture2D), false);
                if (_sourceOverride != null)
                    EditorGUILayout.LabelField($"Using source: {_sourceOverride.name} ({_sourceOverride.width}×{_sourceOverride.height})", TexyStyles.Hint);

                _settings.ApplyToMaterialOnGenerate = EditorGUILayout.Toggle("Apply to material", _settings.ApplyToMaterialOnGenerate);
                _settings.OutputFolder = EditorGUILayout.TextField("Output folder", _settings.OutputFolder);
            }
            EditorGUILayout.EndVertical();
        }

        private string[] ResolutionLabels()
        {
            var labels = new string[Resolutions.Length];
            for (int i = 0; i < Resolutions.Length; i++) labels[i] = $"{Resolutions[i]} × {Resolutions[i]}";
            return labels;
        }

        // ---------------------------------------------------------------- engine

        private void DrawEngineSection()
        {
            EditorGUILayout.BeginVertical(TexyStyles.Card);
            GUILayout.Label("5 · Engine", TexyStyles.SectionTitle);

            _settings.ActiveEngine = (TexySettings.Engine)EditorGUILayout.EnumPopup(
                new GUIContent("Engine", "Procedural runs fully offline. AI calls a configured image endpoint."),
                _settings.ActiveEngine);

            if (_settings.ActiveEngine == TexySettings.Engine.Procedural)
            {
                EditorGUILayout.LabelField("Offline procedural engine — no setup required.", TexyStyles.Hint);
            }
            else
            {
                _showAiSettings = EditorGUILayout.Foldout(_showAiSettings, "AI endpoint settings", true);
                if (_showAiSettings)
                {
                    _settings.AiResponse = (TexySettings.AiResponseFormat)EditorGUILayout.EnumPopup("API format", _settings.AiResponse);
                    _settings.AiEndpoint = EditorGUILayout.TextField("Endpoint URL", _settings.AiEndpoint);
                    if (_settings.AiResponse == TexySettings.AiResponseFormat.OpenAI)
                        _settings.AiModel = EditorGUILayout.TextField("Model", _settings.AiModel);
                    _settings.AiApiKey = EditorGUILayout.PasswordField(new GUIContent("API key", "Stored locally in EditorPrefs, never committed."), _settings.AiApiKey);
                    _settings.AiTimeoutSeconds = EditorGUILayout.IntSlider("Timeout (s)", _settings.AiTimeoutSeconds, 30, 600);
                    EditorGUILayout.LabelField("Data maps (normal/AO/...) are derived from an AI albedo.", TexyStyles.Hint);

                    if (_settings.AiResponse == TexySettings.AiResponseFormat.SDWebUI)
                    {
                        EditorGUILayout.Space(2);
                        _settings.AiRetexture = EditorGUILayout.Toggle(
                            new GUIContent("Retexture mode (img2img)", "Restyle the avatar's EXISTING texture instead of generating a new one. Keeps the UV layout & hides seams — recommended for finished avatars."),
                            _settings.AiRetexture);

                        if (_settings.AiRetexture)
                        {
                            _settings.AiDenoise = EditorGUILayout.Slider(
                                new GUIContent("Denoise (keep ↔ restyle)", "Low keeps the original layout/shading; high restyles more but drifts. 0.5–0.6 is the sweet spot."),
                                _settings.AiDenoise, 0.2f, 0.9f);

                            Texture2D src = ResolveSourceTexture();
                            if (src == null)
                                EditorGUILayout.HelpBox("No base texture. Assign a Material with a main texture, or drag the avatar's PNG/PSD into 'Source texture' under Options.", MessageType.Warning);
                            else if (SourceIsGeneratedOutput(src))
                                EditorGUILayout.HelpBox(
                                    $"⚠ The base is a Texy-generated texture ({src.name}). Restyling our own output spirals toward flat color.\n" +
                                    "Drag the avatar's ORIGINAL albedo into 'Source texture' under Options instead.",
                                    MessageType.Error);
                            else
                                EditorGUILayout.LabelField($"Base: {src.name} ({src.width}×{src.height}).", TexyStyles.Hint);

                            DrawControlNetUI();
                        }

                        EditorGUILayout.Space(2);
                        _settings.AiCfgScale = EditorGUILayout.Slider(
                            new GUIContent("Prompt strength (CFG)", "Higher = follows the prompt harder, more detail/contrast. 8-11 helps avoid flat color."),
                            _settings.AiCfgScale, 1f, 20f);
                        _settings.AiSteps = EditorGUILayout.IntSlider(
                            new GUIContent("Steps", "Sampling steps. 25-35 is a good range."),
                            _settings.AiSteps, 10, 50);
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawControlNetUI()
        {
            EditorGUILayout.Space(2);
            _settings.AiControlNet = EditorGUILayout.Toggle(
                new GUIContent("ControlNet (lock structure)", "Keeps the source's structure while allowing high denoise, so the restyle gains real detail. Requires the sd-webui-controlnet extension + a tile/lineart model."),
                _settings.AiControlNet);

            if (!_settings.AiControlNet) return;

            using (new EditorGUI.IndentLevelScope())
            {
                // Module (preprocessor).
                int modIdx = Mathf.Max(0, Array.IndexOf(CnModules, _settings.AiControlNetModule));
                modIdx = EditorGUILayout.Popup("Preprocessor", modIdx, CnModules);
                _settings.AiControlNetModule = CnModules[Mathf.Clamp(modIdx, 0, CnModules.Length - 1)];

                // Model — dropdown once fetched, text field otherwise.
                if (_cnModels != null && _cnModels.Length > 0)
                {
                    int mi = Mathf.Max(0, Array.IndexOf(_cnModels, _settings.AiControlNetModel));
                    mi = EditorGUILayout.Popup("Model", mi, _cnModels);
                    _settings.AiControlNetModel = _cnModels[Mathf.Clamp(mi, 0, _cnModels.Length - 1)];
                }
                else
                {
                    _settings.AiControlNetModel = EditorGUILayout.TextField(
                        new GUIContent("Model", "Exact installed model name, e.g. control_v11f1e_sd15_tile [a371b31b]. Use Fetch to list them."),
                        _settings.AiControlNetModel);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Fetch installed models", EditorStyles.miniButton))
                    FetchControlNetModels();
                EditorGUILayout.EndHorizontal();

                _settings.AiControlNetWeight = EditorGUILayout.Slider("ControlNet weight", _settings.AiControlNetWeight, 0f, 2f);

                if (!string.IsNullOrEmpty(_cnStatus))
                    EditorGUILayout.LabelField(_cnStatus, TexyStyles.Hint);

                EditorGUILayout.HelpBox("Tile + high Denoise (0.75–0.85) is the avatar sweet spot: lots of new detail, structure preserved.", MessageType.Info);
            }
        }

        private void FetchControlNetModels()
        {
            _cnStatus = "Fetching ControlNet models…";
            var provider = new AITextureProvider(_settings);
            provider.FetchControlNetModels(
                models =>
                {
                    _cnModels = models;
                    _cnStatus = $"Found {models.Length} ControlNet model(s).";
                    if (string.IsNullOrEmpty(_settings.AiControlNetModel) && models.Length > 0)
                    {
                        // Default to a tile model if present (best for avatars), else the first.
                        string tile = Array.Find(models, m => m.ToLowerInvariant().Contains("tile"));
                        _settings.AiControlNetModel = tile ?? models[0];
                    }
                    Repaint();
                },
                err =>
                {
                    _cnModels = null;
                    _cnStatus = err;
                    Repaint();
                });
        }

        // ---------------------------------------------------------------- actions

        private void DrawActions()
        {
            EditorGUILayout.Space(4);

            var provider = ProviderFactory.Create(_settings);
            bool ready = provider.IsReady(out string reason);
            if (!ready)
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            if (_isGenerating)
            {
                Rect r = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(r, _progress, string.IsNullOrEmpty(_status) ? "Generating…" : _status);
                return;
            }

            using (new EditorGUI.DisabledScope(!ready))
            {
                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button(BuildGenerateLabel(), GUILayout.Height(38)))
                    StartGeneration();
                GUI.backgroundColor = Color.white;
            }
        }

        private string BuildGenerateLabel()
        {
            int count = CountSelected();
            string what = string.IsNullOrWhiteSpace(_prompt) ? "Auto-Generate" : "Generate";
            return count <= 1 ? $"{what} Texture" : $"{what} {count} Maps";
        }

        private int CountSelected()
        {
            int n = 0;
            foreach (var kv in _selectedMaps) if (kv.Value) n++;
            return n;
        }

        // ---------------------------------------------------------------- results

        private void DrawResults()
        {
            if (_results.Count == 0) return;

            EditorGUILayout.BeginVertical(TexyStyles.Card);
            GUILayout.Label("Results", TexyStyles.SectionTitle);

            foreach (var entry in _results)
            {
                EditorGUILayout.BeginHorizontal();
                if (entry.Asset != null)
                {
                    Rect r = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                    EditorGUI.DrawPreviewTexture(r, entry.Asset);
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(MapTypeInfo.DisplayName(entry.MapType), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.Provider, TexyStyles.Hint);
                if (entry.Asset != null && GUILayout.Button("Ping asset", EditorStyles.miniButton, GUILayout.Width(90)))
                    EditorGUIUtility.PingObject(entry.Asset);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Clear results"))
                _results.Clear();

            EditorGUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- generation flow

        private void StartGeneration()
        {
            if (CountSelected() == 0)
            {
                EditorUtility.DisplayDialog("Texy", "Select at least one map to generate.", "OK");
                return;
            }

            // Persist user preferences for next session.
            _settings.DefaultResolution = _resolution;
            _settings.Save();

            // Refresh geometry context if the user assigned a mesh but never analyzed it.
            if (_targetObject != null && _meshContext == null)
                MeshAnalyzer.TryAnalyze(_targetObject, _materialSlot, out _meshContext);

            _queue.Clear();
            // Albedo first so AI-derived maps and previews have a base to work from.
            foreach (var m in AllMaps)
                if (_selectedMaps[m] && m == MapType.Albedo) _queue.Enqueue(m);
            foreach (var m in AllMaps)
                if (_selectedMaps[m] && m != MapType.Albedo) _queue.Enqueue(m);

            _isGenerating = true;
            _progress = 0f;
            _status = "Starting…";
            ProcessNext();
        }

        private void ProcessNext()
        {
            if (_queue.Count == 0)
            {
                FinishGeneration();
                return;
            }

            MapType map = _queue.Dequeue();
            var provider = ProviderFactory.Create(_settings);
            var request = BuildRequest(map);

            try
            {
                provider.Generate(request, OnProgress, result => OnMapComplete(provider, result));
            }
            catch (Exception e)
            {
                TexyLog.Error($"{MapTypeInfo.DisplayName(map)} failed: {e.Message}");
                ProcessNext();
            }
        }

        private TextureRequest BuildRequest(MapType map)
        {
            var req = new TextureRequest
            {
                Prompt = _prompt,
                MapType = map,
                Width = _resolution,
                Height = _resolution,
                Seed = _seed,
                Strength = _strength,
                Tileable = _tileable,
                Mesh = _meshContext
            };

            // Source albedo feeds both "derive maps from existing" and AI retexture (img2img).
            // Prefer an explicitly supplied PNG/PSD (higher quality) over the material's runtime texture.
            bool wantSource = _useExistingAlbedo
                              || (_settings.ActiveEngine == TexySettings.Engine.AI && _settings.AiRetexture);
            if (wantSource)
                req.SourceAlbedo = ResolveSourceTexture();

            return req;
        }

        /// <summary>The base texture for retexture / map derivation: the explicit override, else the material's main texture.</summary>
        private Texture2D ResolveSourceTexture()
        {
            if (_sourceOverride != null) return _sourceOverride;
            if (_targetMaterial != null && _targetMaterial.HasProperty("_MainTex"))
                return _targetMaterial.GetTexture("_MainTex") as Texture2D;
            return null;
        }

        /// <summary>
        /// True when the resolved retexture source is itself a Texy output. Feeding our own result back
        /// in spirals toward flat color, so we warn the user to point at the avatar's ORIGINAL texture.
        /// </summary>
        private bool SourceIsGeneratedOutput(Texture2D src)
        {
            if (src == null) return false;
            string path = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(path)) return false;
            string outFolder = (_settings.OutputFolder ?? string.Empty).Replace("\\", "/").TrimEnd('/');
            return !string.IsNullOrEmpty(outFolder) && path.Replace("\\", "/").StartsWith(outFolder + "/");
        }

        private void OnProgress(float p, string status)
        {
            _progress = p;
            _status = status;
            // Visible during the synchronous procedural pass; harmless for async AI.
            EditorUtility.DisplayProgressBar("Texy", status, p);
            Repaint();
        }

        private void OnMapComplete(ITextureProvider provider, TextureResult result)
        {
            if (result.Success)
            {
                string baseName = BuildBaseName();
                Texture2D asset = TextureImportPipeline.SaveAndImport(result.Texture, result.MapType, baseName, _settings.OutputFolder);

                if (result.Texture != null)
                    DestroyImmediate(result.Texture);

                if (_settings.ApplyToMaterialOnGenerate && _targetMaterial != null)
                    MaterialApplier.Apply(_targetMaterial, result.MapType, asset);

                _results.Insert(0, new GeneratedEntry { MapType = result.MapType, Asset = asset, Provider = result.ProviderName });
            }
            else
            {
                TexyLog.Error($"{MapTypeInfo.DisplayName(result.MapType)}: {result.Error}");
            }

            Repaint();
            ProcessNext();
        }

        private string BuildBaseName()
        {
            if (!string.IsNullOrWhiteSpace(_prompt))
            {
                string words = _prompt.Trim();
                int cut = words.IndexOf('\n');
                if (cut > 0) words = words.Substring(0, cut);
                return words.Length > 24 ? words.Substring(0, 24) : words;
            }
            if (_meshContext != null) return _meshContext.Name;
            if (_targetMaterial != null) return _targetMaterial.name;
            return "Texy";
        }

        private void FinishGeneration()
        {
            _isGenerating = false;
            _progress = 0f;
            _status = string.Empty;
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Repaint();
        }
    }
}

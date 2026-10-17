using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ExpoConvergence.EditorTools
{
    /// <summary>
    /// One-click project setup: layers, URP tuning for integrated GPUs, TextMeshPro resources,
    /// procedural textures, materials, data assets and the game scene. Also builds the Windows player.
    /// </summary>
    public static class ExpoProjectSetup
    {
        const string TexDir = "Assets/ExpoConvergence/Textures";
        const string MatDir = "Assets/Resources/ExpoMaterials";
        const string DataDir = "Assets/Resources/ExpoData";
        const string ScenePath = "Assets/Scenes/ExpoConvergence.unity";
        const string BuildPath = "Builds/Windows/ExpoConvergence.exe";

        [MenuItem("Expo/1. Setup Project", priority = 1)]
        public static void SetupProject()
        {
            EnsureLayers();
            ConfigureRendering();
            ImportTmpEssentials();
            GenerateTextures();
            CreateMaterials();
            CreateData();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ExpoSetup] Project setup complete.");
        }

        [MenuItem("Expo/Regenerate Textures and Materials", priority = 20)]
        public static void RegenerateArt()
        {
            GenerateTextures();
            CreateMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log("[ExpoSetup] Textures and materials regenerated.");
        }

        // ------------------------------------------------------------------ layers

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            void Set(int index, string name)
            {
                var p = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(p.stringValue) || p.stringValue == name) p.stringValue = name;
                else Debug.LogWarning($"[ExpoSetup] Layer {index} already named '{p.stringValue}'");
            }
            Set(ExpoLayers.Player, "Player");
            Set(ExpoLayers.Sensor, "Sensor");
            Set(ExpoLayers.Interactable, "Interactable");
            tagManager.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ rendering

        static void ConfigureRendering()
        {
            for (int i = 0; i < QualitySettings.count; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                if (rp != null) TuneAsset(rp);
            }
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def) TuneAsset(def);
            QualitySettings.vSyncCount = 1;
        }

        static void TuneAsset(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            void SetInt(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
            void SetFloat(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; }
            void SetBool(string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }
            SetBool("m_RequireOpaqueTexture", false);
            SetBool("m_RequireDepthTexture", false);
            SetBool("m_SupportsHDR", true);
            SetInt("m_MSAA", 1);
            SetFloat("m_RenderScale", 1f);
            SetBool("m_MainLightShadowsSupported", true);
            SetInt("m_MainLightShadowmapResolution", 2048);
            SetBool("m_AdditionalLightShadowsSupported", false);
            SetFloat("m_ShadowDistance", 45f);
            SetInt("m_ShadowCascadeCount", 2);
            SetFloat("m_Cascade2Split", 0.3f);
            SetBool("m_SoftShadowsSupported", true);
            SetBool("m_ReflectionProbeBlending", true);
            SetBool("m_ReflectionProbeBoxProjection", true);
            so.ApplyModifiedProperties();

            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                if (!(list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData rd)) continue;
                foreach (var f in rd.rendererFeatures)
                {
                    // SSAO is too expensive for integrated GPUs; the night grade does not need it.
                    if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(false);
                }
                EditorUtility.SetDirty(rd);
            }
            EditorUtility.SetDirty(asset);
        }

        // ------------------------------------------------------------------ TextMeshPro

        static void ImportTmpEssentials()
        {
            if (Resources.Load("TMP Settings") != null) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            if (info == null) { Debug.LogWarning("[ExpoSetup] uGUI package not found; cannot import TMP essentials."); return; }
            string pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (File.Exists(pkg)) AssetDatabase.ImportPackage(pkg, false);
            else Debug.LogWarning("[ExpoSetup] TMP Essential Resources package not found at " + pkg);
        }

        // ------------------------------------------------------------------ textures

        static void GenerateTextures()
        {
            Directory.CreateDirectory(TexDir);
            SavePng("T_FloorTiles", 512, FloorTiles);
            SavePng("T_Blocks", 512, StoneBlocks);
            SavePng("T_Mashrabiya", 512, Mashrabiya);
            SavePng("T_Windows", 256, Windows);
            SavePng("T_ClockFace", 1024, ClockFace, 256);
            SavePng("T_WaterNormal", 256, WaterNormal, 0, true);
            SavePng("T_SoftDot", 64, SoftDot);
        }

        static void SavePng(string name, int width, System.Func<int, int, int, int, Color> pixel, int height = 0, bool normalMap = false)
        {
            if (height <= 0) height = width;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    px[y * width + x] = pixel(x, y, width, height);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = name == "T_SoftDot" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = name == "T_FloorTiles" || name == "T_ClockFace" ? 4 : 1;
            if (normalMap) ti.textureType = TextureImporterType.NormalMap;
            if (name == "T_SoftDot") { ti.alphaIsTransparency = true; }
            ti.SaveAndReimport();
        }

        static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static Color FloorTiles(int x, int y, int w, int h)
        {
            // 4x4 polished tiles with fine seams and a subtle inlay line every other tile.
            int tile = w / 4;
            int tx = x / tile, ty = y / tile;
            int lx = x % tile, ly = y % tile;
            float baseV = 0.62f + Hash(tx, ty, 3) * 0.16f;
            float grain = (Hash(x, y, 9) - 0.5f) * 0.05f;
            float seam = (lx < 2 || ly < 2) ? 0.25f : 1f;
            float inlay = ((tx + ty) % 2 == 0 && (Mathf.Abs(lx - tile / 2) < 1)) ? 0.85f : 1f;
            float v = Mathf.Clamp01((baseV + grain) * seam * inlay);
            return new Color(v, v, v, 1f);
        }

        static Color StoneBlocks(int x, int y, int w, int h)
        {
            int rowH = w / 8;
            int row = y / rowH;
            int blockW = w / 4;
            int offset = (row % 2) * blockW / 2;
            int bx = (x + offset) / blockW;
            int lx = (x + offset) % blockW, ly = y % rowH;
            float baseV = 0.7f + Hash(bx, row, 5) * 0.22f;
            float grain = (Hash(x, y, 11) - 0.5f) * 0.08f;
            float mortar = (lx < 3 || ly < 3) ? 0.45f : 1f;
            float v = Mathf.Clamp01((baseV + grain) * mortar);
            return new Color(v, v * 0.98f, v * 0.95f, 1f);
        }

        static Color Mashrabiya(int x, int y, int w, int h)
        {
            // Islamic eight-point star lattice: two overlapping squares per cell, a ring and the cell frame.
            int cells = 4;
            float cs = w / (float)cells;
            float u = ((x % cs) / cs) * 2f - 1f, v = ((y % cs) / cs) * 2f - 1f;
            float sq1 = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            float sq2 = Mathf.Max(Mathf.Abs(u + v), Mathf.Abs(u - v)) / 1.41421f;
            float ring = Mathf.Sqrt(u * u + v * v);
            float line(float d, float r, float width) => Mathf.Clamp01(1f - Mathf.Abs(d - r) / width);
            float pattern = Mathf.Max(line(sq1, 0.6f, 0.05f), line(sq2, 0.6f, 0.05f));
            pattern = Mathf.Max(pattern, line(ring, 0.3f, 0.04f));
            pattern = Mathf.Max(pattern, line(sq1, 1f, 0.06f));
            float star = (sq1 < 0.6f && sq2 < 0.6f && ring > 0.34f) ? 0.12f : 0f;
            float a = Mathf.Clamp01(pattern + star);
            return new Color(a, a, a, 1f);
        }

        static Color Windows(int x, int y, int w, int h)
        {
            const int cols = 8, rows = 16;
            int cw = w / cols, rh = h / rows;
            int cx = x / cw, cy = y / rh, lx = x % cw, ly = y % rh;
            bool frame = lx < 3 || lx > cw - 3 || ly < 4 || ly > rh - 2;
            if (frame) return new Color(0.02f, 0.02f, 0.03f, 1f);
            float r = Hash(cx, cy, 21);
            if (r < 0.52f) return new Color(0.03f, 0.035f, 0.05f, 1f);
            Color warm = new Color(1f, 0.78f, 0.5f), cool = new Color(0.6f, 0.78f, 1f);
            Color c = Color.Lerp(warm, cool, Hash(cx, cy, 33) > 0.65f ? 1f : 0f) * Mathf.Lerp(0.35f, 1f, Hash(cx, cy, 47));
            return new Color(c.r, c.g, c.b, 1f);
        }

        static Color ClockFace(int x, int y, int w, int h)
        {
            // Polar strip: x = angle around the face, y = radius (inner to outer).
            float u = x / (float)w, v = y / (float)h;
            float minute = Mathf.Abs(Mathf.Repeat(u * 60f + 0.5f, 1f) - 0.5f);
            float hour = Mathf.Abs(Mathf.Repeat(u * 12f + 0.5f, 1f) - 0.5f);
            float a = 0f;
            if (v > 0.93f && v < 0.97f) a = 1f;
            if (v > 0.82f && v < 0.9f && minute < 0.06f) a = 0.9f;
            if (v > 0.66f && v < 0.9f && hour < 0.012f * 12f * 0.5f) a = 1f;
            if (v > 0.5f && v < 0.515f) a = Mathf.Max(a, 0.6f);
            if (v > 0.2f && v < 0.21f) a = Mathf.Max(a, 0.45f);
            if (v < 0.48f && hour < 0.004f * 12f) a = Mathf.Max(a, 0.25f);
            return new Color(a, a, a, 1f);
        }

        static Color WaterNormal(int x, int y, int w, int h)
        {
            float H(float px, float py)
            {
                float t = 0f;
                t += Mathf.Sin((px * 3f + py * 1f) / w * Mathf.PI * 2f);
                t += 0.7f * Mathf.Sin((px * -2f + py * 4f) / w * Mathf.PI * 2f + 1.3f);
                t += 0.45f * Mathf.Sin((px * 7f + py * -5f) / w * Mathf.PI * 2f + 2.1f);
                t += 0.3f * Mathf.Sin((px * -9f + py * -11f) / w * Mathf.PI * 2f + 0.4f);
                return t;
            }
            float dx = H(x + 1, y) - H(x - 1, y);
            float dy = H(x, y + 1) - H(x, y - 1);
            Vector3 n = new Vector3(-dx * 2.2f, -dy * 2.2f, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        static Color SoftDot(int x, int y, int w, int h)
        {
            float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
            float d = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            return new Color(1f, 1f, 1f, a);
        }

        // ------------------------------------------------------------------ materials

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static Material SaveMat(string name, Shader shader, System.Action<Material> setup)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            m.enableInstancing = true;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Lit(Material m, Color c, float smooth, float metal, Texture2D baseMap = null, Vector2? tiling = null)
        {
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
        }

        static void Emission(Material m, Color hdr, Texture2D map = null)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", hdr);
            if (map != null) m.SetTexture("_EmissionMap", map);
        }

        static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var holo = Shader.Find("Expo/Hologram");
            var field = Shader.Find("Expo/EnergyField");
            var cone = Shader.Find("Expo/VisionCone");
            var beam = Shader.Find("Expo/Beam");
            var sky = Shader.Find("Expo/NightSky");
            var particle = Shader.Find("Expo/ParticleAdd");

            SaveMat("M_LitBase", lit, m => Lit(m, Color.white, 0.5f, 0f));
            SaveMat("M_EmissiveBase", lit, m => { Lit(m, new Color(0.02f, 0.02f, 0.03f), 0.5f, 0f); Emission(m, Color.white); });
            SaveMat("M_HoloBase", holo, m => { });
            SaveMat("M_FieldBase", field, m => { });
            SaveMat("M_ConeBase", cone, m => { });
            SaveMat("M_BeamBase", beam, m => { });
            SaveMat("M_Sky", sky, m => { });
            SaveMat("M_ParticleAdd", particle, m => { m.SetTexture("_MainTex", Tex("T_SoftDot")); m.SetColor("_Color", new Color(3f, 3f, 3f, 1f)); });
            SaveMat("M_Glass", holo, m =>
            {
                m.SetColor("_Color", new Color(0.45f, 0.7f, 1f, 1f));
                m.SetFloat("_Alpha", 0.16f);
                m.SetFloat("_CoreAlpha", 0.06f);
                m.SetFloat("_ScanStrength", 0f);
                m.SetFloat("_Flicker", 0f);
                m.SetFloat("_FresnelPower", 3f);
            });

            var tiles = Tex("T_FloorTiles");
            var blocks = Tex("T_Blocks");
            SaveMat("M_Floor", lit, m => Lit(m, new Color(0.12f, 0.14f, 0.19f), 0.82f, 0.15f, tiles, new Vector2(0.5f, 0.5f)));
            SaveMat("M_FloorDark", lit, m => Lit(m, new Color(0.055f, 0.065f, 0.09f), 0.86f, 0.2f, tiles, new Vector2(0.25f, 0.25f)));
            SaveMat("M_Path", lit, m => Lit(m, new Color(0.14f, 0.16f, 0.21f), 0.7f, 0.1f, tiles, new Vector2(0.5f, 0.5f)));
            SaveMat("M_Wall", lit, m => Lit(m, new Color(0.1f, 0.12f, 0.17f), 0.38f, 0.25f, blocks, new Vector2(0.25f, 0.25f)));
            SaveMat("M_Metal", lit, m => Lit(m, new Color(0.24f, 0.27f, 0.33f), 0.78f, 0.85f));
            SaveMat("M_DarkMetal", lit, m => Lit(m, new Color(0.06f, 0.065f, 0.085f), 0.66f, 0.8f));
            SaveMat("M_White", lit, m => Lit(m, new Color(0.7f, 0.74f, 0.8f), 0.62f, 0.2f));
            SaveMat("M_Sandstone", lit, m => Lit(m, new Color(0.5f, 0.37f, 0.23f), 0.22f, 0f, blocks, new Vector2(0.33f, 0.33f)));
            SaveMat("M_SandstoneDark", lit, m => Lit(m, new Color(0.27f, 0.19f, 0.11f), 0.28f, 0f, blocks, new Vector2(0.5f, 0.5f)));
            SaveMat("M_Limestone", lit, m => Lit(m, new Color(0.58f, 0.58f, 0.62f), 0.32f, 0f, blocks, new Vector2(0.33f, 0.33f)));
            SaveMat("M_Slate", lit, m => Lit(m, new Color(0.07f, 0.08f, 0.11f), 0.55f, 0.4f));
            SaveMat("M_UKStone", lit, m => Lit(m, new Color(0.085f, 0.1f, 0.09f), 0.45f, 0.2f, blocks, new Vector2(0.33f, 0.33f)));
            SaveMat("M_Hedge", lit, m => Lit(m, new Color(0.035f, 0.11f, 0.055f), 0.12f, 0f));
            SaveMat("M_PalmTrunk", lit, m => Lit(m, new Color(0.22f, 0.15f, 0.09f), 0.2f, 0f, blocks, new Vector2(1f, 2f)));
            SaveMat("M_PalmLeaf", lit, m => Lit(m, new Color(0.06f, 0.18f, 0.09f), 0.35f, 0f));
            SaveMat("M_Water", lit, m =>
            {
                Lit(m, new Color(0.012f, 0.022f, 0.045f), 0.95f, 0f);
                m.SetTexture("_BumpMap", Tex("T_WaterNormal"));
                m.SetFloat("_BumpScale", 0.45f);
                m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BaseMap", new Vector2(0.08f, 0.08f));
            });
            SaveMat("M_Mashrabiya", lit, m =>
            {
                Lit(m, new Color(0.07f, 0.045f, 0.025f), 0.4f, 0.3f);
                Emission(m, ExpoPalette.Hdr(ExpoPalette.Gold, 3.4f), Tex("T_Mashrabiya"));
                m.SetTextureScale("_BaseMap", new Vector2(2.6f, 2.1f));
            });
            SaveMat("M_Windows", lit, m =>
            {
                Lit(m, new Color(0.22f, 0.22f, 0.25f), 0.85f, 0.3f, Tex("T_Windows"), new Vector2(1f / 12f, 1f / 24f));
                Emission(m, new Color(1.25f, 1.15f, 1.0f), Tex("T_Windows"));
            });
            SaveMat("M_ClockFace", lit, m =>
            {
                Lit(m, new Color(0.01f, 0.02f, 0.015f), 0.8f, 0.2f);
                Emission(m, ExpoPalette.Hdr(ExpoPalette.Emerald, 2.4f), Tex("T_ClockFace"));
            });
        }

        // ------------------------------------------------------------------ data

        static void CreateData()
        {
            Directory.CreateDirectory(DataDir);
            foreach (PassportType t in System.Enum.GetValues(typeof(PassportType)))
            {
                string path = $"{DataDir}/Passport_{t}.asset";
                if (AssetDatabase.LoadAssetAtPath<PassportDefinition>(path) == null)
                    AssetDatabase.CreateAsset(PassportDefinition.CreateDefault(t), path);
            }
            void Profile(string name, SensorProfile p)
            {
                string path = $"{DataDir}/{name}.asset";
                if (AssetDatabase.LoadAssetAtPath<SensorProfile>(path) == null) AssetDatabase.CreateAsset(p, path);
            }
            Profile("Sensor_ThermalDrone", SensorProfile.Create(SensorKind.Thermal, 11f, 52f, 1.15f, 5.5f, 2.8f, 6.0f));
            Profile("Sensor_OpticalDrone", SensorProfile.Create(SensorKind.Optical, 11f, 52f, 1.15f, 5.5f, 2.9f, 6.2f));
            Profile("Sensor_OpticalCamera", SensorProfile.Create(SensorKind.Optical, 13f, 46f, 1.25f, 0f));
            Profile("Sensor_ThermalScanner", SensorProfile.Create(SensorKind.Thermal, 10.5f, 20f, 1.7f, 0f));
            Profile("Sensor_OpticalScanner", SensorProfile.Create(SensorKind.Optical, 10.5f, 20f, 1.7f, 0f));
        }

        // ------------------------------------------------------------------ scene

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<GameBootstrap>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.transform.position = new Vector3(-58f, 26f, -62f);
            cam.transform.LookAt(new Vector3(0f, 8f, 0f));
            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Sky.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.085f, 0.11f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.075f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.025f, 0.035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0105f;
            RenderSettings.fogColor = new Color(0.035f, 0.05f, 0.095f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[ExpoSetup] Scene saved to " + ScenePath);
        }

        // ------------------------------------------------------------------ build

        [MenuItem("Expo/2. Build Windows Player", priority = 2)]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[ExpoSetup] Build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors, at {BuildPath}");
        }
    }

    /// <summary>Makes Blender FBX models in Resources/Models readable (static batching) and lightweight.</summary>
    public class ExpoModelPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}

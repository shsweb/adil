using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FartRaft.EditorTools
{
    // Menu: "Fart Raft > ..." in the Unity editor top bar.
    public static class FartRaftBuilder
    {
        const string Root = "Assets/FartRaft";
        const string MaterialsFolder = Root + "/Materials";
        const string ScenesFolder = Root + "/Scenes";
        const string ScenePath = ScenesFolder + "/Game.unity";
        const string PuffTexturePath = MaterialsFolder + "/Puff.png";
        const string ThemePath = Root + "/EnvironmentTheme.asset";
        const string PostProcessingPath = MaterialsFolder + "/PostProcessing.asset";

        static EnvironmentTheme theme;

        static readonly Color[] PlayerColors =
        {
            new Color(0.95f, 0.30f, 0.30f),
            new Color(0.25f, 0.55f, 1.00f),
            new Color(0.30f, 0.85f, 0.35f),
            new Color(1.00f, 0.85f, 0.20f),
        };

        static Font uiFont;
        static Sprite roundedSprite;
        static Sprite circleSprite;

        [MenuItem("Fart Raft/1. Configure Project For iOS and Android")]
        public static void ConfigureMobile()
        {
            PlayerSettings.companyName = "MyStudio";
            PlayerSettings.productName = "Fart Raft";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.mystudio.fartraft");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.mystudio.fartraft");

            // Google Play requires 64-bit (ARM64), which requires IL2CPP.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.iOS.targetOSVersionString = "13.0";

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Fart Raft",
                "Project configured for iOS and Android (landscape, IL2CPP, ARM64).\n\n" +
                "Change the bundle id 'com.mystudio.fartraft' in Project Settings > Player before publishing.", "OK");
        }

        [MenuItem("Fart Raft/2. Build Game Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MaterialsFolder);
            EnsureFolder(ScenesFolder);
            LoadUIResources();
            theme = LoadTheme();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = new Mats();

            SetupLighting();
            var startPoint = BuildCourse(mats, out int checkpointCount);
            var raft = BuildRaft(mats, startPoint, out FartSeat[] seats);
            var cameraFollow = BuildCamera(raft.transform);
            AddPostProcessing(cameraFollow.GetComponent<Camera>());

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            managerGo.AddComponent<KeyboardControls>().seats = seats;
            manager.raft = raft;
            manager.seats = seats;
            manager.startPoint = startPoint;
            manager.cameraFollow = cameraFollow;
            manager.checkpointCount = checkpointCount;

            BuildUI(manager, seats);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Fart Raft",
                "Scene built and saved to " + ScenePath + ".\n\n" +
                "Press Play. In the editor hold A / L / Q / P for players 1-4, " +
                "or use Unity Remote on a phone for touch.", "OK");
        }

        [MenuItem("Fart Raft/3. Upgrade Graphics To URP (Bloom, Color Grading)")]
        public static void UpgradeToUrp()
        {
            var assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
            var rendererType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
            if (assetType == null || rendererType == null)
            {
                UnityEditor.PackageManager.Client.Add("com.unity.render-pipelines.universal");
                EditorUtility.DisplayDialog("Fart Raft",
                    "Installing the Universal Render Pipeline package.\n\n" +
                    "Wait until Unity finishes importing (progress bar bottom-right), then click this menu item again.", "OK");
                return;
            }

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                EnsureFolder(MaterialsFolder);
                const string rendererPath = MaterialsFolder + "/URP_Renderer.asset";
                const string pipelinePath = MaterialsFolder + "/URP_Pipeline.asset";

                var rendererData = AssetDatabase.LoadAssetAtPath(rendererPath, rendererType);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                }

                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);
                if (pipeline == null)
                {
                    var create = assetType.GetMethods().FirstOrDefault(mi => mi.Name == "Create" && mi.IsStatic && mi.GetParameters().Length == 1);
                    if (create == null)
                    {
                        Debug.LogError("[Fart Raft] Could not create a URP asset. Create one manually: Assets > Create > Rendering > URP Asset (with Universal Renderer).");
                        return;
                    }
                    pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { rendererData });
                    AssetDatabase.CreateAsset(pipeline, pipelinePath);
                }
                SetMember(pipeline, "msaaSampleCount", 4);

                GraphicsSettings.defaultRenderPipeline = pipeline;
                int currentLevel = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(currentLevel, false);
                AssetDatabase.SaveAssets();
            }

            BuildScene();
        }

        static EnvironmentTheme LoadTheme()
        {
            var loaded = AssetDatabase.LoadAssetAtPath<EnvironmentTheme>(ThemePath);
            if (loaded == null)
            {
                loaded = ScriptableObject.CreateInstance<EnvironmentTheme>();
                AssetDatabase.CreateAsset(loaded, ThemePath);
            }
            return loaded;
        }

        // ---------------------------------------------------------------- Post-processing (URP only)

        // Uses reflection so the project still compiles without the URP package.
        static void AddPostProcessing(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Debug.Log("[Fart Raft] Built-in render pipeline: skipping bloom/color grading. Use a URP project for post-processing.");
                return;
            }

            var cameraDataType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (cameraDataType != null)
            {
                var data = cam.GetComponent(cameraDataType);
                if (data == null) data = cam.gameObject.AddComponent(cameraDataType);
                SetMember(data, "renderPostProcessing", true);
                SetMember(data, "antialiasing", "FastApproximateAntialiasing");
            }

            var volumeType = FindType("UnityEngine.Rendering.Volume");
            var profileType = FindType("UnityEngine.Rendering.VolumeProfile");
            if (volumeType == null || profileType == null) return;

            AssetDatabase.DeleteAsset(PostProcessingPath);
            var profile = ScriptableObject.CreateInstance(profileType);
            AssetDatabase.CreateAsset(profile, PostProcessingPath);

            AddOverride(profile, "UnityEngine.Rendering.Universal.Tonemapping", ("mode", "ACES"));
            AddOverride(profile, "UnityEngine.Rendering.Universal.Bloom", ("intensity", 0.5f), ("threshold", 1f), ("scatter", 0.6f));
            AddOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments", ("postExposure", 0.35f), ("contrast", 12f), ("saturation", 20f));
            AddOverride(profile, "UnityEngine.Rendering.Universal.Vignette", ("intensity", 0.22f), ("smoothness", 0.4f));
            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("PostProcessing");
            var volume = volumeGo.AddComponent(volumeType);
            SetMember(volume, "isGlobal", true);
            SetMember(volume, "sharedProfile", profile);
        }

        static void AddOverride(ScriptableObject profile, string typeName, params (string name, object value)[] values)
        {
            var type = FindType(typeName);
            var add = profile.GetType().GetMethod("Add", new[] { typeof(System.Type), typeof(bool) });
            if (type == null || add == null) return;

            var component = (ScriptableObject)add.Invoke(profile, new object[] { type, true });
            component.name = type.Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);

            foreach (var (name, value) in values)
            {
                var field = type.GetField(name);
                var parameter = field?.GetValue(component);
                var valueProperty = parameter?.GetType().GetProperties().FirstOrDefault(p => p.Name == "value" && p.CanWrite);
                if (valueProperty == null) continue;
                valueProperty.SetValue(parameter, ConvertValue(value, valueProperty.PropertyType));
            }
        }

        static System.Type FindType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name);
            if (field != null)
            {
                field.SetValue(target, ConvertValue(value, field.FieldType));
                return;
            }
            var property = type.GetProperties().FirstOrDefault(p => p.Name == name && p.CanWrite);
            if (property != null) property.SetValue(target, ConvertValue(value, property.PropertyType));
        }

        static object ConvertValue(object value, System.Type type)
        {
            if (type.IsEnum && value is string s) return System.Enum.Parse(type, s);
            return value;
        }

        // ---------------------------------------------------------------- Materials

        class Mats
        {
            public readonly Material water = Lit("Water", new Color(0.12f, 0.62f, 0.78f), 0.92f);
            public readonly Material grass = Lit("Grass", new Color(0.45f, 0.78f, 0.32f), 0.1f);
            public readonly Material rock = Lit("Rock", new Color(0.55f, 0.53f, 0.50f), 0.15f);
            public readonly Material rock2 = Lit("Rock2", new Color(0.45f, 0.43f, 0.42f), 0.15f);
            public readonly Material rock3 = Lit("Rock3", new Color(0.64f, 0.59f, 0.52f), 0.15f);
            public readonly Material sand = Lit("Sand", new Color(0.93f, 0.84f, 0.62f), 0.05f);
            public readonly Material pine = Lit("Pine", new Color(0.13f, 0.45f, 0.30f), 0.1f);
            public readonly Material bush = Lit("Bush", new Color(0.25f, 0.58f, 0.22f), 0.1f);
            public readonly Material reed = Lit("Reed", new Color(0.50f, 0.65f, 0.25f), 0.1f);
            public readonly Material lily = Lit("LilyPad", new Color(0.30f, 0.62f, 0.25f), 0.3f);
            public readonly Material cloud = Lit("Cloud", Color.white, 0f);
            public readonly Material mountain = Lit("Mountain", new Color(0.35f, 0.55f, 0.40f), 0f);
            public readonly Material mountain2 = Lit("Mountain2", new Color(0.45f, 0.58f, 0.48f), 0f);
            public readonly Material[] flowerHeads =
            {
                Lit("FlowerPink", new Color(1.00f, 0.55f, 0.75f), 0.2f),
                Lit("FlowerYellow", new Color(1.00f, 0.88f, 0.25f), 0.2f),
                Lit("FlowerWhite", new Color(0.97f, 0.97f, 0.95f), 0.2f),
                Lit("FlowerPurple", new Color(0.65f, 0.45f, 0.95f), 0.2f),
            };
            public readonly Material wood = Lit("Wood", new Color(0.60f, 0.40f, 0.22f), 0.2f);
            public readonly Material darkWood = Lit("DarkWood", new Color(0.42f, 0.27f, 0.15f), 0.2f);
            public readonly Material leaves = Lit("Leaves", new Color(0.20f, 0.55f, 0.25f), 0.1f);
            public readonly Material leaves2 = Lit("Leaves2", new Color(0.40f, 0.65f, 0.20f), 0.1f);
            public readonly Material trunk = Lit("Trunk", new Color(0.40f, 0.28f, 0.18f), 0.1f);
            public readonly Material danger = Lit("Danger", new Color(0.90f, 0.15f, 0.15f), 0.4f);
            public readonly Material metal = Lit("Metal", new Color(0.75f, 0.75f, 0.80f), 0.6f);
            public readonly Material yellow = Lit("Flag", new Color(1.00f, 0.80f, 0.10f), 0.3f);
            public readonly Material white = Lit("White", Color.white, 0.4f);
            public readonly Material black = Lit("Black", new Color(0.05f, 0.05f, 0.05f), 0.4f);
            public readonly Material barrel = Lit("Barrel", new Color(0.85f, 0.45f, 0.10f), 0.3f);
            public readonly Material[] players =
            {
                Lit("Player1", PlayerColors[0], 0.3f),
                Lit("Player2", PlayerColors[1], 0.3f),
                Lit("Player3", PlayerColors[2], 0.3f),
                Lit("Player4", PlayerColors[3], 0.3f),
            };
            public readonly Material puff = PuffMaterial();
        }

        static Shader LitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
                var hdrp = Shader.Find("HDRP/Lit");
                if (hdrp != null) return hdrp;
            }
            return Shader.Find("Standard");
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            var mat = LoadOrCreateMaterial(name, LitShader());
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreateMaterial(string name, Shader shader)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            return mat;
        }

        // Sprites/Default is unlit, transparent and works in Built-in and URP.
        static Material PuffMaterial()
        {
            if (!File.Exists(PuffTexturePath))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                File.WriteAllBytes(PuffTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(PuffTexturePath);
            }

            var mat = LoadOrCreateMaterial("Puff", Shader.Find("Sprites/Default"));
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Environment

        static void SetupLighting()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var sky = LoadOrCreateMaterial("Sky", Shader.Find("Skybox/Procedural"));
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.55f, 0.65f, 0.70f));
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.75f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.25f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.05f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.75f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.68f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.33f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.84f, 0.95f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 260f;
        }

        static void BuildScenery(Transform parent, Mats m, System.Random random)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(parent, false);

            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float radius = 25f + R(random) * 18f;
                Vector3 pos = new Vector3(5f + side * (85f + R(random) * 35f), -4f, -30f + i * 24f + R(random) * 10f);
                var hill = Prim(PrimitiveType.Sphere, "Mountain", scenery, pos,
                    new Vector3(radius * 2f, 22f + R(random) * 30f, radius * 2f), R(random) < 0.5f ? m.mountain : m.mountain2, false);
                NoShadows(hill);
            }

            for (int i = 0; i < 12; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(scenery, false);
                cloud.position = new Vector3(-70f + R(random) * 150f, 45f + R(random) * 15f, -20f + i * 30f);
                int puffs = 3 + random.Next(3);
                for (int j = 0; j < puffs; j++)
                {
                    var puff = Prim(PrimitiveType.Sphere, "Puff", cloud,
                        new Vector3((j - puffs * 0.5f) * 5f, R(random) * 2f, R(random) * 4f),
                        new Vector3(9f + R(random) * 8f, 4f + R(random) * 3f, 7f + R(random) * 5f), m.cloud, false);
                    NoShadows(puff);
                }
            }
        }

        static float R(System.Random random)
        {
            return (float)random.NextDouble();
        }

        static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Transform BuildCourse(Mats m, out int checkpointCount)
        {
            var course = new GameObject("Course").transform;

            // River center line and width at each point.
            Vector3[] p =
            {
                new Vector3(0f, 0f, -10f),
                new Vector3(0f, 0f, 40f),
                new Vector3(24f, 0f, 70f),
                new Vector3(24f, 0f, 110f),
                new Vector3(-14f, 0f, 150f),
                new Vector3(-14f, 0f, 190f),
                new Vector3(8f, 0f, 225f),
                new Vector3(8f, 0f, 275f),
            };
            float[] width = { 16f, 16f, 15f, 15f, 14f, 14f, 10f, 10f };
            int n = p.Length;

            var left = new Vector3[n];
            var right = new Vector3[n];
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 dPrev = i > 0 ? (p[i] - p[i - 1]).normalized : (p[1] - p[0]).normalized;
                Vector3 dNext = i < n - 1 ? (p[i + 1] - p[i]).normalized : dPrev;
                Vector3 tangent = (dPrev + dNext).normalized;
                Vector3 normal = RightOf(tangent);
                float miter = 1f / Mathf.Max(0.3f, Vector3.Dot(normal, RightOf(dNext)));
                tangents[i] = tangent;
                left[i] = p[i] - normal * (width[i] * 0.5f * miter);
                right[i] = p[i] + normal * (width[i] * 0.5f * miter);
            }

            var water = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(course, false);
            water.transform.position = new Vector3(5f, 0f, 135f);
            water.GetComponent<MeshRenderer>().sharedMaterial = theme.waterMaterial != null ? theme.waterMaterial : m.water;
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            water.AddComponent<WaterWaves>();

            var banks = new GameObject("Banks").transform;
            banks.SetParent(course, false);
            var decor = new GameObject("Decor").transform;
            decor.SetParent(course, false);
            var random = new System.Random(42);

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 r = RightOf((p[i + 1] - p[i]).normalized);
                BuildBank(banks, decor, m, left[i], left[i + 1], -r, random);
                BuildBank(banks, decor, m, right[i], right[i + 1], r, random);
            }
            BuildBank(banks, decor, m, left[0] - tangents[0], right[0] - tangents[0], -tangents[0], random);
            BuildBank(banks, decor, m, left[n - 1], right[n - 1], tangents[n - 1], random);

            BuildScenery(course, m, random);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(course, false);

            // Section 1: floating barrels to bump around.
            Barrel(obstacles, m, PointOn(p, 0, 0.45f, -3f));
            Barrel(obstacles, m, PointOn(p, 0, 0.50f, 3f));
            Barrel(obstacles, m, PointOn(p, 0, 0.75f, 0f));

            // Section 2: spinning bar.
            SpinnerObstacle(obstacles, m, PointOn(p, 2, 0.5f, 0f), 10f, 50f);

            // Section 3: red spiky buoys (touch = back to checkpoint).
            HazardBuoy(obstacles, m, PointOn(p, 3, 0.35f, 3f));
            HazardBuoy(obstacles, m, PointOn(p, 3, 0.65f, -3f));

            // Section 4: sliding blocks.
            Vector3 r4 = RightOf((p[5] - p[4]).normalized);
            MovingBlock(obstacles, m, PointOn(p, 4, 0.35f, 0f) - r4 * 3.5f, r4 * 7f, 0f);
            MovingBlock(obstacles, m, PointOn(p, 4, 0.65f, 0f) - r4 * 3.5f, r4 * 7f, 0.5f);

            // Section 5: barrel field.
            Barrel(obstacles, m, PointOn(p, 5, 0.3f, -2f));
            Barrel(obstacles, m, PointOn(p, 5, 0.5f, 3f));
            Barrel(obstacles, m, PointOn(p, 5, 0.7f, -1f));

            // Section 6: narrow slalom.
            Post(obstacles, m, PointOn(p, 6, 0.25f, -2f));
            Post(obstacles, m, PointOn(p, 6, 0.45f, 2f));
            Post(obstacles, m, PointOn(p, 6, 0.65f, -2f));

            int[] checkpointAt = { 2, 4, 6 };
            for (int i = 0; i < checkpointAt.Length; i++)
            {
                int w = checkpointAt[i];
                BuildCheckpoint(course, m, i, p[w], tangents[w], width[w]);
            }
            checkpointCount = checkpointAt.Length;

            BuildFinish(course, m, PointOn(p, 6, 0.85f, 0f), tangents[n - 1], width[n - 1]);

            var start = new GameObject("StartPoint").transform;
            start.SetParent(course, false);
            start.position = new Vector3(0f, 0.25f, -2f);
            start.rotation = Quaternion.LookRotation(tangents[0]);
            return start;
        }

        static Vector3 RightOf(Vector3 forward)
        {
            return new Vector3(forward.z, 0f, -forward.x).normalized;
        }

        static Vector3 PointOn(Vector3[] p, int segment, float t, float sideOffset)
        {
            Vector3 a = p[segment];
            Vector3 b = p[segment + 1];
            return Vector3.Lerp(a, b, t) + RightOf((b - a).normalized) * sideOffset;
        }

        const float LandTop = 0.55f;

        // One river bank: invisible wall collider, boulders along the edge, a sand
        // strip, grass land, and decoration (trees, bushes, flowers, reeds, lily pads).
        static void BuildBank(Transform banks, Transform decor, Mats m, Vector3 a, Vector3 b, Vector3 outward, System.Random random)
        {
            Wall(banks, "Wall", a, b);

            Vector3 dir = (b - a).normalized;
            outward.y = 0f;
            outward.Normalize();
            float length = Vector3.Distance(a, b);
            Vector3 mid = (a + b) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(dir);

            var sand = Prim(PrimitiveType.Cube, "Sand", banks, mid + outward * 1.8f + Vector3.up * -0.15f,
                new Vector3(3.6f, 1f, length + 1f), m.sand, false);
            sand.transform.rotation = rotation;
            sand.isStatic = true;

            const float landWidth = 20f;
            var land = Prim(PrimitiveType.Cube, "Land", banks, mid + outward * (3.4f + landWidth * 0.5f) + Vector3.up * (LandTop - 0.5f),
                new Vector3(landWidth, 1f, length + 1f), theme.grassMaterial != null ? theme.grassMaterial : m.grass, false);
            land.transform.rotation = rotation;
            land.isStatic = true;

            EdgeRocks(decor, m, a, b, outward, random);

            for (float d = 1f; d < length; d += 2.8f)
            {
                Vector3 along = a + dir * d;
                float roll = R(random);
                if (roll < 0.22f) Reeds(decor, m, along + outward * (2.4f + R(random) * 1.2f) + Vector3.up * 0.35f, random);
                else if (roll < 0.36f) LilyPad(decor, m, along - outward * (1.6f + R(random) * 1.5f), random);

                Vector3 pos = along + outward * (4.5f + R(random) * 16f) + Vector3.up * LandTop;
                float kind = R(random);
                if (kind < 0.30f)
                {
                    if (!TrySpawn(theme.trees, decor, pos, random)) Tree(decor, m, pos, random);
                }
                else if (kind < 0.45f)
                {
                    if (!TrySpawn(theme.bushes, decor, pos, random)) Bush(decor, m, pos, random);
                }
                else if (kind < 0.60f)
                {
                    if (!TrySpawn(theme.flowers, decor, pos, random)) Flowers(decor, m, pos, random);
                }
                else if (kind < 0.66f)
                {
                    if (!TrySpawn(theme.rocks, decor, pos, random)) Rock(decor, m, pos, random);
                }
            }
        }

        static void Wall(Transform parent, string name, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (a + b) * 0.5f + Vector3.up * 0.3f;
            go.transform.rotation = Quaternion.LookRotation(d.normalized);
            go.AddComponent<BoxCollider>().size = new Vector3(2f, 1.6f, d.magnitude + 2f);
            go.isStatic = true;
        }

        static void EdgeRocks(Transform parent, Mats m, Vector3 a, Vector3 b, Vector3 outward, System.Random random)
        {
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);
            Material[] rockMats = { m.rock, m.rock2, m.rock3 };
            for (float d = 0f; d < length; d += 1.8f)
            {
                float s = 1.4f + R(random);
                Vector3 pos = a + dir * d + outward * (0.2f + R(random) * 0.4f) + Vector3.up * 0.1f;
                if (TrySpawn(theme.rocks, parent, pos, random)) continue;
                var rock = Prim(PrimitiveType.Sphere, "EdgeRock", parent, pos,
                    new Vector3(s * 1.2f, s * (0.55f + R(random) * 0.3f), s), rockMats[random.Next(rockMats.Length)], false);
                rock.transform.rotation = Quaternion.Euler(R(random) * 10f, R(random) * 360f, R(random) * 10f);
                rock.isStatic = true;
            }
        }

        static void Tree(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
            tree.localScale = Vector3.one * (0.8f + R(random) * 0.8f);

            if (R(random) < 0.45f)
            {
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.6f, 0f), new Vector3(0.35f, 0.6f, 0.35f), m.trunk, false);
                for (int i = 0; i < 3; i++)
                {
                    float w = 2.8f - i * 0.65f;
                    MeshPrim(ConeMesh(), "PineTier", tree, new Vector3(0f, 0.9f + i * 1.05f, 0f), new Vector3(w, 1.9f, w), m.pine);
                }
            }
            else
            {
                Material leaves = R(random) < 0.5f ? m.leaves : m.leaves2;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.1f, 0f), new Vector3(0.4f, 1.1f, 0.4f), m.trunk, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 2.9f, 0f), Vector3.one * 2.6f, leaves, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0.8f, 2.4f, 0.3f), Vector3.one * 1.8f, leaves, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(-0.6f, 2.5f, -0.5f), Vector3.one * 1.9f, leaves, false);
            }
        }

        static void Bush(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var bush = new GameObject("Bush").transform;
            bush.SetParent(parent, false);
            bush.position = pos;
            int count = 2 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float s = 0.9f + R(random) * 0.6f;
                Prim(PrimitiveType.Sphere, "Leaf", bush, new Vector3((R(random) - 0.5f) * 1.4f, s * 0.35f, (R(random) - 0.5f) * 1.4f),
                    new Vector3(s, s * 0.8f, s), m.bush, false);
            }
        }

        static void Flowers(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var patch = new GameObject("Flowers").transform;
            patch.SetParent(parent, false);
            patch.position = pos;
            int count = 4 + random.Next(4);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = new Vector3((R(random) - 0.5f) * 1.6f, 0f, (R(random) - 0.5f) * 1.6f);
                Prim(PrimitiveType.Cylinder, "Stem", patch, p + Vector3.up * 0.2f, new Vector3(0.05f, 0.2f, 0.05f), m.reed, false);
                Prim(PrimitiveType.Sphere, "Head", patch, p + Vector3.up * 0.42f, Vector3.one * 0.22f,
                    m.flowerHeads[random.Next(m.flowerHeads.Length)], false);
            }
            NoShadows(patch.gameObject);
        }

        static void Reeds(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var reeds = new GameObject("Reeds").transform;
            reeds.SetParent(parent, false);
            reeds.position = pos;
            int count = 4 + random.Next(4);
            for (int i = 0; i < count; i++)
            {
                float h = 0.6f + R(random) * 0.5f;
                var stalk = Prim(PrimitiveType.Cylinder, "Reed", reeds,
                    new Vector3((R(random) - 0.5f) * 0.9f, h, (R(random) - 0.5f) * 0.9f), new Vector3(0.07f, h, 0.07f), m.reed, false);
                stalk.transform.localRotation = Quaternion.Euler((R(random) - 0.5f) * 14f, 0f, (R(random) - 0.5f) * 14f);
            }
            NoShadows(reeds.gameObject);
        }

        static void LilyPad(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            float s = 1f + R(random) * 0.7f;
            var pad = Prim(PrimitiveType.Cylinder, "LilyPad", parent, pos + Vector3.up * 0.06f, new Vector3(s, 0.02f, s), m.lily, false);
            if (R(random) < 0.35f)
            {
                Prim(PrimitiveType.Sphere, "LilyFlower", pad.transform, new Vector3(0.15f, 6f, 0.1f), new Vector3(0.25f, 8f, 0.25f), m.flowerHeads[0], false);
            }
            NoShadows(pad);
        }

        static void Rock(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            float s = 0.8f + R(random) * 1.5f;
            var rock = Prim(PrimitiveType.Sphere, "Rock", parent, pos + Vector3.up * 0.2f, new Vector3(s * 1.4f, s * 0.8f, s), m.rock2, false);
            rock.transform.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
        }

        static bool TrySpawn(GameObject[] prefabs, Transform parent, Vector3 pos, System.Random random)
        {
            if (prefabs == null || prefabs.Length == 0) return false;
            var prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
            go.transform.localScale = prefab.transform.localScale * (theme.propScale * (0.85f + R(random) * 0.3f));
            return true;
        }

        static Mesh coneMesh;

        // 8-sided low-poly cone, base radius 0.5 at y=0, tip at y=1. Saved as an asset so the scene keeps it.
        static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            string path = MaterialsFolder + "/Cone.asset";
            coneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (coneMesh != null) return coneMesh;

            const int sides = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 tip = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 sideCenter = (p0 + p1 + tip) / 3f;
                AddTriangle(vertices, triangles, tip, p0, p1, new Vector3(sideCenter.x, 0f, sideCenter.z));
                AddTriangle(vertices, triangles, Vector3.zero, p0, p1, Vector3.down);
            }

            coneMesh = new Mesh { name = "Cone" };
            coneMesh.SetVertices(vertices);
            coneMesh.SetTriangles(triangles, 0);
            coneMesh.RecalculateNormals();
            coneMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(coneMesh, path);
            return coneMesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Unity treats Cross(b - a, c - a) as the front-face normal.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                Vector3 t = b;
                b = c;
                c = t;
            }
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        static GameObject MeshPrim(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void Barrel(Transform parent, Mats m, Vector3 pos)
        {
            var go = Prim(PrimitiveType.Cylinder, "Barrel", parent, pos + Vector3.up * 0.25f, new Vector3(1.4f, 0.5f, 1.4f), m.barrel, true);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 6f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            PhysicsCompat.SetDamping(body, 1.2f, 1.2f);
        }

        static void SpinnerObstacle(Transform parent, Mats m, Vector3 pos, float length, float speed)
        {
            Prim(PrimitiveType.Cylinder, "SpinnerPillar", parent, pos + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f), m.metal, true);
            var pivot = new GameObject("Spinner");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = pos;
            pivot.AddComponent<Rigidbody>().isKinematic = true;
            pivot.AddComponent<Spinner>().degreesPerSecond = speed;
            Prim(PrimitiveType.Cube, "Bar", pivot.transform, new Vector3(0f, 0.4f, 0f), new Vector3(length, 0.8f, 0.7f), m.danger, true);
        }

        static void HazardBuoy(Transform parent, Mats m, Vector3 pos)
        {
            var go = Prim(PrimitiveType.Sphere, "HazardBuoy", parent, pos + Vector3.up * 0.3f, Vector3.one * 2.2f, m.danger, true);
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<Hazard>();
            for (int i = 0; i < 6; i++)
            {
                var spike = Prim(PrimitiveType.Cube, "Spike", go.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 1.4f), m.metal, false);
                spike.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 0f : 90f, i * 60f, 0f);
            }
            Prim(PrimitiveType.Cylinder, "Ring", parent, pos + Vector3.up * 0.02f, new Vector3(3.2f, 0.02f, 3.2f), m.white, false);
        }

        static void MovingBlock(Transform parent, Mats m, Vector3 start, Vector3 offset, float phase)
        {
            var go = Prim(PrimitiveType.Cube, "MovingBlock", parent, start + Vector3.up * 0.4f, new Vector3(4.5f, 1.2f, 1.6f), m.darkWood, true);
            go.transform.rotation = Quaternion.LookRotation(Vector3.Cross(offset.normalized, Vector3.up));
            go.AddComponent<Rigidbody>().isKinematic = true;
            var mover = go.AddComponent<Mover>();
            mover.offset = offset;
            mover.period = 3f;
            mover.phase = phase;
        }

        static void Post(Transform parent, Mats m, Vector3 pos)
        {
            var go = Prim(PrimitiveType.Cylinder, "Post", parent, pos + Vector3.up * 0.8f, new Vector3(2.4f, 1.2f, 2.4f), m.darkWood, true);
            go.isStatic = true;
            Prim(PrimitiveType.Cylinder, "Stripe", go.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.05f, 0.15f, 1.05f), m.danger, false);
        }

        static void BuildCheckpoint(Transform parent, Mats m, int index, Vector3 pos, Vector3 tangent, float width)
        {
            var go = new GameObject("Checkpoint" + (index + 1));
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.25f;
            go.transform.rotation = Quaternion.LookRotation(tangent);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(width, 4f, 2f);
            go.AddComponent<Checkpoint>().index = index;

            float half = width * 0.5f - 1.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(half * s, 1.75f, 0f), new Vector3(0.2f, 2f, 0.2f), m.white, false);
                Prim(PrimitiveType.Cube, "Flag", go.transform, new Vector3(half * s - 0.6f * s, 3.3f, 0f), new Vector3(1.2f, 0.7f, 0.05f), m.yellow, false);
            }
        }

        static void BuildFinish(Transform parent, Mats m, Vector3 pos, Vector3 tangent, float width)
        {
            var go = new GameObject("FinishLine");
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.25f;
            go.transform.rotation = Quaternion.LookRotation(tangent);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(width, 4f, 2f);
            go.AddComponent<FinishLine>();

            float half = width * 0.5f - 0.8f;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(half * s, 2.5f, 0f), new Vector3(0.35f, 2.8f, 0.35f), m.white, false);
            }

            int tiles = 12;
            float tile = half * 2f / tiles;
            for (int i = 0; i < tiles; i++)
            {
                for (int row = 0; row < 2; row++)
                {
                    float x = -half + tile * (i + 0.5f);
                    Prim(PrimitiveType.Cube, "Checker", go.transform, new Vector3(x, 5f + row * tile, 0f),
                        new Vector3(tile, tile, 0.2f), (i + row) % 2 == 0 ? m.white : m.black, false);
                }
            }
        }

        // ---------------------------------------------------------------- Raft

        static Raft BuildRaft(Mats m, Transform start, out FartSeat[] seats)
        {
            var raftGo = new GameObject("Raft");
            raftGo.transform.SetPositionAndRotation(start.position, start.rotation);
            var body = raftGo.AddComponent<Rigidbody>();
            body.mass = 40f;
            var box = raftGo.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 0.6f, 4f);
            var raft = raftGo.AddComponent<Raft>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(raftGo.transform, false);
            raft.visual = visual;

            for (int i = 0; i < 5; i++)
            {
                var log = Prim(PrimitiveType.Cylinder, "Log", visual, new Vector3(-1.6f + i * 0.8f, 0f, 0f), new Vector3(0.8f, 2f, 0.8f), i % 2 == 0 ? m.wood : m.darkWood, false);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Prim(PrimitiveType.Cube, "Plank", visual, new Vector3(0f, 0.38f, 1.4f), new Vector3(4.2f, 0.12f, 0.45f), m.darkWood, false);
            Prim(PrimitiveType.Cube, "Plank", visual, new Vector3(0f, 0.38f, -1.4f), new Vector3(4.2f, 0.12f, 0.45f), m.darkWood, false);

            seats = new FartSeat[4];
            Vector2[] corners = { new Vector2(-1.3f, -1.3f), new Vector2(1.3f, -1.3f), new Vector2(-1.3f, 1.3f), new Vector2(1.3f, 1.3f) };
            for (int i = 0; i < 4; i++)
            {
                seats[i] = BuildSeat(m, visual, raft, i, new Vector3(corners[i].x, 0.42f, corners[i].y));
            }
            return raft;
        }

        static FartSeat BuildSeat(Mats m, Transform visual, Raft raft, int index, Vector3 localPos)
        {
            var seatGo = new GameObject("Seat_P" + (index + 1));
            seatGo.transform.SetParent(visual, false);
            seatGo.transform.localPosition = localPos;

            var character = new GameObject("Character").transform;
            character.SetParent(seatGo.transform, false);
            Material body = m.players[index];
            Prim(PrimitiveType.Capsule, "Body", character, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 0.6f, 0.8f), body, false);
            Prim(PrimitiveType.Sphere, "CheekL", character, new Vector3(-0.17f, 0.35f, -0.3f), Vector3.one * 0.38f, body, false);
            Prim(PrimitiveType.Sphere, "CheekR", character, new Vector3(0.17f, 0.35f, -0.3f), Vector3.one * 0.38f, body, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Sphere, "Eye", character, new Vector3(0.16f * s, 0.95f, 0.33f), Vector3.one * 0.22f, m.white, false);
                Prim(PrimitiveType.Sphere, "Pupil", character, new Vector3(0.16f * s, 0.95f, 0.43f), Vector3.one * 0.1f, m.black, false);
            }

            var puffGo = new GameObject("FartParticles");
            puffGo.transform.SetParent(character, false);
            puffGo.transform.localPosition = new Vector3(0f, 0.35f, -0.5f);
            puffGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var particles = BuildFartParticles(puffGo, m.puff);

            var audio = seatGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var seat = seatGo.AddComponent<FartSeat>();
            seat.playerIndex = index;
            seat.raft = raft;
            seat.character = character;
            seat.fartParticles = particles;
            seat.fartAudio = audio;
            return seat;
        }

        static ParticleSystem BuildFartParticles(GameObject go, Material material)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.75f, 0.25f, 0.85f), new Color(0.80f, 0.85f, 0.45f, 0.65f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.maxParticles = 150;

            var emission = ps.emission;
            emission.rateOverTime = 35f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        static CameraFollow BuildCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.farClipPlane = 260f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = target;
            follow.Snap();
            return follow;
        }

        // ---------------------------------------------------------------- UI

        static void LoadUIResources()
        {
#if UNITY_2022_2_OR_NEWER
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        static void BuildUI(GameManager manager, FartSeat[] seats)
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif

            Transform root = canvasGo.transform;
            Vector2 center = new Vector2(0.5f, 0.5f);

            // HUD
            var hud = UIObject("HUD", root);
            Stretch(hud);
            manager.hudPanel = hud.gameObject;
            manager.timerText = Label(hud, "Timer", "0:00.00", 64, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(400f, 90f));
            manager.syncText = Label(hud, "SyncBoost", "SYNC BOOST!", 56, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 80f));
            manager.syncText.color = new Color(0.7f, 1f, 0.3f);
            manager.messageText = Label(hud, "Message", "", 110, center, new Vector2(0f, 120f), new Vector2(1400f, 160f));
            Label(hud, "Hint", "Hold your button to fart.  Slide left/right while holding to aim.  Fart together for a SYNC BOOST!",
                30, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1100f, 60f));
            manager.hudMenuButton = MakeButton(hud, "MenuButton", "MENU", new Vector2(0.5f, 1f), new Vector2(330f, -60f), new Vector2(180f, 80f), new Color(0f, 0f, 0f, 0.45f), 34);

            Vector2[] anchors = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            var buttons = new TouchFartButton[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = anchors[i];
                Vector2 pos = new Vector2(a.x < 0.5f ? 230f : -230f, a.y < 0.5f ? 230f : -230f);
                buttons[i] = FartButton(hud, i, a, pos, seats[i]);
            }
            manager.buttons = buttons;

            // Menu
            var menu = Panel(root, "Menu", new Color(0.05f, 0.1f, 0.2f, 0.85f));
            manager.menuPanel = menu.gameObject;
            var title = Label(menu, "Title", "FART RAFT", 150, center, new Vector2(0f, 300f), new Vector2(1400f, 200f));
            title.color = new Color(0.75f, 1f, 0.35f);
            Label(menu, "Subtitle", "Steer the raft together with fart power!", 44, center, new Vector2(0f, 180f), new Vector2(1400f, 80f));
            manager.soloButton = MakeButton(menu, "Solo", "SOLO\n(2 thumbs)", center, new Vector2(-540f, -10f), new Vector2(320f, 180f), PlayerColors[0], 44);
            manager.twoPlayersButton = MakeButton(menu, "TwoPlayers", "2 PLAYERS", center, new Vector2(-180f, -10f), new Vector2(320f, 180f), PlayerColors[1], 44);
            manager.threePlayersButton = MakeButton(menu, "ThreePlayers", "3 PLAYERS", center, new Vector2(180f, -10f), new Vector2(320f, 180f), PlayerColors[2], 44);
            manager.fourPlayersButton = MakeButton(menu, "FourPlayers", "4 PLAYERS", center, new Vector2(540f, -10f), new Vector2(320f, 180f), PlayerColors[3], 44);
            Label(menu, "MenuHint", "Everyone plays on the same phone - each player holds one corner button.", 34, center, new Vector2(0f, -200f), new Vector2(1500f, 60f));

            // Finish
            var finish = Panel(root, "Finish", new Color(0f, 0f, 0f, 0.7f));
            manager.finishPanel = finish.gameObject;
            manager.finishText = Label(finish, "FinishText", "FINISH!", 90, center, new Vector2(0f, 150f), new Vector2(1200f, 400f));
            manager.playAgainButton = MakeButton(finish, "PlayAgain", "PLAY AGAIN", center, new Vector2(-220f, -200f), new Vector2(380f, 140f), PlayerColors[2], 46);
            manager.finishMenuButton = MakeButton(finish, "BackToMenu", "MENU", center, new Vector2(220f, -200f), new Vector2(380f, 140f), PlayerColors[1], 46);
        }

        static TouchFartButton FartButton(Transform parent, int index, Vector2 anchor, Vector2 pos, FartSeat seat)
        {
            var rt = UIObject("FartButton_P" + (index + 1), parent);
            Place(rt, anchor, pos, new Vector2(320f, 320f));

            var ringRt = UIObject("GasRing", rt);
            Stretch(ringRt);
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = circleSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;

            var faceRt = UIObject("Face", rt);
            Place(faceRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f));
            var face = faceRt.gameObject.AddComponent<Image>();
            face.sprite = circleSprite;
            face.color = PlayerColors[index];
            face.raycastTarget = false;

            var label = Label(rt, "Label", "P" + (index + 1), 56, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 100f));

            var button = rt.gameObject.AddComponent<TouchFartButton>();
            button.seat = seat;
            button.face = face;
            button.gasRing = ring;
            button.label = label;
            button.color = PlayerColors[index];
            return button;
        }

        static RectTransform UIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static RectTransform Panel(Transform parent, string name, Color color)
        {
            var rt = UIObject(name, parent);
            Stretch(rt);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        static Text Label(Transform parent, string name, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = UIObject(name, parent);
            Place(rt, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = uiFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        static Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Color color, int fontSize)
        {
            var rt = UIObject(name, parent);
            Place(rt, anchor, pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(rt, "Label", text, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        // ---------------------------------------------------------------- Helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

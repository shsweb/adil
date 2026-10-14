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
        const string GeneratedFolder = Root + "/Generated";
        const string ScenesFolder = Root + "/Scenes";
        const string ThemePath = Root + "/EnvironmentTheme.asset";

        static readonly Color[] PlayerColors =
        {
            new Color(0.90f, 0.58f, 0.32f),
            new Color(0.36f, 0.60f, 0.95f),
            new Color(0.42f, 0.85f, 0.35f),
            new Color(0.62f, 0.55f, 0.70f),
        };

        static readonly string[] AnimalNames = { "CAPY", "HIPPO", "CROC", "GORILLA" };

        static EnvironmentTheme theme;
        static Font uiFont;
        static Sprite roundedSprite;
        static Sprite circleSprite;

        // ================================================================ Menus

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

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(ScenesFolder);
            AssetDatabase.DeleteAsset(GeneratedFolder);
            EnsureFolder(GeneratedFolder);
            AssetDatabase.DeleteAsset(ScenesFolder + "/Game.unity");
            // Generated meshes and the physics material are shared by all stages, so caches are reset only once.
            ResetCaches();

            var buildScenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < Stages.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Fart Raft", "Building stage " + (i + 1) + ": " + Stages[i].name, (float)i / Stages.Length);
                string path = ScenesFolder + "/" + GameManager.StageSceneName(i) + ".unity";
                BuildStage(i, path);
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorUtility.ClearProgressBar();

            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(buildScenes[0].path);

            EditorUtility.DisplayDialog("Fart Raft",
                Stages.Length + " stages built and saved to " + ScenesFolder + ".\n\n" +
                "Press Play. In the editor each player holds a direction (where the raft should go):\n" +
                "P1: W A S D    P2: arrow keys    P3: I J K L    P4: numpad 8 4 5 6", "OK");
        }

        static void BuildStage(int index, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            stage = Stages[index];
            stagePrefix = "S" + (index + 1) + "_";

            LoadUIResources();
            theme = LoadTheme();

            var m = new Mats();
            var random = new System.Random(42 + index);

            SetupLighting();
            var track = SampleTrack(stage.waypoints, 1f);
            var course = BuildCourse(track, m, random, out Transform startPoint, out int checkpointCount);
            BuildEnvironment(track, course, m, random);
            OptimizeStatic(course);

            var raft = BuildRaft(m, startPoint, out FartSeat[] seats);
            var cameraFollow = BuildCamera(raft.transform, track.pos.ToArray());
            AddPostProcessing(cameraFollow.GetComponent<Camera>());
            ConfigureQuality();

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            managerGo.AddComponent<KeyboardControls>().seats = seats;
            manager.raft = raft;
            manager.seats = seats;
            manager.startPoint = startPoint;
            manager.cameraFollow = cameraFollow;
            manager.coursePath = track.pos.ToArray();
            manager.checkpointCount = checkpointCount;
            manager.impactEffect = BuildImpactEffect(m);
            manager.gameAudio = BuildGameAudio(managerGo);
            manager.stageIndex = index;
            manager.stageNames = Stages.Select(st => st.name).ToArray();

            BuildUI(manager, seats);

            EditorSceneManager.SaveScene(scene, path);
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

        // ================================================================ Track layout

        enum Edge { Open, Bumper, Rock }

        struct Waypoint
        {
            public Vector3 position;
            public float width;

            public Waypoint(float x, float y, float z, float width)
            {
                position = new Vector3(x, y, z);
                this.width = width;
            }
        }

        // A stage is one course: slide layout, walls, obstacles and colors.
        class Stage
        {
            public string name;
            // The slide center line: x, height, z, width. Flat pools alternate with steep
            // slides; the height never goes up, so the raft can never get stuck in a dip.
            public Waypoint[] waypoints;
            // Walls per segment (between waypoint i and i+1), one letter each:
            // R = rocks, B = bumpers, O = open (you can fall off).
            public string left, right;
            public int[] checkpoints;
            public int finishSegment;
            public System.Action<Transform, Mats, Track, System.Random> obstacles;

            public Color slideBody, bumper, coral, coral2, coral3, grass, leaves, leaves2;
            public Color waterDeep, waterLight;
            public Color skyTint, skyGround, fog, ambient, sun;
        }

        static Edge EdgeAt(string edges, int segment)
        {
            switch (edges[Mathf.Clamp(segment, 0, edges.Length - 1)])
            {
                case 'R': return Edge.Rock;
                case 'B': return Edge.Bumper;
                default: return Edge.Open;
            }
        }

        static readonly Stage[] Stages =
        {
            new Stage
            {
                name = "SUNNY SLIDES",
                waypoints = new[]
                {
                    new Waypoint(0f, 60f, -12f, 14f),     // start pool
                    new Waypoint(0f, 59.5f, 20f, 14f),
                    new Waypoint(6f, 52f, 50f, 11f),      // slide
                    new Waypoint(20f, 45f, 80f, 11f),
                    new Waypoint(28f, 44.5f, 110f, 17f),  // pool: buoys
                    new Waypoint(22f, 44f, 140f, 17f),    // pool: big spinner
                    new Waypoint(4f, 36.5f, 168f, 10f),   // narrow slide without walls
                    new Waypoint(-14f, 29f, 195f, 10f),   // slide: pendulums
                    new Waypoint(-22f, 28.5f, 225f, 16f), // canyon pool: rocks and sliding blocks
                    new Waypoint(-14f, 28f, 255f, 16f),
                    new Waypoint(0f, 21f, 282f, 11f),     // slide: spinners
                    new Waypoint(14f, 14.5f, 308f, 11f),
                    new Waypoint(16f, 14f, 340f, 16f),    // finish pool
                },
                left = "RBBBROBROBOR",
                right = "RBOBROBRBBBR",
                checkpoints = new[] { 2, 3, 5, 6, 7, 9, 10 },
                finishSegment = 11,
                obstacles = SunnyObstacles,
                slideBody = new Color(0.30f, 0.78f, 0.72f),
                bumper = new Color(0.62f, 0.32f, 0.85f),
                coral = new Color(0.95f, 0.50f, 0.43f),
                coral2 = new Color(0.98f, 0.64f, 0.50f),
                coral3 = new Color(0.88f, 0.42f, 0.40f),
                grass = new Color(0.50f, 0.80f, 0.30f),
                leaves = new Color(0.30f, 0.62f, 0.25f),
                leaves2 = new Color(0.45f, 0.72f, 0.25f),
                waterDeep = new Color(0.16f, 0.62f, 0.94f),
                waterLight = new Color(0.55f, 0.85f, 1f),
                skyTint = new Color(0.42f, 0.62f, 0.95f),
                skyGround = new Color(0.45f, 0.68f, 0.92f),
                fog = new Color(0.55f, 0.75f, 0.95f),
                ambient = new Color(0.75f, 0.85f, 1f),
                sun = new Color(1f, 0.96f, 0.9f),
            },
            new Stage
            {
                name = "JUNGLE RAPIDS",
                waypoints = new[]
                {
                    new Waypoint(0f, 70f, -12f, 14f),     // start pool
                    new Waypoint(0f, 69.5f, 20f, 14f),
                    new Waypoint(-8f, 62f, 48f, 11f),     // slide
                    new Waypoint(-22f, 55f, 75f, 11f),
                    new Waypoint(-26f, 54.5f, 105f, 16f), // pool: sliding blocks
                    new Waypoint(-14f, 54f, 135f, 16f),   // pool: buoys
                    new Waypoint(2f, 47f, 160f, 9f),      // narrow slide
                    new Waypoint(18f, 40f, 185f, 9f),
                    new Waypoint(26f, 39.5f, 215f, 17f),  // pool: big spinner
                    new Waypoint(18f, 39f, 245f, 17f),    // pool: rocks
                    new Waypoint(2f, 32f, 270f, 11f),     // slide: pendulums
                    new Waypoint(-12f, 25f, 295f, 11f),
                    new Waypoint(-18f, 24.5f, 325f, 16f), // pool: buoys
                    new Waypoint(-10f, 24f, 355f, 16f),   // pool: spinner
                    new Waypoint(4f, 17f, 380f, 11f),     // slide: spinners
                    new Waypoint(14f, 10f, 405f, 11f),
                    new Waypoint(14f, 9.5f, 435f, 16f),   // finish pool
                },
                left = "RBBRRBORRBORRBBR",
                right = "RBBRRBBRRBBRRBOR",
                checkpoints = new[] { 3, 5, 7, 9, 11, 13 },
                finishSegment = 15,
                obstacles = JungleObstacles,
                slideBody = new Color(0.62f, 0.45f, 0.26f),
                bumper = new Color(0.98f, 0.62f, 0.15f),
                coral = new Color(0.42f, 0.55f, 0.30f),
                coral2 = new Color(0.52f, 0.62f, 0.34f),
                coral3 = new Color(0.36f, 0.47f, 0.28f),
                grass = new Color(0.28f, 0.66f, 0.22f),
                leaves = new Color(0.16f, 0.48f, 0.18f),
                leaves2 = new Color(0.34f, 0.64f, 0.18f),
                waterDeep = new Color(0.08f, 0.60f, 0.60f),
                waterLight = new Color(0.45f, 0.90f, 0.80f),
                skyTint = new Color(0.40f, 0.72f, 0.72f),
                skyGround = new Color(0.30f, 0.58f, 0.50f),
                fog = new Color(0.52f, 0.78f, 0.72f),
                ambient = new Color(0.70f, 0.90f, 0.82f),
                sun = new Color(1f, 0.95f, 0.80f),
            },
            new Stage
            {
                name = "VOLCANO RUN",
                waypoints = new[]
                {
                    new Waypoint(0f, 80f, -12f, 13f),     // start pool
                    new Waypoint(0f, 79.5f, 18f, 13f),
                    new Waypoint(10f, 71f, 45f, 10f),     // steep slide: pendulum, buoys
                    new Waypoint(24f, 62f, 72f, 10f),
                    new Waypoint(30f, 61.5f, 100f, 15f),  // pool: spinner
                    new Waypoint(22f, 61f, 128f, 15f),    // pool: sliding blocks
                    new Waypoint(6f, 53f, 152f, 9f),      // narrow slide: spinner, pendulums
                    new Waypoint(-10f, 45f, 176f, 9f),
                    new Waypoint(-20f, 44.5f, 204f, 15f), // pool: buoys and rocks
                    new Waypoint(-12f, 44f, 232f, 15f),   // pool: four-arm spinner
                    new Waypoint(4f, 36f, 256f, 10f),     // slide: pendulums
                    new Waypoint(18f, 28f, 280f, 10f),
                    new Waypoint(24f, 27.5f, 308f, 16f),  // pool: big spinner
                    new Waypoint(16f, 27f, 336f, 16f),    // pool: blocks and buoys
                    new Waypoint(0f, 19f, 360f, 10f),     // slide: spinners, pendulum
                    new Waypoint(-12f, 11f, 384f, 10f),
                    new Waypoint(-14f, 10.5f, 414f, 16f), // finish pool
                },
                left = "RBBRRBORRBBRRBBR",
                right = "RBORRBBRROBRRBOR",
                checkpoints = new[] { 2, 3, 5, 6, 7, 9, 11, 13 },
                finishSegment = 15,
                obstacles = VolcanoObstacles,
                slideBody = new Color(0.36f, 0.31f, 0.33f),
                bumper = new Color(0.95f, 0.32f, 0.16f),
                coral = new Color(0.32f, 0.27f, 0.28f),
                coral2 = new Color(0.42f, 0.31f, 0.28f),
                coral3 = new Color(0.24f, 0.21f, 0.23f),
                grass = new Color(0.90f, 0.42f, 0.12f),
                leaves = new Color(0.38f, 0.26f, 0.20f),
                leaves2 = new Color(0.55f, 0.32f, 0.16f),
                waterDeep = new Color(0.10f, 0.48f, 0.88f),
                waterLight = new Color(0.50f, 0.78f, 1f),
                skyTint = new Color(0.95f, 0.50f, 0.35f),
                skyGround = new Color(0.55f, 0.26f, 0.22f),
                fog = new Color(0.85f, 0.52f, 0.42f),
                ambient = new Color(1f, 0.78f, 0.66f),
                sun = new Color(1f, 0.78f, 0.58f),
            },
        };

        static Stage stage;
        static string stagePrefix = "";

        class Track
        {
            public readonly List<Vector3> pos = new List<Vector3>();
            public readonly List<Vector3> forward = new List<Vector3>();
            public readonly List<Vector3> right = new List<Vector3>();
            public readonly List<float> width = new List<float>();
            public readonly List<float> distance = new List<float>();
            public readonly List<int> segment = new List<int>();
            public int Count => pos.Count;

            public Vector3 Edge(int k, float side) => pos[k] + right[k] * (side * width[k] * 0.5f);

            public int IndexAt(int seg, float t)
            {
                int first = segment.IndexOf(seg);
                int last = segment.LastIndexOf(seg);
                if (first < 0) return 0;
                return Mathf.RoundToInt(Mathf.Lerp(first, last, t));
            }

            public Vector3 Point(int k, float sideOffset) => pos[k] + right[k] * sideOffset;
        }

        static Track SampleTrack(Waypoint[] w, float step)
        {
            var track = new Track();
            int n = w.Length;

            // Height tangents (Fritsch-Carlson) keep the height curve monotone between waypoints.
            var slopes = new float[n];
            for (int i = 0; i < n; i++)
            {
                float before = i > 0 ? w[i].position.y - w[i - 1].position.y : 0f;
                float after = i < n - 1 ? w[i + 1].position.y - w[i].position.y : 0f;
                if (i == 0) slopes[i] = after;
                else if (i == n - 1) slopes[i] = before;
                else if (before * after <= 0f) slopes[i] = 0f;
                else slopes[i] = 2f / (1f / before + 1f / after);
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = w[Mathf.Max(i - 1, 0)].position;
                Vector3 p1 = w[i].position;
                Vector3 p2 = w[i + 1].position;
                Vector3 p3 = w[Mathf.Min(i + 2, n - 1)].position;
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    Vector3 p = CatmullRom(p0, p1, p2, p3, t);
                    p.y = Hermite(p1.y, slopes[i], p2.y, slopes[i + 1], t);
                    track.pos.Add(p);
                    track.width.Add(Mathf.Lerp(w[i].width, w[i + 1].width, Mathf.SmoothStep(0f, 1f, t)));
                    track.segment.Add(i);
                }
            }
            track.pos.Add(w[n - 1].position);
            track.width.Add(w[n - 1].width);
            track.segment.Add(n - 2);

            float total = 0f;
            for (int k = 0; k < track.Count; k++)
            {
                Vector3 d = track.pos[Mathf.Min(k + 1, track.Count - 1)] - track.pos[Mathf.Max(k - 1, 0)];
                d.y = 0f;
                d.Normalize();
                track.forward.Add(d);
                track.right.Add(Vector3.Cross(Vector3.up, d).normalized);
                if (k > 0) total += Vector3.Distance(track.pos[k], track.pos[k - 1]);
                track.distance.Add(total);
            }
            return track;
        }

        static float Hermite(float y1, float m1, float y2, float m2, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * y1 + (t3 - 2f * t2 + t) * m1 + (-2f * t3 + 3f * t2) * y2 + (t3 - t2) * m2;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ================================================================ Course

        static Transform BuildCourse(Track tr, Mats m, System.Random random, out Transform start, out int checkpointCount)
        {
            var course = new GameObject("Course").transform;

            BuildSlide(tr, course, m);
            BuildEdges(tr, course, m, random);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(course, false);

            // Spiky things (buoys, spinners, pendulums) are deadly. Rocks and blocks only bump.
            stage.obstacles(obstacles, m, tr, random);
            foreach (var hazard in obstacles.GetComponentsInChildren<Hazard>())
            {
                foreach (var t in hazard.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Hazard.Layer;
            }

            int[] checkpointSegments = stage.checkpoints;
            for (int i = 0; i < checkpointSegments.Length; i++)
            {
                BuildCheckpoint(course, m, tr, tr.IndexAt(checkpointSegments[i], 0.04f), i);
            }
            checkpointCount = checkpointSegments.Length;

            BuildStartArch(course, m, tr, 12);
            BuildFinish(course, m, tr, tr.IndexAt(stage.finishSegment, 0.6f));

            start = new GameObject("StartPoint").transform;
            start.SetParent(course, false);
            start.position = tr.pos[6] + Vector3.up * 0.9f;
            start.rotation = Quaternion.LookRotation(tr.forward[6]);
            return course;
        }

        static void SunnyObstacles(Transform obstacles, Mats m, Track tr, System.Random random)
        {
            // Segment 3: pool with spiky buoys.
            Buoy(obstacles, m, tr, tr.IndexAt(3, 0.35f), -3.5f);
            Buoy(obstacles, m, tr, tr.IndexAt(3, 0.55f), 3.5f);
            Buoy(obstacles, m, tr, tr.IndexAt(3, 0.80f), -1f);

            // Segment 4: pool with a big four-arm spinner.
            CrossSpinner(obstacles, m, tr, tr.IndexAt(4, 0.5f), 4.5f, 3, 35f);

            // Segment 6: slide with swinging pendulums.
            PendulumArch(obstacles, m, tr, tr.IndexAt(6, 0.35f), 0f);
            PendulumArch(obstacles, m, tr, tr.IndexAt(6, 0.70f), 0.5f);

            // Segment 7: rocky canyon pool with rocks and sliding blocks.
            WaterRock(obstacles, m, tr, tr.IndexAt(7, 0.30f), 3f, random);
            WaterRock(obstacles, m, tr, tr.IndexAt(7, 0.55f), -3.5f, random);
            SlidingBlock(obstacles, m, tr, tr.IndexAt(7, 0.75f), 0f);

            // Segment 8: open pool with buoys and a sliding block.
            SlidingBlock(obstacles, m, tr, tr.IndexAt(8, 0.30f), 0.5f);
            Buoy(obstacles, m, tr, tr.IndexAt(8, 0.60f), 2.5f);
            Buoy(obstacles, m, tr, tr.IndexAt(8, 0.80f), -3f);

            // Segment 9: slide with two counter-rotating spinners.
            CrossSpinner(obstacles, m, tr, tr.IndexAt(9, 0.35f), 3.8f, 2, 50f);
            CrossSpinner(obstacles, m, tr, tr.IndexAt(9, 0.75f), 3.8f, 2, -50f);
        }

        static void JungleObstacles(Transform obstacles, Mats m, Track tr, System.Random random)
        {
            Buoy(obstacles, m, tr, tr.IndexAt(2, 0.50f), 2.5f);

            SlidingBlock(obstacles, m, tr, tr.IndexAt(3, 0.45f), 0f);
            SlidingBlock(obstacles, m, tr, tr.IndexAt(3, 0.85f), 0.5f);

            Buoy(obstacles, m, tr, tr.IndexAt(4, 0.30f), -3f);
            Buoy(obstacles, m, tr, tr.IndexAt(4, 0.50f), 3f);
            Buoy(obstacles, m, tr, tr.IndexAt(4, 0.75f), 0f);

            Buoy(obstacles, m, tr, tr.IndexAt(5, 0.50f), -2f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(7, 0.55f), 4.5f, 3, 35f);

            WaterRock(obstacles, m, tr, tr.IndexAt(8, 0.30f), -3.5f, random);
            SlidingBlock(obstacles, m, tr, tr.IndexAt(8, 0.60f), 0.25f);
            WaterRock(obstacles, m, tr, tr.IndexAt(8, 0.85f), 3.5f, random);

            PendulumArch(obstacles, m, tr, tr.IndexAt(9, 0.35f), 0f);
            PendulumArch(obstacles, m, tr, tr.IndexAt(9, 0.70f), 0.5f);

            Buoy(obstacles, m, tr, tr.IndexAt(11, 0.35f), 3f);
            Buoy(obstacles, m, tr, tr.IndexAt(11, 0.60f), -3f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(12, 0.50f), 3.8f, 2, 45f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(13, 0.35f), 3.8f, 2, 50f);
            CrossSpinner(obstacles, m, tr, tr.IndexAt(13, 0.75f), 3.8f, 2, -50f);

            Buoy(obstacles, m, tr, tr.IndexAt(14, 0.50f), -2f);
        }

        static void VolcanoObstacles(Transform obstacles, Mats m, Track tr, System.Random random)
        {
            PendulumArch(obstacles, m, tr, tr.IndexAt(1, 0.60f), 0f);

            Buoy(obstacles, m, tr, tr.IndexAt(2, 0.40f), -2.5f);
            Buoy(obstacles, m, tr, tr.IndexAt(2, 0.75f), 2f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(3, 0.60f), 4.2f, 3, 40f);

            SlidingBlock(obstacles, m, tr, tr.IndexAt(4, 0.35f), 0f);
            SlidingBlock(obstacles, m, tr, tr.IndexAt(4, 0.70f), 0.5f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(5, 0.50f), 3.4f, 2, 55f);

            PendulumArch(obstacles, m, tr, tr.IndexAt(6, 0.35f), 0f);
            PendulumArch(obstacles, m, tr, tr.IndexAt(6, 0.70f), 0.5f);

            Buoy(obstacles, m, tr, tr.IndexAt(7, 0.30f), 3f);
            WaterRock(obstacles, m, tr, tr.IndexAt(7, 0.55f), -3f, random);
            Buoy(obstacles, m, tr, tr.IndexAt(7, 0.80f), 2f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(8, 0.50f), 4.2f, 4, -35f);

            PendulumArch(obstacles, m, tr, tr.IndexAt(9, 0.30f), 0.25f);
            PendulumArch(obstacles, m, tr, tr.IndexAt(9, 0.65f), 0.75f);

            Buoy(obstacles, m, tr, tr.IndexAt(10, 0.50f), 1.5f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(11, 0.55f), 4.5f, 3, 40f);

            SlidingBlock(obstacles, m, tr, tr.IndexAt(12, 0.30f), 0f);
            Buoy(obstacles, m, tr, tr.IndexAt(12, 0.55f), -3f);
            SlidingBlock(obstacles, m, tr, tr.IndexAt(12, 0.80f), 0.5f);

            CrossSpinner(obstacles, m, tr, tr.IndexAt(13, 0.35f), 3.6f, 2, 55f);
            CrossSpinner(obstacles, m, tr, tr.IndexAt(13, 0.75f), 3.6f, 2, -55f);

            PendulumArch(obstacles, m, tr, tr.IndexAt(14, 0.50f), 0f);
        }

        static void BuildSlide(Track tr, Transform parent, Mats m)
        {
            int count = tr.Count;

            // Water surface: shared-vertex ribbon so the UVs can flow downstream.
            var vertices = new Vector3[count * 2];
            var uv = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            for (int k = 0; k < count; k++)
            {
                vertices[k * 2] = tr.Edge(k, -1f);
                vertices[k * 2 + 1] = tr.Edge(k, 1f);
                float v = tr.distance[k] / 8f;
                uv[k * 2] = new Vector2(0f, v);
                uv[k * 2 + 1] = new Vector2(1f, v);
            }
            for (int k = 0; k < count - 1; k++)
            {
                int a = k * 2, b = a + 1, c = a + 2, d = a + 3;
                int t = k * 6;
                triangles[t] = a;
                triangles[t + 1] = c;
                triangles[t + 2] = b;
                triangles[t + 3] = b;
                triangles[t + 4] = c;
                triangles[t + 5] = d;
            }
            var floorMesh = new Mesh { name = "SlideWater", vertices = vertices, uv = uv, triangles = triangles };
            floorMesh.RecalculateNormals();
            floorMesh.RecalculateBounds();
            SaveMesh(floorMesh);

            var water = MeshObject(floorMesh, "SlideWater", parent, Vector3.zero, Vector3.one,
                theme.waterMaterial != null ? theme.waterMaterial : m.slideWater);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var floorCollider = water.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = floorMesh;
            SetSlippery(floorCollider);
            water.AddComponent<UVScroll>().speed = new Vector2(0f, -0.45f);

            // Slide body underneath (visible when you look over an open edge).
            var sideVerts = new List<Vector3>();
            var sideTris = new List<int>();
            Vector3 down = Vector3.down * 1.4f;
            for (int k = 0; k < count - 1; k++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = tr.Edge(k, s), b = tr.Edge(k + 1, s);
                    AddQuad(sideVerts, sideTris, a, b, b + down, a + down, tr.right[k] * s);
                }
                Vector3 l0 = tr.Edge(k, -1f) + down, r0 = tr.Edge(k, 1f) + down;
                Vector3 l1 = tr.Edge(k + 1, -1f) + down, r1 = tr.Edge(k + 1, 1f) + down;
                AddQuad(sideVerts, sideTris, l0, r0, r1, l1, Vector3.down);
            }
            int last = count - 1;
            AddQuad(sideVerts, sideTris, tr.Edge(0, -1f), tr.Edge(0, 1f), tr.Edge(0, 1f) + down, tr.Edge(0, -1f) + down, -tr.forward[0]);
            AddQuad(sideVerts, sideTris, tr.Edge(last, -1f), tr.Edge(last, 1f), tr.Edge(last, 1f) + down, tr.Edge(last, -1f) + down, tr.forward[last]);
            MeshObject(BuildFlatMesh("SlideBody", sideVerts, sideTris), "SlideBody", parent, Vector3.zero, Vector3.one, m.slideBody);

            // White foam lines along both edges.
            var foamVerts = new List<Vector3>();
            var foamTris = new List<int>();
            Vector3 lift = Vector3.up * 0.03f;
            for (int k = 0; k < count - 1; k++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = tr.Edge(k, s) + lift, b = tr.Edge(k + 1, s) + lift;
                    Vector3 ai = a - tr.right[k] * (s * 0.6f), bi = b - tr.right[k + 1] * (s * 0.6f);
                    AddQuad(foamVerts, foamTris, a, b, bi, ai, Vector3.up);
                }
            }
            var foam = MeshObject(BuildFlatMesh("SlideFoam", foamVerts, foamTris), "SlideFoam", parent, Vector3.zero, Vector3.one, m.foam);
            foam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildEdges(Track tr, Transform parent, Mats m, System.Random random)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(parent, false);

            for (int s = -1; s <= 1; s += 2)
            {
                string edges = s < 0 ? stage.left : stage.right;
                for (int k = 0; k < tr.Count - 1; k++)
                {
                    Edge edge = EdgeAt(edges, tr.segment[k]);
                    if (edge == Edge.Bumper && k % 3 == 0)
                    {
                        BumperPiece(walls, m, tr.Edge(k, s), tr.Edge(Mathf.Min(k + 3, tr.Count - 1), s), tr.right[k] * s);
                    }
                    else if (edge == Edge.Rock && k % 2 == 0)
                    {
                        Vector3 pos = tr.Edge(k, s) + tr.right[k] * (s * (0.9f + R(random) * 0.5f)) + Vector3.up * 0.2f;
                        WallRock(walls, m, pos, 2.2f + R(random) * 1.6f, random);
                    }
                }
            }

            // Close the start and the end so the raft cannot slide off backwards or past the finish.
            int last = tr.Count - 1;
            RockRow(walls, m, tr.Edge(0, -1f) - tr.forward[0], tr.Edge(0, 1f) - tr.forward[0], random);
            RockRow(walls, m, tr.Edge(last, -1f) + tr.forward[last], tr.Edge(last, 1f) + tr.forward[last], random);
        }

        static void RockRow(Transform parent, Mats m, Vector3 a, Vector3 b, System.Random random)
        {
            float length = Vector3.Distance(a, b);
            for (float d = 0f; d <= length; d += 2f)
            {
                WallRock(parent, m, Vector3.Lerp(a, b, d / length) + Vector3.up * 0.2f, 2.4f + R(random) * 1.2f, random);
            }
        }

        static void BumperPiece(Transform parent, Mats m, Vector3 a, Vector3 b, Vector3 outward)
        {
            Vector3 d = b - a;
            Vector3 offset = outward * 0.35f + Vector3.up * 0.45f;
            var go = Prim(PrimitiveType.Cylinder, "Bumper", parent, (a + b) * 0.5f + offset,
                new Vector3(0.9f, d.magnitude * 0.5f + 0.15f, 0.9f), m.bumper, true);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.AddComponent<Bumper>().bounce = 4f;

            for (int j = 0; j < 2; j++)
            {
                Vector3 p = Vector3.Lerp(a, b, 0.25f + 0.5f * j) + offset;
                Spike(parent, m, p - outward * 0.35f, -outward, 0.28f, 0.5f);
                Spike(parent, m, p + Vector3.up * 0.35f, Vector3.up, 0.24f, 0.4f);
            }
        }

        static void Spike(Transform parent, Mats m, Vector3 worldPos, Vector3 direction, float width, float length)
        {
            var spike = MeshObject(ConeMesh(), "Spike", null, worldPos, new Vector3(width, length, width), m.spike);
            spike.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            spike.transform.SetParent(parent, true);
        }

        static void WallRock(Transform parent, Mats m, Vector3 pos, float size, System.Random random)
        {
            var mesh = RockMesh(random.Next(3));
            var rock = MeshObject(mesh, "Rock", parent, pos,
                new Vector3(size * (0.9f + R(random) * 0.3f), size * (0.7f + R(random) * 0.5f), size * (0.9f + R(random) * 0.3f)),
                R(random) < 0.5f ? m.rock : m.rock2);
            rock.transform.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
            var collider = rock.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            rock.AddComponent<Bumper>().bounce = 3f;
        }

        // ================================================================ Obstacles

        static void Buoy(Transform parent, Mats m, Track tr, int k, float sideOffset)
        {
            Vector3 pos = tr.Point(k, sideOffset);
            var buoy = Prim(PrimitiveType.Sphere, "Buoy", parent, pos + Vector3.up * 0.45f, Vector3.one * 1.7f, m.buoy, true);
            buoy.AddComponent<Hazard>();
            Prim(PrimitiveType.Cylinder, "Band", buoy.transform, Vector3.zero, new Vector3(1.04f, 0.08f, 1.04f), m.white, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a)).normalized;
                Spike(buoy.transform, m, buoy.transform.position + dir * 0.8f, dir, 0.3f, 0.5f);
            }
            Spike(buoy.transform, m, buoy.transform.position + Vector3.up * 0.82f, Vector3.up, 0.3f, 0.5f);
        }

        static void CrossSpinner(Transform parent, Mats m, Track tr, int k, float armLength, int arms, float speed)
        {
            Vector3 pos = tr.pos[k];
            Prim(PrimitiveType.Cylinder, "Hub", parent, pos + Vector3.up * 0.6f, new Vector3(1.4f, 0.6f, 1.4f), m.metal, true);

            var pivot = new GameObject("Spinner");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = pos;
            pivot.AddComponent<Rigidbody>().isKinematic = true;
            pivot.AddComponent<Spinner>().degreesPerSecond = speed;
            pivot.AddComponent<Hazard>();

            for (int i = 0; i < arms; i++)
            {
                var arm = new GameObject("Arm").transform;
                arm.SetParent(pivot.transform, false);
                arm.localRotation = Quaternion.Euler(0f, i * 360f / arms, 0f);
                Prim(PrimitiveType.Cube, "Beam", arm, new Vector3(0f, 0.55f, armLength * 0.5f + 0.5f), new Vector3(0.6f, 0.6f, armLength), m.magenta, true);
                for (int j = 1; j <= 3; j++)
                {
                    float z = 0.5f + armLength * j / 4f;
                    var spikeA = MeshObject(ConeMesh(), "Spike", arm, new Vector3(0.35f, 0.55f, z), new Vector3(0.25f, 0.45f, 0.25f), m.spike);
                    spikeA.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    var spikeB = MeshObject(ConeMesh(), "Spike", arm, new Vector3(-0.35f, 0.55f, z), new Vector3(0.25f, 0.45f, 0.25f), m.spike);
                    spikeB.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        static void WaterRock(Transform parent, Mats m, Track tr, int k, float sideOffset, System.Random random)
        {
            WallRock(parent, m, tr.Point(k, sideOffset) + Vector3.up * 0.1f, 2.4f, random);
            parent.GetChild(parent.childCount - 1).GetComponent<Bumper>().bounce = 4.5f;
        }

        static void PendulumArch(Transform parent, Mats m, Track tr, int k, float phase)
        {
            Vector3 pos = tr.pos[k];
            Quaternion rotation = Quaternion.LookRotation(tr.forward[k]);
            float half = tr.width[k] * 0.5f + 0.6f;
            const float height = 6.5f;

            var arch = new GameObject("PendulumArch").transform;
            arch.SetParent(parent, false);
            arch.SetPositionAndRotation(pos, rotation);
            Prim(PrimitiveType.Cylinder, "Post", arch, new Vector3(-half, height * 0.5f, 0f), new Vector3(0.5f, height * 0.5f, 0.5f), m.wood, false);
            Prim(PrimitiveType.Cylinder, "Post", arch, new Vector3(half, height * 0.5f, 0f), new Vector3(0.5f, height * 0.5f, 0.5f), m.wood, false);
            var beam = Prim(PrimitiveType.Cylinder, "Beam", arch, new Vector3(0f, height, 0f), new Vector3(0.45f, half + 0.3f, 0.45f), m.wood, false);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var pivot = new GameObject("Pendulum");
            pivot.transform.SetParent(arch, false);
            pivot.transform.localPosition = new Vector3(0f, height, 0f);
            pivot.AddComponent<Rigidbody>().isKinematic = true;
            var pendulum = pivot.AddComponent<Pendulum>();
            pendulum.phase = phase;
            pivot.AddComponent<Hazard>();

            const float armLength = 5.6f;
            Prim(PrimitiveType.Cylinder, "Rope", pivot.transform, new Vector3(0f, -armLength * 0.5f, 0f), new Vector3(0.12f, armLength * 0.5f, 0.12f), m.darkWood, false);
            var block = Prim(PrimitiveType.Cube, "Block", pivot.transform, new Vector3(0f, -armLength - 0.1f, 0f), new Vector3(1.8f, 1.6f, 1.8f), m.magenta, true);
            for (int s = -1; s <= 1; s += 2)
            {
                var spike = MeshObject(ConeMesh(), "Spike", block.transform, new Vector3(0.5f * s, 0f, 0f), new Vector3(0.3f, 0.4f, 0.3f), m.spike);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * s);
            }
        }

        static void SlidingBlock(Transform parent, Mats m, Track tr, int k, float phase)
        {
            float travel = tr.width[k] - 5f;
            Vector3 start = tr.Point(k, -travel * 0.5f) + Vector3.up * 0.6f;
            var block = Prim(PrimitiveType.Cube, "SlidingBlock", parent, start, new Vector3(3f, 1.3f, 1.3f), m.wood, true);
            block.transform.rotation = Quaternion.LookRotation(tr.forward[k]);
            Prim(PrimitiveType.Cube, "Stripe", block.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.02f, 0.25f, 1.02f), m.signYellow, false);
            block.AddComponent<Rigidbody>().isKinematic = true;
            var mover = block.AddComponent<Mover>();
            mover.offset = tr.right[k] * travel;
            mover.period = 3.2f;
            mover.phase = phase;
            block.AddComponent<Bumper>().bounce = 6f;
        }

        static void BuildCheckpoint(Transform parent, Mats m, Track tr, int k, int index)
        {
            var go = new GameObject("Checkpoint" + (index + 1));
            go.transform.SetParent(parent, false);
            go.transform.position = tr.pos[k] + Vector3.up * 0.9f;
            go.transform.rotation = Quaternion.LookRotation(tr.forward[k]);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(tr.width[k] + 6f, 6f, 2f);
            go.AddComponent<Checkpoint>().index = index;

            float half = tr.width[k] * 0.5f + 0.8f;
            Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(-half, 2.5f, 0f), new Vector3(0.3f, 3.2f, 0.3f), m.bamboo, false);
            Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(half, 2.5f, 0f), new Vector3(0.3f, 3.2f, 0.3f), m.bamboo, false);
            var board = Prim(PrimitiveType.Cube, "Sign", go.transform, new Vector3(-half + 2.6f, 5.2f, 0f), new Vector3(5f, 1.4f, 0.3f), m.signBlue, false);
            Text3D(board.transform.parent, "CHECKPOINT", board.transform.localPosition + new Vector3(0f, 0f, -0.2f), 0.06f, new Color(1f, 0.85f, 0.2f));
            Prim(PrimitiveType.Cube, "Flag", go.transform, new Vector3(half - 0.6f, 5.4f, 0f), new Vector3(1.2f, 0.8f, 0.06f), m.signYellow, false);
        }

        static void BuildStartArch(Transform parent, Mats m, Track tr, int k)
        {
            var arch = new GameObject("StartArch").transform;
            arch.SetParent(parent, false);
            arch.SetPositionAndRotation(tr.pos[k], Quaternion.LookRotation(tr.forward[k]));
            float half = tr.width[k] * 0.5f + 0.8f;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cylinder, "Bamboo", arch, new Vector3(half * s, 3.5f, 0f), new Vector3(0.55f, 3.5f, 0.55f), m.bamboo, false);
                for (int j = 0; j < 3; j++)
                {
                    Prim(PrimitiveType.Cylinder, "Ring", arch, new Vector3(half * s, 1.5f + j * 2f, 0f), new Vector3(0.62f, 0.06f, 0.62f), m.darkWood, false);
                }
                Prim(PrimitiveType.Sphere, "Leaves", arch, new Vector3(half * s, 7.3f, 0f), new Vector3(1.8f, 0.7f, 1.8f), m.leaves, false);
            }
            var bar = Prim(PrimitiveType.Cylinder, "Bar", arch, new Vector3(0f, 6.6f, 0f), new Vector3(0.45f, half, 0.45f), m.bamboo, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "Sign", arch, new Vector3(0f, 5.4f, 0f), new Vector3(5.2f, 1.8f, 0.3f), m.signBlue, false);
            Text3D(arch, "START", new Vector3(0f, 5.4f, -0.2f), 0.1f, new Color(1f, 0.85f, 0.2f));
        }

        static void BuildFinish(Transform parent, Mats m, Track tr, int k)
        {
            var go = new GameObject("FinishLine");
            go.transform.SetParent(parent, false);
            go.transform.position = tr.pos[k] + Vector3.up * 0.9f;
            go.transform.rotation = Quaternion.LookRotation(tr.forward[k]);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(tr.width[k] + 6f, 6f, 2f);
            go.AddComponent<FinishLine>();

            float half = tr.width[k] * 0.5f + 0.8f;
            Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(-half, 2.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f), m.white, false);
            Prim(PrimitiveType.Cylinder, "Pole", go.transform, new Vector3(half, 2.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f), m.white, false);
            int tiles = 14;
            float tile = half * 2f / tiles;
            for (int i = 0; i < tiles; i++)
            {
                for (int row = 0; row < 2; row++)
                {
                    Prim(PrimitiveType.Cube, "Checker", go.transform, new Vector3(-half + tile * (i + 0.5f), 5.3f + row * tile, 0f),
                        new Vector3(tile, tile, 0.2f), (i + row) % 2 == 0 ? m.white : m.black, false);
                }
            }
            Text3D(go.transform, "FINISH", new Vector3(0f, 6.9f, -0.2f), 0.1f, new Color(1f, 0.85f, 0.2f));
        }

        static void Text3D(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = uiFont;
            mesh.fontSize = 96;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = uiFont.material;
        }

        // ================================================================ Environment

        static void SetupLighting()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            light.color = stage.sun;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var sky = LoadOrCreateMaterial(stagePrefix + "Sky", Shader.Find("Skybox/Procedural"));
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", stage.skyTint);
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", stage.skyGround);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.6f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.05f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.03f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = stage.ambient;
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.82f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.55f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = stage.fog;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;
        }

        static void BuildEnvironment(Track tr, Transform parent, Mats m, System.Random random)
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(parent, false);
            Material[] corals = { m.coral, m.coral2, m.coral3 };

            // Cliff pillars beside the slide. Kept clear of the edges so a falling raft never lands on one.
            for (int k = 0; k < tr.Count; k += 20)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    if (R(random) > 0.75f) continue;
                    float radius = 3f + R(random) * 5f;
                    float offset = tr.width[k] * 0.5f + 4f + radius + R(random) * 18f;
                    Vector3 p = tr.Point(k, offset * s);
                    Pillar(env, m, p, tr.pos[k].y - 1.5f - R(random) * 7f, radius, corals[random.Next(corals.Length)], true, random);
                }
            }

            // Supports under the slide.
            for (int k = 15; k < tr.Count; k += 40)
            {
                Pillar(env, m, tr.pos[k], tr.pos[k].y - 1.3f, 2.2f + R(random), corals[random.Next(corals.Length)], false, random);
            }

            // Far mesas.
            for (int i = 0; i < 26; i++)
            {
                int k = random.Next(tr.Count);
                float side = R(random) < 0.5f ? -1f : 1f;
                Vector3 p = tr.Point(k, side * (55f + R(random) * 70f));
                var mesa = Pillar(env, m, p, tr.pos[k].y - 6f + R(random) * 20f, 9f + R(random) * 16f, corals[random.Next(corals.Length)], true, random);
                NoShadows(mesa);
            }

            // Sea of clouds below and a few clouds in the sky.
            for (int i = 0; i < 45; i++)
            {
                int k = random.Next(tr.Count);
                Vector3 p = tr.Point(k, (R(random) - 0.5f) * 180f);
                p.y = R(random) * 6f - 2f;
                var cloud = Prim(PrimitiveType.Sphere, "CloudSea", env, p,
                    new Vector3(22f + R(random) * 18f, 5f + R(random) * 4f, 18f + R(random) * 14f), m.cloud, false);
                NoShadows(cloud);
            }
            for (int i = 0; i < 10; i++)
            {
                int k = random.Next(tr.Count);
                Vector3 p = tr.Point(k, (R(random) - 0.5f) * 140f);
                p.y = 55f + R(random) * 15f;
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(env, false);
                cloud.position = p;
                int puffs = 3 + random.Next(3);
                for (int j = 0; j < puffs; j++)
                {
                    Prim(PrimitiveType.Sphere, "Puff", cloud, new Vector3((j - puffs * 0.5f) * 5f, R(random) * 2f, R(random) * 4f),
                        new Vector3(9f + R(random) * 8f, 4f + R(random) * 3f, 7f + R(random) * 5f), m.cloud, false);
                }
                NoShadows(cloud.gameObject);
            }
        }

        static GameObject Pillar(Transform parent, Mats m, Vector3 center, float topY, float radius, Material coral, bool decorate, System.Random random)
        {
            const float depth = 80f;
            var pillar = new GameObject("Pillar").transform;
            pillar.SetParent(parent, false);
            pillar.position = new Vector3(center.x, topY, center.z);
            pillar.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);

            MeshObject(PillarMesh(), "Cliff", pillar, new Vector3(0f, -depth, 0f), new Vector3(radius * 2f, depth, radius * 2f), coral);
            MeshObject(PillarMesh(), "Ledge", pillar, new Vector3(0f, -3.5f, 0f), new Vector3(radius * 2.08f, 0.9f, radius * 2.08f), m.coral2);
            MeshObject(PillarMesh(), "Grass", pillar, new Vector3(0f, -0.3f, 0f), new Vector3(radius * 2.1f, 0.6f, radius * 2.1f),
                theme.grassMaterial != null ? theme.grassMaterial : m.grass);

            if (decorate)
            {
                int items = 1 + random.Next(3);
                for (int i = 0; i < items; i++)
                {
                    float a = R(random) * Mathf.PI * 2f;
                    float r = R(random) * radius * 0.6f;
                    Vector3 p = pillar.position + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r);
                    float kind = R(random);
                    if (kind < 0.5f)
                    {
                        if (!TrySpawn(theme.trees, pillar, p, random)) Tree(pillar, m, p, random);
                    }
                    else if (kind < 0.65f)
                    {
                        if (!TrySpawn(theme.flowers, pillar, p, random)) Mushroom(pillar, m, p, random);
                    }
                    else if (kind < 0.85f)
                    {
                        if (!TrySpawn(theme.bushes, pillar, p, random)) Bush(pillar, m, p, random);
                    }
                    else if (!TrySpawn(theme.rocks, pillar, p, random))
                    {
                        var rock = MeshObject(RockMesh(random.Next(3)), "Rock", pillar, Vector3.zero, Vector3.one * (1.2f + R(random) * 1.5f), m.rock);
                        rock.transform.position = p;
                    }
                }
            }
            return pillar.gameObject;
        }

        static void Tree(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, true);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
            tree.localScale = Vector3.one * (0.9f + R(random) * 0.8f);

            if (R(random) < 0.35f)
            {
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.6f, 0f), new Vector3(0.35f, 0.6f, 0.35f), m.trunk, false);
                for (int i = 0; i < 3; i++)
                {
                    float w = 2.8f - i * 0.65f;
                    MeshObject(ConeMesh(), "PineTier", tree, new Vector3(0f, 0.9f + i * 1.05f, 0f), new Vector3(w, 1.9f, w), m.pine);
                }
            }
            else
            {
                Material leaves = R(random) < 0.5f ? m.leaves : m.leaves2;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.3f, 0f), new Vector3(0.35f, 1.3f, 0.35f), m.trunk, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 3.2f, 0f), new Vector3(2.8f, 1.6f, 2.8f), leaves, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0.7f, 2.7f, 0.4f), new Vector3(1.9f, 1.1f, 1.9f), leaves, false);
                Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(-0.6f, 2.8f, -0.5f), new Vector3(2f, 1.2f, 2f), leaves, false);
            }
        }

        static void Mushroom(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var shroom = new GameObject("Mushroom").transform;
            shroom.SetParent(parent, true);
            shroom.position = pos;
            shroom.localScale = Vector3.one * (0.8f + R(random) * 0.8f);
            Prim(PrimitiveType.Cylinder, "Stem", shroom, new Vector3(0f, 0.6f, 0f), new Vector3(0.45f, 0.6f, 0.45f), m.mushroomStem, false);
            Prim(PrimitiveType.Sphere, "Cap", shroom, new Vector3(0f, 1.3f, 0f), new Vector3(1.7f, 0.9f, 1.7f), m.mushroomCap, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Sphere, "Dot", shroom, new Vector3(Mathf.Cos(a) * 0.5f, 1.62f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.25f, m.white, false);
            }
        }

        static void Bush(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            var bush = new GameObject("Bush").transform;
            bush.SetParent(parent, true);
            bush.position = pos;
            int count = 2 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float s = 0.9f + R(random) * 0.6f;
                Prim(PrimitiveType.Sphere, "Leaf", bush, new Vector3((R(random) - 0.5f) * 1.4f, s * 0.35f, (R(random) - 0.5f) * 1.4f),
                    new Vector3(s, s * 0.8f, s), m.bush, false);
            }
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

        // ================================================================ Raft and characters

        const float RaftScale = 0.72f;

        static Raft BuildRaft(Mats m, Transform start, out FartSeat[] seats)
        {
            var raftGo = new GameObject("Raft");
            raftGo.transform.SetPositionAndRotation(start.position, start.rotation);
            var body = raftGo.AddComponent<Rigidbody>();
            body.mass = 40f;
            var collider = raftGo.AddComponent<MeshCollider>();
            collider.sharedMesh = CylinderMesh("RaftCollider", 2.55f * RaftScale, 0.9f, 16, true);
            collider.convex = true;
            SetSlippery(collider);
            var raft = raftGo.AddComponent<Raft>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(raftGo.transform, false);
            raft.visual = visual;

            // Only the boat is scaled; the animals keep their size.
            var boat = new GameObject("Boat").transform;
            boat.SetParent(visual, false);
            boat.localScale = new Vector3(RaftScale, 0.85f, RaftScale);

            var ring = TorusMesh("RaftRing", 2.1f, 0.45f, 28, 10);
            MeshObject(ring, "Tube", boat, Vector3.zero, Vector3.one, m.raftTube);
            MeshObject(ring, "TubeBottom", boat, new Vector3(0f, -0.2f, 0f), new Vector3(1.01f, 0.6f, 1.01f), m.raftBand);
            MeshObject(CylinderMesh("RaftFloor", 1.8f, 0.14f, 20, false), "Floor", boat, new Vector3(0f, 0.02f, 0f), Vector3.one, m.raftFloor);
            for (float x = -1.35f; x <= 1.36f; x += 0.45f)
            {
                float chord = 2f * Mathf.Sqrt(Mathf.Max(0f, 1.75f * 1.75f - x * x));
                Prim(PrimitiveType.Cube, "PlankLine", boat, new Vector3(x, 0.1f, 0f), new Vector3(0.04f, 0.02f, chord), m.darkWood, false);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var strap = Prim(PrimitiveType.Cube, "Strap", boat, new Vector3(Mathf.Sin(a) * 2.1f, 0f, Mathf.Cos(a) * 2.1f),
                    new Vector3(0.28f, 0.98f, 1.0f), m.strap, false);
                strap.transform.localRotation = Quaternion.Euler(0f, (45f + i * 90f), 0f);
                var handle = MeshObject(TorusMesh("Handle", 0.22f, 0.05f, 12, 6), "Handle", boat,
                    new Vector3(Mathf.Sin(a) * 2.62f, 0.05f, Mathf.Cos(a) * 2.62f), Vector3.one, m.strap);
                handle.transform.localRotation = Quaternion.Euler(0f, (45f + i * 90f), 90f);
            }

            raft.wake = BuildWake(raftGo.transform, m.wakeRing);

            seats = new FartSeat[4];
            float[] defaultAngles = { 225f, 135f, 315f, 45f };
            for (int i = 0; i < 4; i++)
            {
                seats[i] = BuildSeat(m, visual, raft, i);
                seats[i].rimRadius = 2.1f * RaftScale;
                seats[i].minGap = 40f;
                seats[i].SetAngle(defaultAngles[i]);
            }
            return raft;
        }

        static FartSeat BuildSeat(Mats m, Transform visual, Raft raft, int index)
        {
            var seatGo = new GameObject("Player" + (index + 1) + "_" + AnimalNames[index]);
            seatGo.transform.SetParent(visual, false);

            var character = new GameObject("Character").transform;
            character.SetParent(seatGo.transform, false);
            BuildAnimal(character, m, index, out Transform leftArm, out Transform rightArm);

            var puffGo = new GameObject("FartParticles");
            puffGo.transform.SetParent(character, false);
            puffGo.transform.localPosition = new Vector3(0f, 0.4f, -0.45f);
            puffGo.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);
            var particles = BuildFartParticles(puffGo, m.puff);

            var audio = seatGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var seat = seatGo.AddComponent<FartSeat>();
            seat.playerIndex = index;
            seat.raft = raft;
            seat.character = character;
            seat.leftArm = leftArm;
            seat.rightArm = rightArm;
            seat.fartParticles = particles;
            seat.fartAudio = audio;
            seat.fartClips = FartClips();
            return seat;
        }

        // White rings left on the water behind the raft.
        static ParticleSystem BuildWake(Transform raft, Material material)
        {
            var go = new GameObject("Wake");
            go.transform.SetParent(raft, false);
            go.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = 3.2f * RaftScale;
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0.45f;

            var shape = ps.shape;
            shape.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.9f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            return ps;
        }

        // Chunky pill-shaped animals with googly eyes. They face +Z (the raft center).
        static void BuildAnimal(Transform root, Mats m, int index, out Transform leftArm, out Transform rightArm)
        {
            Material main = m.animalMain[index];
            Material light = m.animalLight[index];
            bool gorilla = index == 3;

            Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.68f, 0f), gorilla ? new Vector3(0.95f, 0.62f, 0.82f) : new Vector3(0.85f, 0.62f, 0.78f), main, false);
            Prim(PrimitiveType.Sphere, "Belly", root, new Vector3(0f, 0.62f, 0.26f), new Vector3(0.6f, 0.72f, 0.3f), light, false);
            Prim(PrimitiveType.Sphere, "ButtL", root, new Vector3(-0.17f, 0.42f, -0.3f), Vector3.one * 0.42f, main, false);
            Prim(PrimitiveType.Sphere, "ButtR", root, new Vector3(0.17f, 0.42f, -0.3f), Vector3.one * 0.42f, main, false);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cylinder, "Leg", root, new Vector3(0.2f * s, 0.08f, 0.02f), new Vector3(0.24f, 0.1f, 0.24f), main, false);
            }

            leftArm = ArmPivot(root, main, -1f, gorilla);
            rightArm = ArmPivot(root, main, 1f, gorilla);

            switch (index)
            {
                case 0: // capybara
                    Prim(PrimitiveType.Sphere, "Snout", root, new Vector3(0f, 0.98f, 0.36f), new Vector3(0.46f, 0.32f, 0.36f), light, false);
                    Prim(PrimitiveType.Sphere, "Nose", root, new Vector3(0f, 1.04f, 0.53f), new Vector3(0.16f, 0.09f, 0.08f), m.black, false);
                    Prim(PrimitiveType.Sphere, "EarL", root, new Vector3(-0.24f, 1.3f, 0.02f), Vector3.one * 0.15f, light, false);
                    Prim(PrimitiveType.Sphere, "EarR", root, new Vector3(0.24f, 1.3f, 0.02f), Vector3.one * 0.15f, light, false);
                    break;
                case 1: // hippo
                    Prim(PrimitiveType.Sphere, "Snout", root, new Vector3(0f, 0.92f, 0.36f), new Vector3(0.66f, 0.4f, 0.44f), light, false);
                    Prim(PrimitiveType.Sphere, "NostrilL", root, new Vector3(-0.12f, 1.05f, 0.56f), Vector3.one * 0.07f, m.black, false);
                    Prim(PrimitiveType.Sphere, "NostrilR", root, new Vector3(0.12f, 1.05f, 0.56f), Vector3.one * 0.07f, m.black, false);
                    Prim(PrimitiveType.Sphere, "EarL", root, new Vector3(-0.27f, 1.32f, 0f), Vector3.one * 0.12f, main, false);
                    Prim(PrimitiveType.Sphere, "EarR", root, new Vector3(0.27f, 1.32f, 0f), Vector3.one * 0.12f, main, false);
                    break;
                case 2: // crocodile
                    Prim(PrimitiveType.Sphere, "Snout", root, new Vector3(0f, 0.98f, 0.5f), new Vector3(0.4f, 0.24f, 0.78f), light, false);
                    Prim(PrimitiveType.Sphere, "NostrilL", root, new Vector3(-0.08f, 1.08f, 0.85f), Vector3.one * 0.06f, m.black, false);
                    Prim(PrimitiveType.Sphere, "NostrilR", root, new Vector3(0.08f, 1.08f, 0.85f), Vector3.one * 0.06f, m.black, false);
                    for (int i = 0; i < 3; i++)
                    {
                        var bump = MeshObject(ConeMesh(), "Bump", root, new Vector3(0f, 1.15f - i * 0.28f, -0.36f), new Vector3(0.18f, 0.2f, 0.18f), light);
                        bump.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                    }
                    break;
                default: // gorilla
                    Prim(PrimitiveType.Sphere, "Face", root, new Vector3(0f, 0.98f, 0.32f), new Vector3(0.52f, 0.46f, 0.26f), light, false);
                    Prim(PrimitiveType.Cube, "Brow", root, new Vector3(0f, 1.24f, 0.36f), new Vector3(0.52f, 0.1f, 0.14f), main, false);
                    Prim(PrimitiveType.Sphere, "Nose", root, new Vector3(0f, 1.0f, 0.46f), new Vector3(0.16f, 0.08f, 0.06f), m.black, false);
                    break;
            }

            float eyeY = index == 1 ? 1.2f : 1.16f;
            float eyeZ = index == 2 ? 0.26f : 0.3f;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Sphere, "Eye", root, new Vector3(0.16f * s, eyeY, eyeZ), Vector3.one * 0.32f, m.eyeWhite, false);
                Prim(PrimitiveType.Sphere, "Pupil", root, new Vector3(0.16f * s + 0.03f * s * (index % 2 == 0 ? 1f : -1f), eyeY + 0.02f * s, eyeZ + 0.15f),
                    Vector3.one * 0.12f, m.black, false);
            }
        }

        static Transform ArmPivot(Transform root, Material mat, float side, bool gorilla)
        {
            var pivot = new GameObject(side < 0f ? "ArmL" : "ArmR").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3((gorilla ? 0.5f : 0.44f) * side, 0.88f, 0.05f);
            Prim(PrimitiveType.Capsule, "Arm", pivot, new Vector3(0.04f * side, -0.26f, 0.06f),
                gorilla ? new Vector3(0.24f, 0.32f, 0.24f) : new Vector3(0.18f, 0.27f, 0.18f), mat, false);
            return pivot;
        }

        static ParticleSystem BuildFartParticles(GameObject go, Material material)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            // A thick jet: fast, dense particles that slow down and puff up into a cloud.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.95f, 0.1f, 0.95f), new Color(0.8f, 1f, 0.3f, 0.85f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 34f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.12f;

            var velocity = ps.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.limit = 0.8f;
            velocity.dampen = 0.08f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 1f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        static ParticleSystem BuildImpactEffect(Mats m)
        {
            var go = new GameObject("ImpactEffect");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.3f), new Color(1f, 0.6f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m.star;
            return ps;
        }

        static CameraFollow BuildCamera(Transform target, Vector3[] path)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 58f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = target;
            follow.path = path;
            follow.Snap();
            return follow;
        }

        // ================================================================ UI

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
            Vector2 top = new Vector2(0.5f, 1f);

            // HUD
            var hud = UIObject("HUD", root);
            Stretch(hud);
            manager.hudPanel = hud.gameObject;

            for (int i = 0; i < seats.Length; i++)
            {
                var tag = Label(hud, "NameTag_P" + (i + 1), AnimalNames[i], 30, center, Vector2.zero, new Vector2(240f, 50f));
                tag.color = Color.Lerp(PlayerColors[i], Color.white, 0.35f);
                tag.gameObject.AddComponent<WorldLabel>().target = seats[i].transform;
            }

            var barBack = UIObject("ProgressBar", hud);
            Place(barBack, top, new Vector2(0f, -22f), new Vector2(1500f, 22f));
            var barImage = barBack.gameObject.AddComponent<Image>();
            barImage.sprite = roundedSprite;
            barImage.type = Image.Type.Sliced;
            barImage.color = new Color(1f, 1f, 1f, 0.85f);
            var fillRt = UIObject("Fill", barBack);
            Stretch(fillRt);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = roundedSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(0.45f, 0.85f, 0.25f);
            manager.progressFill = fill;

            var timerBack = UIObject("TimerBack", hud);
            Place(timerBack, top, new Vector2(0f, -78f), new Vector2(300f, 70f));
            var timerImage = timerBack.gameObject.AddComponent<Image>();
            timerImage.sprite = roundedSprite;
            timerImage.type = Image.Type.Sliced;
            timerImage.color = new Color(0.25f, 0.3f, 0.38f, 0.75f);
            manager.timerText = Label(timerBack, "Timer", "0:00.00", 48, center, Vector2.zero, new Vector2(300f, 70f));

            var deathsBack = UIObject("DeathsBack", hud);
            Place(deathsBack, new Vector2(1f, 1f), new Vector2(-160f, -78f), new Vector2(240f, 64f));
            var deathsImage = deathsBack.gameObject.AddComponent<Image>();
            deathsImage.sprite = roundedSprite;
            deathsImage.type = Image.Type.Sliced;
            deathsImage.color = new Color(0.25f, 0.3f, 0.38f, 0.75f);
            manager.deathsText = Label(deathsBack, "Deaths", "DEATHS 0", 36, center, Vector2.zero, new Vector2(240f, 64f));

            manager.hudMenuButton = MakeButton(hud, "MenuButton", "MENU", top, new Vector2(280f, -78f), new Vector2(170f, 64f), new Color(0.25f, 0.3f, 0.38f, 0.75f), 32);
            manager.syncText = Label(hud, "SyncBoost", "SYNC BOOST!", 60, top, new Vector2(0f, -160f), new Vector2(800f, 90f));
            manager.syncText.color = new Color(0.7f, 1f, 0.2f);
            manager.messageText = Label(hud, "Message", "", 110, center, new Vector2(0f, 140f), new Vector2(1400f, 160f));
            manager.hintText = Label(hud, "Hint", "Push your joystick where the raft should go - your animal runs to the other side and farts!",
                28, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(1100f, 60f));

            Vector2[] anchors = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            var joysticks = new FartJoystick[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = anchors[i];
                Vector2 pos = new Vector2(a.x < 0.5f ? 240f : -240f, a.y < 0.5f ? 230f : -330f);
                joysticks[i] = Joystick(hud, i, a, pos, seats[i]);
            }
            manager.joysticks = joysticks;

            // Menu
            var menu = Panel(root, "Menu", new Color(0.1f, 0.25f, 0.45f, 0.82f));
            manager.menuPanel = menu.gameObject;
            var title = Label(menu, "Title", "FART RAFT", 160, center, new Vector2(0f, 300f), new Vector2(1400f, 210f));
            title.color = new Color(0.75f, 1f, 0.25f);
            Label(menu, "Subtitle", "Share one raft. Fart together. Don't fall off!", 46, center, new Vector2(0f, 175f), new Vector2(1400f, 80f));
            manager.soloButton = MakeButton(menu, "Solo", "SOLO", center, new Vector2(-540f, -10f), new Vector2(320f, 180f), PlayerColors[0], 44);
            manager.twoPlayersButton = MakeButton(menu, "TwoPlayers", "2 PLAYERS", center, new Vector2(-180f, -10f), new Vector2(320f, 180f), PlayerColors[1], 44);
            manager.threePlayersButton = MakeButton(menu, "ThreePlayers", "3 PLAYERS", center, new Vector2(180f, -10f), new Vector2(320f, 180f), PlayerColors[2], 44);
            manager.fourPlayersButton = MakeButton(menu, "FourPlayers", "4 PLAYERS", center, new Vector2(540f, -10f), new Vector2(320f, 180f), PlayerColors[3], 44);
            manager.stageButton = MakeButton(menu, "Stage", "STAGE 1", center, new Vector2(-300f, -195f), new Vector2(640f, 110f), new Color(0.15f, 0.2f, 0.3f, 0.9f), 40);
            manager.stageText = manager.stageButton.GetComponentInChildren<Text>();
            manager.difficultyButton = MakeButton(menu, "Difficulty", "MODE: EASY", center, new Vector2(330f, -195f), new Vector2(460f, 110f), new Color(0.15f, 0.2f, 0.3f, 0.9f), 40);
            manager.difficultyText = manager.difficultyButton.GetComponentInChildren<Text>();
            Label(menu, "MenuHint", "Tap STAGE to pick a course. Tap MODE: EASY = raft goes by itself, HARD = like the real game.", 32, center, new Vector2(0f, -300f), new Vector2(1600f, 60f));
            Label(menu, "MenuHint2", "Everyone plays on the same phone - each player gets a joystick in one corner.", 32, center, new Vector2(0f, -360f), new Vector2(1600f, 60f));

            // Finish
            var finish = Panel(root, "Finish", new Color(0f, 0f, 0f, 0.7f));
            manager.finishPanel = finish.gameObject;
            manager.finishText = Label(finish, "FinishText", "FINISH!", 84, center, new Vector2(0f, 150f), new Vector2(1400f, 400f));
            manager.playAgainButton = MakeButton(finish, "PlayAgain", "PLAY AGAIN", center, new Vector2(-420f, -200f), new Vector2(380f, 140f), PlayerColors[1], 46);
            manager.nextStageButton = MakeButton(finish, "NextStage", "NEXT STAGE", center, new Vector2(0f, -200f), new Vector2(380f, 140f), PlayerColors[2], 46);
            manager.finishMenuButton = MakeButton(finish, "BackToMenu", "MENU", center, new Vector2(420f, -200f), new Vector2(380f, 140f), PlayerColors[0], 46);

            var flashRt = UIObject("Flash", root);
            Stretch(flashRt);
            var flash = flashRt.gameObject.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
            flash.enabled = false;
            manager.flash = flash;

            hud.gameObject.SetActive(false);
            finish.gameObject.SetActive(false);
        }

        static FartJoystick Joystick(Transform parent, int index, Vector2 anchor, Vector2 pos, FartSeat seat)
        {
            var rt = UIObject("Joystick_P" + (index + 1), parent);
            Place(rt, anchor, pos, new Vector2(340f, 340f));

            // Invisible, slightly larger touch area so fingers do not slip off.
            var hitArea = rt.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);

            var ringRt = UIObject("GasRing", rt);
            Place(ringRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = circleSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.raycastTarget = false;

            var baseRt = UIObject("Base", rt);
            Place(baseRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f));
            var baseImage = baseRt.gameObject.AddComponent<Image>();
            baseImage.sprite = circleSprite;
            baseImage.color = new Color(0.12f, 0.16f, 0.24f, 0.55f);
            baseImage.raycastTarget = false;

            var knobRt = UIObject("Knob", rt);
            Place(knobRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            var knob = knobRt.gameObject.AddComponent<Image>();
            knob.sprite = circleSprite;
            knob.color = PlayerColors[index];
            knob.raycastTarget = false;

            bool top = anchor.y > 0.5f;
            var label = Label(rt, "Label", AnimalNames[index], 40, new Vector2(0.5f, 0.5f), new Vector2(0f, top ? -195f : 195f), new Vector2(300f, 60f));
            label.color = Color.Lerp(PlayerColors[index], Color.white, 0.4f);

            var stick = rt.gameObject.AddComponent<FartJoystick>();
            stick.seat = seat;
            stick.knob = knobRt;
            stick.knobImage = knob;
            stick.gasRing = ring;
            stick.label = label;
            stick.color = PlayerColors[index];
            stick.radius = 120f;
            return stick;
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

        // ================================================================ Materials and textures

        class Mats
        {
            // Stage colors; every stage gets its own copy of these materials.
            public readonly Material slideWater = Textured(stagePrefix + "SlideWater", WaterTexture(), new Color(0.86f, 0.86f, 0.86f), 0.6f);
            public readonly Material slideBody = Lit(stagePrefix + "SlideBody", stage.slideBody, 0.3f);
            public readonly Material bumper = Lit(stagePrefix + "Bumper", stage.bumper, 0.3f);
            public readonly Material coral = Lit(stagePrefix + "Coral", stage.coral, 0.05f);
            public readonly Material coral2 = Lit(stagePrefix + "Coral2", stage.coral2, 0.05f);
            public readonly Material coral3 = Lit(stagePrefix + "Coral3", stage.coral3, 0.05f);
            public readonly Material grass = Lit(stagePrefix + "Grass", stage.grass, 0.05f);
            public readonly Material leaves = Lit(stagePrefix + "Leaves", stage.leaves, 0.05f);
            public readonly Material leaves2 = Lit(stagePrefix + "Leaves2", stage.leaves2, 0.05f);

            public readonly Material foam = Lit("Foam", new Color(0.96f, 0.99f, 1f), 0.2f);
            public readonly Material spike = Lit("Spike", new Color(0.96f, 0.96f, 0.94f), 0.3f);
            public readonly Material buoy = Lit("Buoy", new Color(1.00f, 0.48f, 0.15f), 0.35f);
            public readonly Material magenta = Lit("Magenta", new Color(0.90f, 0.32f, 0.62f), 0.3f);
            public readonly Material metal = Lit("Metal", new Color(0.70f, 0.72f, 0.78f), 0.5f);
            public readonly Material wood = Lit("Wood", new Color(0.66f, 0.45f, 0.26f), 0.15f);
            public readonly Material darkWood = Lit("DarkWood", new Color(0.45f, 0.29f, 0.16f), 0.15f);
            public readonly Material bamboo = Lit("Bamboo", new Color(0.96f, 0.80f, 0.36f), 0.2f);
            public readonly Material signBlue = Lit("SignBlue", new Color(0.18f, 0.45f, 0.85f), 0.3f);
            public readonly Material signYellow = Lit("SignYellow", new Color(1.00f, 0.84f, 0.20f), 0.3f);
            public readonly Material rock = Lit("Rock", new Color(0.42f, 0.41f, 0.43f), 0.05f);
            public readonly Material rock2 = Lit("Rock2", new Color(0.33f, 0.33f, 0.36f), 0.05f);
            public readonly Material trunk = Lit("Trunk", new Color(0.45f, 0.30f, 0.18f), 0.05f);
            public readonly Material pine = Lit("Pine", new Color(0.18f, 0.50f, 0.32f), 0.05f);
            public readonly Material bush = Lit("Bush", new Color(0.28f, 0.60f, 0.24f), 0.05f);
            public readonly Material mushroomCap = Lit("MushroomCap", new Color(0.90f, 0.20f, 0.20f), 0.3f);
            public readonly Material mushroomStem = Lit("MushroomStem", new Color(0.97f, 0.93f, 0.85f), 0.1f);
            public readonly Material cloud = Lit("Cloud", Color.white, 0f);
            public readonly Material white = Lit("White", Color.white, 0.3f);
            public readonly Material black = Lit("Black", new Color(0.05f, 0.05f, 0.06f), 0.5f);
            public readonly Material eyeWhite = Lit("EyeWhite", Color.white, 0.7f);
            public readonly Material raftTube = Lit("RaftTube", new Color(1.00f, 0.62f, 0.38f), 0.45f);
            public readonly Material raftBand = Lit("RaftBand", new Color(0.25f, 0.72f, 0.72f), 0.45f);
            public readonly Material raftFloor = Lit("RaftFloor", new Color(0.78f, 0.58f, 0.35f), 0.1f);
            public readonly Material strap = Lit("Strap", new Color(0.80f, 0.20f, 0.20f), 0.3f);
            public readonly Material[] animalMain =
            {
                Lit("CapyMain", new Color(0.68f, 0.44f, 0.26f), 0.15f),
                Lit("HippoMain", new Color(0.36f, 0.50f, 0.78f), 0.15f),
                Lit("CrocMain", new Color(0.38f, 0.72f, 0.28f), 0.15f),
                Lit("GorillaMain", new Color(0.22f, 0.21f, 0.25f), 0.15f),
            };
            public readonly Material[] animalLight =
            {
                Lit("CapyLight", new Color(0.82f, 0.60f, 0.40f), 0.15f),
                Lit("HippoLight", new Color(0.62f, 0.72f, 0.92f), 0.15f),
                Lit("CrocLight", new Color(0.70f, 0.90f, 0.45f), 0.15f),
                Lit("GorillaLight", new Color(0.52f, 0.45f, 0.42f), 0.15f),
            };
            public readonly Material puff = Unlit("Puff", SoftCircleTexture());
            public readonly Material star = Unlit("Star", StarTexture());
            public readonly Material wakeRing = Unlit("WakeRing", RingTexture());
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

        static Material Textured(string name, Texture2D texture, Color color, float smoothness)
        {
            var mat = Lit(name, color, smoothness);
            mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Sprites/Default is unlit, transparent and works in Built-in and URP.
        static Material Unlit(string name, Texture2D texture)
        {
            var mat = LoadOrCreateMaterial(name, Shader.Find("Sprites/Default"));
            mat.mainTexture = texture;
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

        delegate Color PixelFunction(float u, float v);

        static Texture2D SaveTexture(string name, int size, PixelFunction pixel)
        {
            string path = GeneratedFolder + "/" + name + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D SoftCircleTexture()
        {
            return SaveTexture("Puff", 64, (u, v) =>
            {
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, a * 1.6f));
            });
        }

        static Texture2D RingTexture()
        {
            return SaveTexture("WakeRing", 128, (u, v) =>
            {
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.09f);
                return new Color(1f, 1f, 1f, ring * ring);
            });
        }

        static Texture2D StarTexture()
        {
            return SaveTexture("Star", 64, (u, v) =>
            {
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.45f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2.5f)), 3f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((edge - r) * 12f));
            });
        }

        // Cyan water with light flow streaks; every term repeats over the tile so it scrolls seamlessly.
        static Texture2D WaterTexture()
        {
            Color deep = stage.waterDeep;
            Color light = stage.waterLight;
            Color streak = new Color(0.88f, 0.97f, 1f);
            return SaveTexture(stagePrefix + "SlideWater", 256, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                float waves = Mathf.Sin(6f * a + 2f * Mathf.Sin(2f * b)) * Mathf.Cos(3f * b + 1.5f * Mathf.Sin(a));
                float ripples = Mathf.Sin(10f * a + 3f * Mathf.Sin(3f * b + a)) * Mathf.Sin(5f * b);
                Color c = Color.Lerp(deep, light, 0.5f + 0.25f * waves);
                if (waves > 0.78f || ripples > 0.92f) c = Color.Lerp(c, streak, 0.85f);
                return c;
            });
        }

        // ================================================================ Post-processing (URP only)

        // Uses reflection so the project still compiles without the URP package.
        static void AddPostProcessing(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Debug.Log("[Fart Raft] Built-in render pipeline: skipping bloom/color grading. Use Fart Raft > 3 to switch to URP.");
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

            var profile = ScriptableObject.CreateInstance(profileType);
            AssetDatabase.CreateAsset(profile, GeneratedFolder + "/" + stagePrefix + "PostProcessing.asset");

            AddOverride(profile, "UnityEngine.Rendering.Universal.Tonemapping", ("mode", "Neutral"));
            AddOverride(profile, "UnityEngine.Rendering.Universal.Bloom", ("intensity", 0.4f), ("threshold", 1f), ("scatter", 0.6f));
            AddOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments", ("postExposure", 0.2f), ("contrast", 8f), ("saturation", 22f));
            AddOverride(profile, "UnityEngine.Rendering.Universal.Vignette", ("intensity", 0.18f), ("smoothness", 0.4f));
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

        // ================================================================ Physics material

#if UNITY_6000_0_OR_NEWER
        static PhysicsMaterial slippery;
#else
        static PhysicMaterial slippery;
#endif

        static void SetSlippery(Collider collider)
        {
            if (slippery == null)
            {
#if UNITY_6000_0_OR_NEWER
                slippery = new PhysicsMaterial("Slippery")
                {
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
#else
                slippery = new PhysicMaterial("Slippery")
                {
                    frictionCombine = PhysicMaterialCombine.Minimum,
                    bounceCombine = PhysicMaterialCombine.Minimum,
#endif
                    dynamicFriction = 0.01f,
                    staticFriction = 0.01f,
                    bounciness = 0f,
                };
                AssetDatabase.CreateAsset(slippery, GeneratedFolder + "/Slippery.asset");
            }
            collider.sharedMaterial = slippery;
        }

        // ================================================================ Meshes

        static Mesh coneMesh;
        static Mesh pillarMesh;
        static readonly Mesh[] rockMeshes = new Mesh[3];
        static int meshCounter;

        static void ResetCaches()
        {
            coneMesh = null;
            pillarMesh = null;
            for (int i = 0; i < rockMeshes.Length; i++) rockMeshes[i] = null;
            slippery = null;
            fartClips = null;
            meshCounter = 0;
        }

        static void SaveMesh(Mesh mesh)
        {
            AssetDatabase.CreateAsset(mesh, GeneratedFolder + "/" + mesh.name + "_" + (meshCounter++) + ".asset");
        }

        static Mesh BuildFlatMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh);
            return mesh;
        }

        // 8-sided low-poly cone: base radius 0.5 at y=0, tip at y=1.
        static Mesh ConeMesh()
        {
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
                Vector3 mid = (p0 + p1) * 0.5f;
                AddTriangle(vertices, triangles, tip, p0, p1, new Vector3(mid.x, 0f, mid.z));
                AddTriangle(vertices, triangles, Vector3.zero, p0, p1, Vector3.down);
            }
            coneMesh = BuildFlatMesh("Cone", vertices, triangles);
            return coneMesh;
        }

        // 9-sided low-poly column: radius 0.5, from y=0 to y=1.
        static Mesh PillarMesh()
        {
            if (pillarMesh != null) return pillarMesh;
            pillarMesh = CylinderMesh("Pillar", 0.5f, 1f, 9, false, 0f);
            return pillarMesh;
        }

        static Mesh CylinderMesh(string name, float radius, float height, int sides, bool centered, float baseY = float.NaN)
        {
            float y0 = float.IsNaN(baseY) ? -height * 0.5f : baseY;
            float y1 = y0 + height;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 b0 = d0 + Vector3.up * y0, b1 = d1 + Vector3.up * y0;
                Vector3 t0 = d0 + Vector3.up * y1, t1 = d1 + Vector3.up * y1;
                AddQuad(vertices, triangles, b0, b1, t1, t0, (d0 + d1) * 0.5f);
                AddTriangle(vertices, triangles, Vector3.up * y1, t0, t1, Vector3.up);
                AddTriangle(vertices, triangles, Vector3.up * y0, b0, b1, Vector3.down);
            }
            return BuildFlatMesh(name, vertices, triangles);
        }

        static Mesh TorusMesh(string name, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int i = 0; i <= majorSegments; i++)
            {
                float u = i * Mathf.PI * 2f / majorSegments;
                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = j * Mathf.PI * 2f / minorSegments;
                    Vector3 normal = new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Sin(v), Mathf.Cos(v) * Mathf.Sin(u));
                    Vector3 ringCenter = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)) * majorRadius;
                    vertices.Add(ringCenter + normal * minorRadius);
                    normals.Add(normal);
                }
            }

            var triangles = new List<int>();
            int stride = minorSegments + 1;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * stride + j, b = (i + 1) * stride + j, c = (i + 1) * stride + j + 1, d = i * stride + j + 1;
                    AddIndexedTriangle(vertices, triangles, a, b, c, normals[a]);
                    AddIndexedTriangle(vertices, triangles, a, c, d, normals[a]);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            SaveMesh(mesh);
            return mesh;
        }

        // Faceted low-poly boulder: a jittered, subdivided icosahedron (radius about 0.5).
        static Mesh RockMesh(int variant)
        {
            if (rockMeshes[variant] != null) return rockMeshes[variant];

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index)) return index;
                points.Add((points[a] + points[b]) * 0.5f);
                midpoints[key] = points.Count - 1;
                return points.Count - 1;
            }

            var subdivided = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                subdivided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            var random = new System.Random(100 + variant);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i].normalized * (0.5f * (0.8f + (float)random.NextDouble() * 0.35f));
                points[i] = new Vector3(p.x, p.y * 0.8f, p.z);
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int f = 0; f < subdivided.Count; f += 3)
            {
                Vector3 a = points[subdivided[f]], b = points[subdivided[f + 1]], c = points[subdivided[f + 2]];
                AddTriangle(vertices, triangles, a, b, c, (a + b + c) / 3f);
            }
            rockMeshes[variant] = BuildFlatMesh("Rock" + variant, vertices, triangles);
            return rockMeshes[variant];
        }

        static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            AddTriangle(vertices, triangles, a, b, c, outward);
            AddTriangle(vertices, triangles, a, c, d, outward);
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Unity treats Cross(b - a, c - a) as the front-face normal.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
            }
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        static void AddIndexedTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), outward) < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        // ================================================================ Performance

        // Everything that never moves is marked static (Unity merges it into a few draw
        // calls) and stops casting shadows. Moving obstacles keep their shadows.
        static void OptimizeStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponentInParent<Rigidbody>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                var r = t.GetComponent<Renderer>();
                if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void ConfigureQuality()
        {
            QualitySettings.shadowDistance = 45f;
            QualitySettings.shadowCascades = 1;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;
            SetMember(pipeline, "shadowDistance", 45f);
            SetMember(pipeline, "shadowCascadeCount", 1);
            SetMember(pipeline, "msaaSampleCount", 2);
            EditorUtility.SetDirty(pipeline);
        }

        // ================================================================ Audio

        const string AudioFolder = Root + "/Audio";

        static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".ogg");
            if (clip == null) Debug.LogWarning("[Fart Raft] Missing sound " + AudioFolder + "/" + name + ".ogg");
            return clip;
        }

        static AudioClip[] fartClips;

        static AudioClip[] FartClips()
        {
            if (fartClips != null) return fartClips;
            fartClips = AssetDatabase.FindAssets("fart_ t:AudioClip", new[] { AudioFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(clip => clip != null)
                .ToArray();
            return fartClips;
        }

        static GameAudio BuildGameAudio(GameObject go)
        {
            go.AddComponent<AudioSource>();
            var audio = go.AddComponent<GameAudio>();
            audio.bump = LoadClip("sfx_bump");
            audio.punch = LoadClip("sfx_punch");
            audio.explosion = LoadClip("sfx_explosion");
            audio.fall = LoadClip("sfx_fall");
            audio.splash = LoadClip("sfx_splash");
            audio.checkpoint = LoadClip("sfx_checkpoint");
            audio.cheer = LoadClip("sfx_cheer");
            return audio;
        }

        // ================================================================ Helpers

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

        static GameObject MeshObject(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
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

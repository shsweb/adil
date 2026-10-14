using System.IO;
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = new Mats();

            SetupLighting();
            var startPoint = BuildCourse(mats, out int checkpointCount);
            var raft = BuildRaft(mats, startPoint, out FartSeat[] seats);
            var cameraFollow = BuildCamera(raft.transform);

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

        // ---------------------------------------------------------------- Materials

        class Mats
        {
            public readonly Material water = Lit("Water", new Color(0.15f, 0.55f, 0.85f), 0.9f);
            public readonly Material grass = Lit("Grass", new Color(0.35f, 0.70f, 0.30f), 0.1f);
            public readonly Material rock = Lit("Rock", new Color(0.55f, 0.50f, 0.45f), 0.15f);
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
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.sun = light;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.82f, 0.92f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 220f;
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

            var water = Prim(PrimitiveType.Plane, "Water", course, new Vector3(5f, 0f, 135f), new Vector3(14f, 1f, 36f), m.water, false);
            water.isStatic = true;

            var banks = new GameObject("Banks").transform;
            banks.SetParent(course, false);
            var decor = new GameObject("Decor").transform;
            decor.SetParent(course, false);
            var random = new System.Random(42);

            for (int i = 0; i < n - 1; i++)
            {
                BuildBank(banks, decor, m, left[i], left[i + 1], -1f, random);
                BuildBank(banks, decor, m, right[i], right[i + 1], 1f, random);
            }
            Wall(banks, "StartWall", left[0] - tangents[0] * 1f, right[0] - tangents[0] * 1f, m.rock);
            Wall(banks, "EndWall", left[n - 1], right[n - 1], m.rock);

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

        static void BuildBank(Transform banks, Transform decor, Mats m, Vector3 a, Vector3 b, float side, System.Random random)
        {
            Wall(banks, "Wall", a, b, m.rock);

            Vector3 dir = (b - a).normalized;
            Vector3 outward = RightOf(dir) * side;
            const float landWidth = 16f;
            var land = Prim(PrimitiveType.Cube, "Land", banks, (a + b) * 0.5f + outward * (landWidth * 0.5f + 1f),
                new Vector3(landWidth, 1f, Vector3.Distance(a, b)), m.grass, false);
            land.transform.rotation = Quaternion.LookRotation(dir);
            land.isStatic = true;

            float length = Vector3.Distance(a, b);
            for (float d = 2f; d < length; d += 6f)
            {
                if (random.NextDouble() < 0.35) continue;
                float off = 3f + (float)random.NextDouble() * 11f;
                Vector3 pos = a + dir * d + outward * off;
                if (random.NextDouble() < 0.8) Tree(decor, m, pos, random);
                else Rock(decor, m, pos, random);
            }
        }

        static void Wall(Transform parent, string name, Vector3 a, Vector3 b, Material mat)
        {
            Vector3 d = b - a;
            var go = Prim(PrimitiveType.Cube, name, parent, (a + b) * 0.5f + Vector3.up * 0.3f,
                new Vector3(2f, 1.6f, d.magnitude + 2f), mat, true);
            go.transform.rotation = Quaternion.LookRotation(d.normalized);
            go.isStatic = true;
        }

        static void Tree(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            float s = 0.8f + (float)random.NextDouble() * 0.7f;
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.position = pos;
            tree.localScale = Vector3.one * s;
            Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.7f, 0f), new Vector3(0.4f, 1.2f, 0.4f), m.trunk, false);
            Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 3.6f, 0f), Vector3.one * 2.6f,
                random.NextDouble() < 0.5 ? m.leaves : m.leaves2, false);
        }

        static void Rock(Transform parent, Mats m, Vector3 pos, System.Random random)
        {
            float s = 0.8f + (float)random.NextDouble() * 1.5f;
            var rock = Prim(PrimitiveType.Sphere, "Rock", parent, pos + Vector3.up * 0.5f, new Vector3(s * 1.4f, s * 0.8f, s), m.rock, false);
            rock.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
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

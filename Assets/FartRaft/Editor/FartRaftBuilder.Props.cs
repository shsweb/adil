using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FartRaft.EditorTools
{
    // Models from Asset Store packs imported into this Unity project (they are not part of the repo).
    // Every set is optional: when a pack is missing the builder falls back to its own primitive props.
    //   EmbersStorm - Free Nature Pack, ArchiBoxCo Nature Pack, YGS Beach Party & Fun Pack, Quirky Series (FREE),
    //   Low Poly Tropical Island Lite (palms, plants, rocks, pier, tent, chest, barrels).
    public static partial class FartRaftBuilder
    {
        class StageProps
        {
            public GameObject[] trees = new GameObject[0];
            public GameObject[] bushes = new GameObject[0];
            public GameObject[] flowers = new GameObject[0];
            public GameObject[] rocks = new GameObject[0];
            public GameObject[] decor = new GameObject[0];
            public GameObject[] animals = new GameObject[0];
            public GameObject beachBall;
            public GameObject fish;
        }

        enum Fit { Ground, Center }

        static StageProps props = new StageProps();
        static string[] prefabPaths;
        static readonly Dictionary<Material, Material> convertedMaterials = new Dictionary<Material, Material>();
        static int convertedCounter;

        static void ResetPropCaches()
        {
            prefabPaths = null;
            convertedMaterials.Clear();
            convertedCounter = 0;
            loggedCharacters = -1;
        }

        // All prefabs whose lower-case path contains every one of the given parts.
        static GameObject[] Prefabs(params string[] parts)
        {
            if (prefabPaths == null)
            {
                prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.StartsWith(Root + "/"))
                    .ToArray();
            }
            return prefabPaths
                .Where(p => parts.All(part => p.ToLowerInvariant().Contains(part)))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null)
                .ToArray();
        }

        static GameObject[] Join(params GameObject[][] sets) => sets.SelectMany(s => s).Distinct().ToArray();

        static GameObject[] Named(GameObject[] set, params string[] names) =>
            set.Where(g => names.Any(n => g.name.ToLowerInvariant().Contains(n))).ToArray();

        // Low Poly Tropical Island Lite. The folder name varies a little between imports.
        static GameObject[] TropicalPrefabs()
        {
            var found = Prefabs("lowpolytropicalenvironment");
            if (found.Length > 0) return found;
            found = Prefabs("tropicalenvironment_lite");
            if (found.Length > 0) return found;
            found = Prefabs("low poly tropical");
            if (found.Length > 0) return found;
            return Prefabs("tropical island");
        }

        static void LoadStageProps(int index)
        {
            const string embers = "embersstorm";
            var oak = Prefabs(embers, "/trees/oak/");
            var pine = Prefabs(embers, "/trees/pine tree/");
            var deadTrees = Prefabs(embers, "/trees/dead trees/dead tree");
            var deadBushes = Prefabs(embers, "/trees/dead trees/dead bush");
            var bushes = Prefabs(embers, "/trees/bush/");
            var plants = Prefabs(embers, "/plants/plant/");
            var grass = Prefabs(embers, "/plants/grass/");
            var flowers = Prefabs(embers, "/plants/flowers/");
            var mushrooms = Prefabs(embers, "/plants/mushrooms/");
            var rocks = Join(Prefabs(embers, "/rocks/"), Prefabs("archiboxco/nature pack/prefabs/rock"));
            var archiTrees = Prefabs("archiboxco/nature pack/prefabs/tree");

            var beach = Prefabs("beach party", "/prefabs/");
            var tropical = TropicalPrefabs();
            var palms = Named(tropical, "palm");
            var tropPlants = Named(tropical, "plant");
            var tropRocks = Named(tropical, "rock");
            var tropParty = Named(tropical, "barrel", "chest", "tent", "plank", "skull");
            var animals = Prefabs("quirky series", "/prefabs/").Where(g =>
            {
                string path = AssetDatabase.GetAssetPath(g).ToLowerInvariant();
                return !path.Contains("single lods") && !path.Contains("_scripts");
            }).ToArray();

            props = new StageProps
            {
                beachBall = Named(beach, "beach ball").FirstOrDefault(),
                fish = Named(animals, "herring").FirstOrDefault(),
            };

            switch (index)
            {
                case 0:
                    props.trees = Join(palms, oak, archiTrees);
                    props.bushes = Join(tropPlants, bushes);
                    props.flowers = Join(flowers, mushrooms);
                    props.rocks = Join(tropRocks, rocks);
                    props.decor = Join(tropParty, Named(beach, "umbella", "umbrella", "chair", "sand castle", "surfboard", "sea star", "bucket"));
                    props.animals = animals.Where(a => a != props.fish).ToArray();
                    break;
                case 1:
                    props.trees = Join(palms, pine, oak);
                    props.bushes = Join(tropPlants, bushes, plants);
                    props.flowers = Join(mushrooms, grass);
                    props.rocks = Join(tropRocks, rocks);
                    props.decor = tropParty;
                    props.animals = Named(animals, "colobus", "gecko", "taipan", "sparrow", "pudu", "muskrat");
                    break;
                default:
                    props.trees = deadTrees;
                    props.bushes = Join(deadBushes, tropPlants);
                    props.rocks = Join(tropRocks, rocks);
                    props.decor = Named(tropical, "skull", "barrel");
                    props.animals = Named(animals, "gecko", "taipan", "muskrat");
                    break;
            }

            // Prefabs dragged into the Environment Theme asset win over the automatic picks.
            if (theme.trees != null && theme.trees.Length > 0) props.trees = theme.trees;
            if (theme.bushes != null && theme.bushes.Length > 0) props.bushes = theme.bushes;
            if (theme.flowers != null && theme.flowers.Length > 0) props.flowers = theme.flowers;
            if (theme.rocks != null && theme.rocks.Length > 0) props.rocks = theme.rocks;

            Debug.Log($"[Fart Raft] Stage {index + 1} props: {props.trees.Length} trees, {props.bushes.Length} bushes, " +
                      $"{props.flowers.Length} flowers, {props.rocks.Length} rocks, {props.decor.Length} beach props, " +
                      $"{props.animals.Length} animals, beach ball {(props.beachBall != null ? "yes" : "no")}, fish {(props.fish != null ? "yes" : "no")}.");
        }

        static GameObject Pick(GameObject[] set, System.Random random) =>
            set == null || set.Length == 0 ? null : set[random.Next(set.Length)];

        // Places a prefab scaled so its height (or its biggest side) is `size` meters.
        static GameObject SpawnFitted(GameObject prefab, Transform parent, Vector3 pos, float size, bool byHeight, Fit fit, bool randomYaw, System.Random random)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = randomYaw ? Quaternion.Euler(0f, R(random) * 360f, 0f) : parent.rotation;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.CullCompletely;
            FixMaterials(go);

            if (TryGetBounds(go, out Bounds bounds))
            {
                float current = byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (current > 0.01f) go.transform.localScale *= size * theme.propScale / current;
                if (TryGetBounds(go, out bounds))
                {
                    go.transform.position += fit == Fit.Ground ? Vector3.up * (pos.y - bounds.min.y) : pos - bounds.center;
                }
            }
            return go;
        }

        static bool TrySpawnFitted(GameObject[] set, Transform parent, Vector3 pos, float size, bool byHeight, System.Random random)
        {
            var prefab = Pick(set, random);
            if (prefab == null) return false;
            SpawnFitted(prefab, parent, pos, size, byHeight, Fit.Ground, true, random);
            return true;
        }

        // Like TrySpawnFitted, but the prop faces `yaw` degrees instead of a random direction.
        static bool TrySpawnYaw(GameObject[] set, Transform parent, Vector3 pos, float size, bool byHeight, float yaw, System.Random random)
        {
            var prefab = Pick(set, random);
            if (prefab == null) return false;
            var pivot = new GameObject(prefab.name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            SpawnFitted(prefab, pivot.transform, pos, size, byHeight, Fit.Ground, false, random);
            return true;
        }

        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }

        // Older packs use Built-in shaders, which render pink under URP (and URP shaders do not work in Built-in).
        // Swap those materials for copies that use the shader this project can render.
        static void FixMaterials(GameObject go)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !NeedsConversion(mats[i], srp)) continue;
                    mats[i] = Converted(mats[i]);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static bool NeedsConversion(Material mat, bool srp)
        {
            var shader = mat.shader;
            if (shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader") return true;
            string name = shader.name;
            if (srp) return name == "Standard" || name == "Standard (Specular setup)" || name.StartsWith("Legacy Shaders/") || name.StartsWith("Mobile/");
            return name.StartsWith("Universal Render Pipeline/");
        }

        static Material Converted(Material source)
        {
            if (convertedMaterials.TryGetValue(source, out Material done)) return done;

            ReadSavedProperties(source, out Color color, out Texture texture, out string textureName, out string colorName);
            Debug.Log($"[Fart Raft] Converted material '{source.name}' (shader '{(source.shader != null ? source.shader.name : "none")}'): " +
                      $"texture {(texture != null ? textureName + "=" + texture.name : "none")}, color {(colorName ?? "none")}={color}");

            var mat = new Material(LitShader()) { name = source.name + "_Converted" };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, GeneratedFolder + "/Converted_" + (convertedCounter++) + ".mat");
            convertedMaterials[source] = mat;
            return mat;
        }

        static readonly string[] MainTextureNames = { "_BaseMap", "_MainTex", "_Albedo", "_AlbedoMap", "_BaseColorMap", "_Diffuse", "_MainTexture", "_Texture" };
        static readonly string[] MainColorNames = { "_BaseColor", "_Color", "_MainColor", "_Tint", "_AlbedoColor", "_DiffuseColor" };
        static readonly string[] NotMainParts = { "normal", "bump", "mask", "emiss", "metal", "occlus", "spec", "height", "detail", "rough", "shadow", "rim", "outline" };

        // Reads the values stored in the material file. When the original shader is missing,
        // Material.GetColor / GetTexture return nothing, which is why converted props came out white.
        static void ReadSavedProperties(Material source, out Color color, out Texture texture, out string textureName, out string colorName)
        {
            var textures = new List<KeyValuePair<string, Texture>>();
            var colors = new List<KeyValuePair<string, Color>>();
            var so = new SerializedObject(source);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs != null)
            {
                for (int i = 0; i < texEnvs.arraySize; i++)
                {
                    var entry = texEnvs.GetArrayElementAtIndex(i);
                    var tex = entry.FindPropertyRelative("second.m_Texture")?.objectReferenceValue as Texture;
                    if (tex != null) textures.Add(new KeyValuePair<string, Texture>(entry.FindPropertyRelative("first").stringValue, tex));
                }
            }
            var colorEnvs = so.FindProperty("m_SavedProperties.m_Colors");
            if (colorEnvs != null)
            {
                for (int i = 0; i < colorEnvs.arraySize; i++)
                {
                    var entry = colorEnvs.GetArrayElementAtIndex(i);
                    colors.Add(new KeyValuePair<string, Color>(entry.FindPropertyRelative("first").stringValue, entry.FindPropertyRelative("second").colorValue));
                }
            }

            var pickedTexture = PickMain(textures, MainTextureNames);
            texture = pickedTexture.Value;
            textureName = pickedTexture.Key;
            var pickedColor = PickMain(colors, MainColorNames);
            colorName = pickedColor.Key;
            color = colorName != null ? pickedColor.Value : Color.white;
            color.a = 1f;
        }

        static KeyValuePair<string, T> PickMain<T>(List<KeyValuePair<string, T>> items, string[] preferred)
        {
            foreach (var name in preferred)
            {
                foreach (var item in items)
                {
                    if (item.Key == name) return item;
                }
            }
            foreach (var item in items)
            {
                string lower = item.Key.ToLowerInvariant();
                if (NotMainParts.Any(part => lower.Contains(part))) continue;
                if (lower.Contains("base") || lower.Contains("main") || lower.Contains("albedo") || lower.Contains("diff") || lower.Contains("color") || lower.Contains("tex")) return item;
            }
            return default;
        }

        // Funny animals standing on a cliff edge, watching the raft go by.
        static void Spectator(Transform pillar, Vector3 lookAt, float radius, System.Random random)
        {
            var prefab = Pick(props.animals, random);
            if (prefab == null) return;
            Vector3 toTrack = lookAt - pillar.position;
            toTrack.y = 0f;
            if (toTrack.sqrMagnitude < 0.01f) return;
            toTrack.Normalize();
            Vector3 pos = pillar.position + toTrack * (radius * 0.6f) + Vector3.up * 0.3f;
            var go = SpawnFitted(prefab, pillar, pos, 1.6f + R(random) * 0.6f, false, Fit.Ground, false, random);
            go.transform.rotation = Quaternion.LookRotation(toTrack);
        }

        // ================================================================ Funny obstacles

        // A giant beach ball rolling back and forth across a pool. It only bumps, but hard.
        static void BeachBall(Transform parent, Mats m, Track tr, int k, float phase)
        {
            const float radius = 1.6f;
            float travel = tr.width[k] - radius * 2f - 1f;
            var root = new GameObject("BeachBall");
            root.transform.SetParent(parent, false);
            root.transform.position = tr.Point(k, -travel * 0.5f) + Vector3.up * radius;
            root.AddComponent<SphereCollider>().radius = radius;
            root.AddComponent<Rigidbody>().isKinematic = true;
            var mover = root.AddComponent<Mover>();
            mover.offset = tr.right[k] * travel;
            mover.period = 4.5f;
            mover.phase = phase;
            root.AddComponent<Bumper>().bounce = 8f;

            Transform visual;
            if (props.beachBall != null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                SpawnFitted(props.beachBall, visual, root.transform.position, radius * 2f, false, Fit.Center, false, new System.Random(k));
            }
            else
            {
                visual = Prim(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * radius * 2f, m.glove, false).transform;
                Prim(PrimitiveType.Cylinder, "Stripe", visual, Vector3.zero, new Vector3(1.02f, 0.12f, 1.02f), m.white, false);
                var stripe = Prim(PrimitiveType.Cylinder, "Stripe", visual, Vector3.zero, new Vector3(1.02f, 0.12f, 1.02f), m.signYellow, false);
                stripe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            visual.gameObject.AddComponent<Roller>().radius = radius;
        }

        // A boxing glove on a spring that shoots out of the wall and punches the raft across the slide.
        static void BoxingGlove(Transform parent, Mats m, Track tr, int k, float side, float phase)
        {
            Vector3 inward = -tr.right[k] * side;
            var root = new GameObject("BoxingGlove");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(tr.Edge(k, side) - inward * 1.3f + Vector3.up * 0.9f, Quaternion.LookRotation(inward));
            root.AddComponent<Rigidbody>().isKinematic = true;
            var puncher = root.AddComponent<Puncher>();
            puncher.punch = inward * (tr.width[k] * 0.55f);
            puncher.period = 3.2f;
            puncher.phase = phase;
            root.AddComponent<Bumper>().bounce = 11f;

            Prim(PrimitiveType.Sphere, "Glove", root.transform, Vector3.zero, new Vector3(1.6f, 1.4f, 1.7f), m.glove, true);
            Prim(PrimitiveType.Sphere, "Thumb", root.transform, new Vector3(0.75f, 0.25f, -0.15f), new Vector3(0.6f, 0.6f, 0.8f), m.glove, false);
            var cuff = Prim(PrimitiveType.Cylinder, "Cuff", root.transform, new Vector3(0f, 0f, -0.95f), new Vector3(1.1f, 0.25f, 1.1f), m.white, false);
            cuff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var spring = Prim(PrimitiveType.Cylinder, "Spring", root.transform, new Vector3(0f, 0f, -3.4f), new Vector3(0.3f, 2.3f, 0.3f), m.metal, false);
            spring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                var coil = Prim(PrimitiveType.Cylinder, "Coil", root.transform, new Vector3(0f, 0f, -1.4f - i * 0.75f), new Vector3(0.6f, 0.05f, 0.6f), m.signYellow, false);
                coil.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // A big fish that leaps over the slide from side to side.
        static void JumpingFish(Transform parent, Mats m, Track tr, int k, float phase)
        {
            float reach = tr.width[k] * 0.5f + 3f;
            Vector3 from = tr.Point(k, -reach) + Vector3.down * 2f;
            Vector3 to = tr.Point(k, reach) + Vector3.down * 2f;

            var root = new GameObject("JumpingFish");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            root.AddComponent<SphereCollider>().radius = 1.1f;
            root.AddComponent<Rigidbody>().isKinematic = true;
            var jumper = root.AddComponent<Jumper>();
            jumper.from = from;
            jumper.to = to;
            jumper.height = 3.4f;
            jumper.jumpTime = 1.5f;
            jumper.period = 3.4f;
            jumper.phase = phase;
            root.AddComponent<Bumper>().bounce = 9f;

            if (props.fish != null)
            {
                SpawnFitted(props.fish, root.transform, from, 3f, false, Fit.Center, false, new System.Random(k));
            }
            else
            {
                var body = Prim(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, new Vector3(1.2f, 1.4f, 2.8f), m.fish, false);
                var tail = MeshObject(ConeMesh(), "Tail", root.transform, new Vector3(0f, 0f, -1.7f), new Vector3(1.2f, 0.9f, 0.3f), m.fish);
                tail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Sphere, "EyeL", body.transform, new Vector3(-0.42f, 0.15f, 0.3f), new Vector3(0.25f, 0.22f, 0.12f), m.eyeWhite, false);
                Prim(PrimitiveType.Sphere, "EyeR", body.transform, new Vector3(0.42f, 0.15f, 0.3f), new Vector3(0.25f, 0.22f, 0.12f), m.eyeWhite, false);
            }
        }

        // A floating can of beans: grab it for a few seconds of MEGA FART.
        static void Beans(Transform parent, Mats m, Track tr, int k, float sideOffset)
        {
            var root = new GameObject("Beans");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(tr.Point(k, sideOffset) + Vector3.up * 1.4f, Quaternion.LookRotation(tr.forward[k]));
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 3f, 2.4f);
            root.AddComponent<Rigidbody>().isKinematic = true;
            var pickup = root.AddComponent<BeansPickup>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            pickup.visual = visual;
            Prim(PrimitiveType.Cylinder, "Can", visual, Vector3.zero, new Vector3(1.1f, 0.7f, 1.1f), m.metal, false);
            Prim(PrimitiveType.Cylinder, "Label", visual, Vector3.zero, new Vector3(1.14f, 0.5f, 1.14f), m.beansCan, false);
            Text3D(visual, "BEANS", new Vector3(0f, 0f, -0.6f), 0.035f, Color.white);
            var back = new GameObject("Back").transform;
            back.SetParent(visual, false);
            back.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Text3D(back, "BEANS", new Vector3(0f, 0f, -0.6f), 0.035f, Color.white);
        }
    }
}

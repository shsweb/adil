using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FartRaft.EditorTools
{
    // Playable characters. Classic animals and the penguin are built from shapes. The rest are
    // animated animals from the Quirky Series - FREE Animals Pack (Asset Store, not in this repo);
    // without that pack only the built animals are available.
    public static partial class FartRaftBuilder
    {
        const string CharactersFolder = Root + "/Characters";
        const float CharacterHeight = 1.35f;
        const float CharacterFootprint = 1.25f;

        static int loggedCharacters = -1;

        static CharacterSkin[] BuildSkins(Transform character, Mats m)
        {
            var skins = new CharacterSkin[CharacterRoster.All.Length];
            int models = 0;
            for (int i = 0; i < skins.Length; i++)
            {
                var info = CharacterRoster.All[i];
                var root = new GameObject("Skin_" + info.name).transform;
                root.SetParent(character, false);
                var skin = root.gameObject.AddComponent<CharacterSkin>();

                if (info.classic >= 0)
                {
                    BuildAnimal(root, m, info.classic, out skin.leftArm, out skin.rightArm);
                }
                else if (info.model == "Penguin")
                {
                    // The Gobkit file imports as one pink blob (broken shader and a bad size).
                    // A built penguin is black, white and orange, so the slot is never empty or pink.
                    BuildPenguin(root, m, out skin.leftArm, out skin.rightArm);
                }
                else if (info.prefab != null && SpawnPrefabCharacter(info.prefab, root, skin))
                {
                    models++;
                }
                else if (info.model != null && SpawnCharacterModel(info.model, root, skin))
                {
                    models++;
                }
                else
                {
                    Object.DestroyImmediate(root.gameObject);
                    continue;
                }
                root.gameObject.SetActive(false);
                skins[i] = skin;
            }

            if (loggedCharacters != models)
            {
                loggedCharacters = models;
                int wanted = CharacterRoster.All.Count(c => c.prefab != null);
                if (models < wanted)
                    Debug.LogWarning($"[Fart Raft] Only {models} of {wanted} Quirky animals found. Import 'Quirky Series - FREE Animals Pack' " +
                                     "(Window > Package Manager > My Assets) and rebuild.");
                else
                    Debug.Log($"[Fart Raft] {models} Quirky animals loaded as playable characters.");
            }
            return skins;
        }

        static bool SpawnCharacterModel(string file, Transform parent, CharacterSkin skin)
        {
            string path = CharactersFolder + "/" + file + ".glb";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (!FitCharacter(go, parent)) return false;

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            skin.model = go;
            skin.clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
            return true;
        }

        static GameObject[] quirkyAnimals;

        // A Quirky Series animal: same height as the built animals, playing its own Idle / Walk / Attack clips.
        static bool SpawnPrefabCharacter(string animal, Transform parent, CharacterSkin skin)
        {
            if (quirkyAnimals == null)
            {
                quirkyAnimals = Prefabs("quirky series", "/prefabs/").Where(g =>
                {
                    string path = AssetDatabase.GetAssetPath(g).ToLowerInvariant();
                    return !path.Contains("single lods") && !path.Contains("_scripts");
                }).ToArray();
            }
            var prefab = Named(quirkyAnimals, animal).FirstOrDefault();
            if (prefab == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (!FitCharacter(go, parent)) return false;

            var animator = go.GetComponentInChildren<Animator>();
            var clips = new List<AnimationClip>();
            if (animator != null && animator.runtimeAnimatorController != null)
                clips.AddRange(animator.runtimeAnimatorController.animationClips);
            if (clips.Count == 0)
            {
                // No controller: read the clips from the model file the mesh comes from.
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                string meshPath = smr != null && smr.sharedMesh != null ? AssetDatabase.GetAssetPath(smr.sharedMesh) : null;
                if (!string.IsNullOrEmpty(meshPath))
                    clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")));
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // CharacterSkin plays the clips itself; the pack's controller would fight it.
                if (clips.Count > 0) animator.runtimeAnimatorController = null;
            }
            skin.model = animator != null ? animator.gameObject : go;
            skin.clips = clips.Where(c => c != null).Distinct().ToArray();
            return true;
        }

        // Scales a model to the shared character size, feet at y = 0, centered on its parent.
        static bool FitCharacter(GameObject go, Transform parent)
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            FixMaterials(go);

            if (!TryGetBounds(go, out Bounds bounds))
            {
                Object.DestroyImmediate(go);
                return false;
            }
            // Reject the broken size (tiny mesh or a skeleton tens of metres tall). A sane import is fitted below.
            if (bounds.size.y < 0.05f || bounds.size.y > 20f)
            {
                Object.DestroyImmediate(go);
                return false;
            }
            float s = Mathf.Min(CharacterHeight / bounds.size.y, CharacterFootprint / Mathf.Max(bounds.size.x, bounds.size.z, 0.0001f));
            go.transform.localScale *= s;
            go.transform.localPosition = new Vector3(-bounds.center.x * s, -bounds.min.y * s, -bounds.center.z * s);
            go.transform.SetParent(parent, false);
            return true;
        }

        static void BuildPenguin(Transform root, Mats m, out Transform leftArm, out Transform rightArm)
        {
            Prim(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.62f, 0f), new Vector3(0.78f, 0.7f, 0.7f), m.black, false);
            Prim(PrimitiveType.Sphere, "Belly", root, new Vector3(0f, 0.55f, 0.22f), new Vector3(0.5f, 0.72f, 0.28f), m.white, false);
            Prim(PrimitiveType.Sphere, "ButtL", root, new Vector3(-0.16f, 0.4f, -0.28f), Vector3.one * 0.36f, m.black, false);
            Prim(PrimitiveType.Sphere, "ButtR", root, new Vector3(0.16f, 0.4f, -0.28f), Vector3.one * 0.36f, m.black, false);
            Prim(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 1.22f, 0.06f), new Vector3(0.52f, 0.46f, 0.48f), m.black, false);
            var beak = MeshObject(ConeMesh(), "Beak", root, new Vector3(0f, 1.12f, 0.28f), new Vector3(0.14f, 0.28f, 0.14f), m.buoy);
            beak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "FootL", root, new Vector3(-0.16f, 0.05f, 0.2f), new Vector3(0.22f, 0.08f, 0.32f), m.buoy, false);
            Prim(PrimitiveType.Sphere, "FootR", root, new Vector3(0.16f, 0.05f, 0.2f), new Vector3(0.22f, 0.08f, 0.32f), m.buoy, false);
            leftArm = Flipper(root, -1f);
            rightArm = Flipper(root, 1f);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Sphere, "Eye", root, new Vector3(0.14f * s, 1.28f, 0.22f), Vector3.one * 0.2f, m.eyeWhite, false);
                Prim(PrimitiveType.Sphere, "Pupil", root, new Vector3(0.16f * s, 1.28f, 0.3f), Vector3.one * 0.08f, m.black, false);
            }
            root.gameObject.AddComponent<ButtWobble>();
        }

        static Transform Flipper(Transform root, float side)
        {
            var pivot = new GameObject(side < 0f ? "ArmL" : "ArmR").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0.34f * side, 0.85f, 0.02f);
            var flipper = Prim(PrimitiveType.Sphere, "Flipper", pivot, new Vector3(0.08f * side, -0.2f, 0.04f), new Vector3(0.14f, 0.34f, 0.24f),
                root.GetComponentInChildren<Renderer>().sharedMaterial, false);
            flipper.transform.localRotation = Quaternion.Euler(0f, 0f, 18f * side);
            return pivot;
        }
    }
}

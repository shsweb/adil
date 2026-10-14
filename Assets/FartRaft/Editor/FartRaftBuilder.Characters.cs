using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FartRaft.EditorTools
{
    // Playable characters. Gobkit animals (CC0, gobkit.itch.io) ship as .glb files in
    // Assets/FartRaft/Characters and need the glTFast package; without it only the classic
    // primitive animals are available.
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
                else if (!SpawnCharacterModel(info.model, root, skin))
                {
                    Object.DestroyImmediate(root.gameObject);
                    continue;
                }
                else
                {
                    models++;
                }
                root.gameObject.SetActive(false);
                skins[i] = skin;
            }

            if (loggedCharacters != models)
            {
                loggedCharacters = models;
                int wanted = CharacterRoster.All.Count(c => c.classic < 0);
                if (models < wanted)
                    Debug.LogWarning($"[Fart Raft] Only {models} of {wanted} character models loaded. Install the glTFast package " +
                                     "(Package Manager > + > Install package by name > com.unity.cloud.gltfast) and rebuild.");
                else
                    Debug.Log($"[Fart Raft] {models} character models loaded.");
            }
            return skins;
        }

        static bool SpawnCharacterModel(string file, Transform parent, CharacterSkin skin)
        {
            string path = CharactersFolder + "/" + file + ".glb";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            FixMaterials(go);

            if (TryGetBounds(go, out Bounds bounds) && bounds.size.y > 0.0001f)
            {
                float s = Mathf.Min(CharacterHeight / bounds.size.y, CharacterFootprint / Mathf.Max(bounds.size.x, bounds.size.z, 0.0001f));
                go.transform.localScale *= s;
                go.transform.localPosition = new Vector3(-bounds.center.x * s, -bounds.min.y * s, -bounds.center.z * s);
            }
            go.transform.SetParent(parent, false);

            skin.model = go;
            skin.clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
            return true;
        }
    }
}

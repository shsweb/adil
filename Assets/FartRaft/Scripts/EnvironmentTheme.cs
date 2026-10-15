using UnityEngine;

namespace FartRaft
{
    // Drag prefabs from any asset pack in here, then run "Fart Raft > 2. Build Game Scene".
    // Empty lists fall back to the built-in primitive props.
    [CreateAssetMenu(menuName = "Fart Raft/Environment Theme", fileName = "EnvironmentTheme")]
    public class EnvironmentTheme : ScriptableObject
    {
        public GameObject[] trees;
        public GameObject[] bushes;
        public GameObject[] rocks;
        public GameObject[] flowers;

        [Tooltip("Optional water material from an asset pack, applied to the wave mesh.")]
        public Material waterMaterial;

        [Tooltip("Optional ground material for the river banks.")]
        public Material grassMaterial;

        [Tooltip("Scale applied to every spawned prefab. Packs use different sizes.")]
        public float propScale = 1f;
    }
}

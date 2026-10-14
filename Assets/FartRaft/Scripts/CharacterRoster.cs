using UnityEngine;

namespace FartRaft
{
    // Every animal a player can pick. Models live in Assets/FartRaft/Characters/<model>.glb
    // (needs the glTFast package); "classic" ones are built from primitives and always exist.
    public static class CharacterRoster
    {
        public class Info
        {
            public string name;
            public string model;
            public int classic = -1;
            public float pitch = 1f;
            public Color puff;
        }

        public static readonly Info[] All =
        {
            new Info { name = "HIPPO", model = "Hippo", pitch = 0.72f, puff = new Color(0.62f, 0.95f, 0.1f) },
            new Info { name = "PIG", model = "Pig", pitch = 0.95f, puff = new Color(1f, 0.55f, 0.75f) },
            new Info { name = "PENGUIN", model = "Penguin", pitch = 1.25f, puff = new Color(0.6f, 0.9f, 1f) },
            new Info { name = "DUCK", model = "Duck", pitch = 1.4f, puff = new Color(1f, 0.9f, 0.25f) },
            new Info { name = "CORGI", model = "Corgi", pitch = 1.12f, puff = new Color(1f, 0.65f, 0.3f) },
            new Info { name = "FLAMINGO", model = "Flamingo", pitch = 1.3f, puff = new Color(1f, 0.4f, 0.6f) },
            new Info { name = "T-REX", model = "Trex", pitch = 0.65f, puff = new Color(0.7f, 0.45f, 1f) },
            new Info { name = "SEAL", model = "Seal", pitch = 0.85f, puff = new Color(0.55f, 0.75f, 0.95f) },
            new Info { name = "CAPY", classic = 0, pitch = 1f, puff = new Color(0.75f, 0.95f, 0.2f) },
            new Info { name = "CROC", classic = 2, pitch = 0.9f, puff = new Color(0.4f, 0.9f, 0.35f) },
            new Info { name = "GORILLA", classic = 3, pitch = 0.8f, puff = new Color(0.8f, 1f, 0.3f) },
        };

        static readonly int[] Defaults = { 0, 1, 2, 3 };
        static readonly int[] ClassicFallbacks = { 8, 9, 10, 8 };

        public static int Default(int player) => Defaults[player % Defaults.Length];
        public static int Fallback(int player) => ClassicFallbacks[player % ClassicFallbacks.Length];

        static string Key(int player) => "character_p" + (player + 1);

        public static int Load(int player) => Mathf.Clamp(PlayerPrefs.GetInt(Key(player), Default(player)), 0, All.Length - 1);

        public static void Save(int player, int index)
        {
            PlayerPrefs.SetInt(Key(player), index);
            PlayerPrefs.Save();
        }
    }
}

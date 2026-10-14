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
            new Info { name = "CAPY", classic = 0, pitch = 1f, puff = new Color(0.75f, 0.95f, 0.2f) },
            new Info { name = "PENGUIN", model = "Penguin", pitch = 1.25f, puff = new Color(0.6f, 0.9f, 1f) },
            new Info { name = "CROC", classic = 2, pitch = 0.9f, puff = new Color(0.4f, 0.9f, 0.35f) },
            new Info { name = "GORILLA", classic = 3, pitch = 0.75f, puff = new Color(0.8f, 1f, 0.3f) },
        };

        static readonly int[] Defaults = { 0, 1, 2, 3 };
        static readonly int[] ClassicFallbacks = { 0, 3, 2, 0 };

        public static int Default(int player) => Defaults[player % Defaults.Length];
        public static int Fallback(int player) => ClassicFallbacks[player % ClassicFallbacks.Length];

        static string Key(int player) => "character_v2_p" + (player + 1);

        public static int Load(int player) => Mathf.Clamp(PlayerPrefs.GetInt(Key(player), Default(player)), 0, All.Length - 1);

        public static void Save(int player, int index)
        {
            PlayerPrefs.SetInt(Key(player), index);
            PlayerPrefs.Save();
        }
    }
}

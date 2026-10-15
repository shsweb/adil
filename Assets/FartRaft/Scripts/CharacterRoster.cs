using UnityEngine;

namespace FartRaft
{
    // Every animal a player can pick. "classic" ones are built from primitives and always exist.
    // "prefab" ones are animated animals from the Quirky Series - FREE Animals Pack (Asset Store),
    // found by name when the pack is imported; without the pack they are skipped in the picker.
    public static class CharacterRoster
    {
        public class Info
        {
            public string name;
            public string model;
            public string prefab;
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
            new Info { name = "HIPPO", classic = 1, pitch = 0.85f, puff = new Color(0.55f, 0.75f, 1f) },
            new Info { name = "MONKEY", prefab = "colobus", pitch = 1.1f, puff = new Color(0.85f, 0.95f, 0.3f) },
            new Info { name = "DEER", prefab = "pudu", pitch = 1.2f, puff = new Color(1f, 0.8f, 0.45f) },
            new Info { name = "MUSKRAT", prefab = "muskrat", pitch = 1.05f, puff = new Color(0.7f, 0.9f, 0.35f) },
            new Info { name = "GECKO", prefab = "gecko", pitch = 1.3f, puff = new Color(0.45f, 0.95f, 0.6f) },
            new Info { name = "BIRDY", prefab = "sparrow", pitch = 1.5f, puff = new Color(0.65f, 0.85f, 1f) },
        };

        // The Quirky animals come first; without that pack each player falls back to a classic animal.
        static readonly int[] Defaults = { 5, 6, 7, 8 };
        static readonly int[] ClassicFallbacks = { 0, 3, 2, 0 };

        public static int Default(int player) => Defaults[player % Defaults.Length];
        public static int Fallback(int player) => ClassicFallbacks[player % ClassicFallbacks.Length];

        public static void ApplyPuff(ParticleSystem particles, int index)
        {
            if (particles == null) return;
            Color c = All[index].puff;
            var main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(c.r, c.g, c.b, 0.95f),
                new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), 0.85f));
        }

        static string Key(int player) => "character_v3_p" + (player + 1);

        public static int Load(int player) => Mathf.Clamp(PlayerPrefs.GetInt(Key(player), Default(player)), 0, All.Length - 1);

        public static void Save(int player, int index)
        {
            PlayerPrefs.SetInt(Key(player), index);
            PlayerPrefs.Save();
        }
    }
}

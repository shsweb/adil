using UnityEngine;

namespace FartRaft
{
    // Options from the SETTINGS window, saved between sessions.
    public static class GameSettings
    {
        const string MusicKey = "music_on";
        const string SoundKey = "sound_on";
        const string PlayersKey = "players";

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Players
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 2), 1, 4);
            set { PlayerPrefs.SetInt(PlayersKey, Mathf.Clamp(value, 1, 4)); PlayerPrefs.Save(); }
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    // Background music that survives stage loads and cross-fades between songs.
    public class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer instance;

        public static MusicPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Music");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<MusicPlayer>();
                }
                return instance;
            }
        }

        public float volume = 0.35f;
        public float fadeSpeed = 1.2f;

        AudioSource[] sources;
        int active;
        float targetPitch = 1f;
        bool silent;

        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].loop = true;
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0f;
            }
        }

        public void Play(AudioClip clip)
        {
            if (clip == null) return;
            targetPitch = 1f;
            silent = false;
            if (sources[active].clip == clip && sources[active].isPlaying) return;
            active = 1 - active;
            sources[active].clip = clip;
            sources[active].pitch = 1f;
            sources[active].volume = 0f;
            sources[active].Play();
        }

        public void Stop()
        {
            silent = true;
        }

        // Slightly faster music near the finish.
        public void SetTempo(float pitch) => targetPitch = pitch;

        void Update()
        {
            bool on = GameSettings.MusicOn;
            for (int i = 0; i < sources.Length; i++)
            {
                var s = sources[i];
                float target = on && !silent && i == active ? volume : 0f;
                s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * fadeSpeed * volume);
                if (i == active) s.pitch = Mathf.MoveTowards(s.pitch, targetPitch, Time.unscaledDeltaTime * 0.2f);
                if (s.volume <= 0f && s.isPlaying && (i != active || silent)) s.Stop();
            }
        }
    }
}

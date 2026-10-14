using UnityEngine;

namespace FartRaft
{
    // One-shot sound effects (bumps, crashes, falls, checkpoints, finish).
    [RequireComponent(typeof(AudioSource))]
    public class GameAudio : MonoBehaviour
    {
        public AudioClip bump;
        public AudioClip punch;
        public AudioClip explosion;
        public AudioClip fall;
        public AudioClip splash;
        public AudioClip checkpoint;
        public AudioClip cheer;

        AudioSource source;
        float nextBumpTime;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        public void Bump(float strength)
        {
            if (Time.time < nextBumpTime) return;
            nextBumpTime = Time.time + 0.25f;
            Play(bump, Mathf.Clamp(0.35f + strength * 0.08f, 0.35f, 0.9f), Random.Range(0.9f, 1.15f));
        }

        public void Crash()
        {
            Play(punch, 1f, 1f);
            Play(explosion, 0.8f, Random.Range(0.95f, 1.05f));
        }

        public void Fall() => Play(fall, 0.8f, 1f);
        public void Splash() => Play(splash, 0.6f, Random.Range(0.9f, 1.1f));
        public void Checkpoint() => Play(checkpoint, 0.8f, 1f);
        public void Cheer() => Play(cheer, 0.8f, 1f);

        void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}

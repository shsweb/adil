using UnityEngine;

namespace FartRaft
{
    // Procedurally generated looping fart, so the project needs no audio files.
    // Replace with a real recording by assigning a clip to the seat AudioSources.
    public static class FartSound
    {
        static AudioClip clip;

        public static AudioClip Clip
        {
            get
            {
                if (clip == null) clip = Create();
                return clip;
            }
        }

        static AudioClip Create()
        {
            const int sampleRate = 22050;
            int count = sampleRate;
            var data = new float[count];
            var random = new System.Random(7);
            float phase = 0f;
            float brown = 0f;
            float filtered = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                // Whole-number modulation rates keep the 1 second loop seamless.
                float frequency = 70f + 25f * Mathf.Sin(2f * Mathf.PI * 3f * t) + 10f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                phase += frequency / sampleRate;
                if (phase > 1f) phase -= 1f;

                float pulse = phase < 0.25f ? 1f : -0.33f;
                brown = Mathf.Clamp((brown + ((float)random.NextDouble() * 2f - 1f) * 0.08f) * 0.995f, -1f, 1f);
                float raw = pulse * 0.55f + brown * 0.6f;
                filtered += (raw - filtered) * 0.25f;
                data[i] = filtered * 0.6f;
            }

            var result = AudioClip.Create("ProceduralFart", count, 1, sampleRate, false);
            result.SetData(data, 0);
            return result;
        }
    }
}

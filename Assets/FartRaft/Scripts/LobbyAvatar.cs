using UnityEngine;

namespace FartRaft
{
    // A player's animal standing in the lobby. Idles, farts now and then, and dances when picked.
    public class LobbyAvatar : MonoBehaviour
    {
        public CharacterSkin[] skins;
        public Transform body;
        public ParticleSystem puff;
        public AudioSource fartAudio;
        public AudioClip[] fartClips;

        public int CharacterIndex { get; private set; } = -1;

        CharacterSkin skin;
        float cheerUntil = -100f;
        float nextIdleFart;
        float phase;

        void Awake()
        {
            phase = Random.value * 10f;
            nextIdleFart = Time.time + Random.Range(2f, 6f);
        }

        public bool HasCharacter(int index) =>
            skins != null && index >= 0 && index < skins.Length && skins[index] != null;

        public void SetCharacter(int index)
        {
            if (!HasCharacter(index)) return;
            CharacterIndex = index;
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] != null) skins[i].gameObject.SetActive(i == index);
            }
            skin = skins[index];
            CharacterRoster.ApplyPuff(puff, index);
        }

        public void Cheer()
        {
            cheerUntil = Time.time + 1.2f;
            if (skin != null) skin.Cheer();
            Fart(14, 1f);
        }

        void Fart(int amount, float volume)
        {
            if (puff != null) puff.Emit(amount);
            if (fartAudio == null || fartClips == null || fartClips.Length == 0 || !GameSettings.SoundOn || CharacterIndex < 0) return;
            fartAudio.pitch = Random.Range(0.95f, 1.05f) * CharacterRoster.All[CharacterIndex].pitch;
            fartAudio.PlayOneShot(fartClips[Random.Range(0, fartClips.Length)], volume);
        }

        void Update()
        {
            float t = Time.time;
            if (t > nextIdleFart)
            {
                nextIdleFart = t + Random.Range(5f, 10f);
                Fart(6, 0.35f);
            }

            if (body == null) return;
            bool cheering = t < cheerUntil;
            float hop = cheering ? Mathf.Abs(Mathf.Sin((cheerUntil - t) * 10f)) * 0.5f : 0f;
            float breathe = Mathf.Sin(t * 2.2f + phase) * 0.025f;
            body.localPosition = new Vector3(0f, hop, 0f);
            body.localScale = new Vector3(1f - breathe, 1f + breathe, 1f - breathe);
            body.localRotation = Quaternion.Euler(0f, cheering ? Mathf.Sin((cheerUntil - t) * 12f) * 25f : Mathf.Sin(t * 0.7f + phase) * 8f, 0f);
        }
    }
}

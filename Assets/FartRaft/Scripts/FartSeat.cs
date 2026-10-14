using UnityEngine;

namespace FartRaft
{
    // One player sitting on the raft. While held, the player farts and pushes the
    // raft from their seat position, so off-center players also turn the raft.
    public class FartSeat : MonoBehaviour
    {
        public int playerIndex;
        public Raft raft;
        public Transform character;
        public ParticleSystem fartParticles;
        public AudioSource fartAudio;

        [Header("Fart power")]
        public float thrust = 100f;
        public float thrustScale = 1f;
        public float maxAimAngle = 55f;
        public float aimSpeed = 260f;

        [Header("Gas tank")]
        public float maxGas = 100f;
        public float drainPerSecond = 30f;
        public float refillPerSecond = 20f;
        public float restartThreshold = 25f;

        [HideInInspector] public bool locked = true;

        public bool TouchHeld { get; set; }
        public bool KeyHeld { get; set; }
        public float AimInput { get; set; }
        public float BoostMultiplier { get; set; } = 1f;

        public bool IsFarting { get; private set; }
        public bool IsExhausted => exhausted;
        public float Gas01 => gas / maxGas;

        float gas;
        bool exhausted;
        float aim;
        float squash;

        void Awake()
        {
            gas = maxGas;
            if (fartAudio != null)
            {
                fartAudio.clip = FartSound.Clip;
                fartAudio.loop = true;
                fartAudio.volume = 0f;
                fartAudio.pitch = 0.85f + 0.1f * playerIndex;
            }
        }

        void OnEnable()
        {
            if (fartAudio != null && fartAudio.clip != null && !fartAudio.isPlaying) fartAudio.Play();
        }

        public void ResetGas()
        {
            gas = maxGas;
            exhausted = false;
        }

        void FixedUpdate()
        {
            if (exhausted && gas >= restartThreshold) exhausted = false;

            IsFarting = (TouchHeld || KeyHeld) && !locked && !exhausted;

            if (IsFarting)
            {
                gas -= drainPerSecond * Time.fixedDeltaTime;
                if (gas <= 0f)
                {
                    gas = 0f;
                    exhausted = true;
                    IsFarting = false;
                }
            }
            else
            {
                gas = Mathf.Min(maxGas, gas + refillPerSecond * Time.fixedDeltaTime);
            }

            if (IsFarting && raft != null)
            {
                Vector3 direction = raft.transform.rotation * Quaternion.Euler(0f, aim, 0f) * Vector3.forward;
                Vector3 point = transform.position;
                point.y = raft.transform.position.y;
                raft.Body.AddForceAtPosition(direction * (thrust * thrustScale * BoostMultiplier), point, ForceMode.Force);
            }
        }

        void Update()
        {
            aim = Mathf.MoveTowards(aim, AimInput * maxAimAngle, aimSpeed * Time.deltaTime);
            squash = Mathf.MoveTowards(squash, IsFarting ? 1f : 0f, Time.deltaTime * 6f);

            if (character != null)
            {
                character.localRotation = Quaternion.Euler(0f, aim, 0f);
                float wobble = IsFarting ? Mathf.Sin(Time.time * 40f) * 0.05f : 0f;
                character.localScale = new Vector3(1f + squash * 0.12f + wobble, 1f - squash * 0.15f, 1f + squash * 0.12f - wobble);
            }

            if (fartParticles != null)
            {
                var emission = fartParticles.emission;
                emission.enabled = IsFarting;
                if (IsFarting && !fartParticles.isPlaying) fartParticles.Play();
            }

            if (fartAudio != null)
            {
                fartAudio.volume = Mathf.MoveTowards(fartAudio.volume, IsFarting ? 0.7f : 0f, Time.deltaTime * 6f);
            }
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    // One player standing on the rim of the round raft, facing the center.
    // Farting blasts gas outward and pushes the raft toward the opposite side,
    // so players walk around the rim to choose the direction.
    public class FartSeat : MonoBehaviour
    {
        public int playerIndex;
        public Raft raft;
        public Transform character;
        public Transform leftArm;
        public Transform rightArm;
        public ParticleSystem fartParticles;
        public AudioSource fartAudio;

        [Header("Position on the rim")]
        public float rimRadius = 2.1f;
        public float standHeight = 0.42f;
        [Tooltip("Degrees around the raft. 0 = raft front, 180 = back.")]
        public float angle = 180f;
        public float walkSpeed = 140f;

        [Header("Fart power")]
        public float thrust = 80f;
        public float thrustScale = 1f;

        [Header("Gas tank")]
        public float maxGas = 100f;
        public float drainPerSecond = 30f;
        public float refillPerSecond = 22f;
        public float restartThreshold = 25f;

        [HideInInspector] public bool locked = true;

        public bool TouchHeld { get; set; }
        public bool KeyHeld { get; set; }
        public float MoveInput { get; set; }
        public float KeyMoveInput { get; set; }
        public float BoostMultiplier { get; set; } = 1f;

        public bool IsFarting { get; private set; }
        public bool IsExhausted => exhausted;
        public float Gas01 => gas / maxGas;

        float gas;
        bool exhausted;
        bool wasFarting;
        float lean;

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
            PlaceOnRim();
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

        public void SetAngle(float degrees)
        {
            angle = degrees;
            PlaceOnRim();
        }

        void PlaceOnRim()
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            transform.localPosition = outward * rimRadius + Vector3.up * standHeight;
            transform.localRotation = Quaternion.LookRotation(-outward);
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
                Vector3 towardCenter = raft.transform.position - transform.position;
                towardCenter.y = 0f;
                if (towardCenter.sqrMagnitude > 0.0001f)
                {
                    raft.Body.AddForce(towardCenter.normalized * (thrust * thrustScale * BoostMultiplier), ForceMode.Force);
                }
            }
        }

        void Update()
        {
            float move = Mathf.Clamp(MoveInput + KeyMoveInput, -1f, 1f);
            if (!locked && Mathf.Abs(move) > 0.05f)
            {
                angle = Mathf.Repeat(angle - move * walkSpeed * Time.deltaTime, 360f);
            }
            PlaceOnRim();

            Animate();

            if (fartParticles != null)
            {
                var emission = fartParticles.emission;
                emission.enabled = IsFarting;
                if (IsFarting && !wasFarting) fartParticles.Emit(6);
            }
            wasFarting = IsFarting;

            if (fartAudio != null)
            {
                fartAudio.volume = Mathf.MoveTowards(fartAudio.volume, IsFarting ? 0.7f : 0f, Time.deltaTime * 6f);
            }
        }

        void Animate()
        {
            if (character == null) return;

            bool airborne = raft != null && !raft.Grounded;
            float t = Time.time;
            lean = Mathf.MoveTowards(lean, IsFarting ? 1f : 0f, Time.deltaTime * 6f);

            float wobble = IsFarting ? Mathf.Sin(t * 38f) * 0.05f : 0f;
            float idle = Mathf.Sin(t * 3f + playerIndex) * 0.02f;
            character.localRotation = Quaternion.Euler(lean * 22f, 0f, 0f);
            character.localScale = new Vector3(1f + lean * 0.1f + wobble, 1f - lean * 0.12f + idle, 1f + lean * 0.1f - wobble);

            float armSwing;
            if (airborne) armSwing = Mathf.Sin(t * 22f + playerIndex) * 70f;
            else if (IsFarting) armSwing = -35f;
            else armSwing = Mathf.Sin(t * 2.5f + playerIndex) * 8f;

            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(armSwing, 0f, airborne ? 40f : 10f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(airborne ? -armSwing : armSwing, 0f, airborne ? -40f : -10f);
        }
    }
}

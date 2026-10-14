using System.Collections.Generic;
using UnityEngine;

namespace FartRaft
{
    // One player standing on the rim of the round raft, facing the center.
    // The player points a joystick where they want the raft to go: the animal
    // runs around the rim to the opposite side and farts, pushing the raft that way.
    public class FartSeat : MonoBehaviour
    {
        static readonly List<FartSeat> Active = new List<FartSeat>();

        public int playerIndex;
        public Raft raft;
        public Transform character;
        public Transform leftArm;
        public Transform rightArm;
        public ParticleSystem fartParticles;
        public AudioSource fartAudio;
        public AudioClip[] fartClips;

        [Header("Position on the rim")]
        public float rimRadius = 2.1f;
        public float standHeight = 0.42f;
        [Tooltip("Degrees around the raft. 0 = raft front, 180 = back.")]
        public float angle = 180f;
        public float runSpeed = 600f;
        [Tooltip("Players can only fart once they are this close (degrees) to the right spot.")]
        public float aimTolerance = 50f;
        public float minGap = 32f;
        [Tooltip("Jump straight to the right spot instead of running there (Easy mode).")]
        public bool instantAim;

        [Header("Fart power")]
        [Tooltip("Acceleration (m/s²) one player gives the raft.")]
        public float thrust = 3f;
        public float thrustScale = 1f;
        [Tooltip("Instant speed kick (m/s) when a fart starts, so the raft reacts right away.")]
        public float startKick = 0.8f;
        public float stickDeadZone = 0.2f;

        [Header("Gas tank")]
        public float maxGas = 100f;
        public float drainPerSecond = 22f;
        public float refillPerSecond = 26f;
        public float restartThreshold = 25f;

        [HideInInspector] public bool locked = true;

        public Vector2 StickInput { get; set; }
        public Vector2 KeyStickInput { get; set; }
        public float BoostMultiplier { get; set; } = 1f;

        public bool IsFarting { get; private set; }
        public bool WantsToFart { get; private set; }
        public bool IsExhausted => exhausted;
        public float Gas01 => gas / maxGas;

        float gas;
        bool exhausted;
        bool wasFarting;
        bool fartingLastStep;
        bool aligned;
        float power;
        float lean;
        Camera cam;

        void Awake()
        {
            gas = maxGas;
            if (fartAudio != null)
            {
                fartAudio.playOnAwake = false;
                fartAudio.loop = fartClips == null || fartClips.Length == 0;
                if (fartAudio.loop) fartAudio.clip = FartSound.Clip;
            }
            PlaceOnRim();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

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

            IsFarting = WantsToFart && aligned && !locked && !exhausted;

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

            bool started = IsFarting && !fartingLastStep;
            fartingLastStep = IsFarting;
            if (!IsFarting || raft == null || raft.Body.isKinematic) return;

            Vector3 towardCenter = raft.transform.position - transform.position;
            towardCenter.y = 0f;
            if (towardCenter.sqrMagnitude < 0.0001f) return;
            towardCenter.Normalize();

            if (started) raft.Body.AddForce(towardCenter * startKick, ForceMode.VelocityChange);
            raft.Body.AddForce(towardCenter * (thrust * thrustScale * BoostMultiplier * power), ForceMode.Acceleration);
        }

        void Update()
        {
            Steer();
            KeepApart();
            PlaceOnRim();
            Animate();
            UpdateEffects();
        }

        void Steer()
        {
            Vector2 stick = Vector2.ClampMagnitude(StickInput + KeyStickInput, 1f);
            WantsToFart = !locked && stick.magnitude > stickDeadZone;
            if (!WantsToFart || raft == null)
            {
                aligned = false;
                return;
            }

            if (cam == null) cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // The stick says where the raft should go; stand on the opposite side to push it there.
            Vector3 moveDirection = (right * stick.x + forward * stick.y).normalized;
            Vector3 localOutward = raft.transform.InverseTransformDirection(-moveDirection);
            float targetAngle = Mathf.Atan2(localOutward.x, localOutward.z) * Mathf.Rad2Deg;

            if (instantAim)
            {
                angle = Mathf.MoveTowardsAngle(angle, targetAngle, runSpeed * 3f * Time.deltaTime);
                aligned = true;
            }
            else
            {
                angle = Mathf.MoveTowardsAngle(angle, targetAngle, runSpeed * Time.deltaTime);
                aligned = Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle)) < aimTolerance;
            }
            power = Mathf.InverseLerp(stickDeadZone, 1f, stick.magnitude) * 0.5f + 0.5f;
        }

        void KeepApart()
        {
            foreach (var other in Active)
            {
                if (other == this || other.raft != raft) continue;
                float delta = Mathf.DeltaAngle(other.angle, angle);
                if (Mathf.Abs(delta) >= minGap) continue;
                float push = (minGap - Mathf.Abs(delta)) * 0.5f;
                angle += delta >= 0f ? push : -push;
            }
            angle = Mathf.Repeat(angle, 360f);
        }

        void Animate()
        {
            if (character == null) return;

            bool airborne = raft != null && !raft.Grounded;
            float t = Time.time;
            lean = Mathf.MoveTowards(lean, IsFarting ? 1f : 0f, Time.deltaTime * 8f);

            float wobble = IsFarting ? Mathf.Sin(t * 38f) * 0.05f : 0f;
            float idle = Mathf.Sin(t * 3f + playerIndex) * 0.02f;
            float run = WantsToFart && !aligned ? Mathf.Abs(Mathf.Sin(t * 25f)) * 0.08f : 0f;
            character.localPosition = new Vector3(0f, run, 0f);
            character.localRotation = Quaternion.Euler(lean * 25f, 0f, 0f);
            character.localScale = new Vector3(1f + lean * 0.1f + wobble, 1f - lean * 0.12f + idle, 1f + lean * 0.1f - wobble);

            float armSwing;
            if (airborne) armSwing = Mathf.Sin(t * 22f + playerIndex) * 70f;
            else if (IsFarting) armSwing = -40f;
            else if (run > 0f) armSwing = Mathf.Sin(t * 25f) * 50f;
            else armSwing = Mathf.Sin(t * 2.5f + playerIndex) * 8f;

            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(armSwing, 0f, airborne ? 40f : 10f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(airborne || run > 0f ? -armSwing : armSwing, 0f, airborne ? -40f : -10f);
        }

        void UpdateEffects()
        {
            if (fartParticles != null)
            {
                var emission = fartParticles.emission;
                emission.enabled = IsFarting;
                if (IsFarting && !wasFarting) fartParticles.Emit(8);
            }

            if (fartAudio != null)
            {
                if (fartAudio.loop)
                {
                    if (!fartAudio.isPlaying) fartAudio.Play();
                    fartAudio.volume = Mathf.MoveTowards(fartAudio.volume, IsFarting ? 0.7f : 0f, Time.deltaTime * 6f);
                }
                else if (IsFarting)
                {
                    if (!fartAudio.isPlaying || !wasFarting)
                    {
                        fartAudio.clip = fartClips[Random.Range(0, fartClips.Length)];
                        fartAudio.pitch = Random.Range(0.92f, 1.12f) + playerIndex * 0.06f;
                        fartAudio.volume = 1f;
                        fartAudio.Play();
                    }
                }
                else if (fartAudio.isPlaying)
                {
                    fartAudio.volume = Mathf.MoveTowards(fartAudio.volume, 0f, Time.deltaTime * 8f);
                    if (fartAudio.volume <= 0f) fartAudio.Stop();
                }
            }

            wasFarting = IsFarting;
        }
    }
}

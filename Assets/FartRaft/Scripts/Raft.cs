using UnityEngine;

namespace FartRaft
{
    // Round inflatable raft sliding down the water slide. Yaw is free (it spins),
    // tilt is locked on the Rigidbody for stability and faked on the visual,
    // which follows the slope of the track underneath.
    [RequireComponent(typeof(Rigidbody))]
    public class Raft : MonoBehaviour
    {
        public Transform visual;

        [Header("Water drag (only horizontal, so falling and slopes stay fast)")]
        public float linearDrag = 0.18f;
        public float quadraticDrag = 0.012f;
        public float angularDamping = 1.2f;
        [Tooltip("Extra pull into the slide surface so the raft does not hop over bumps.")]
        public float stickAcceleration = 8f;

        [Header("Easy-mode helpers (0 / off = original physics)")]
        [Tooltip("Constant push (m/s²) down the track, so players only need to steer.")]
        public float autoForward;
        [Tooltip("Horizontal speed limit in m/s. 0 = no limit.")]
        public float maxSpeed;
        [Tooltip("Keep the raft facing down the track instead of spinning.")]
        public bool lockYaw;
        [Tooltip("Extra drag across the track, so the raft stops sliding sideways when you let go.")]
        public float sideDrag;

        public Vector3 TrackForward { get; set; } = Vector3.forward;

        float autoForwardPausedUntil;

        // Stops the auto-forward push for a moment, so a bounce does not carry the raft straight back into the obstacle.
        public void PauseAutoForward(float seconds)
        {
            autoForwardPausedUntil = Time.time + seconds;
        }

        [Header("Look")]
        public float bobHeight = 0.05f;
        public float bobSpeed = 2.2f;
        public float groundCheckDistance = 1.2f;
        public ParticleSystem wake;

        Rigidbody body;
        Vector3 groundNormal = Vector3.up;
        float airTime;

        public bool Grounded { get; private set; }

        public Rigidbody Body
        {
            get
            {
                if (body == null) body = GetComponent<Rigidbody>();
                return body;
            }
        }

        public static bool IsRaft(Collider other)
        {
            return other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<Raft>() != null;
        }

        void Awake()
        {
            Body.useGravity = true;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PhysicsCompat.SetDamping(Body, 0f, angularDamping);
        }

        void FixedUpdate()
        {
            // The ray starts inside the raft collider, so it never hits the raft itself.
            bool wasGrounded = Grounded;
            Grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit,
                groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);
            groundNormal = Grounded ? hit.normal : Vector3.up;

            if (Grounded && !wasGrounded && airTime > 0.35f && GameManager.Instance != null) GameManager.Instance.Audio.Splash();
            airTime = Grounded ? 0f : airTime + Time.fixedDeltaTime;

            if (Body.isKinematic) return;

            Vector3 velocity = PhysicsCompat.GetVelocity(Body);
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            Body.AddForce(-flat * (linearDrag + quadraticDrag * speed), ForceMode.Acceleration);
            if (maxSpeed > 0f && speed > maxSpeed) Body.AddForce(-flat / speed * ((speed - maxSpeed) * 4f), ForceMode.Acceleration);

            if (Grounded)
            {
                Body.AddForce(-groundNormal * stickAcceleration, ForceMode.Acceleration);
                if (autoForward > 0f && Time.time >= autoForwardPausedUntil) Body.AddForce(TrackForward * autoForward, ForceMode.Acceleration);
                if (sideDrag > 0f)
                {
                    Vector3 side = flat - TrackForward * Vector3.Dot(flat, TrackForward);
                    Body.AddForce(-side * sideDrag, ForceMode.Acceleration);
                }
            }

            if (lockYaw)
            {
                float targetYaw = Mathf.Atan2(TrackForward.x, TrackForward.z) * Mathf.Rad2Deg;
                float error = Mathf.DeltaAngle(Body.rotation.eulerAngles.y, targetYaw);
                Body.angularVelocity = new Vector3(0f, error * Mathf.Deg2Rad * 4f, 0f);
            }
        }

        // Knock the raft away from a contact point (bumpers, and spiky things in Easy mode).
        public void Bounce(Vector3 point, float strength)
        {
            Vector3 away = transform.position - point;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = -transform.forward;
            Body.AddForce(away.normalized * strength, ForceMode.VelocityChange);
        }

        void Update()
        {
            if (wake != null)
            {
                var emission = wake.emission;
                emission.enabled = Grounded && !Body.isKinematic;
            }

            if (visual == null) return;

            float t = Time.time * bobSpeed;
            Quaternion bob = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 2f, 0f, Mathf.Sin(t * 1.3f) * 2f);
            Quaternion target;
            if (Grounded)
            {
                Vector3 localNormal = transform.InverseTransformDirection(groundNormal);
                target = Quaternion.FromToRotation(Vector3.up, localNormal) * bob;
            }
            else
            {
                Vector3 localVelocity = transform.InverseTransformDirection(PhysicsCompat.GetVelocity(Body));
                target = Quaternion.Euler(Mathf.Clamp(localVelocity.z, -15f, 15f), 0f, Mathf.Clamp(-localVelocity.x, -15f, 15f));
            }

            visual.localPosition = new Vector3(0f, Grounded ? Mathf.Sin(t) * bobHeight : 0f, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void OnCollisionEnter(Collision collision)
        {
            var manager = GameManager.Instance;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            if (collision.collider.GetComponentInParent<Hazard>() != null)
            {
                if (manager != null) manager.OnHazard(point);
                return;
            }

            var bumper = collision.collider.GetComponentInParent<Bumper>();
            if (bumper == null) return;

            Bounce(point, bumper.bounce);

            if (manager != null)
            {
                float strength = collision.relativeVelocity.magnitude;
                manager.Audio.Bump(strength);
                if (strength > 3f) manager.PlayImpact(point, 8);
            }
        }

        // Frozen during menus, countdowns and crashes so it does not drift down the slope.
        public void SetFrozen(bool frozen)
        {
            if (frozen && !Body.isKinematic)
            {
                PhysicsCompat.SetVelocity(Body, Vector3.zero);
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = frozen;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            visual.localRotation = Quaternion.identity;
            if (wake != null) wake.Clear();
            if (Body.isKinematic) return;
            PhysicsCompat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
        }
    }
}

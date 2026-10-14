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
        public float linearDamping = 0.6f;
        public float angularDamping = 2.5f;
        public float bobHeight = 0.05f;
        public float bobSpeed = 2.2f;
        public float groundCheckDistance = 1.2f;
        public float impactEffectSpeed = 4f;

        Rigidbody body;

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
            PhysicsCompat.SetDamping(Body, linearDamping, angularDamping);
        }

        void Update()
        {
            if (visual == null) return;

            // The ray starts inside the raft collider, so it never hits the raft itself.
            Grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit,
                groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);

            float t = Time.time * bobSpeed;
            Quaternion bob = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 2f, 0f, Mathf.Sin(t * 1.3f) * 2f);
            Quaternion target = bob;
            if (Grounded)
            {
                Vector3 localNormal = transform.InverseTransformDirection(hit.normal);
                target = Quaternion.FromToRotation(Vector3.up, localNormal) * bob;
            }
            else
            {
                Vector3 localVelocity = transform.InverseTransformDirection(PhysicsCompat.GetVelocity(Body));
                target = Quaternion.Euler(Mathf.Clamp(localVelocity.z, -15f, 15f), 0f, Mathf.Clamp(-localVelocity.x, -15f, 15f));
            }

            visual.localPosition = new Vector3(0f, Grounded ? Mathf.Sin(t) * bobHeight : 0f, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, Time.deltaTime * 8f);
        }

        void OnCollisionEnter(Collision collision)
        {
            var bumper = collision.collider.GetComponentInParent<Bumper>();
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            if (bumper != null)
            {
                Vector3 away = transform.position - point;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f) away = -transform.forward;
                Body.AddForce(away.normalized * bumper.bounce, ForceMode.VelocityChange);
            }

            if ((bumper != null || collision.relativeVelocity.magnitude > impactEffectSpeed) && GameManager.Instance != null)
            {
                GameManager.Instance.PlayImpact(point);
            }
        }

        // Frozen during menus and countdowns so it does not drift down the slope.
        public void SetFrozen(bool frozen)
        {
            Body.isKinematic = frozen;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (Body.isKinematic) return;
            PhysicsCompat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    // The raft slides on a flat water plane: height and tilt are locked on the
    // Rigidbody so the physics stays stable on mobile, and the bobbing/tilting is
    // purely visual on the child "visual" transform.
    [RequireComponent(typeof(Rigidbody))]
    public class Raft : MonoBehaviour
    {
        public Transform visual;
        public float linearDamping = 0.8f;
        public float angularDamping = 1.5f;
        public float bobHeight = 0.08f;
        public float bobSpeed = 1.6f;
        public float maxTilt = 6f;

        Rigidbody body;

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
            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezeRotationX
                               | RigidbodyConstraints.FreezeRotationZ;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PhysicsCompat.SetDamping(Body, linearDamping, angularDamping);
        }

        void Update()
        {
            if (visual == null) return;

            float t = Time.time * bobSpeed;
            Vector3 localVelocity = transform.InverseTransformDirection(PhysicsCompat.GetVelocity(Body));
            float pitch = Mathf.Clamp(-localVelocity.z * 0.6f, -maxTilt, maxTilt) + Mathf.Sin(t * 0.8f) * 1.5f;
            float roll = Mathf.Clamp(localVelocity.x * 0.8f, -maxTilt, maxTilt) + Mathf.Sin(t * 1.3f) * 1.5f;

            visual.localPosition = new Vector3(0f, Mathf.Sin(t) * bobHeight, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(pitch, 0f, roll), Time.deltaTime * 5f);
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            PhysicsCompat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
        }
    }
}

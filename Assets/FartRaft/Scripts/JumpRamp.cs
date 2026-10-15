using UnityEngine;

namespace FartRaft
{
    // A wedge on the slide. A fast raft hops off the lip. A slow raft just climbs it.
    public class JumpRamp : MonoBehaviour
    {
        public float boost = 8f;

        void OnCollisionEnter(Collision collision)
        {
            var raft = collision.collider.GetComponentInParent<Raft>();
            if (raft == null) return;
            Vector3 velocity = PhysicsCompat.GetVelocity(raft.Body);
            float speed = Vector3.Dot(velocity, transform.forward);
            if (speed < 3f) return;
            raft.Body.AddForce(Vector3.up * boost + transform.forward * 2f, ForceMode.VelocityChange);
        }
    }
}

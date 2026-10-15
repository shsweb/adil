using UnityEngine;

namespace FartRaft
{
    // Deadly obstacle: touching it blows up the run and respawns the raft at the
    // last checkpoint. Works for solid colliders (see Raft.OnCollisionEnter) and triggers.
    public class Hazard : MonoBehaviour
    {
        // Physics layer of every hazard collider (set by the builder), so the raft can pass through them in Easy mode after an OUCH.
        public const int Layer = 8;

        void OnTriggerEnter(Collider other)
        {
            var raft = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Raft>() : null;
            if (raft != null && GameManager.Instance != null) GameManager.Instance.OnHazard(raft, other.transform.position);
        }
    }
}

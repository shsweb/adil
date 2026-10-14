using UnityEngine;

namespace FartRaft
{
    // Deadly obstacle: touching it blows up the run and respawns the raft at the
    // last checkpoint. Works for solid colliders (see Raft.OnCollisionEnter) and triggers.
    public class Hazard : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (Raft.IsRaft(other) && GameManager.Instance != null) GameManager.Instance.OnHazard(other.transform.position);
        }
    }
}

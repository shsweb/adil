using UnityEngine;

namespace FartRaft
{
    // Touching a hazard sends the raft back to the last checkpoint.
    public class Hazard : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (Raft.IsRaft(other) && GameManager.Instance != null) GameManager.Instance.OnHazard();
        }
    }
}

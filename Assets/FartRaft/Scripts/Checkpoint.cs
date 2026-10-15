using UnityEngine;

namespace FartRaft
{
    public class Checkpoint : MonoBehaviour
    {
        public int index;

        void OnTriggerEnter(Collider other)
        {
            var raft = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Raft>() : null;
            if (raft != null && GameManager.Instance != null) GameManager.Instance.OnCheckpoint(this, raft);
        }
    }
}

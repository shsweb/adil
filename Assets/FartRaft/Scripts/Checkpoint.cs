using UnityEngine;

namespace FartRaft
{
    public class Checkpoint : MonoBehaviour
    {
        public int index;

        void OnTriggerEnter(Collider other)
        {
            if (Raft.IsRaft(other) && GameManager.Instance != null) GameManager.Instance.OnCheckpoint(this);
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    public class FinishLine : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var raft = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Raft>() : null;
            if (raft != null && GameManager.Instance != null) GameManager.Instance.OnFinish(raft);
        }
    }
}

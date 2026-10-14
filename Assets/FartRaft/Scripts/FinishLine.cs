using UnityEngine;

namespace FartRaft
{
    public class FinishLine : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (Raft.IsRaft(other) && GameManager.Instance != null) GameManager.Instance.OnFinish();
        }
    }
}

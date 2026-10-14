using UnityEngine;

namespace FartRaft
{
    // Marks an obstacle that knocks the raft away on contact. The raft applies
    // the push itself (see Raft.OnCollisionEnter), so this works on child colliders.
    public class Bumper : MonoBehaviour
    {
        public float bounce = 6f;
    }
}

using UnityEngine;

namespace FartRaft
{
    // Round island used by Raft Rumble. Fall off and you lose.
    public class RumbleArena : MonoBehaviour
    {
        public float radius = 11f;
        public Transform spawnA;
        public Transform spawnB;

        public Vector3 Center => transform.position;

        public bool IsOff(Vector3 position)
        {
            Vector3 flat = position - Center;
            flat.y = 0f;
            return flat.magnitude > radius || position.y < Center.y - 2.5f;
        }
    }
}

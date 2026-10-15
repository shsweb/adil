using UnityEngine;

namespace FartRaft
{
    // Gentle up-and-down motion for lobby props (pool floats, the little raft).
    public class Bob : MonoBehaviour
    {
        public float height = 0.1f;
        public float period = 2.4f;
        public float phase;

        Vector3 start;

        void Start()
        {
            start = transform.localPosition;
        }

        void Update()
        {
            float y = Mathf.Sin((Time.time / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f) * height;
            var p = start;
            p.y += y;
            transform.localPosition = p;
        }
    }
}

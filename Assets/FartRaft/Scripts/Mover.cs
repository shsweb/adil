using UnityEngine;

namespace FartRaft
{
    [RequireComponent(typeof(Rigidbody))]
    public class Mover : MonoBehaviour
    {
        public Vector3 offset = new Vector3(6f, 0f, 0f);
        public float period = 3f;
        [Range(0f, 1f)] public float phase;

        Rigidbody body;
        Vector3 start;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            start = transform.position;
        }

        void FixedUpdate()
        {
            float t = (Mathf.Sin((Time.time / period + phase) * Mathf.PI * 2f) + 1f) * 0.5f;
            body.MovePosition(start + offset * t);
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    // Swings around its local Z axis. Place the pivot at the top and hang the
    // swinging parts below it as children.
    [RequireComponent(typeof(Rigidbody))]
    public class Pendulum : MonoBehaviour
    {
        public float amplitude = 55f;
        public float period = 2.6f;
        [Range(0f, 1f)] public float phase;

        Rigidbody body;
        Quaternion baseRotation;
        float clock;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            baseRotation = transform.rotation;
        }

        void FixedUpdate()
        {
            clock += Time.fixedDeltaTime * GameManager.ObstacleSpeed;
            float angle = amplitude * Mathf.Sin((clock / period + phase) * Mathf.PI * 2f);
            body.MoveRotation(baseRotation * Quaternion.Euler(0f, 0f, angle));
        }
    }
}

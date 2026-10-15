using UnityEngine;

namespace FartRaft
{
    // Boxing glove on a spring: waits inside the wall, punches out across the slide, then slowly pulls back.
    [RequireComponent(typeof(Rigidbody))]
    public class Puncher : MonoBehaviour
    {
        [Tooltip("World-space offset from the wall to the fully punched-out position.")]
        public Vector3 punch = new Vector3(-5f, 0f, 0f);
        public float period = 3f;
        [Range(0f, 1f)] public float phase;

        Rigidbody body;
        Vector3 home;
        float clock;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            home = transform.position;
        }

        void FixedUpdate()
        {
            clock += Time.fixedDeltaTime * GameManager.ObstacleSpeed;
            float t = Mathf.Repeat(clock / period + phase, 1f);
            float extend;
            if (t < 0.08f) extend = t / 0.08f;
            else if (t < 0.3f) extend = 1f;
            else if (t < 0.65f) extend = 1f - (t - 0.3f) / 0.35f;
            else extend = 0f;
            body.MovePosition(home + punch * extend);
        }
    }
}

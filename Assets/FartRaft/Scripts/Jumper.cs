using UnityEngine;

namespace FartRaft
{
    // A big fish that leaps over the slide from one side to the other, then hides below and jumps back.
    [RequireComponent(typeof(Rigidbody))]
    public class Jumper : MonoBehaviour
    {
        public Vector3 from;
        public Vector3 to;
        public float height = 4f;
        public float jumpTime = 1.4f;
        public float period = 3.6f;
        [Range(0f, 1f)] public float phase;

        Rigidbody body;
        float clock;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            clock += Time.fixedDeltaTime * GameManager.ObstacleSpeed;
            float cycle = clock / period + phase;
            bool back = Mathf.FloorToInt(cycle) % 2 == 1;
            Vector3 a = back ? to : from;
            Vector3 b = back ? from : to;

            float t = Mathf.Repeat(cycle, 1f) * period / jumpTime;
            if (t > 1f)
            {
                body.MovePosition(b + Vector3.down * 6f);
                return;
            }

            Vector3 position = Vector3.Lerp(a, b, t) + Vector3.up * (4f * height * t * (1f - t));
            Vector3 velocity = (b - a) + Vector3.up * (4f * height * (1f - 2f * t));
            body.MovePosition(position);
            if (velocity.sqrMagnitude > 0.01f) body.MoveRotation(Quaternion.LookRotation(velocity));
        }
    }
}

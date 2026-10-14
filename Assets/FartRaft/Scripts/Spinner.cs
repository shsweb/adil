using UnityEngine;

namespace FartRaft
{
    [RequireComponent(typeof(Rigidbody))]
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 50f;

        Rigidbody body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, degreesPerSecond * Time.fixedDeltaTime, 0f));
        }
    }
}

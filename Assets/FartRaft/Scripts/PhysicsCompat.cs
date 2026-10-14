using UnityEngine;

namespace FartRaft
{
    // Unity 6 renamed velocity/drag to linearVelocity/linearDamping.
    public static class PhysicsCompat
    {
        public static Vector3 GetVelocity(Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        public static void SetDamping(Rigidbody body, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = linear;
            body.angularDamping = angular;
#else
            body.drag = linear;
            body.angularDrag = angular;
#endif
        }
    }
}

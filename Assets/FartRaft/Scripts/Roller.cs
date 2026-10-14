using UnityEngine;

namespace FartRaft
{
    // Spins a ball visual to match how far its parent moved, so a sliding ball looks like it rolls.
    public class Roller : MonoBehaviour
    {
        public float radius = 1.5f;

        Vector3 lastPosition;

        void Start()
        {
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.000001f || radius <= 0f) return;
            Vector3 axis = Vector3.Cross(Vector3.up, delta.normalized);
            transform.Rotate(axis, delta.magnitude / radius * Mathf.Rad2Deg, Space.World);
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float distance = 13f;
        public float height = 15f;
        public float lookAhead = 5f;
        public float positionSmoothing = 4f;
        public float rotationSmoothing = 2.5f;

        float yaw;

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-rotationSmoothing * dt));
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = target.position + rotation * new Vector3(0f, height, -distance);

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSmoothing * dt));
            transform.LookAt(target.position + rotation * Vector3.forward * lookAhead);
        }

        public void Snap()
        {
            if (target == null) return;
            yaw = target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.position = target.position + rotation * new Vector3(0f, height, -distance);
            transform.LookAt(target.position + rotation * Vector3.forward * lookAhead);
        }
    }
}

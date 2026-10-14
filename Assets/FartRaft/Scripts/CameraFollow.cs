using UnityEngine;

namespace FartRaft
{
    // High camera behind the raft, looking down the track. It follows the track
    // direction rather than the raft rotation, because the raft spins freely.
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3[] path;
        public float distance = 9f;
        public float height = 9f;
        public float lookAhead = 6f;
        public int pathLookAhead = 10;
        public float positionSmoothing = 5f;
        public float rotationSmoothing = 2.5f;

        [Header("Close-up (character select)")]
        public bool closeUp;
        public float closeDistance = 4.6f;
        public float closeHeight = 3.2f;
        public float closeFocusHeight = 0.9f;

        float yaw;
        float closeBlend;
        int pathIndex = -1;

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            closeBlend = Mathf.MoveTowards(closeBlend, closeUp ? 1f : 0f, dt * 2.5f);
            yaw = Mathf.LerpAngle(yaw, TargetYaw(), 1f - Mathf.Exp(-rotationSmoothing * dt));
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            transform.position = Vector3.Lerp(transform.position, Desired(rotation), 1f - Mathf.Exp(-positionSmoothing * dt));
            transform.LookAt(LookPoint(rotation));
        }

        Vector3 Desired(Quaternion rotation)
        {
            float k = Mathf.SmoothStep(0f, 1f, closeBlend);
            return target.position + rotation * new Vector3(0f, Mathf.Lerp(height, closeHeight, k), -Mathf.Lerp(distance, closeDistance, k));
        }

        Vector3 LookPoint(Quaternion rotation)
        {
            float k = Mathf.SmoothStep(0f, 1f, closeBlend);
            return target.position + rotation * Vector3.forward * (lookAhead * (1f - k)) + Vector3.up * (closeFocusHeight * k);
        }

        float TargetYaw()
        {
            if (path == null || path.Length < 2) return target.eulerAngles.y;
            pathIndex = CoursePath.FindNearest(path, target.position, pathIndex);
            Vector3 d = CoursePath.FlatDirection(path, pathIndex, pathLookAhead);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        public void Snap()
        {
            if (target == null) return;
            pathIndex = -1;
            yaw = TargetYaw();
            closeBlend = closeUp ? 1f : 0f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.position = Desired(rotation);
            transform.LookAt(LookPoint(rotation));
        }
    }
}

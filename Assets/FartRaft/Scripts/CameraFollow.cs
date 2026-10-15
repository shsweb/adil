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
        [Tooltip("While set (lobby), the camera sits still at this spot instead of following the raft.")]
        public Transform fixedView;
        [Tooltip("Raft Rumble: look down at both rafts instead of along the slide.")]
        public bool arenaView;
        public Transform secondTarget;

        float yaw;
        int pathIndex = -1;

        void LateUpdate()
        {
            if (fixedView != null)
            {
                transform.SetPositionAndRotation(fixedView.position, fixedView.rotation);
                return;
            }
            if (target == null) return;

            if (arenaView)
            {
                Vector3 mid = secondTarget != null ? (target.position + secondTarget.position) * 0.5f : target.position;
                Vector3 overhead = mid + new Vector3(0f, 18f, -14f);
                transform.position = Vector3.Lerp(transform.position, overhead, 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mid - transform.position), 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));
                return;
            }

            float dt = Time.deltaTime;
            yaw = Mathf.LerpAngle(yaw, TargetYaw(), 1f - Mathf.Exp(-rotationSmoothing * dt));
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = target.position + rotation * new Vector3(0f, height, -distance);

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSmoothing * dt));
            transform.LookAt(target.position + rotation * Vector3.forward * lookAhead);
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
            if (arenaView)
            {
                Vector3 mid = secondTarget != null ? (target.position + secondTarget.position) * 0.5f : target.position;
                transform.position = mid + new Vector3(0f, 18f, -14f);
                transform.LookAt(mid);
                return;
            }
            pathIndex = -1;
            yaw = TargetYaw();
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.position = target.position + rotation * new Vector3(0f, height, -distance);
            transform.LookAt(target.position + rotation * Vector3.forward * lookAhead);
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    // Drives the yellow raft. In a team race it follows the course. In Raft Rumble it shoves the player.
    public class RivalDriver : MonoBehaviour
    {
        public Raft raft;
        public FartSeat[] seats;
        public Vector3[] path;
        public Transform player;
        public RumbleArena arena;

        public int Progress { get; private set; } = -1;
        public int Checkpoints { get; private set; }
        public Vector3 RespawnPosition { get; set; }
        public Quaternion RespawnRotation { get; set; } = Quaternion.identity;

        public bool racing;
        public PlayMode mode = PlayMode.Team;
        public float urgency = 1f;

        public void ResetProgress()
        {
            Progress = -1;
            Checkpoints = 0;
        }

        public void NoteCheckpoint(int index)
        {
            if (index == Checkpoints) Checkpoints++;
        }

        void Update()
        {
            if (!racing || seats == null || raft == null) return;

            Vector3 dir = mode == PlayMode.Rumble ? RumbleDir() : RaceDir();
            Vector2 stick = ToStick(dir);
            foreach (var seat in seats)
            {
                if (seat == null || !seat.gameObject.activeSelf) continue;
                seat.StickInput = stick;
                seat.BoostMultiplier = urgency;
            }

            if (mode != PlayMode.Rumble && path != null && path.Length > 0)
                Progress = CoursePath.FindNearest(path, raft.transform.position, Progress);
        }

        Vector3 RaceDir()
        {
            if (path == null || path.Length < 2) return transform.forward;
            int index = Mathf.Clamp(Progress, 0, path.Length - 1);
            Vector3 forward = CoursePath.FlatDirection(path, index, 8);
            Vector3 toCenter = path[index] - raft.transform.position;
            toCenter.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float weave = Mathf.Sin(Time.time * 0.4f) * 1.6f;
            return (forward * 5f + Vector3.ClampMagnitude(toCenter, 5f) + side * weave).normalized;
        }

        Vector3 RumbleDir()
        {
            Vector3 dir = player != null ? player.position - raft.transform.position : transform.forward;
            dir.y = 0f;
            if (arena == null || dir.sqrMagnitude < 0.01f) return dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;

            Vector3 toCenter = arena.Center - raft.transform.position;
            toCenter.y = 0f;
            float edge = toCenter.magnitude / Mathf.Max(1f, arena.radius);
            if (edge > 0.72f) dir = Vector3.Lerp(dir.normalized, toCenter.normalized, Mathf.Clamp01((edge - 0.72f) * 3f));
            return dir.normalized;
        }

        Vector2 ToStick(Vector3 worldDir)
        {
            var cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            right.Normalize();
            return Vector2.ClampMagnitude(new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, forward)), 1f);
        }
    }
}

using UnityEngine;

namespace FartRaft
{
    public static class CoursePath
    {
        // Nearest path point to position. Searches around the previous index
        // (pass -1 for a full search) so it stays cheap every frame.
        public static int FindNearest(Vector3[] path, Vector3 position, int hint)
        {
            if (path == null || path.Length == 0) return 0;

            int from = 0;
            int to = path.Length - 1;
            if (hint >= 0)
            {
                from = Mathf.Max(0, hint - 25);
                to = Mathf.Min(path.Length - 1, hint + 25);
            }

            int best = from;
            float bestDistance = float.MaxValue;
            for (int i = from; i <= to; i++)
            {
                float d = (path[i] - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        public static Vector3 FlatDirection(Vector3[] path, int index, int ahead)
        {
            int a = Mathf.Clamp(index, 0, path.Length - 1);
            int b = Mathf.Clamp(index + ahead, 0, path.Length - 1);
            if (a == b) a = Mathf.Max(0, b - 1);
            Vector3 d = path[b] - path[a];
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace FartRaft
{
    // Screen-space UI label that follows a world object (player name tags).
    [RequireComponent(typeof(Graphic))]
    public class WorldLabel : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 2.3f, 0f);

        Graphic graphic;
        RectTransform rect;
        Camera cam;

        void Awake()
        {
            graphic = GetComponent<Graphic>();
            rect = (RectTransform)transform;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (target == null || cam == null || !target.gameObject.activeInHierarchy)
            {
                graphic.enabled = false;
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(target.position + offset);
            graphic.enabled = screen.z > 0f;
            rect.position = new Vector3(screen.x, screen.y, 0f);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FartRaft
{
    // On-screen button for one player. Hold to fart, slide left/right while
    // holding to aim. Each button tracks its own finger, so several players can
    // press different buttons on the same phone at once.
    public class TouchFartButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public FartSeat seat;
        public Image face;
        public Image gasRing;
        public Text label;
        public Color color = Color.white;
        public float dragForFullAim = 140f;

        const int NoPointer = int.MinValue;
        int pointerId = NoPointer;
        Vector2 pressPosition;
        Canvas canvas;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;
            pressPosition = eventData.position;
            if (seat != null)
            {
                seat.TouchHeld = true;
                seat.AimInput = 0f;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId || seat == null) return;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            seat.AimInput = Mathf.Clamp((eventData.position.x - pressPosition.x) / (dragForFullAim * scale), -1f, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Release()
        {
            pointerId = NoPointer;
            if (seat != null)
            {
                seat.TouchHeld = false;
                seat.AimInput = 0f;
            }
        }

        void Update()
        {
            if (seat == null) return;

            if (gasRing != null)
            {
                gasRing.fillAmount = seat.Gas01;
                gasRing.color = seat.IsExhausted ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.7f, 1f, 0.3f, 0.9f);
            }

            if (face != null)
            {
                Color c = seat.IsExhausted ? Color.gray : color;
                c.a = seat.IsFarting ? 0.95f : 0.6f;
                face.color = c;
                face.rectTransform.localScale = Vector3.one * (seat.TouchHeld ? 0.92f : 1f);
            }
        }
    }
}

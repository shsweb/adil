using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FartRaft
{
    // Small on-screen joystick for one player. Push it toward where the raft
    // should go; the animal runs to the opposite side of the raft and farts.
    // Each joystick tracks its own finger, so several players can share one phone.
    public class FartJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public FartSeat seat;
        public RectTransform knob;
        public Image knobImage;
        public Image gasRing;
        public Text label;
        public Color color = Color.white;
        public float radius = 120f;

        const int NoPointer = int.MinValue;
        int pointerId = NoPointer;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;
            Move(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Move(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Move(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local, radius);
            if (knob != null) knob.anchoredPosition = offset;
            if (seat != null) seat.StickInput = offset / radius;
        }

        void Release()
        {
            pointerId = NoPointer;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (seat != null) seat.StickInput = Vector2.zero;
        }

        void Update()
        {
            if (seat == null) return;

            if (gasRing != null)
            {
                gasRing.fillAmount = seat.Gas01;
                gasRing.color = seat.IsExhausted ? new Color(1f, 0.3f, 0.3f, 0.95f) : new Color(0.7f, 1f, 0.2f, 0.95f);
            }

            if (knobImage != null)
            {
                Color c = seat.IsExhausted ? Color.gray : color;
                c.a = seat.IsFarting ? 1f : 0.85f;
                knobImage.color = c;
                knobImage.rectTransform.localScale = Vector3.one * (seat.IsFarting ? 1.1f : 1f);
            }
        }
    }
}

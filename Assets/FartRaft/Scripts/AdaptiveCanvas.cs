using UnityEngine;
using UnityEngine.UI;

namespace FartRaft
{
    // Wide phones (19.5:9, 20:9) keep the full UI height on screen; tablets keep the full width.
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveCanvas : MonoBehaviour
    {
        CanvasScaler scaler;
        float lastAspect;

        void Awake() => scaler = GetComponent<CanvasScaler>();

        void Update()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (Mathf.Approximately(aspect, lastAspect)) return;
            lastAspect = aspect;
            scaler.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
        }
    }
}

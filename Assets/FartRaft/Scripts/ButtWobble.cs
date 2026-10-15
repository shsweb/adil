using UnityEngine;

namespace FartRaft
{
    // The two butt spheres sway, which is the funny wobble players expect.
    public class ButtWobble : MonoBehaviour
    {
        Transform left;
        Transform right;
        Vector3 leftStart;
        Vector3 rightStart;

        void Awake()
        {
            left = transform.Find("ButtL");
            right = transform.Find("ButtR");
            if (left != null) leftStart = left.localPosition;
            if (right != null) rightStart = right.localPosition;
        }

        void Update()
        {
            float w = Mathf.Sin(Time.time * 7f) * 0.045f;
            if (left != null) left.localPosition = leftStart + new Vector3(w, 0f, -w);
            if (right != null) right.localPosition = rightStart + new Vector3(-w, 0f, w);
        }
    }
}

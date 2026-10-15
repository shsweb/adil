using UnityEngine;

namespace FartRaft
{
    // Scrolls the main texture so the slide water looks like it is flowing.
    public class UVScroll : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0f, -0.5f);

        Material material;

        void Start()
        {
            var r = GetComponent<Renderer>();
            if (r != null) material = r.material;
        }

        void Update()
        {
            if (material != null) material.mainTextureOffset += speed * Time.deltaTime;
        }
    }
}

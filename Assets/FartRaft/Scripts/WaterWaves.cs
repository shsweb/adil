using UnityEngine;

namespace FartRaft
{
    // Generates a grid mesh and animates gentle waves on the CPU. The mesh is
    // rebuilt on load instead of being saved into the scene.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterWaves : MonoBehaviour
    {
        public float sizeX = 260f;
        public float sizeZ = 420f;
        public float cellSize = 3f;
        public float amplitude = 0.12f;
        public float wavelength = 10f;
        public float speed = 1.2f;

        Mesh mesh;
        Vector3[] baseVertices;
        Vector3[] vertices;
        Vector3[] normals;

        void OnEnable()
        {
            Build();
        }

        void OnDisable()
        {
            if (mesh != null) DestroyImmediate(mesh);
            mesh = null;
        }

        void Build()
        {
            int nx = Mathf.Max(2, Mathf.CeilToInt(sizeX / cellSize) + 1);
            int nz = Mathf.Max(2, Mathf.CeilToInt(sizeZ / cellSize) + 1);
            baseVertices = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float u = (float)x / (nx - 1);
                    float v = (float)z / (nz - 1);
                    baseVertices[z * nx + x] = new Vector3((u - 0.5f) * sizeX, 0f, (v - 0.5f) * sizeZ);
                    uv[z * nx + x] = new Vector2(u * sizeX / 10f, v * sizeZ / 10f);
                }
            }

            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int z = 0; z < nz - 1; z++)
            {
                for (int x = 0; x < nx - 1; x++)
                {
                    int i = z * nx + x;
                    triangles[t++] = i;
                    triangles[t++] = i + nx;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + nx;
                    triangles[t++] = i + nx + 1;
                }
            }

            vertices = (Vector3[])baseVertices.Clone();
            normals = new Vector3[vertices.Length];

            mesh = new Mesh { name = "WaterWaves", hideFlags = HideFlags.DontSave };
            if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(sizeX, amplitude * 6f + 1f, sizeZ));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Animate(0f);
        }

        void Update()
        {
            if (Application.isPlaying && mesh != null) Animate(Time.time * speed);
        }

        void Animate(float time)
        {
            float k1 = 2f * Mathf.PI / wavelength;
            float k2 = k1 * 1.4f;
            float k3 = k1 * 0.6f;
            float a1 = amplitude;
            float a2 = amplitude * 0.6f;
            float a3 = amplitude * 0.8f;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 p = baseVertices[i];
                float p1 = k1 * p.x + time;
                float p2 = k2 * p.z - time * 1.3f;
                float p3 = k3 * (p.x + p.z) + time * 0.7f;

                p.y = a1 * Mathf.Sin(p1) + a2 * Mathf.Sin(p2) + a3 * Mathf.Sin(p3);
                float dx = a1 * k1 * Mathf.Cos(p1) + a3 * k3 * Mathf.Cos(p3);
                float dz = a2 * k2 * Mathf.Cos(p2) + a3 * k3 * Mathf.Cos(p3);

                vertices[i] = p;
                normals[i] = new Vector3(-dx, 1f, -dz).normalized;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
        }
    }
}

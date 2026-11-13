using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>
    /// Hiệu ứng vệt chém kiếm (Sword Slash Wave).
    /// Xuất hiện hình vòng cung vệt trắng, di chuyển nhẹ về phía trước,
    /// mở rộng theo dạng sóng và mờ dần trong đúng 0.5 giây rồi biến mất.
    /// </summary>
    public class SwordSlashWave : MonoBehaviour
    {
        [Header("Thông số vệt sóng")]
        [Tooltip("Thời gian tồn tại của vệt kiếm (0.5 giây)")]
        [SerializeField] private float lifetime = 0.5f;

        [Tooltip("Tốc độ đẩy về phía trước")]
        [SerializeField] private float forwardSpeed = 3.0f;

        [Tooltip("Kích thước ban đầu và kích thước khi tan dần")]
        [SerializeField] private Vector3 startScale = new Vector3(1.0f, 0.6f, 1.0f);
        [SerializeField] private Vector3 endScale = new Vector3(1.6f, 1.2f, 1.6f);

        [SerializeField] private Renderer waveRenderer;

        private float _elapsedTime = 0f;
        private Material _instancedMaterial;
        private Color _initialColor = Color.white;

        private void Awake()
        {
            if (waveRenderer == null)
            {
                waveRenderer = GetComponentInChildren<Renderer>();
            }

            if (waveRenderer != null)
            {
                _instancedMaterial = waveRenderer.material;
                _initialColor = _instancedMaterial.color;
            }

            transform.localScale = startScale;
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsedTime / lifetime);

            // 1. Đẩy nhẹ vệt sóng kiếm về phía trước
            transform.position += transform.forward * (forwardSpeed * (1f - progress * 0.5f) * Time.deltaTime);

            // 2. Mở rộng vệt kiếm dạng sóng xung kích
            transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, progress));

            // 3. Làm mờ dần alpha mượt mà theo đường cong mờ dần
            if (_instancedMaterial != null)
            {
                float alpha = Mathf.SmoothStep(1f, 0f, progress);
                Color c = _initialColor;
                c.a = _initialColor.a * alpha;
                _instancedMaterial.color = c;
            }

            // 4. Biến mất hoàn toàn sau 0.5 giây
            if (_elapsedTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
            }
        }

        /// <summary>
        /// Tạo một Mesh hình vòng cung lưỡi kiếm (Crescent Slash Arc).
        /// </summary>
        public static Mesh CreateSlashArcMesh(float innerRadius = 0.8f, float outerRadius = 1.6f, float arcAngle = 120f, int segments = 24)
        {
            Mesh mesh = new Mesh();
            mesh.name = "SlashArcMesh";

            int vertexCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            int[] triangles = new int[segments * 6];

            float halfAngle = arcAngle * 0.5f;
            float angleStep = arcAngle / segments;

            int vertIndex = 0;
            int triIndex = 0;

            for (int i = 0; i <= segments; i++)
            {
                float angle = -halfAngle + i * angleStep;
                float rad = angle * Mathf.Deg2Rad;
                float cos = Mathf.Sin(rad);
                float sin = Mathf.Cos(rad);

                // Độ dày thon nhọn ở 2 đầu (taper tips)
                float t = (float)i / segments;
                float taper = Mathf.Sin(t * Mathf.PI);
                float currentOuter = Mathf.Lerp(innerRadius, outerRadius, taper);

                // Đỉnh trong và ngoài
                vertices[vertIndex] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[vertIndex + 1] = new Vector3(cos * currentOuter, 0.05f * taper, sin * currentOuter);

                uvs[vertIndex] = new Vector2(t, 0f);
                uvs[vertIndex + 1] = new Vector2(t, 1f);

                Color vertColor = new Color(1f, 1f, 1f, taper);
                colors[vertIndex] = vertColor;
                colors[vertIndex + 1] = vertColor;

                if (i < segments)
                {
                    triangles[triIndex++] = vertIndex;
                    triangles[triIndex++] = vertIndex + 1;
                    triangles[triIndex++] = vertIndex + 2;

                    triangles[triIndex++] = vertIndex + 1;
                    triangles[triIndex++] = vertIndex + 3;
                    triangles[triIndex++] = vertIndex + 2;
                }

                vertIndex += 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}

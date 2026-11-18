using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Common
{
    /// <summary>Làm viền độ hiếm cao sáng lên rồi dịu xuống theo nhịp (chạy cả khi game tạm dừng).</summary>
    public class RarityFrameGlow : MonoBehaviour
    {
        private Image image;
        private Color baseColor = Color.white;

        public void SetBaseColor(Color color)
        {
            baseColor = color;
        }

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        private void Update()
        {
            if (image == null)
            {
                return;
            }

            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            Color c = Color.Lerp(baseColor * 0.7f, Color.Lerp(baseColor, Color.white, 0.4f), t);
            c.a = 1f;
            image.color = c;
        }

        private void OnDisable()
        {
            if (image != null)
            {
                image.color = baseColor;
            }
        }
    }
}

using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Co RectTransform này vào vùng an toàn của màn hình (tránh tai thỏ, viền bo, thanh điều hướng).
    /// Gắn lên một object con của Canvas rồi đặt các nút sát cạnh màn hình vào bên trong.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var screenSize = new Vector2(Screen.width, Screen.height);
            rectTransform.anchorMin = appliedSafeArea.position / screenSize;
            rectTransform.anchorMax = (appliedSafeArea.position + appliedSafeArea.size) / screenSize;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}

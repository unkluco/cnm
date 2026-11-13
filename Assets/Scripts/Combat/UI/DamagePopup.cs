using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Hiệu ứng số sát thương bay lên (Floating Damage Text) khi mục tiêu bị đánh trúng.
    /// Tự động quay mặt về Camera, bay lên nhẹ và mờ dần rồi tự hủy.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private Text damageText;
        [SerializeField] private float floatSpeed = 1.6f;
        [SerializeField] private float fadeDuration = 0.75f;
        [SerializeField] private Vector3 randomOffsetRange = new Vector3(0.4f, 0.2f, 0.4f);

        private float _spawnTime;
        private Color _initialColor;
        private Camera _mainCamera;

        public static DamagePopup Create(GameObject popupPrefab, Vector3 worldPosition, float damageAmount)
        {
            GameObject obj;
            if (popupPrefab != null)
            {
                obj = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
            }
            else
            {
                // Fallback tạo nhanh Canvas World-Space nếu chưa có prefab
                obj = new GameObject("DamagePopup");
                obj.transform.position = worldPosition;

                Canvas canvas = obj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform rt = obj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(2f, 1f);
                obj.transform.localScale = Vector3.one * 0.02f;

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(obj.transform, false);
                Text t = textObj.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.alignment = TextAnchor.MiddleCenter;
                t.fontSize = 48;
                t.fontStyle = FontStyle.Bold;

                DamagePopup popup = obj.AddComponent<DamagePopup>();
                popup.damageText = t;
                popup.Setup(damageAmount);
                return popup;
            }

            DamagePopup dp = obj.GetComponent<DamagePopup>();
            if (dp != null)
            {
                dp.Setup(damageAmount);
            }
            return dp;
        }

        public void Setup(float damageAmount)
        {
            _spawnTime = Time.time;
            _mainCamera = Camera.main;

            // Thêm chút ngẫu nhiên vào vị trí bay
            transform.position += new Vector3(
                Random.Range(-randomOffsetRange.x, randomOffsetRange.x),
                Random.Range(0f, randomOffsetRange.y),
                Random.Range(-randomOffsetRange.z, randomOffsetRange.z)
            );

            if (damageText != null)
            {
                int roundedDmg = Mathf.RoundToInt(damageAmount);
                damageText.text = $"-{roundedDmg}";
                
                // Sát thương lớn (>40) hiện màu cam/vàng chí mạng, thường hiện màu đỏ tươi
                if (roundedDmg >= 40)
                {
                    damageText.color = new Color(1f, 0.7f, 0.1f, 1f);
                    transform.localScale *= 1.3f;
                }
                else
                {
                    damageText.color = new Color(1f, 0.25f, 0.25f, 1f);
                }

                _initialColor = damageText.color;
            }
        }

        private void LateUpdate()
        {
            // Luôn quay mặt về Camera (Billboard)
            if (_mainCamera != null)
            {
                transform.rotation = _mainCamera.transform.rotation;
            }

            // Bay lên theo trục Y
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            // Mờ dần theo thời gian
            float elapsed = Time.time - _spawnTime;
            if (damageText != null && fadeDuration > 0f)
            {
                float alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
                damageText.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, alpha);
            }

            if (elapsed >= fadeDuration)
            {
                Destroy(gameObject);
            }
        }
    }
}

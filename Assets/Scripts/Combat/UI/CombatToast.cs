using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Dòng thông báo ngắn giữa màn hình ("Máu đang đầy", "Không thể dịch chuyển khi đang giao tranh"...).
    /// Gọi CombatToast.Show(text) từ bất kỳ đâu; Canvas tự tạo lần đầu và giữ qua Scene hiện tại.
    /// </summary>
    public class CombatToast : MonoBehaviour
    {
        private const float ShowSeconds = 1.8f;
        private const float FadeSeconds = 0.35f;

        private static CombatToast instance;

        private Text label;
        private CanvasGroup group;
        private float hideAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookEvents()
        {
            // Không dùng được vật phẩm (máu đầy, đang giao tranh...) thì báo cho người chơi.
            LightSouls.Combat.Items.ConsumableDataSO.UseRejected -= HandleUseRejected;
            LightSouls.Combat.Items.ConsumableDataSO.UseRejected += HandleUseRejected;
        }

        private static void HandleUseRejected(GameObject user, string reason)
        {
            if (user != null && user.CompareTag("Player"))
            {
                Show(reason);
            }
        }

        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (instance == null)
            {
                instance = Create();
            }

            instance.label.text = message;
            instance.hideAt = Time.unscaledTime + ShowSeconds;
            instance.group.alpha = 1f;
            instance.gameObject.SetActive(true);
        }

        private static CombatToast Create()
        {
            var root = new GameObject("CombatToast", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bgRect = (RectTransform)background.transform;
            bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0.72f);
            bgRect.sizeDelta = new Vector2(900f, 90f);
            var bg = background.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(background.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 40;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var toast = root.AddComponent<CombatToast>();
            toast.label = text;
            toast.group = group;
            return toast;
        }

        private void Update()
        {
            float remaining = hideAt - Time.unscaledTime;
            if (remaining <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            group.alpha = Mathf.Clamp01(remaining / FadeSeconds);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}

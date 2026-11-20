using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.UI
{
    /// <summary>A scene-local, non-blocking title card celebrating arrival in the actual generated dungeon.</summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup))]
    public sealed class DungeonArrivalBanner : MonoBehaviour
    {
        private const float EnterSeconds = 0.42f;
        private const float HoldSeconds = 3f;
        private const float ExitSeconds = 0.7f;
        public const float Duration = EnterSeconds + HoldSeconds + ExitSeconds;

        private RectTransform safeArea, banner, leftSigil, rightSigil;
        private CanvasGroup group;
        private Text heading, title, subtitle;
        private Font font;
        private readonly List<Image> accentDetails = new List<Image>();
        private float startedAt;

        public string DungeonId { get; private set; }
        public string DungeonName { get; private set; }
        public bool FromMysteryPortal { get; private set; }
        public bool IsShowing => gameObject.activeInHierarchy && Time.unscaledTime - startedAt < Duration;

        public static DungeonArrivalBanner Create()
        {
            var root = new GameObject("DungeonArrivalBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            return root.AddComponent<DungeonArrivalBanner>();
        }

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over the HUD but behind pause, shop and inventory panels.
            canvas.sortingOrder = 6;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildLayout();
        }

        private void BuildLayout()
        {
            safeArea = Rect(transform, "SafeArea", Vector2.zero, Vector2.zero);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            banner = Rect(safeArea, "ArrivalTitleCard", Vector2.zero, new Vector2(1200f, 206f));
            // Below the upper-right minimap and above the player's action area.
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.65f);
            var shadow = Picture(banner, "Shadow", new Vector2(0, -9), new Vector2(1162, 210), new Color(0.025f, 0.032f, 0.05f, 0.8f));
            DarkFantasyTheme.FramePanel(shadow, new Color(0.25f, 0.28f, 0.34f, 0.7f));
            var frame = Picture(banner, "AntiqueFrame", Vector2.zero, new Vector2(1160, 206), Color.white);
            DarkFantasyTheme.FramePanel(frame, Color.white);
            Picture(banner, "InnerSilk", Vector2.zero, new Vector2(1118, 178), new Color(0.05f, 0.065f, 0.095f, 0.56f));

            // Fine gilded dividers, angular heraldic wings and symmetrical insignia.
            foreach (int sign in new[] { -1, 1 })
            {
                float x = sign * 470f;
                var seal = Picture(banner, sign < 0 ? "CrestLeft" : "CrestRight", new Vector2(x, 0), new Vector2(150, 150), Color.white);
                seal.sprite = DarkFantasyTheme.JoystickRing;
                seal.preserveAspect = true;
                var inner = Rect(seal.transform, "Rune", Vector2.zero, new Vector2(94, 94));
                if (sign < 0) leftSigil = inner; else rightSigil = inner;
                Accent(banner, "SealDivider", new Vector2(sign * 385f, 0), new Vector2(2, 139));
                Diamond(banner, new Vector2(sign * 385f, 0), 10, DarkFantasyTheme.Gold);
                Accent(banner, "WingSpine", new Vector2(sign * 583f, 0), new Vector2(70, 3));
                Diamond(banner, new Vector2(sign * 603f, 0), 22, DarkFantasyTheme.Bronze);
                AccentDiamond(banner, new Vector2(sign * 603f, 0), 13);
                for (int row = -1; row <= 1; row += 2)
                {
                    Line(banner, new Vector2(sign * 548, row * 64), new Vector2(sign * 583, row * 29), 3, DarkFantasyTheme.Bronze);
                    Line(banner, new Vector2(sign * 583, row * 29), new Vector2(sign * 603, 0), 3, DarkFantasyTheme.Bronze);
                    Diamond(banner, new Vector2(sign * 536, row * 76), 8, DarkFantasyTheme.Gold);
                }
                Accent(banner, "HeaderRule", new Vector2(sign * 302f, 69f), new Vector2(106, 2));
                AccentDiamond(banner, new Vector2(sign * 359f, 69f), 8);
            }

            heading = Label(banner, "Heading", "ĐÃ BƯỚC VÀO HẦM NGỤC", new Vector2(0, 69), new Vector2(490, 28), 20, DarkFantasyTheme.Gold, true);
            title = Label(banner, "DungeonName", string.Empty, new Vector2(0, 15), new Vector2(734, 75), 54, DarkFantasyTheme.Ivory, true);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.025f, 0.04f, 0.94f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            Accent(banner, "TitleRule", new Vector2(0, -26), new Vector2(510, 2));
            AccentDiamond(banner, new Vector2(-264, -26), 8);
            AccentDiamond(banner, new Vector2(264, -26), 8);
            subtitle = Label(banner, "DungeonDescription", string.Empty, new Vector2(0, -60), new Vector2(730, 58), 25, DarkFantasyTheme.Ivory, false);
        }

        public void Show(string dungeonId, string dungeonName, string description, bool fromMysteryPortal)
        {
            DungeonId = dungeonId ?? string.Empty;
            DungeonName = string.IsNullOrWhiteSpace(dungeonName) ? "Hầm Ngục" : dungeonName;
            FromMysteryPortal = fromMysteryPortal;
            gameObject.SetActive(true);
            Color accent = DungeonId switch
            {
                "dungeon.lava" => new Color32(247, 147, 76, 255),
                "dungeon.maze" => new Color32(115, 211, 213, 255),
                _ => DarkFantasyTheme.Gold
            };
            foreach (var detail in accentDetails) detail.color = accent;
            heading.color = accent;
            heading.text = fromMysteryPortal ? "CỔNG BÍ ẨN  ·  ĐÃ ĐẾN" : "ĐÃ BƯỚC VÀO HẦM NGỤC";
            title.color = Color.Lerp(DarkFantasyTheme.Ivory, accent, 0.32f);
            title.text = DungeonName.ToUpperInvariant();
            subtitle.text = string.IsNullOrWhiteSpace(description) ? "Một hành trình mới đang chờ phía trước." : description;
            DrawSigil(leftSigil, DungeonId, accent);
            DrawSigil(rightSigil, DungeonId, accent);
            startedAt = Time.unscaledTime;
            group.alpha = 0f;
            banner.anchoredPosition = new Vector2(0, 23f);
            FitToScreen();
        }

        private void DrawSigil(RectTransform parent, string id, Color color)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (id == "dungeon.lava")
            {
                // Angular flame rune engraved into the medallion.
                Path(parent, color, 5, new Vector2(-26, -29), new Vector2(-37, -5), new Vector2(-22, 17),
                    new Vector2(-13, 4), new Vector2(0, 39), new Vector2(22, 13), new Vector2(35, -4),
                    new Vector2(25, -28), new Vector2(0, -39), new Vector2(-26, -29));
                Path(parent, DarkFantasyTheme.Ivory, 3, new Vector2(-10, -24), new Vector2(0, 1), new Vector2(14, -16), new Vector2(4, -27), new Vector2(-10, -24));
            }
            else if (id == "dungeon.maze")
            {
                Path(parent, color, 5, new Vector2(-37, -16), new Vector2(-37, 37), new Vector2(37, 37),
                    new Vector2(37, -37), new Vector2(-37, -37), new Vector2(-37, -30));
                Path(parent, color, 5, new Vector2(-21, -21), new Vector2(-21, 21), new Vector2(21, 21), new Vector2(21, -21), new Vector2(-6, -21));
                Path(parent, DarkFantasyTheme.Ivory, 3, new Vector2(-6, 7), new Vector2(7, 7), new Vector2(7, -6), new Vector2(-9, -6));
                Diamond(parent, new Vector2(-37, -23), 7, color);
            }
            else
            {
                var emblem = Picture(parent, "SunSigil", Vector2.zero, new Vector2(90, 90), color);
                emblem.sprite = Resources.Load<Sprite>("Art/DarkFantasy/UI/SunEmblem");
                emblem.preserveAspect = true;
            }
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= Duration || BackButtonStack.HasOpenPanel)
            {
                group.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / EnterSeconds));
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - EnterSeconds - HoldSeconds) / ExitSeconds));
            group.alpha = enter * (1f - exit);
            banner.anchoredPosition = new Vector2(0, Mathf.Lerp(23f, 0f, enter) + 16f * exit);
        }

        private void LateUpdate() => FitToScreen();
        private void FitToScreen()
        {
            float fit = Mathf.Min(1f, Mathf.Min(safeArea.rect.width / 1270f, safeArea.rect.height / 700f));
            if (fit > 0.1f) banner.localScale = Vector3.one * fit;
        }

        private Image Accent(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = Picture(parent, name, position, size, DarkFantasyTheme.Gold);
            accentDetails.Add(image);
            return image;
        }
        private void AccentDiamond(Transform parent, Vector2 position, float size)
        {
            var image = Diamond(parent, position, size, DarkFantasyTheme.Gold);
            accentDetails.Add(image);
        }
        private static Image Diamond(Transform parent, Vector2 position, float size, Color color)
        {
            var image = Picture(parent, "Diamond", position, Vector2.one * size, color);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            return image;
        }
        private static void Path(Transform parent, Color color, float thickness, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++) Line(parent, points[i - 1], points[i], thickness, color);
        }
        private static void Line(Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            var image = Picture(parent, "Engraving", (from + to) * 0.5f, new Vector2(delta.magnitude + thickness * 0.5f, thickness), color);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
        private static Image Picture(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color, bool bold)
        {
            var text = Rect(parent, name, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(15, fontSize - 14);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}

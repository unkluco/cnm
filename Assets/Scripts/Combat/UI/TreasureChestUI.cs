using System.Collections.Generic;
using LightSouls.Combat.Items;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Bảng hòm kho báu (Canvas sort order 8): lưới 3 x 2 ô, mỗi ô hiện ảnh và số lượng vật phẩm.
    /// Chạm một ô để lấy vào túi đồ; nút "Lấy hết" lấy mọi ô. Túi đầy thì đồ ở lại trong hòm.
    /// Giao diện tự dựng bằng mã khi chạy, nên chỉ cần một GameObject có component này trong Scene
    /// (GameBootstrap tự tạo nếu Scene chưa có). Tích hợp BackButtonStack: nút Back đóng bảng,
    /// và PlayerController tự khóa di chuyển khi bảng đang mở.
    /// </summary>
    public class TreasureChestUI : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.16f, 0.12f, 0.08f, 0.97f);
        private static readonly Color SlotColor = new Color(0.3f, 0.23f, 0.14f, 1f);
        private static readonly Color EmptySlotColor = new Color(0.1f, 0.08f, 0.06f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.3f);

        private class SlotView
        {
            public Button Button;
            public Image Background;
            public Image Icon;
            public Text Count;
            public Text Name;
        }

        private readonly List<SlotView> slotViews = new List<SlotView>();
        private GameObject root;
        private Text statusText;
        private Button takeAllButton;
        private TreasureChest currentChest;
        private IInventory currentInventory;
        private Font font;

        public bool IsOpen => root != null && root.activeSelf;
        public TreasureChest CurrentChest => currentChest;

        private void Awake()
        {
            BuildLayout();
            root.SetActive(false);
        }

        private void OnEnable()
        {
            TreasureChest.OpenRequested += HandleOpenRequested;
        }

        private void OnDisable()
        {
            TreasureChest.OpenRequested -= HandleOpenRequested;
            Close();
        }

        private void HandleOpenRequested(TreasureChest chest, GameObject interactor)
        {
            Open(chest, interactor);
        }

        public void Open(TreasureChest chest, GameObject interactor)
        {
            if (chest == null)
            {
                return;
            }

            IInventory inventory = interactor != null ? interactor.GetComponentInParent<IInventory>() : null;
            if (inventory == null && interactor != null)
            {
                inventory = interactor.GetComponentInChildren<IInventory>();
            }

            Detach();
            currentChest = chest;
            currentInventory = inventory;
            currentChest.Changed += Refresh;

            root.SetActive(true);
            BackButtonStack.Push(this, Close);
            SetStatus(inventory != null ? "Chạm một ô để lấy vật phẩm." : "Không tìm thấy túi đồ của người chơi!");
            Refresh();
        }

        public void Close()
        {
            BackButtonStack.Remove(this);
            Detach();

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        /// <summary>Lấy ô thứ index (0..5) của hòm đang mở.</summary>
        public void TakeSlot(int index)
        {
            if (currentChest == null || currentInventory == null)
            {
                return;
            }

            IReadOnlyList<TreasureChest.ChestSlot> slots = currentChest.Slots;
            if (index < 0 || index >= slots.Count || slots[index].IsEmpty)
            {
                return;
            }

            TreasureChest.ChestSlot slot = slots[index];
            if (currentChest.TryTake(index, currentInventory))
            {
                SetStatus($"Đã lấy: {slot.Item.DisplayName} x{slot.Count}");
                CloseIfEmpty();
            }
            else
            {
                SetStatus("Túi đồ đã đầy!");
            }
        }

        public void TakeAll()
        {
            if (currentChest == null || currentInventory == null)
            {
                return;
            }

            int taken = currentChest.TakeAll(currentInventory);
            if (currentChest.IsEmpty)
            {
                Close();
                return;
            }

            SetStatus(taken > 0 ? "Túi đồ đã đầy, còn đồ ở lại trong hòm." : "Túi đồ đã đầy!");
        }

        private void CloseIfEmpty()
        {
            if (currentChest != null && currentChest.IsEmpty)
            {
                Close();
            }
        }

        private void Detach()
        {
            if (currentChest != null)
            {
                currentChest.Changed -= Refresh;
            }

            currentChest = null;
            currentInventory = null;
        }

        private void Refresh()
        {
            if (currentChest == null)
            {
                return;
            }

            IReadOnlyList<TreasureChest.ChestSlot> slots = currentChest.Slots;
            for (int i = 0; i < slotViews.Count; i++)
            {
                SlotView view = slotViews[i];
                bool hasItem = i < slots.Count && !slots[i].IsEmpty;

                view.Background.color = hasItem ? SlotColor : EmptySlotColor;
                view.Button.interactable = hasItem;
                view.Icon.enabled = hasItem && slots[i].Item.Icon != null;
                view.Icon.sprite = hasItem ? slots[i].Item.Icon : null;
                view.Count.text = hasItem && slots[i].Count > 1 ? "x" + slots[i].Count : string.Empty;
                view.Name.text = hasItem ? slots[i].Item.DisplayName : string.Empty;
            }

            takeAllButton.interactable = !currentChest.IsEmpty;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        // ---------------------------------------------------------------- Dựng giao diện

        private void BuildLayout()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas_TreasureChest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = canvasGo;

            // Nền mờ chặn chạm xuống màn chơi; chạm ra ngoài bảng thì đóng.
            Image dim = CreateImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            Image panel = CreateImage("Panel", canvasGo.transform, PanelColor);
            panel.rectTransform.sizeDelta = new Vector2(860f, 700f);

            Text title = CreateText("Title", panel.transform, "HÒM KHO BÁU", 40, GoldColor, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-120f, 80f));

            Button closeButton = CreateButton("CloseButton", panel.transform, "X", new Color(0.75f, 0.2f, 0.2f), 36);
            Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(96f, 96f));
            closeButton.onClick.AddListener(Close);

            var gridGo = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(panel.transform, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220f, 200f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = TreasureChest.Columns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            Place((RectTransform)gridGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(720f, 430f));

            for (int i = 0; i < TreasureChest.SlotCount; i++)
            {
                slotViews.Add(CreateSlot(gridGo.transform, i));
            }

            statusText = CreateText("Status", panel.transform, string.Empty, 26, new Color(1f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
            Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-130f, 75f), new Vector2(-340f, 80f));

            takeAllButton = CreateButton("TakeAllButton", panel.transform, "Lấy hết", new Color(0.25f, 0.6f, 0.3f), 30);
            Place((RectTransform)takeAllButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 75f), new Vector2(240f, 96f));
            takeAllButton.onClick.AddListener(TakeAll);
        }

        private SlotView CreateSlot(Transform parent, int index)
        {
            Image background = CreateImage("Slot_" + index, parent, SlotColor);
            var button = background.gameObject.AddComponent<Button>();
            // Ô trống bị tắt Button nhưng vẫn phải thấy khung ô: không làm tối khi disabled.
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            int captured = index;
            button.onClick.AddListener(() => TakeSlot(captured));

            Image icon = CreateImage("Icon", background.transform, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(130f, 130f));

            Text count = CreateText("Count", background.transform, string.Empty, 28, Color.white, TextAnchor.UpperRight);
            count.fontStyle = FontStyle.Bold;
            Place(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -26f), new Vector2(90f, 44f));

            Text name = CreateText("Name", background.transform, string.Empty, 20, new Color(0.95f, 0.9f, 0.8f), TextAnchor.MiddleCenter);
            Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(-12f, 40f));

            return new SlotView { Button = button, Background = background, Icon = icon, Count = count, Name = name };
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color, int fontSize)
        {
            Image image = CreateImage(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            Text text = CreateText("Text", image.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}

using System.Collections.Generic;
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Bảng hòm kho báu (Canvas sort order 8) gồm hai bên:
    ///   - Trái "TÚI ĐỒ": 5 ô thanh nhanh + 15 ô balo của PlayerInventory.
    ///   - Phải "HÒM": 6 ô (3 x 2) của TreasureChest.
    /// Chạm một ô bên túi để cất vào hòm, chạm một ô bên hòm để lấy vào túi. Nút "Lấy hết" lấy mọi ô.
    /// Hòm mở lại được nhiều lần; bảng không tự đóng khi hòm trống.
    /// Giao diện tự dựng bằng mã khi chạy (GameBootstrap tự tạo nếu Scene chưa có).
    /// Tích hợp BackButtonStack: nút Back đóng bảng, và PlayerController tự khóa di chuyển khi bảng mở.
    /// </summary>
    public class TreasureChestUI : MonoBehaviour
    {
        private const int InventoryColumns = 5;

        private static readonly Color PanelColor = DarkFantasyTheme.Panel;
        private static readonly Color SectionColor = DarkFantasyTheme.Inset;
        private static readonly Color SlotColor = DarkFantasyTheme.Slot;
        private static readonly Color HotbarSlotColor = new Color32(80, 78, 83, 255);
        private static readonly Color EmptySlotColor = DarkFantasyTheme.EmptySlot;
        private static readonly Color GoldColor = DarkFantasyTheme.Gold;

        /// <summary>Một ô hiển thị: nền, ảnh, số lượng và khung độ hiếm (RarityFrame).</summary>
        private class SlotView
        {
            public Image Background;
            public Image Icon;
            public Text Count;
            public Color FilledColor;
        }

        private readonly List<SlotView> chestViews = new List<SlotView>();
        private readonly List<SlotView> inventoryViews = new List<SlotView>();
        private readonly List<string> inventoryKeys = new List<string>();

        private GameObject root;
        private Text statusText;
        private Text inventoryHint;
        private Button takeAllButton;
        private TreasureChest currentChest;
        private IInventory currentInventory;
        private PlayerInventory playerInventory;
        private Font font;

        public bool IsOpen => root != null && root.activeSelf;
        public TreasureChest CurrentChest => currentChest;

        private void Awake()
        {
            for (int i = 0; i < PlayerInventory.HotbarSize; i++) inventoryKeys.Add("hotbar/" + i);
            for (int i = 0; i < PlayerInventory.BackpackSize; i++) inventoryKeys.Add("backpack/" + i);

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
            playerInventory = inventory as PlayerInventory;
            currentChest.Changed += Refresh;
            if (currentInventory != null)
            {
                currentInventory.Changed += Refresh;
            }

            root.SetActive(true);
            BackButtonStack.Push(this, Close);
            SetStatus(inventory != null
                ? "Chạm ô bên hòm để lấy, chạm ô bên túi để cất vào hòm."
                : "Không tìm thấy túi đồ của người chơi!");
            Refresh();
        }

        public void Close()
        {
            BackButtonStack.Remove(this);
            if (currentChest != null)
            {
                currentChest.SetOpen(false);
            }

            Detach();

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        /// <summary>Lấy ô thứ index (0..5) của hòm vào túi đồ.</summary>
        public void TakeSlot(int index)
        {
            if (currentChest == null || currentInventory == null)
            {
                return;
            }

            IReadOnlyList<ItemStack> slots = currentChest.Slots;
            if (index < 0 || index >= slots.Count || slots[index].IsEmpty)
            {
                return;
            }

            ItemStack stack = slots[index];
            int moved = currentChest.Container.TransferTo(index, currentInventory);
            if (moved <= 0)
            {
                SetStatus("Túi đồ đã đầy!");
            }
            else if (moved < stack.Count)
            {
                SetStatus($"Đã lấy: {stack.Item.DisplayName} x{moved} (túi đầy, phần còn lại ở trong hòm)");
            }
            else
            {
                SetStatus($"Đã lấy: {stack.Item.DisplayName} x{moved}");
            }
        }

        /// <summary>Cất cả ô túi đồ slotKey ("hotbar/i" hoặc "backpack/i") vào hòm.</summary>
        public void StoreSlot(string slotKey)
        {
            if (currentChest == null || playerInventory == null)
            {
                return;
            }

            InventorySlot slot = playerInventory.GetSlot(slotKey);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            ItemDefinitionSO item = slot.Item;
            int stored = currentChest.Put(item, slot.Count);
            if (stored <= 0)
            {
                SetStatus("Hòm đã đầy!");
                return;
            }

            if (!playerInventory.TryRemoveFromSlot(slotKey, stored))
            {
                // Không bớt được khỏi túi (hiếm): hoàn tác để không nhân đôi vật phẩm.
                RemoveFromChest(item, stored);
                SetStatus("Không cất được vật phẩm này.");
                return;
            }

            SetStatus($"Đã cất: {item.DisplayName} x{stored}");
        }

        public void TakeAll()
        {
            if (currentChest == null || currentInventory == null)
            {
                return;
            }

            if (currentChest.IsEmpty)
            {
                SetStatus("Hòm trống.");
                return;
            }

            currentChest.TakeAll(currentInventory);
            SetStatus(currentChest.IsEmpty ? "Đã lấy hết đồ trong hòm." : "Túi đồ đã đầy, còn đồ ở lại trong hòm.");
        }

        private void RemoveFromChest(ItemDefinitionSO item, int count)
        {
            ItemContainer container = currentChest.Container;
            for (int i = container.SlotCount - 1; i >= 0 && count > 0; i--)
            {
                if (container.Get(i).Item == item)
                {
                    count -= container.RemoveAt(i, count);
                }
            }
        }

        private void Detach()
        {
            if (currentChest != null)
            {
                currentChest.Changed -= Refresh;
            }

            if (currentInventory != null)
            {
                currentInventory.Changed -= Refresh;
            }

            currentChest = null;
            currentInventory = null;
            playerInventory = null;
        }

        private void Refresh()
        {
            if (currentChest == null)
            {
                return;
            }

            IReadOnlyList<ItemStack> slots = currentChest.Slots;
            for (int i = 0; i < chestViews.Count; i++)
            {
                ItemStack stack = i < slots.Count ? slots[i] : default;
                ShowStack(chestViews[i], stack.IsEmpty ? null : stack.Item, stack.Count);
            }

            for (int i = 0; i < inventoryViews.Count; i++)
            {
                InventorySlot slot = playerInventory != null ? playerInventory.GetSlot(inventoryKeys[i]) : null;
                bool has = slot != null && !slot.IsEmpty;
                ShowStack(inventoryViews[i], has ? slot.Item : null, has ? slot.Count : 0);
            }

            inventoryHint.gameObject.SetActive(playerInventory == null);
            takeAllButton.interactable = !currentChest.IsEmpty && currentInventory != null;
        }

        private static void ShowStack(SlotView view, ItemDefinitionSO item, int count)
        {
            bool has = item != null;
            view.Background.color = has ? view.FilledColor : EmptySlotColor;
            view.Icon.enabled = has && item.Icon != null;
            view.Icon.sprite = has ? item.Icon : null;
            view.Count.text = has && count > 1 ? "x" + count : string.Empty;
            RarityFrame.Apply(view.Background.rectTransform, item);
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
            DarkFantasyTheme.FramePanel(panel, Color.white);
            panel.rectTransform.sizeDelta = new Vector2(1640f, 900f);

            Text title = CreateText("Title", panel.transform, "HÒM KHO BÁU", 42, GoldColor, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -55f), new Vector2(-240f, 80f));

            Button closeButton = CreateButton("CloseButton", panel.transform, "X", DarkFantasyTheme.Danger, 36);
            Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(96f, 96f));
            closeButton.onClick.AddListener(Close);

            // ---- Bên trái: túi đồ (thanh nhanh + balo)
            RectTransform left = CreateSection(panel.transform, "InventorySection", "TÚI ĐỒ",
                new Vector2(40f, 150f), new Vector2(900f, 640f));
            Transform invGrid = CreateGrid(left, "InventoryGrid", InventoryColumns, new Vector2(140f, 110f), new Vector2(14f, 14f),
                new Vector2(0f, -20f), new Vector2(InventoryColumns * 140f + 4 * 14f, 4 * 110f + 3 * 14f));
            for (int i = 0; i < inventoryKeys.Count; i++)
            {
                string key = inventoryKeys[i];
                Color filled = i < PlayerInventory.HotbarSize ? HotbarSlotColor : SlotColor;
                inventoryViews.Add(CreateSlot(invGrid, "Inv_" + key.Replace('/', '_'), filled, () => StoreSlot(key)));
            }

            Text hotbarLabel = CreateText("HotbarLabel", left, "Hàng đầu: thanh nhanh", 20, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleLeft);
            Place(hotbarLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 26f), new Vector2(-40f, 36f));

            inventoryHint = CreateText("NoInventory", left, "Không có túi đồ để cất.", 26, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
            Stretch(inventoryHint.rectTransform);

            // Mũi tên ở giữa gợi ý có thể chuyển qua lại.
            Text arrows = CreateText("Arrows", panel.transform, "»«", 64, GoldColor, TextAnchor.MiddleCenter);
            Place(arrows.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1000f, 470f), new Vector2(120f, 120f));

            // ---- Bên phải: các ô của hòm
            RectTransform right = CreateSection(panel.transform, "ChestSection", "HÒM",
                new Vector2(1070f, 150f), new Vector2(530f, 640f));
            Transform chestGrid = CreateGrid(right, "ChestGrid", TreasureChest.Columns, new Vector2(140f, 140f), new Vector2(16f, 16f),
                new Vector2(0f, 10f), new Vector2(TreasureChest.Columns * 140f + 2 * 16f, 2 * 140f + 16f));
            for (int i = 0; i < TreasureChest.SlotCount; i++)
            {
                int captured = i;
                chestViews.Add(CreateSlot(chestGrid, "Chest_" + i, SlotColor, () => TakeSlot(captured)));
            }

            takeAllButton = CreateButton("TakeAllButton", right, "Lấy hết", DarkFantasyTheme.Success, 30);
            Place((RectTransform)takeAllButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(260f, 90f));
            takeAllButton.onClick.AddListener(TakeAll);

            statusText = CreateText("Status", panel.transform, string.Empty, 26, new Color(1f, 0.95f, 0.75f), TextAnchor.MiddleCenter);
            statusText.supportRichText = true;
            Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 75f), new Vector2(-80f, 80f));
        }

        private RectTransform CreateSection(Transform parent, string name, string label, Vector2 bottomLeft, Vector2 size)
        {
            Image bg = CreateImage(name, parent, SectionColor);
            DarkFantasyTheme.FramePanel(bg, Color.white);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = bottomLeft;
            rt.sizeDelta = size;

            Text header = CreateText("Header", rt, label, 32, GoldColor, TextAnchor.MiddleCenter);
            header.fontStyle = FontStyle.Bold;
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(0f, 60f));
            return rt;
        }

        private static Transform CreateGrid(Transform parent, string name, int columns, Vector2 cell, Vector2 spacing, Vector2 position, Vector2 size)
        {
            var gridGo = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(parent, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            Place((RectTransform)gridGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            return gridGo.transform;
        }

        private SlotView CreateSlot(Transform parent, string name, Color filledColor, UnityEngine.Events.UnityAction onClick)
        {
            Image background = CreateImage(name, parent, EmptySlotColor);
            DarkFantasyTheme.FrameSlot(background, EmptySlotColor);
            var button = background.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            Image icon = CreateImage("Icon", background.transform, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRt = icon.rectTransform;
            iconRt.anchorMin = new Vector2(0.12f, 0.12f);
            iconRt.anchorMax = new Vector2(0.88f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            Text count = CreateText("Count", background.transform, string.Empty, 24, Color.white, TextAnchor.LowerRight);
            count.fontStyle = FontStyle.Bold;
            var shadow = count.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            RectTransform countRt = count.rectTransform;
            countRt.anchorMin = Vector2.zero;
            countRt.anchorMax = Vector2.one;
            countRt.offsetMin = new Vector2(6f, 4f);
            countRt.offsetMax = new Vector2(-8f, -4f);

            return new SlotView { Background = background, Icon = icon, Count = count, FilledColor = filledColor };
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
            DarkFantasyTheme.FrameButton(button, color);
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

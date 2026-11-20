using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Giao diện bảng Cửa hàng tại Sảnh (Canvas sort order 8).
    /// Bố cục: bên trái là lưới vật phẩm (ảnh, tên, giá) trong khung cuộn được; chạm một ô thì
    /// bên phải hiện chi tiết (ảnh lớn, mô tả, giá) và nút Mua.
    /// Lưới và bảng chi tiết tự dựng bằng mã bên trong vùng itemsContainer, nên Scene chỉ cần
    /// khung bảng (tiêu đề, số xu, nút đóng, dòng thông báo) và một vùng trống cho itemsContainer.
    /// Tích hợp BackButtonStack để nút Back đóng bảng, cập nhật số xu theo RunState.
    /// Quy trình mua: HasSpaceFor -> TrySpendCoins -> TryAdd.
    /// </summary>
    public class LobbyShopUI : MonoBehaviour
    {
        private static readonly Color CellColor = DarkFantasyTheme.Slot;
        private static readonly Color CellSelectedColor = DarkFantasyTheme.Selected;
        private static readonly Color DetailColor = DarkFantasyTheme.Panel;
        private static readonly Color PriceColor = DarkFantasyTheme.Gold;

        [Header("Catalog & References")]
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text statusNotificationText;

        [Header("Vùng nội dung")]
        [Tooltip("Vùng chứa lưới vật phẩm và bảng chi tiết (tự dựng khi chạy).")]
        [SerializeField] private RectTransform itemsContainer;
        [SerializeField] private Button closeButton;

        [Header("Lưới")]
        [Tooltip("Số cột của lưới; bề rộng ô tự tính theo bề rộng khung.")]
        [SerializeField, Min(1)] private int columns = 3;
        [Tooltip("Tỉ lệ cao / rộng của một ô.")]
        [SerializeField] private float cellAspect = 1.15f;
        [SerializeField] private float cellSpacing = 16f;

        private class CellView
        {
            public ItemDefinitionSO Item;
            public Image Background;
        }

        private readonly List<CellView> cells = new List<CellView>();
        private GameObject currentInteractor;
        private ItemDefinitionSO selectedItem;
        private bool layoutBuilt;
        private Font font;

        private ScrollRect gridScroll;
        private RectTransform gridContent;
        private GridLayoutGroup gridLayout;
        private float lastViewportWidth = -1f;
        private GameObject detailEmptyHint;
        private GameObject detailBody;
        private Image detailIcon;
        private Text detailName;
        private Text detailPrice;
        private Text detailDescription;
        private Button buyButton;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public ItemDefinitionSO SelectedItem => selectedItem;
        public ScrollRect GridScroll => gridScroll;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            EnsureLayout();
        }

        private void LateUpdate()
        {
            FitCellsToViewport();
        }

        /// <summary>Chia đều bề rộng khung cho số cột, để lưới luôn vừa khít trên mọi tỉ lệ màn hình.</summary>
        private void FitCellsToViewport()
        {
            if (gridScroll == null || gridLayout == null)
            {
                return;
            }

            float width = gridScroll.viewport.rect.width;
            // While the canvas is resizing (safe area, device aspect, camera change), the
            // content and viewport may briefly disagree. Fit the narrower actual rect.
            if (gridContent != null && gridContent.rect.width > 0f)
                width = Mathf.Min(width, gridContent.rect.width);
            if (width <= 0f)
            {
                return;
            }

            float usable = width - gridLayout.padding.left - gridLayout.padding.right - cellSpacing * (columns - 1);
            float cellWidth = Mathf.Max(1f, usable / Mathf.Max(1, columns));
            if (Mathf.Approximately(width, lastViewportWidth) && Mathf.Approximately(gridLayout.cellSize.x, cellWidth)) return;
            lastViewportWidth = width;
            gridLayout.cellSize = new Vector2(cellWidth, cellWidth * cellAspect);
            if (gridContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
        }

        private void OnEnable()
        {
            RunState.CoinsChanged += HandleCoinsChanged;
            UpdateCoinsDisplay(RunState.Coins);
        }

        private void OnDisable()
        {
            RunState.CoinsChanged -= HandleCoinsChanged;
            BackButtonStack.Remove(this);
        }

        public void Open(GameObject interactor)
        {
            currentInteractor = interactor != null ? interactor : GameObject.FindWithTag("Player");

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            BackButtonStack.Push(this, Close);
            UpdateCoinsDisplay(RunState.Coins);
            ClearStatusNotification();
            PopulateItems();
        }

        public void Close()
        {
            BackButtonStack.Remove(this);

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            currentInteractor = null;
        }

        public void SetCatalog(ShopCatalogSO newCatalog)
        {
            catalog = newCatalog;
            if (IsOpen)
            {
                PopulateItems();
            }
        }

        private void HandleCoinsChanged(int newAmount)
        {
            UpdateCoinsDisplay(newAmount);
        }

        private void UpdateCoinsDisplay(int amount)
        {
            if (coinsText != null)
            {
                coinsText.text = $"Xu: {amount}";
            }

            RefreshBuyButton();
        }

        private void ShowStatus(string message)
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = message;
            }
            Debug.Log($"[LobbyShopUI] {message}");
        }

        private void ClearStatusNotification()
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = string.Empty;
            }
        }

        public void PopulateItems()
        {
            EnsureLayout();
            if (gridContent == null)
            {
                return;
            }

            for (int i = gridContent.childCount - 1; i >= 0; i--)
            {
                var oldCell = gridContent.GetChild(i).gameObject;
                oldCell.SetActive(false); // Do not include deferred destruction in this frame's layout.
                Destroy(oldCell);
            }
            cells.Clear();

            if (catalog != null && catalog.Items != null)
            {
                foreach (ItemDefinitionSO item in catalog.Items)
                {
                    if (item != null)
                    {
                        cells.Add(CreateCell(item));
                    }
                }
            }

            // Danh sách mới mở thì cuộn về đầu.
            gridContent.anchoredPosition = Vector2.zero;
            if (gridScroll != null)
            {
                gridScroll.StopMovement();
                Canvas.ForceUpdateCanvases();
                FitCellsToViewport();
                gridScroll.verticalNormalizedPosition = 1f;
            }

            SelectItem(null);
        }

        /// <summary>Chọn một vật phẩm để xem chi tiết (null = bỏ chọn).</summary>
        public void SelectItem(ItemDefinitionSO item)
        {
            selectedItem = item;

            foreach (CellView cell in cells)
            {
                cell.Background.color = cell.Item == item ? CellSelectedColor : CellColor;
            }

            if (detailBody == null)
            {
                return;
            }

            detailBody.SetActive(item != null);
            detailEmptyHint.SetActive(item == null);

            if (item != null)
            {
                detailIcon.sprite = item.Icon;
                detailIcon.enabled = item.Icon != null;
                detailName.text = item.DisplayName;
                detailName.color = ItemRarityStyle.GetColor(item.Rarity);
                detailPrice.supportRichText = true;
                detailPrice.text = $"Giá: {item.Price} xu   •   {ItemRarityStyle.ColoredName(item.Rarity)}";
                string stats = item.GetStatsText();
                detailDescription.text = string.IsNullOrEmpty(stats) ? item.Description : item.Description + "\n\n" + stats;
                RarityFrame.Apply(detailIcon.rectTransform, item);
            }

            RefreshBuyButton();
        }

        public void BuySelected()
        {
            TryBuyItem(selectedItem);
        }

        private void RefreshBuyButton()
        {
            if (buyButton != null)
            {
                buyButton.interactable = selectedItem != null && RunState.Coins >= selectedItem.Price;
            }
        }

        public void TryBuyItem(ItemDefinitionSO item)
        {
            if (item == null)
            {
                return;
            }

            // 1. Tìm IInventory trên người chơi (tag Player)
            GameObject player = currentInteractor != null ? currentInteractor : GameObject.FindWithTag("Player");
            if (player == null)
            {
                ShowStatus("Lỗi: Không tìm thấy người chơi!");
                return;
            }

            IInventory inventory = player.GetComponent<IInventory>();
            if (inventory == null)
            {
                inventory = player.GetComponentInParent<IInventory>();
            }

            if (inventory == null)
            {
                ShowStatus("Lỗi: Người chơi không có túi đồ (IInventory)!");
                return;
            }

            // 2. Kiểm tra HasSpaceFor
            if (!inventory.HasSpaceFor(item, 1))
            {
                ShowStatus(item.Category==ItemCategory.Material?"Ngăn nguyên liệu đã đạt giới hạn!":"Túi đồ đã đầy, không thể mua thêm!");
                return;
            }

            // 3. RunState.TrySpendCoins(Price)
            if (!RunState.TrySpendCoins(item.Price))
            {
                ShowStatus($"Không đủ xu! Cần {item.Price} xu (hiện có: {RunState.Coins} xu).");
                return;
            }

            // 4. TryAdd vào túi đồ
            if (!inventory.TryAdd(item, 1))
            {
                // Hoàn lại xu nếu add thất bại bất ngờ
                RunState.AddCoins(item.Price);
                ShowStatus("Lỗi khi thêm vật phẩm vào túi đồ!");
                return;
            }

            ShowStatus($"Đã mua thành công: {item.DisplayName} (-{item.Price} xu).");
        }

        // ---------------------------------------------------------------- Dựng giao diện

        private void EnsureLayout()
        {
            if (layoutBuilt || itemsContainer == null)
            {
                return;
            }

            layoutBuilt = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Bố cục cũ (danh sách dọc) có thể còn layout group trên vùng chứa: bỏ đi.
            var oldLayout = itemsContainer.GetComponent<LayoutGroup>();
            if (oldLayout != null)
            {
                DestroyImmediate(oldLayout);
            }

            for (int i = itemsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(itemsContainer.GetChild(i).gameObject);
            }

            BuildGrid();
            BuildDetailPanel();
        }

        private void BuildGrid()
        {
            // Khung cuộn: kéo dọc để xem hết danh sách, phần tràn bị cắt bởi RectMask2D.
            var scrollGo = new GameObject("GridScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(itemsContainer, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(0.6f, 1f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(-12f, 0f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            gridContent = (RectTransform)contentGo.transform;
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.anchoredPosition = Vector2.zero;
            gridContent.sizeDelta = Vector2.zero;

            gridLayout = contentGo.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(200f, 200f * cellAspect);
            gridLayout.spacing = new Vector2(cellSpacing, cellSpacing);
            gridLayout.padding = new RectOffset(16, 16, 16, 16);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            gridScroll = scrollGo.GetComponent<ScrollRect>();
            gridScroll.content = gridContent;
            gridScroll.viewport = scrollRt;
            gridScroll.horizontal = false;
            gridScroll.vertical = true;
            gridScroll.movementType = ScrollRect.MovementType.Elastic;
            gridScroll.scrollSensitivity = 40f;
            gridScroll.inertia = true;
        }

        private void BuildDetailPanel()
        {
            Image panel = CreateImage("DetailPanel", itemsContainer, DetailColor);
            DarkFantasyTheme.FramePanel(panel, Color.white);
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0.6f, 0f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.offsetMin = new Vector2(12f, 0f);
            panelRt.offsetMax = Vector2.zero;

            Text hint = CreateText("EmptyHint", panel.transform, "Chạm vào một vật phẩm để xem chi tiết.", 26, new Color(0.75f, 0.78f, 0.85f), TextAnchor.MiddleCenter);
            Stretch(hint.rectTransform, new Vector2(24f, 24f));
            detailEmptyHint = hint.gameObject;

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(panel.transform, false);
            Stretch((RectTransform)body.transform, Vector2.zero);
            detailBody = body;

            detailIcon = CreateImage("Icon", body.transform, Color.white);
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            Anchor(detailIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(170f, 170f));

            detailName = CreateText("Name", body.transform, string.Empty, 32, Color.white, TextAnchor.MiddleCenter);
            detailName.fontStyle = FontStyle.Bold;
            AnchorStretchX(detailName.rectTransform, 1f, -230f, 50f);

            detailPrice = CreateText("Price", body.transform, string.Empty, 28, PriceColor, TextAnchor.MiddleCenter);
            detailPrice.fontStyle = FontStyle.Bold;
            AnchorStretchX(detailPrice.rectTransform, 1f, -280f, 40f);

            detailDescription = CreateText("Description", body.transform, string.Empty, 22, new Color(0.85f, 0.87f, 0.92f), TextAnchor.UpperLeft);
            detailDescription.resizeTextForBestFit = true;
            detailDescription.resizeTextMinSize = 14;
            detailDescription.resizeTextMaxSize = 22;
            var descRt = detailDescription.rectTransform;
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.offsetMin = new Vector2(24f, 130f);
            descRt.offsetMax = new Vector2(-24f, -310f);

            Image buyImage = CreateImage("BuyButton", body.transform, DarkFantasyTheme.Success);
            buyButton = buyImage.gameObject.AddComponent<Button>();
            DarkFantasyTheme.FrameButton(buyButton, DarkFantasyTheme.Success);
            buyButton.onClick.AddListener(BuySelected);
            var buyColors = buyButton.colors;
            buyColors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            buyButton.colors = buyColors;
            Anchor(buyImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(260f, 96f));

            Text buyText = CreateText("Text", buyImage.transform, "Mua", 32, Color.white, TextAnchor.MiddleCenter);
            buyText.fontStyle = FontStyle.Bold;
            Stretch(buyText.rectTransform, Vector2.zero);

            body.SetActive(false);
        }

        private CellView CreateCell(ItemDefinitionSO item)
        {
            Image background = CreateImage("Cell_" + item.ItemId, gridContent, CellColor);
            DarkFantasyTheme.FrameSlot(background, CellColor);
            var button = background.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => SelectItem(item));

            // Bố cục theo tỉ lệ ô: ảnh ở 60% trên, tên và giá ở dưới.
            Image icon = CreateImage("Icon", background.transform, Color.white);
            icon.sprite = item.Icon;
            icon.enabled = item.Icon != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            AnchorBox(icon.rectTransform, new Vector2(0.15f, 0.4f), new Vector2(0.85f, 0.95f));

            Text name = CreateText("Name", background.transform, item.DisplayName, 22, Color.white, TextAnchor.MiddleCenter);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 14;
            name.resizeTextMaxSize = 24;
            AnchorBox(name.rectTransform, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.4f));

            Text price = CreateText("Price", background.transform, item.Price + " xu", 22, PriceColor, TextAnchor.MiddleCenter);
            price.fontStyle = FontStyle.Bold;
            AnchorBox(price.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.18f));

            RarityFrame.Apply(background.rectTransform, item);
            return new CellView { Item = item, Background = background };
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

        private static void Stretch(RectTransform rt, Vector2 padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = padding;
            rt.offsetMax = -padding;
        }

        private static void AnchorBox(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Kéo giãn theo chiều ngang, neo ở anchorY (0 = đáy, 1 = đỉnh), y là vị trí tâm.</summary>
        private static void AnchorStretchX(RectTransform rt, float anchorY, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-16f, height);
        }
    }
}

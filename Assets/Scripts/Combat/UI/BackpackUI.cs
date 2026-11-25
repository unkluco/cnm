using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LightSouls.Common;
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;

namespace LightSouls.Combat.UI
{
    [Serializable]
    public class BackpackSlotView
    {
        public string slotKey;
        public Button button;
        public Image iconImage;
        public Text countText;
        public GameObject highlightObject;
        public Text labelText;
    }

    /// <summary>
    /// Giao diện Balo người chơi (Canvas sort order 8).
    /// Hiển thị 5 ô thanh nhanh, 15 ô balo, và 2 ô trang bị (Giáp, Giày).
    /// Hỗ trợ chạm để hoán đổi/chuyển đồ giữa các ô.
    /// Tích hợp BackButtonStack để nút Back đóng Balo trước khi mở Pause Menu.
    /// </summary>
    public partial class BackpackUI : MonoBehaviour
    {
        [Header("Tham chiếu Nhân vật & Túi đồ")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInventory inventory;

        [Header("Nút Đóng (X)")]
        [SerializeField] private Button closeButton;

        [Header("Các nhóm ô")]
        [SerializeField] private List<BackpackSlotView> hotbarSlots = new List<BackpackSlotView>();
        [SerializeField] private List<BackpackSlotView> backpackSlots = new List<BackpackSlotView>();
        [SerializeField] private BackpackSlotView armorSlot;
        [SerializeField] private BackpackSlotView bootsSlot;

        [Header("Bảng thông tin chi tiết vật phẩm")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Text itemNameText;
        [SerializeField] private Text itemCategoryText;
        [SerializeField] private Text itemDescriptionText;
        [SerializeField] private Text itemStatsText;
        [SerializeField] private Image itemIconImage;

        [Header("Nút Vứt Đồ (Drop Item)")]
        [SerializeField] private Button dropItemButton;
        [SerializeField] private Text dropItemButtonText;

        [Header("Kéo thả vật phẩm (Drag & Drop)")]
        [SerializeField] private GameObject dragProxyObject;
        [SerializeField] private Image dragProxyImage;
        [SerializeField] private Text dragProxyCountText;
        [SerializeField] private float dragScaleMultiplier = 1.3f;

        private readonly Dictionary<string, BackpackSlotView> _allViews = new Dictionary<string, BackpackSlotView>();
        private string _selectedSlotKey = null;
        private string _draggingSourceKey = null;
        private InventorySlotUI _draggingSourceSlotUI = null;
        private bool _dropOccurred = false;
        private bool _isOpen = false;
        private ScrollableItemDetails itemDetailScroll;

        public bool IsOpen => _isOpen;

        public InventorySlot GetSlotData(string key)
        {
            return inventory != null ? inventory.GetSlot(key) : null;
        }

        private void Awake()
        {
            RegisterViews();
            BuildMaterialTabs();

            if(itemStatsText!=null&&itemDescriptionText!=null)
            {
                // The old stats field had room for only a few lines. Give the
                // four skill descriptions and the item lore one scroll region.
                var rt=itemStatsText.rectTransform;
                rt.anchoredPosition=new Vector2(0,-82);rt.sizeDelta=new Vector2(320,300);
                itemDetailScroll=ScrollableItemDetails.Wrap(itemStatsText,"ItemDetailScroll");
                itemDescriptionText.gameObject.SetActive(false);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (dropItemButton != null)
            {
                dropItemButton.onClick.AddListener(OnDropButtonClicked);
            }
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (inventory == null && player != null)
            {
                inventory = player.Inventory;
            }

            if (inventory != null)
            {
                // Gỡ trước khi gắn: SetPlayer có thể đã chạy trước Start.
                inventory.Changed -= Refresh;
                inventory.Changed += Refresh;
            }

            // Mặc định ẩn lúc bắt đầu, trừ khi đã được Open() trước khi Start chạy
            // (bảng được đặt ẩn sẵn trong Scene thì Start chỉ chạy ở lần mở đầu tiên).
            if (!_isOpen)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }

            BackButtonStack.Remove(this);
        }

        private void RegisterViews()
        {
            _allViews.Clear();

            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                string key = $"hotbar/{i}";
                hotbarSlots[i].slotKey = key;
                _allViews[key] = hotbarSlots[i];
                int idx = i;
                if (hotbarSlots[i].button != null)
                {
                    hotbarSlots[i].button.onClick.AddListener(() => OnSlotClicked(key));
                }
            }

            for (int i = 0; i < backpackSlots.Count; i++)
            {
                string key = $"backpack/{i}";
                backpackSlots[i].slotKey = key;
                _allViews[key] = backpackSlots[i];
                int idx = i;
                if (backpackSlots[i].button != null)
                {
                    backpackSlots[i].button.onClick.AddListener(() => OnSlotClicked(key));
                }
            }

            if (armorSlot != null)
            {
                armorSlot.slotKey = "equip/armor";
                _allViews[armorSlot.slotKey] = armorSlot;
                if (armorSlot.button != null)
                {
                    armorSlot.button.onClick.AddListener(() => OnSlotClicked(armorSlot.slotKey));
                }
            }

            if (bootsSlot != null)
            {
                bootsSlot.slotKey = "equip/boots";
                _allViews[bootsSlot.slotKey] = bootsSlot;
                if (bootsSlot.button != null)
                {
                    bootsSlot.button.onClick.AddListener(() => OnSlotClicked(bootsSlot.slotKey));
                }
            }

            // Gắn hoặc cấu hình InventorySlotUI cho toàn bộ các ô để hỗ trợ Kéo - Thả (Drag & Drop)
            foreach (var view in _allViews.Values)
            {
                if (view.button != null)
                {
                    var slotUI = view.button.GetComponent<InventorySlotUI>();
                    if (slotUI == null)
                    {
                        slotUI = view.button.gameObject.AddComponent<InventorySlotUI>();
                    }
                    slotUI.Initialize(this, view.slotKey);
                }
            }
        }

        public void SetPlayer(PlayerController newPlayer)
        {
            if (player == newPlayer) return;

            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }

            player = newPlayer;
            inventory = player != null ? player.Inventory : null;

            if (inventory != null)
            {
                inventory.Changed += Refresh;
            }
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen || Time.timeScale <= 0f || BackButtonStack.HasOpenPanel) return;
            if (player == null)
            {
                SetPlayer(FindAnyObjectByType<PlayerController>());
            }
            if (player == null || player.IsDead) return;
            _isOpen = true;

            gameObject.SetActive(true);

            // Đăng ký với BackButtonStack để phím Back (Esc) đóng Balo
            BackButtonStack.Push(this, Close);

            ClearSelection();
            SetMaterialTab(false);
            Refresh();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            BackButtonStack.Remove(this);

            ClearSelection();
            gameObject.SetActive(false);
        }

        public void OnSlotClicked(string slotKey)
        {
            if (inventory == null) return;
            if(_selectedSlotKey!=null&&_selectedSlotKey.StartsWith(PlayerInventory.MaterialSlotPrefix))ClearSelection();

            var clickedSlot = inventory.GetSlot(slotKey);

            // Trường hợp 1: Chưa chọn ô nào
            if (string.IsNullOrEmpty(_selectedSlotKey))
            {
                if (clickedSlot != null && !clickedSlot.IsEmpty)
                {
                    SelectSlot(slotKey);
                }
                return;
            }

            // Trường hợp 2: Đang chọn 1 ô
            if (_selectedSlotKey == slotKey)
            {
                // Bấm lại chính ô đó -> Bỏ chọn
                ClearSelection();
                return;
            }

            // Bấm ô khác -> Thực hiện chuyển / hoán đổi đồ giữa 2 ô
            string sourceKey = _selectedSlotKey;
            bool success = inventory.SwapOrMoveSlots(sourceKey, slotKey);

            if (success)
            {
                // Nếu sau khi chuyển ô đích có đồ thì chọn ô đích
                var targetSlot = inventory.GetSlot(slotKey);
                if (targetSlot != null && !targetSlot.IsEmpty)
                {
                    SelectSlot(slotKey);
                }
                else
                {
                    ClearSelection();
                }
            }
            else
            {
                // Nếu không chuyển được (ví dụ sai loại trang bị) nhưng ô đích có đồ, đổi vùng chọn sang ô đích
                if (clickedSlot != null && !clickedSlot.IsEmpty)
                {
                    SelectSlot(slotKey);
                }
                else
                {
                    ClearSelection();
                }
            }

            Refresh();
        }

        #region Kéo Thả Vật Phẩm (Drag & Drop)

        private void EnsureDragProxy()
        {
            if (dragProxyObject != null) return;

            dragProxyObject = new GameObject("DragProxy");
            dragProxyObject.transform.SetParent(transform, false);
            RectTransform rt = dragProxyObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            dragProxyImage = dragProxyObject.AddComponent<Image>();
            dragProxyImage.raycastTarget = false;

            GameObject cnt = new GameObject("Count");
            cnt.transform.SetParent(dragProxyObject.transform, false);
            RectTransform cntRt = cnt.AddComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(1f, 0f);
            cntRt.anchorMax = new Vector2(1f, 0f);
            cntRt.anchoredPosition = new Vector2(-10f, 10f);
            cntRt.sizeDelta = new Vector2(40f, 24f);

            dragProxyCountText = cnt.AddComponent<Text>();
            dragProxyCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dragProxyCountText.fontSize = 20;
            dragProxyCountText.fontStyle = FontStyle.Bold;
            dragProxyCountText.alignment = TextAnchor.LowerRight;
            dragProxyCountText.color = Color.white;
            dragProxyCountText.raycastTarget = false;

            dragProxyObject.SetActive(false);
        }

        public void OnSlotPointerDown(InventorySlotUI sourceUI, InventorySlot slot, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty) return;
            SelectSlot(sourceUI.SlotKey);
        }

        public void OnBeginDragSlot(InventorySlotUI sourceUI, InventorySlot slot, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty) return;

            _draggingSourceKey = sourceUI.SlotKey;
            _draggingSourceSlotUI = sourceUI;
            _dropOccurred = false;

            EnsureDragProxy();

            if (dragProxyObject != null)
            {
                dragProxyObject.SetActive(true);
                dragProxyObject.transform.SetAsLastSibling();
                // Phóng to item lên khi đè và kéo (1.3x theo yêu cầu)
                dragProxyObject.transform.localScale = Vector3.one * dragScaleMultiplier;
                dragProxyObject.transform.position = eventData.position;

                if (dragProxyImage != null)
                {
                    dragProxyImage.sprite = slot.Item.Icon;
                    dragProxyImage.preserveAspect = true;
                }

                if (dragProxyCountText != null)
                {
                    dragProxyCountText.gameObject.SetActive(slot.Count > 1);
                    dragProxyCountText.text = slot.Count.ToString();
                }
            }

            // Làm mờ nhẹ ô nguồn
            if (sourceUI.IconImage != null)
            {
                sourceUI.IconImage.color = new Color(1f, 1f, 1f, 0.35f);
            }

            // Chọn ô nguồn để hiện thông tin
            SelectSlot(sourceUI.SlotKey);
        }

        public void OnDragSlot(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (dragProxyObject != null && dragProxyObject.activeSelf)
            {
                dragProxyObject.transform.position = eventData.position;
            }
        }

        public void OnDropOnSlot(InventorySlotUI targetUI, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_draggingSourceKey) || targetUI == null || inventory == null) return;

            string targetKey = targetUI.SlotKey;
            if (targetKey != _draggingSourceKey)
            {
                bool success = inventory.SwapOrMoveSlots(_draggingSourceKey, targetKey);
                if (success)
                {
                    SelectSlot(targetKey);
                }
                _dropOccurred = true;
            }
        }

        public void OnEndDragSlot(InventorySlotUI sourceUI, UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Dự phòng nếu con trỏ thả lệch nhưng trúng ô khác
            if (!_dropOccurred && !string.IsNullOrEmpty(_draggingSourceKey) && inventory != null)
            {
                var hitObj = eventData.pointerCurrentRaycast.gameObject;
                if (hitObj != null)
                {
                    var hitSlot = hitObj.GetComponentInParent<InventorySlotUI>();
                    if (hitSlot != null && hitSlot.SlotKey != _draggingSourceKey)
                    {
                        inventory.SwapOrMoveSlots(_draggingSourceKey, hitSlot.SlotKey);
                        SelectSlot(hitSlot.SlotKey);
                    }
                }
            }

            if (dragProxyObject != null)
            {
                dragProxyObject.transform.localScale = Vector3.one;
                dragProxyObject.SetActive(false);
            }

            if (_draggingSourceSlotUI != null && _draggingSourceSlotUI.IconImage != null)
            {
                _draggingSourceSlotUI.IconImage.color = Color.white;
            }

            _draggingSourceKey = null;
            _draggingSourceSlotUI = null;
            _dropOccurred = false;

            Refresh();
        }

        #endregion

        private void SelectSlot(string slotKey)
        {
            _selectedSlotKey = slotKey;
            UpdateHighlights();

            var slot = inventory != null ? inventory.GetSlot(slotKey) : null;
            if (slot != null && !slot.IsEmpty)
            {
                ShowDetail(slot.Item, slot.Count);
            }
            else
            {
                HideDetail();
            }
        }

        private void ClearSelection()
        {
            _selectedSlotKey = null;
            UpdateHighlights();
            HideDetail();
        }

        private void UpdateHighlights()
        {
            foreach (var kvp in _allViews)
            {
                if (kvp.Value.highlightObject != null)
                {
                    kvp.Value.highlightObject.SetActive(kvp.Key == _selectedSlotKey);
                }
            }
            UpdateMaterialHighlights();
        }

        private void ShowDetail(ItemDefinitionSO item, int count)
        {
            if (detailPanel != null) detailPanel.SetActive(true);

            if (itemNameText != null)
            {
                itemNameText.text = item.DisplayName;
                itemNameText.color = ItemRarityStyle.GetColor(item.Rarity);
            }
            if (itemDescriptionText != null) itemDescriptionText.text = item.Description;

            if (itemCategoryText != null)
            {
                itemCategoryText.text = item.Category switch
                {
                    ItemCategory.Weapon => "[VŨ KHÍ]",
                    ItemCategory.Equipment => "[TRANG BỊ]",
                    ItemCategory.Consumable => "[TIÊU HAO]",
                    ItemCategory.Material => "[NGUYÊN LIỆU]",
                    _ => "[VẬT PHẨM]"
                } + "  " + ItemRarityStyle.ColoredName(item.Rarity);
                itemCategoryText.supportRichText = true;
            }

            if (itemStatsText != null)
            {
                // Mỗi loại vật phẩm tự mô tả chỉ số (ItemDefinitionSO.GetStatsText), kể cả hiệu ứng đặc biệt.
                string stats=item.GetStatsText();
                if(itemDetailScroll!=null)itemDetailScroll.SetText(string.IsNullOrEmpty(stats)?item.Description:stats+"\n\n"+item.Description);
                else itemStatsText.text=stats;
            }

            if (itemIconImage != null)
            {
                itemIconImage.gameObject.SetActive(item.Icon != null);
                itemIconImage.sprite = item.Icon;
            }

            if (dropItemButton != null)
            {
                dropItemButton.gameObject.SetActive(true);
                if (dropItemButtonText != null)
                {
                    dropItemButtonText.text = count > 1 ? $"Vứt vật phẩm (x{count})" : "Vứt vật phẩm";
                }
            }
        }

        private void HideDetail()
        {
            if (dropItemButton != null)
            {
                dropItemButton.gameObject.SetActive(false);
            }

            if (detailPanel != null) detailPanel.SetActive(false);
        }

        public void OnDropButtonClicked()
        {
            if (inventory == null || string.IsNullOrEmpty(_selectedSlotKey)) return;

            var slot = inventory.GetSlot(_selectedSlotKey);
            if (slot == null || slot.IsEmpty)
            {
                ClearSelection();
                return;
            }

            ItemDefinitionSO itemToDrop = slot.Item;
            int countToDrop = slot.Count;

            bool removed = inventory.TryRemoveFromSlot(_selectedSlotKey, countToDrop);
            if (removed)
            {
                Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
                ItemPickup.Spawn(itemToDrop, countToDrop, playerPos);
                Debug.Log($"[BackpackUI] Đã vứt {itemToDrop.DisplayName} x{countToDrop} tại vị trí {playerPos}");
            }

            ClearSelection();
            Refresh();
        }

        public void Refresh()
        {
            if (inventory == null) return;

            foreach (var kvp in _allViews)
            {
                string key = kvp.Key;
                var view = kvp.Value;
                var slot = inventory.GetSlot(key);
                if (view.button != null)
                {
                    RarityFrame.Apply(view.button.transform as RectTransform, slot != null && !slot.IsEmpty ? slot.Item : null);
                }

                if (slot != null && !slot.IsEmpty)
                {
                    if (view.iconImage != null)
                    {
                        view.iconImage.gameObject.SetActive(true);
                        view.iconImage.sprite = slot.Item.Icon;
                        view.iconImage.color = Color.white;
                    }

                    if (view.countText != null)
                    {
                        view.countText.gameObject.SetActive(slot.Count > 1);
                        view.countText.text = slot.Count.ToString();
                    }
                }
                else
                {
                    if (view.iconImage != null)
                    {
                        view.iconImage.gameObject.SetActive(false);
                    }

                    if (view.countText != null)
                    {
                        view.countText.gameObject.SetActive(false);
                    }
                }
            }

            // Nếu ô đang chọn bỗng nhiên trống, deselect
            if (!string.IsNullOrEmpty(_selectedSlotKey))
            {
                var cur = inventory.GetSlot(_selectedSlotKey);
                if (cur == null || cur.IsEmpty)
                {
                    ClearSelection();
                }
                else if(cur.Item.Category==ItemCategory.Material)ShowDetail(cur.Item,cur.Count);
            }
            RefreshMaterialTab();
        }
    }
}

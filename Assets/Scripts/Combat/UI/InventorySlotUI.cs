using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LightSouls.Common;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Component quản lý 1 ô đồ trong Balo hoặc Thanh nhanh, hỗ trợ cả 2 phương thức thao tác:
    /// 1. Kéo thả (Drag & Drop): Đè vào item -> item phóng to và di chuyển theo pointer ngón tay -> thả vào ô đích.
    /// 2. Chạm (Tap-to-swap): Chạm ô này rồi chạm ô khác để chuyển đồ hoặc xem chi tiết.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        [Header("Thông tin ô")]
        [SerializeField] private string slotKey;

        [Header("Thành phần UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private Text labelText;

        private BackpackUI _backpackUI;
        private bool _isDragging = false;
        private bool _isPointerDown = false;

        public string SlotKey
        {
            get => slotKey;
            set => slotKey = value;
        }

        public Button Button => button;
        public Image IconImage => iconImage;
        public Text CountText => countText;
        public GameObject HighlightObject => highlightObject;
        public Text LabelText => labelText;

        public void Initialize(BackpackUI backpackUI, string key)
        {
            _backpackUI = backpackUI;
            slotKey = key;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_backpackUI == null || string.IsNullOrEmpty(slotKey)) return;

            var slot = _backpackUI.GetSlotData(slotKey);
            if (slot == null || slot.IsEmpty) return;

            _isPointerDown = true;

            // Khi đè vào 1 item: item phóng to lên ngay lập tức (1.3x)
            if (iconImage != null)
            {
                iconImage.transform.localScale = Vector3.one * 1.3f;
            }

            _backpackUI.OnSlotPointerDown(this, slot, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPointerDown = false;

            // Nếu không trong trạng thái kéo thì đưa scale trở lại 1.0
            if (!_isDragging && iconImage != null)
            {
                iconImage.transform.localScale = Vector3.one;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_backpackUI == null || string.IsNullOrEmpty(slotKey)) return;

            var slot = _backpackUI.GetSlotData(slotKey);
            if (slot == null || slot.IsEmpty)
            {
                return; // Ô trống thì không kéo
            }

            _isDragging = true;

            // Trả lại scale của icon gốc về 1.0 (vì DragProxy sẽ đảm nhiệm visual phóng to 1.3x khi di chuyển)
            if (iconImage != null)
            {
                iconImage.transform.localScale = Vector3.one;
            }

            _backpackUI.OnBeginDragSlot(this, slot, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _backpackUI == null) return;
            _backpackUI.OnDragSlot(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            if (iconImage != null)
            {
                iconImage.transform.localScale = Vector3.one;
            }

            if (_backpackUI != null)
            {
                _backpackUI.OnEndDragSlot(this, eventData);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_backpackUI == null) return;
            _backpackUI.OnDropOnSlot(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isDragging) return;

            if (_backpackUI != null && !string.IsNullOrEmpty(slotKey))
            {
                _backpackUI.OnSlotClicked(slotKey);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LightSouls.Common;
using LightSouls.Combat.Inventory;

namespace LightSouls.Combat.UI
{
    [Serializable]
    public class HotbarSlotView
    {
        public Button button;
        public Image iconImage;
        public Text countText;
        public GameObject highlightObject;
        public Image backgroundImage;
    }

    /// <summary>
    /// Hiển thị thanh nhanh gồm 5 ô và nút mở balo (...) ở giữa cạnh dưới màn hình.
    /// Kích thước ô tối thiểu 96px trên khung 1920x1080.
    /// Chạm chọn ô bằng uGUI Button.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private BackpackUI backpackUI;

        [Header("Các ô thanh nhanh (5 ô)")]
        [SerializeField] private List<HotbarSlotView> slotViews = new List<HotbarSlotView>();

        [Header("Nút mở Balo (...)")]
        [SerializeField] private Button backpackButton;

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

            if (backpackUI == null)
            {
                backpackUI = FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);
            }

            if (backpackButton != null)
            {
                backpackButton.onClick.AddListener(OnBackpackButtonClicked);
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                int index = i;
                if (slotViews[i].button != null)
                {
                    slotViews[i].button.onClick.AddListener(() => OnSlotClicked(index));
                }
            }

            if (inventory != null)
            {
                inventory.OnHotbarSelectionChanged += HandleSelectionChanged;
                inventory.Changed += Refresh;
                inventory.OnToggleBackpackRequested += OnBackpackButtonClicked;
                Refresh();
                HandleSelectionChanged(inventory.SelectedHotbarIndex, inventory.SelectedItem);
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.OnHotbarSelectionChanged -= HandleSelectionChanged;
                inventory.Changed -= Refresh;
                inventory.OnToggleBackpackRequested -= OnBackpackButtonClicked;
            }

            if (backpackButton != null)
            {
                backpackButton.onClick.RemoveListener(OnBackpackButtonClicked);
            }
        }

        public void SetPlayer(PlayerController newPlayer)
        {
            if (player == newPlayer) return;

            if (inventory != null)
            {
                inventory.OnHotbarSelectionChanged -= HandleSelectionChanged;
                inventory.Changed -= Refresh;
                inventory.OnToggleBackpackRequested -= OnBackpackButtonClicked;
            }

            player = newPlayer;
            inventory = player != null ? player.Inventory : null;

            if (inventory != null)
            {
                inventory.OnHotbarSelectionChanged += HandleSelectionChanged;
                inventory.Changed += Refresh;
                inventory.OnToggleBackpackRequested += OnBackpackButtonClicked;
                Refresh();
                HandleSelectionChanged(inventory.SelectedHotbarIndex, inventory.SelectedItem);
            }
        }

        private void OnSlotClicked(int index)
        {
            if (inventory != null)
            {
                inventory.SelectHotbarSlot(index);
            }
        }

        private void OnBackpackButtonClicked()
        {
            if (backpackUI != null)
            {
                backpackUI.Toggle();
            }
        }

        public void HandleSelectionChanged(int selectedIndex, ItemDefinitionSO item)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i].highlightObject != null)
                {
                    slotViews[i].highlightObject.SetActive(i == selectedIndex);
                }
            }
        }

        public void Refresh()
        {
            if (inventory == null) return;

            var slots = inventory.HotbarSlots;
            for (int i = 0; i < slotViews.Count && i < slots.Count; i++)
            {
                var view = slotViews[i];
                var slot = slots[i];

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
        }
    }
}

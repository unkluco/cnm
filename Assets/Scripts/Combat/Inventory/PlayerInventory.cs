using System;
using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;

namespace LightSouls.Combat.Inventory
{
    [Serializable]
    public class InventorySlot
    {
        public string SlotKey;
        public ItemDefinitionSO Item;
        public int Count;

        public bool IsEmpty => Item == null || Count <= 0;

        public InventorySlot(string slotKey)
        {
            SlotKey = slotKey;
            Item = null;
            Count = 0;
        }

        public void Clear()
        {
            Item = null;
            Count = 0;
        }

        public void Set(ItemDefinitionSO item, int count)
        {
            Item = item;
            Count = Mathf.Max(0, count);
            if (Count == 0) Item = null;
        }
    }

    [Serializable]
    public struct StartingItemEntry
    {
        public string SlotKey;
        public ItemDefinitionSO Item;
        [Min(1)] public int Count;
    }

    /// <summary>
    /// Quản lý túi đồ nhân vật (IInventory):
    /// - 5 ô thanh nhanh (hotbar/0 .. hotbar/4)
    /// - 15 ô balo (backpack/0 .. backpack/14)
    /// - 2 ô trang bị (equip/armor, equip/boots)
    /// Lưu trữ qua Scene bằng RunState.SaveInventory.
    /// </summary>
    public class PlayerInventory : MonoBehaviour, IInventory
    {
        public const int HotbarSize = 5;
        public const int BackpackSize = 15;

        [Header("Cơ sở dữ liệu vật phẩm")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        [Header("Trang bị khởi đầu (khi RunState rỗng)")]
        [SerializeField] private List<StartingItemEntry> defaultStartingItems = new List<StartingItemEntry>();

        private readonly InventorySlot[] _hotbarSlots = new InventorySlot[HotbarSize];
        private readonly InventorySlot[] _backpackSlots = new InventorySlot[BackpackSize];
        private readonly Dictionary<string, InventorySlot> _equipmentSlots = new Dictionary<string, InventorySlot>();
        private readonly Dictionary<string, InventorySlot> _allSlots = new Dictionary<string, InventorySlot>();

        private int _selectedHotbarIndex = 0;
        private ISpeedModifiable _speedModifiable;
        private HealthComponent _healthComponent;

        public int SelectedHotbarIndex => _selectedHotbarIndex;
        public ItemDefinitionSO SelectedItem => _hotbarSlots[_selectedHotbarIndex].Item;
        public string SelectedSlotKey => _hotbarSlots[_selectedHotbarIndex].SlotKey;

        public IReadOnlyList<InventorySlot> HotbarSlots => _hotbarSlots;
        public IReadOnlyList<InventorySlot> BackpackSlots => _backpackSlots;
        public IReadOnlyDictionary<string, InventorySlot> EquipmentSlots => _equipmentSlots;

        /// <summary>Phát sự kiện mỗi khi ô chọn trên thanh nhanh thay đổi (index, item).</summary>
        public event Action<int, ItemDefinitionSO> OnHotbarSelectionChanged;

        /// <summary>Phát sự kiện IInventory khi túi đồ thay đổi.</summary>
        public event Action Changed;

        /// <summary>Phát sự kiện yêu cầu bật/tắt balo (phím Tab hoặc nút mở balo).</summary>
        public event Action OnToggleBackpackRequested;

        // Chỉ số trang bị
        public float CurrentDamageReduction { get; private set; } = 0f;

        private void Awake()
        {
            _speedModifiable = GetComponent<ISpeedModifiable>();
            _healthComponent = GetComponent<HealthComponent>();

            // 1. Khởi tạo danh sách ô
            for (int i = 0; i < HotbarSize; i++)
            {
                string key = $"hotbar/{i}";
                _hotbarSlots[i] = new InventorySlot(key);
                _allSlots[key] = _hotbarSlots[i];
            }

            for (int i = 0; i < BackpackSize; i++)
            {
                string key = $"backpack/{i}";
                _backpackSlots[i] = new InventorySlot(key);
                _allSlots[key] = _backpackSlots[i];
            }

            string armorKey = "equip/armor";
            _equipmentSlots[armorKey] = new InventorySlot(armorKey);
            _allSlots[armorKey] = _equipmentSlots[armorKey];

            string bootsKey = "equip/boots";
            _equipmentSlots[bootsKey] = new InventorySlot(bootsKey);
            _allSlots[bootsKey] = _equipmentSlots[bootsKey];
        }

        private void Start()
        {
            LoadFromRunState();
            SelectHotbarSlot(0);
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectHotbarSlot(0);
                else if (kb.digit2Key.wasPressedThisFrame) SelectHotbarSlot(1);
                else if (kb.digit3Key.wasPressedThisFrame) SelectHotbarSlot(2);
                else if (kb.digit4Key.wasPressedThisFrame) SelectHotbarSlot(3);
                else if (kb.digit5Key.wasPressedThisFrame) SelectHotbarSlot(4);

                if (kb.tabKey.wasPressedThisFrame)
                {
                    OnToggleBackpackRequested?.Invoke();
                }
            }
        }

        /// <summary>
        /// Chọn ô trên thanh nhanh (0 đến 4).
        /// </summary>
        public void SelectHotbarSlot(int index)
        {
            if (index < 0 || index >= HotbarSize) return;

            _selectedHotbarIndex = index;
            OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex, SelectedItem);
        }

        public InventorySlot GetSlot(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return null;
            _allSlots.TryGetValue(slotKey, out var slot);
            return slot;
        }

        #region Triển khai IInventory

        public bool HasSpaceFor(ItemDefinitionSO item, int count = 1)
        {
            if (item == null || count <= 0) return true;

            int remaining = count;

            // Kiểm tra xếp chồng vào các ô chung (hotbar + backpack)
            foreach (var slot in GetGeneralSlots())
            {
                if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                {
                    int spaceInSlot = item.MaxStack - slot.Count;
                    if (spaceInSlot > 0)
                    {
                        remaining -= spaceInSlot;
                        if (remaining <= 0) return true;
                    }
                }
            }

            // Kiểm tra các ô trống
            foreach (var slot in GetGeneralSlots())
            {
                if (slot.IsEmpty)
                {
                    remaining -= item.MaxStack;
                    if (remaining <= 0) return true;
                }
            }

            return remaining <= 0;
        }

        public bool TryAdd(ItemDefinitionSO item, int count = 1)
        {
            if (item == null || count <= 0) return true;

            // Quy ước: làm đủ hoặc không làm gì
            if (!HasSpaceFor(item, count))
            {
                return false;
            }

            int remaining = count;

            // 1. Thêm vào ô cùng loại có thể xếp chồng
            if (item.MaxStack > 1)
            {
                foreach (var slot in GetGeneralSlots())
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId && slot.Count < item.MaxStack)
                    {
                        int add = Mathf.Min(remaining, item.MaxStack - slot.Count);
                        slot.Count += add;
                        remaining -= add;
                        if (remaining <= 0) break;
                    }
                }
            }

            // 2. Thêm vào các ô trống còn lại
            if (remaining > 0)
            {
                foreach (var slot in GetGeneralSlots())
                {
                    if (slot.IsEmpty)
                    {
                        int add = Mathf.Min(remaining, item.MaxStack);
                        slot.Set(item, add);
                        remaining -= add;
                        if (remaining <= 0) break;
                    }
                }
            }

            ApplyEquipmentStats();
            SaveToRunState();
            Changed?.Invoke();
            OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex, SelectedItem);

            return true;
        }

        public bool TryRemove(ItemDefinitionSO item, int count = 1)
        {
            if (item == null || count <= 0) return true;

            // Quy ước: làm đủ hoặc không làm gì
            if (GetCount(item) < count)
            {
                return false;
            }

            int remaining = count;

            // Ưu tiên bớt từ balo, sau đó đến thanh nhanh, cuối cùng ô trang bị
            foreach (var slot in _backpackSlots)
            {
                if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                {
                    int take = Mathf.Min(remaining, slot.Count);
                    slot.Count -= take;
                    if (slot.Count <= 0) slot.Clear();
                    remaining -= take;
                    if (remaining <= 0) break;
                }
            }

            if (remaining > 0)
            {
                foreach (var slot in _hotbarSlots)
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                    {
                        int take = Mathf.Min(remaining, slot.Count);
                        slot.Count -= take;
                        if (slot.Count <= 0) slot.Clear();
                        remaining -= take;
                        if (remaining <= 0) break;
                    }
                }
            }

            if (remaining > 0)
            {
                foreach (var slot in _equipmentSlots.Values)
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                    {
                        int take = Mathf.Min(remaining, slot.Count);
                        slot.Count -= take;
                        if (slot.Count <= 0) slot.Clear();
                        remaining -= take;
                        if (remaining <= 0) break;
                    }
                }
            }

            ApplyEquipmentStats();
            SaveToRunState();
            Changed?.Invoke();
            OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex, SelectedItem);

            return true;
        }

        public int GetCount(ItemDefinitionSO item)
        {
            if (item == null) return 0;
            int total = 0;

            foreach (var slot in _allSlots.Values)
            {
                if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        #endregion

        #region Thao tác di chuyển & Hoán đổi ô

        /// <summary>
        /// Bớt count vật phẩm trực tiếp từ một ô xác định.
        /// </summary>
        public bool TryRemoveFromSlot(string slotKey, int count = 1)
        {
            var slot = GetSlot(slotKey);
            if (slot == null || slot.IsEmpty || slot.Count < count) return false;

            slot.Count -= count;
            if (slot.Count <= 0) slot.Clear();

            ApplyEquipmentStats();
            SaveToRunState();
            Changed?.Invoke();
            OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex, SelectedItem);

            return true;
        }

        /// <summary>
        /// Hoán đổi hoặc di chuyển vật phẩm giữa 2 ô (dùng cho kéo thả hoặc chạm chuyển đồ trong Balo).
        /// </summary>
        public bool SwapOrMoveSlots(string fromKey, string toKey)
        {
            if (fromKey == toKey) return false;

            var fromSlot = GetSlot(fromKey);
            var toSlot = GetSlot(toKey);

            if (fromSlot == null || toSlot == null || fromSlot.IsEmpty) return false;

            // Kiểm tra điều kiện hợp lệ nếu toSlot hoặc fromSlot là ô trang bị
            if (!CanPlaceInSlot(toSlot, fromSlot.Item)) return false;
            if (!toSlot.IsEmpty && !CanPlaceInSlot(fromSlot, toSlot.Item)) return false;

            // Nếu cùng loại và có thể xếp chồng
            if (!toSlot.IsEmpty && toSlot.Item.ItemId == fromSlot.Item.ItemId && toSlot.Item.MaxStack > 1)
            {
                int space = toSlot.Item.MaxStack - toSlot.Count;
                if (space > 0)
                {
                    int move = Mathf.Min(space, fromSlot.Count);
                    toSlot.Count += move;
                    fromSlot.Count -= move;
                    if (fromSlot.Count <= 0) fromSlot.Clear();
                }
                else
                {
                    // Đã đầy stack, đổi chỗ
                    SwapSlotData(fromSlot, toSlot);
                }
            }
            else
            {
                // Hoán đổi thông thường
                SwapSlotData(fromSlot, toSlot);
            }

            ApplyEquipmentStats();
            SaveToRunState();
            Changed?.Invoke();
            OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex, SelectedItem);

            return true;
        }

        private void SwapSlotData(InventorySlot a, InventorySlot b)
        {
            var tempItem = a.Item;
            int tempCount = a.Count;

            a.Item = b.Item;
            a.Count = b.Count;

            b.Item = tempItem;
            b.Count = tempCount;
        }

        private bool CanPlaceInSlot(InventorySlot targetSlot, ItemDefinitionSO item)
        {
            if (item == null) return true;

            if (targetSlot.SlotKey == "equip/armor")
            {
                return item is EquipmentDataSO eq && eq.SlotType == EquipmentSlotType.Armor;
            }

            if (targetSlot.SlotKey == "equip/boots")
            {
                return item is EquipmentDataSO eq && eq.SlotType == EquipmentSlotType.Boots;
            }

            return true;
        }

        #endregion

        #region Lưu trữ RunState & Áp dụng Trang bị

        private void ApplyEquipmentStats()
        {
            // 1. Giáp
            var armorSlot = GetSlot("equip/armor");
            if (armorSlot != null && !armorSlot.IsEmpty && armorSlot.Item is EquipmentDataSO armor)
            {
                CurrentDamageReduction = armor.DamageReduction;
            }
            else
            {
                CurrentDamageReduction = 0f;
            }

            if (_healthComponent != null)
            {
                _healthComponent.DamageReduction = CurrentDamageReduction;
            }

            // 2. Giày (tăng tốc độ chạy qua ISpeedModifiable)
            var bootsSlot = GetSlot("equip/boots");
            if (_speedModifiable != null)
            {
                if (bootsSlot != null && !bootsSlot.IsEmpty && bootsSlot.Item is EquipmentDataSO boots)
                {
                    _speedModifiable.SetSpeedMultiplier(this, boots.SpeedMultiplier);
                }
                else
                {
                    _speedModifiable.RemoveSpeedMultiplier(this);
                }
            }
        }

        private void SaveToRunState()
        {
            List<ItemSlotRecord> records = new List<ItemSlotRecord>();
            foreach (var slot in _allSlots.Values)
            {
                if (!slot.IsEmpty)
                {
                    records.Add(new ItemSlotRecord(slot.SlotKey, slot.Item.ItemId, slot.Count));
                }
            }
            RunState.SaveInventory(records);
        }

        private void LoadFromRunState()
        {
            // Xóa sạch ô hiện tại
            foreach (var slot in _allSlots.Values)
            {
                slot.Clear();
            }

            var savedSlots = RunState.InventorySlots;
            if (savedSlots != null && savedSlots.Count > 0 && itemDatabase != null)
            {
                foreach (var record in savedSlots)
                {
                    if (itemDatabase.TryGetItem(record.ItemId, out var item))
                    {
                        var slot = GetSlot(record.SlotKey);
                        if (slot != null)
                        {
                            slot.Set(item, record.Count);
                        }
                    }
                }
            }
            else
            {
                // Khởi tạo đồ mặc định nếu RunState rỗng
                foreach (var entry in defaultStartingItems)
                {
                    if (entry.Item != null)
                    {
                        var slot = GetSlot(entry.SlotKey);
                        if (slot != null)
                        {
                            slot.Set(entry.Item, entry.Count);
                        }
                    }
                }
                SaveToRunState();
            }

            ApplyEquipmentStats();
            Changed?.Invoke();
        }

        private IEnumerable<InventorySlot> GetGeneralSlots()
        {
            for (int i = 0; i < HotbarSize; i++) yield return _hotbarSlots[i];
            for (int i = 0; i < BackpackSize; i++) yield return _backpackSlots[i];
        }

        #endregion
    }
}

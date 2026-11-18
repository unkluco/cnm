using System;
using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Items;
using LightSouls.Combat.Stats;
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
        private HealthComponent _healthComponent;
        private PlayerStats _stats;
        private PlayerMovement _movement;
        private readonly Dictionary<string, ParticleSystem> _equipmentAuras = new Dictionary<string, ParticleSystem>();
        private readonly Dictionary<string, EquipmentDataSO> _appliedEquipment = new Dictionary<string, EquipmentDataSO>();

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

        // Chỉ số trang bị (tính từ PlayerStats: gồm cả giáp và hiệu ứng tạm thời)
        public float CurrentDamageReduction => _stats != null ? 1f - _stats.Get(StatType.DamageTaken) : 0f;

        private static readonly string[] EquipmentKeys = { "equip/armor", "equip/boots" };

        private void Awake()
        {
            _healthComponent = GetComponent<HealthComponent>();
            _stats = PlayerStats.GetOrAdd(gameObject);
            _movement = GetComponent<PlayerMovement>();
            if (_healthComponent != null)
            {
                _healthComponent.DeathPrevention += TryPreventDeath;
            }

            if (_movement != null)
            {
                _movement.OnDashExecuted += HandleDashExecuted;
            }

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
            // Mỗi ô trang bị là một nguồn trong PlayerStats: mặc thì đăng ký chỉ số, tháo thì gỡ.
            foreach (string key in EquipmentKeys)
            {
                var slot = GetSlot(key);
                var equipment = slot != null && !slot.IsEmpty ? slot.Item as EquipmentDataSO : null;

                EquipmentDataSO previous;
                _appliedEquipment.TryGetValue(key, out previous);
                if (previous == equipment)
                {
                    continue;
                }

                _appliedEquipment[key] = equipment;
                if (_stats != null)
                {
                    if (equipment != null) _stats.SetModifiers(key, equipment.GetModifiers());
                    else _stats.RemoveSource(key);
                }

                UpdateEquipmentAura(key, equipment);
            }
        }

        private void UpdateEquipmentAura(string key, EquipmentDataSO equipment)
        {
            ParticleSystem aura;
            if (_equipmentAuras.TryGetValue(key, out aura) && aura != null)
            {
                Destroy(aura.gameObject);
            }

            _equipmentAuras.Remove(key);
            if (equipment == null || equipment.AuraColor.a <= 0f)
            {
                return;
            }

            // Giày: hạt ở chân; giáp: hạt quanh thân.
            bool boots = equipment.SlotType == EquipmentSlotType.Boots;
            Vector3 offset = boots ? new Vector3(0f, 0.15f, 0f) : new Vector3(0f, 1.0f, 0f);
            _equipmentAuras[key] = VfxFactory.Aura(transform, equipment.AuraColor, boots ? 10f : 8f, boots ? 0.35f : 0.5f,
                offset, boots ? 0.1f : 0.12f, boots ? 0.3f : 0.6f, "VFX_Equip_" + key.Replace('/', '_'));
        }

        private void HandleDashExecuted()
        {
            // Lướt bung hạt theo màu trang bị (Áo Choàng Bóng Đêm, Giày Lò Xo...).
            foreach (EquipmentDataSO equipment in _appliedEquipment.Values)
            {
                if (equipment != null && equipment.AuraColor.a > 0f)
                {
                    VfxFactory.Burst(transform.position + Vector3.up * 0.6f, equipment.AuraColor, 14, 3f, 0.18f, 0.45f, 0f);
                }
            }
        }

        /// <summary>
        /// Gọi bởi HealthComponent khi máu sắp về 0: tìm vật phẩm "tự kích hoạt khi gục ngã" (Lông Phượng Hoàng),
        /// trừ một cái và kích hoạt. Trả true nếu đã cứu.
        /// </summary>
        private bool TryPreventDeath()
        {
            foreach (var slot in _allSlots.Values)
            {
                if (slot.IsEmpty || !(slot.Item is ConsumableDataSO consumable) || !consumable.TriggersOnDeath)
                {
                    continue;
                }

                if (TryRemoveFromSlot(slot.SlotKey, 1))
                {
                    consumable.TriggerOnDeath(gameObject);
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_healthComponent != null)
            {
                _healthComponent.DeathPrevention -= TryPreventDeath;
            }

            if (_movement != null)
            {
                _movement.OnDashExecuted -= HandleDashExecuted;
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

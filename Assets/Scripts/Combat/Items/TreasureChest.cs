using System;
using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Hòm kho báu trong hầm ngục: 6 ô (lưới 3 x 2), mỗi ô có xác suất chứa một vật phẩm quay từ LootTableSO.
    /// Người chơi bấm Tương tác (E) để mở; TreasureChestUI nghe sự kiện OpenRequested và hiện bảng các ô.
    /// Chạm một ô để lấy vật phẩm vào túi đồ. Hòm đã lấy hết thì không mở được nữa.
    /// Collider trigger trên Layer Interactable (12) để PlayerInteraction quét thấy.
    /// </summary>
    public class TreasureChest : MonoBehaviour, IInteractable
    {
        public const int SlotCount = 6;
        public const int Columns = 3;

        [Serializable]
        public struct ChestSlot
        {
            public ItemDefinitionSO Item;
            public int Count;

            public bool IsEmpty => Item == null || Count <= 0;
        }

        [Header("Vật phẩm")]
        [SerializeField] private LootTableSO lootTable;

        [Tooltip("Xác suất mỗi ô có vật phẩm (0..1).")]
        [SerializeField, Range(0f, 1f)] private float slotFillChance = 0.5f;

        [Tooltip("Số ô có vật phẩm tối thiểu, để hòm không bao giờ trống trơn.")]
        [SerializeField, Range(0, SlotCount)] private int minFilledSlots = 2;

        [Header("Hình ảnh")]
        [Tooltip("Nắp hòm, xoay quanh trục X khi mở.")]
        [SerializeField] private Transform lid;
        [SerializeField] private float lidOpenAngle = 105f;
        [SerializeField] private float lidOpenSpeed = 360f;

        private readonly ChestSlot[] slots = new ChestSlot[SlotCount];
        private bool rolled;
        private bool opened;
        private float lidAngle;

        /// <summary>Phát khi người chơi tương tác với một hòm; TreasureChestUI mở bảng cho hòm này.</summary>
        public static event Action<TreasureChest, GameObject> OpenRequested;

        /// <summary>Phát khi đồ trong hòm thay đổi (đã lấy một ô).</summary>
        public event Action Changed;

        public string Prompt => "Mở hòm";
        public bool IsOpened => opened;
        public IReadOnlyList<ChestSlot> Slots
        {
            get
            {
                EnsureRolled();
                return slots;
            }
        }

        public bool IsEmpty
        {
            get
            {
                EnsureRolled();
                foreach (ChestSlot slot in slots)
                {
                    if (!slot.IsEmpty)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void SetLootTable(LootTableSO table)
        {
            lootTable = table;
        }

        private void Start()
        {
            EnsureRolled();
        }

        private void Update()
        {
            if (lid == null || !opened || Mathf.Approximately(lidAngle, lidOpenAngle))
            {
                return;
            }

            lidAngle = Mathf.MoveTowards(lidAngle, lidOpenAngle, lidOpenSpeed * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
        }

        /// <summary>Quay đồ cho hòm (chỉ một lần). Gọi lại Roll(seed) để quay lại có kiểm soát trong test.</summary>
        public void EnsureRolled()
        {
            if (!rolled)
            {
                Roll(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            }
        }

        public void Roll(int seed)
        {
            rolled = true;
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = default;
            }

            if (lootTable == null)
            {
                return;
            }

            UnityEngine.Random.State previous = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            var filledIndices = new List<int>();
            for (int i = 0; i < SlotCount; i++)
            {
                if (UnityEngine.Random.value < slotFillChance && TryRollSlot(i))
                {
                    filledIndices.Add(i);
                }
            }

            // Bảo đảm tối thiểu: lấp thêm vào các ô trống ngẫu nhiên.
            int guard = SlotCount * 4;
            while (filledIndices.Count < minFilledSlots && guard-- > 0)
            {
                int index = UnityEngine.Random.Range(0, SlotCount);
                if (slots[index].IsEmpty && TryRollSlot(index))
                {
                    filledIndices.Add(index);
                }
            }

            UnityEngine.Random.state = previous;
        }

        private bool TryRollSlot(int index)
        {
            if (lootTable.RollDrop(out ItemDefinitionSO item, out int count) && item != null)
            {
                slots[index] = new ChestSlot { Item = item, Count = Mathf.Clamp(count, 1, Mathf.Max(1, item.MaxStack)) };
                return true;
            }

            return false;
        }

        public bool CanInteract(GameObject interactor)
        {
            return interactor != null && interactor.CompareTag("Player") && !IsEmpty;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            opened = true;
            OpenRequested?.Invoke(this, interactor);
        }

        /// <summary>
        /// Lấy cả ô vào túi đồ. Thất bại (túi đầy) thì đồ giữ nguyên trong hòm.
        /// </summary>
        public bool TryTake(int index, IInventory inventory)
        {
            EnsureRolled();
            if (inventory == null || index < 0 || index >= SlotCount || slots[index].IsEmpty)
            {
                return false;
            }

            ChestSlot slot = slots[index];
            if (!inventory.TryAdd(slot.Item, slot.Count))
            {
                return false;
            }

            slots[index] = default;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Lấy lần lượt mọi ô còn đồ. Trả về số ô đã lấy được.</summary>
        public int TakeAll(IInventory inventory)
        {
            int taken = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty && TryTake(i, inventory))
                {
                    taken++;
                }
            }

            return taken;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenRequested = null;
        }
    }
}

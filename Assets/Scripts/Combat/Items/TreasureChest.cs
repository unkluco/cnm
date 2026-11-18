using System;
using System.Collections.Generic;
using LightSouls.Combat.Effects;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Hòm kho báu trong hầm ngục: 6 ô (lưới 3 x 2), mỗi ô có xác suất chứa một vật phẩm quay từ LootTableSO.
    /// Người chơi bấm Tương tác (E) để mở bao nhiêu lần cũng được; TreasureChestUI nghe sự kiện OpenRequested
    /// và hiện bảng hai bên: túi đồ và các ô của hòm, chạm để chuyển đồ qua lại (lấy ra hoặc cất vào).
    /// Dữ liệu ô nằm trong ItemContainer (Common) nên có thể tái dùng cho các loại kho khác.
    /// Collider trigger trên Layer Interactable (12) để PlayerInteraction quét thấy.
    /// </summary>
    public class TreasureChest : MonoBehaviour, IInteractable
    {
        public const int SlotCount = 6;
        public const int Columns = 3;

        private static readonly Color OpenBurstColor = new Color(1f, 0.8f, 0.3f, 1f);

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

        private readonly ItemContainer container = new ItemContainer(SlotCount);
        private bool rolled;
        private bool hooked;
        private bool isOpen;
        private bool everOpened;
        private float lidAngle;

        /// <summary>Phát khi người chơi tương tác với một hòm; TreasureChestUI mở bảng cho hòm này.</summary>
        public static event Action<TreasureChest, GameObject> OpenRequested;

        /// <summary>Phát khi đồ trong hòm thay đổi (lấy ra hoặc cất vào).</summary>
        public event Action Changed;

        public string Prompt => "Mở hòm";

        /// <summary>Đã từng được mở ít nhất một lần.</summary>
        public bool IsOpened => everOpened;

        /// <summary>Bảng của hòm đang mở (nắp đang nâng).</summary>
        public bool IsOpen => isOpen;

        public ItemContainer Container
        {
            get
            {
                if (!hooked)
                {
                    hooked = true;
                    container.Changed += () => Changed?.Invoke();
                }

                EnsureRolled();
                return container;
            }
        }

        public IReadOnlyList<ItemStack> Slots => Container.Slots;
        public bool IsEmpty => Container.IsEmpty;

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
            if (lid == null)
            {
                return;
            }

            float target = isOpen ? lidOpenAngle : 0f;
            if (Mathf.Approximately(lidAngle, target))
            {
                return;
            }

            lidAngle = Mathf.MoveTowards(lidAngle, target, lidOpenSpeed * Time.deltaTime);
            lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
        }

        /// <summary>Quay đồ cho hòm (chỉ một lần). Gọi Roll(seed) để quay lại có kiểm soát trong test.</summary>
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
            container.Clear();

            if (lootTable == null)
            {
                return;
            }

            UnityEngine.Random.State previous = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            int filled = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (UnityEngine.Random.value < slotFillChance && TryRollSlot(i))
                {
                    filled++;
                }
            }

            // Bảo đảm tối thiểu: lấp thêm vào các ô trống ngẫu nhiên.
            int guard = SlotCount * 4;
            while (filled < minFilledSlots && guard-- > 0)
            {
                int index = UnityEngine.Random.Range(0, SlotCount);
                if (container.Get(index).IsEmpty && TryRollSlot(index))
                {
                    filled++;
                }
            }

            UnityEngine.Random.state = previous;
        }

        private bool TryRollSlot(int index)
        {
            if (lootTable.RollDrop(out ItemDefinitionSO item, out int count) && item != null)
            {
                container.Set(index, new ItemStack(item, Mathf.Clamp(count, 1, Mathf.Max(1, item.MaxStack))));
                return true;
            }

            return false;
        }

        public bool CanInteract(GameObject interactor)
        {
            // Mở lại được bất cứ lúc nào, kể cả hòm trống (để cất đồ vào).
            return interactor != null && interactor.CompareTag("Player");
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            EnsureRolled();
            SetOpen(true);
            OpenRequested?.Invoke(this, interactor);
        }

        /// <summary>Nâng hoặc hạ nắp hòm. Bảng giao diện gọi SetOpen(false) khi đóng.</summary>
        public void SetOpen(bool open)
        {
            if (open && !isOpen)
            {
                VfxFactory.Burst(transform.position + Vector3.up * 0.9f, OpenBurstColor, everOpened ? 10 : 28, 3.5f, 0.16f, 0.6f, -0.4f);
                everOpened = true;
            }

            isOpen = open;
        }

        /// <summary>Lấy ô index vào túi đồ (túi gần đầy thì lấy một phần). True nếu lấy được ít nhất một món.</summary>
        public bool TryTake(int index, IInventory inventory)
        {
            return Container.TransferTo(index, inventory) > 0;
        }

        /// <summary>Lấy lần lượt mọi ô còn đồ. Trả về số ô có lấy được đồ.</summary>
        public int TakeAll(IInventory inventory)
        {
            int taken = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!container.Get(i).IsEmpty && TryTake(i, inventory))
                {
                    taken++;
                }
            }

            return taken;
        }

        /// <summary>Cất tối đa count món vào hòm. Trả về số món đã cất (người gọi tự bớt khỏi túi đồ).</summary>
        public int Put(ItemDefinitionSO item, int count)
        {
            return Container.Put(item, count);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenRequested = null;
        }
    }
}

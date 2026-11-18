using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>Một ô chứa: vật phẩm và số lượng. Ô trống khi Item null hoặc Count &lt;= 0.</summary>
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinitionSO Item;
        public int Count;

        public ItemStack(ItemDefinitionSO item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;
    }

    /// <summary>
    /// Kho chứa có số ô cố định, dùng chung cho mọi thứ "chứa đồ" ngoài túi người chơi
    /// (hòm kho báu, sau này là rương ở sảnh, thùng đồ của NPC...).
    /// Chỉ là dữ liệu C# thuần, không phụ thuộc Combat hay giao diện, nên dễ viết test và tái sử dụng.
    /// Cất đồ: xếp chồng vào ô cùng loại trước (theo MaxStack), rồi mới vào ô trống.
    /// </summary>
    public class ItemContainer
    {
        private readonly ItemStack[] slots;

        public ItemContainer(int slotCount)
        {
            slots = new ItemStack[Mathf.Max(1, slotCount)];
        }

        /// <summary>Phát mỗi khi nội dung thay đổi.</summary>
        public event Action Changed;

        public int SlotCount => slots.Length;
        public IReadOnlyList<ItemStack> Slots => slots;

        public bool IsEmpty
        {
            get
            {
                foreach (ItemStack slot in slots)
                {
                    if (!slot.IsEmpty)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public ItemStack Get(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : default;
        }

        /// <summary>Đặt thẳng một ô (dùng khi quay đồ ngẫu nhiên hoặc nạp dữ liệu đã lưu).</summary>
        public void Set(int index, ItemStack stack)
        {
            if (index < 0 || index >= slots.Length)
            {
                return;
            }

            slots[index] = stack.IsEmpty ? default : stack;
            Changed?.Invoke();
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = default;
            }

            Changed?.Invoke();
        }

        /// <summary>Số món tối đa của item còn cất vừa.</summary>
        public int SpaceFor(ItemDefinitionSO item)
        {
            if (item == null)
            {
                return 0;
            }

            int maxStack = Mathf.Max(1, item.MaxStack);
            int space = 0;
            foreach (ItemStack slot in slots)
            {
                if (slot.IsEmpty)
                {
                    space += maxStack;
                }
                else if (SameItem(slot.Item, item))
                {
                    space += Mathf.Max(0, maxStack - slot.Count);
                }
            }

            return space;
        }

        /// <summary>Cất tối đa count món. Trả về số món đã cất được (0 khi hết chỗ).</summary>
        public int Put(ItemDefinitionSO item, int count)
        {
            if (item == null || count <= 0)
            {
                return 0;
            }

            int maxStack = Mathf.Max(1, item.MaxStack);
            int remaining = count;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && SameItem(slots[i].Item, item) && slots[i].Count < maxStack)
                {
                    int move = Mathf.Min(remaining, maxStack - slots[i].Count);
                    slots[i].Count += move;
                    remaining -= move;
                }
            }

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int move = Mathf.Min(remaining, maxStack);
                    slots[i] = new ItemStack(item, move);
                    remaining -= move;
                }
            }

            int stored = count - remaining;
            if (stored > 0)
            {
                Changed?.Invoke();
            }

            return stored;
        }

        /// <summary>Bớt tối đa count món ở ô index. Trả về số món đã bớt.</summary>
        public int RemoveAt(int index, int count)
        {
            if (index < 0 || index >= slots.Length || slots[index].IsEmpty || count <= 0)
            {
                return 0;
            }

            int removed = Mathf.Min(count, slots[index].Count);
            slots[index].Count -= removed;
            if (slots[index].Count <= 0)
            {
                slots[index] = default;
            }

            Changed?.Invoke();
            return removed;
        }

        /// <summary>
        /// Chuyển ô index sang túi đồ, được bao nhiêu chuyển bấy nhiêu (túi gần đầy thì chuyển một phần).
        /// Trả về số món đã chuyển.
        /// </summary>
        public int TransferTo(int index, IInventory inventory)
        {
            if (inventory == null || index < 0 || index >= slots.Length || slots[index].IsEmpty)
            {
                return 0;
            }

            ItemStack stack = slots[index];
            for (int amount = stack.Count; amount > 0; amount--)
            {
                if (inventory.TryAdd(stack.Item, amount))
                {
                    RemoveAt(index, amount);
                    return amount;
                }
            }

            return 0;
        }

        private static bool SameItem(ItemDefinitionSO a, ItemDefinitionSO b)
        {
            return a == b || (a != null && b != null && !string.IsNullOrEmpty(a.ItemId) && a.ItemId == b.ItemId);
        }
    }
}

using System;
using System.Collections.Generic;
using LightSouls.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class ItemContainerTests
    {
        private class TestItem : ItemDefinitionSO
        {
            public override ItemCategory Category => ItemCategory.Consumable;
        }

        /// <summary>Túi đồ giả: tối đa capacity món, bất kể loại.</summary>
        private class FakeInventory : IInventory
        {
            public int Capacity;
            public int Stored;
            public event Action Changed;

            public bool HasSpaceFor(ItemDefinitionSO item, int count = 1) => Stored + count <= Capacity;

            public bool TryAdd(ItemDefinitionSO item, int count = 1)
            {
                if (!HasSpaceFor(item, count)) return false;
                Stored += count;
                Changed?.Invoke();
                return true;
            }

            public bool TryRemove(ItemDefinitionSO item, int count = 1) => false;
            public int GetCount(ItemDefinitionSO item) => Stored;
        }

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        private TestItem MakeItem(string id, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<TestItem>();
            created.Add(item);
            var so = new SerializedObject(item);
            so.FindProperty("itemId").stringValue = id;
            so.FindProperty("maxStack").intValue = maxStack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        [Test]
        public void Put_StacksIntoMatchingSlotBeforeEmptySlots()
        {
            var potion = MakeItem("potion", 5);
            var container = new ItemContainer(3);
            container.Set(2, new ItemStack(potion, 3));

            int stored = container.Put(potion, 4);

            Assert.AreEqual(4, stored);
            Assert.AreEqual(5, container.Get(2).Count, "Ô cùng loại được lấp đầy trước.");
            Assert.AreEqual(2, container.Get(0).Count, "Phần dư vào ô trống đầu tiên.");
            Assert.IsTrue(container.Get(1).IsEmpty);
        }

        [Test]
        public void Put_ReturnsOnlyWhatFits()
        {
            var sword = MakeItem("sword", 1);
            var container = new ItemContainer(2);

            Assert.AreEqual(1, container.Put(sword, 1));
            Assert.AreEqual(1, container.Put(sword, 1));
            Assert.AreEqual(0, container.Put(sword, 1), "Hòm đầy thì không cất thêm.");
            Assert.AreEqual(0, container.SpaceFor(sword));
        }

        [Test]
        public void TransferTo_MovesPartialStackWhenInventoryNearlyFull()
        {
            var potion = MakeItem("potion", 5);
            var container = new ItemContainer(2);
            container.Set(0, new ItemStack(potion, 4));
            var inventory = new FakeInventory { Capacity = 3 };

            int moved = container.TransferTo(0, inventory);

            Assert.AreEqual(3, moved);
            Assert.AreEqual(1, container.Get(0).Count, "Phần không vừa túi ở lại trong hòm.");
            Assert.AreEqual(3, inventory.Stored);
        }

        [Test]
        public void RemoveAt_ClearsSlotAndRaisesChanged()
        {
            var potion = MakeItem("potion", 5);
            var container = new ItemContainer(1);
            container.Set(0, new ItemStack(potion, 2));
            int changes = 0;
            container.Changed += () => changes++;

            Assert.AreEqual(2, container.RemoveAt(0, 10));
            Assert.IsTrue(container.IsEmpty);
            Assert.AreEqual(1, changes);
        }
    }
}

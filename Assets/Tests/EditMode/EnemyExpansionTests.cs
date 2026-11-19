using System;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class EnemyExpansionTests
    {
        [Test]
        public void AutoAimRadiusDependsOnWeaponAndMatchesVisibleCircle()
        {
            const string folder = "Assets/ScriptableObjects/Combat/Weapons/";
            var sword = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(folder + "Weapon_Sword_Iron.asset");
            var shotgun = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(folder + "Weapon_Gun_Shotgun.asset");
            var crossbow = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(folder + "Weapon_Crossbow.asset");
            Assert.That(PlayerCombat.GetAutoAimRadius(sword, 12f), Is.EqualTo(sword.AttackRange * 1.8f).Within(0.001f));
            Assert.AreEqual(8f, PlayerCombat.GetAutoAimRadius(shotgun, 12f));
            Assert.AreEqual(12f, PlayerCombat.GetAutoAimRadius(crossbow, 12f));
            Assert.AreEqual(0f, PlayerCombat.GetAutoAimRadius(null, 12f));

            var host = new GameObject("AimRangeTest");
            try
            {
                var marker = host.AddComponent<AimRangeIndicator>();
                float radius = PlayerCombat.GetAutoAimRadius(shotgun, 12f);
                marker.Show(radius);
                Assert.AreEqual(radius, marker.VisibleRadius);
                var line = host.GetComponentInChildren<LineRenderer>();
                Assert.IsTrue(line.loop);
                Assert.Greater(line.positionCount, 32);
                for (int i = 0; i < line.positionCount; i++)
                    Assert.That(line.GetPosition(i).magnitude, Is.EqualTo(radius).Within(0.001f));
                Assert.IsNull(host.GetComponentInChildren<Collider>());
                marker.Hide();
                Assert.AreEqual(0f, marker.VisibleRadius);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void AllTenSpecialEnemyPrefabsHaveUniqueKindAndRequiredComponents()
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Combat/SpecialEnemies" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.AreEqual(10, prefabs.Length);
            var kinds = prefabs.Select(prefab => prefab.GetComponent<SpecialEnemy>()).ToArray();
            Assert.IsTrue(kinds.All(enemy => enemy != null));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(SpecialEnemyKind)), kinds.Select(enemy => enemy.Kind));
            foreach (var prefab in prefabs)
            {
                Assert.AreEqual(7, prefab.layer);
                Assert.IsTrue(prefab.CompareTag("Enemy"));
                Assert.NotNull(prefab.GetComponent<HealthComponent>());
                Assert.NotNull(prefab.GetComponent<UnityEngine.AI.NavMeshAgent>());
                Assert.NotNull(prefab.GetComponent<CapsuleCollider>());
                Assert.AreEqual(1, prefab.GetComponents<EnemyBase>().Length);
            }
        }
    }
}

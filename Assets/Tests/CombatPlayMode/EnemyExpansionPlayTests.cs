using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LightSouls.Combat.Tests
{
    public class EnemyExpansionPlayTests
    {
        private static IEnumerator WaitFor(System.Func<bool> condition, string message, float seconds = 12f)
        {
            float stopAt = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < stopAt) yield return null;
            Assert.IsTrue(condition(), message);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            RunState.Reset();
            yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);
        }

        [UnityTest]
        public IEnumerator MainGameShowsAimRangeAndRotatesThroughTenSpecialEnemies()
        {
            RunState.Reset();
            yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(() => Object.FindAnyObjectByType<PlayerController>() != null, "Game did not spawn a player.");
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.NotNull(player.Combat.CurrentWeaponData);
            player.Combat.PerformAttack();
            var marker = player.GetComponent<AimRangeIndicator>();
            Assert.NotNull(marker);
            Assert.AreEqual(player.Combat.AutoAimRadius, marker.VisibleRadius);
            Assert.Greater(marker.VisibleRadius, 0f);
            yield return new WaitForSeconds(0.85f);
            Assert.AreEqual(0f, marker.VisibleRadius);

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.AreEqual(10, spawner.SpecialEnemyPrefabs.Count);
            var seen = new HashSet<SpecialEnemyKind>();
            var point = new GameObject("TestSpawnPoint");
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position + Vector3.right * 2f, out var navPoint, 3f, NavMesh.AllAreas));
            point.transform.position = navPoint.position;
            for (int i = 0; i < 10; i++)
            {
                spawner.SpawnWave(new[] { point.transform });
                Assert.AreEqual(1, spawner.AliveEnemyCount);
                var special = Object.FindObjectsByType<SpecialEnemy>(FindObjectsSortMode.None).Single(e => !e.IsDead);
                Assert.IsTrue(seen.Add(special.Kind), "Special enemies repeated before all ten had spawned.");
                // A real death event, including shielded enemies, must unlock the wave.
                special.TakeDamage(new DamageInfo(10000f, player.gameObject, special.transform.position));
                Assert.AreEqual(0, spawner.AliveEnemyCount);
                yield return null;
            }
            Object.Destroy(point);
            Assert.AreEqual(10, seen.Count);
        }

        [UnityTest]
        public IEnumerator EverySpecialEnemyWarnsAndResolvesItsAbility()
        {
            RunState.Reset();
            yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(() => Object.FindAnyObjectByType<PlayerController>() != null, "Game did not spawn a player.");
            var player = Object.FindAnyObjectByType<PlayerController>();
            var health = player.GetComponent<HealthComponent>();
            var pool = Object.FindAnyObjectByType<EnemySpawner>().SpecialEnemyPrefabs;

            // A separate test platform in the actual scene keeps room obstacles and other waves out of the ability checks.
            var arena = new GameObject("AbilityTestArena");
            Vector3 center = new Vector3(1000, 0, 1000);
            arena.transform.position = center;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(arena.transform, false);
            floor.transform.localPosition = Vector3.down * 0.1f;
            floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
            floor.layer = 8;
            var surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << 8;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            player.Teleport(center + Vector3.up * 0.05f);
            player.SetInputActive(false);
            Time.timeScale = 3f;
            yield return null;

            // The collider of the outside enemy overlaps the circle, but its center does not;
            // it must never pull the player away from the direction they chose.
            float radius = player.Combat.AutoAimRadius;
            var inside = Object.Instantiate(pool[0], center + Vector3.right * (radius - 0.4f), Quaternion.identity);
            var outside = Object.Instantiate(pool[0], center + Vector3.left * (radius + 0.2f), Quaternion.identity);
            inside.GetComponent<SpecialEnemy>().enabled = false;
            outside.GetComponent<SpecialEnemy>().enabled = false;
            Physics.SyncTransforms();
            player.SetInputActive(true);
            player.transform.rotation = Quaternion.identity;
            player.Combat.PerformAttack();
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.right), 0.98f, "A living target inside the ring must be aimed at.");
            Object.Destroy(inside);
            yield return new WaitForSeconds(player.Combat.CurrentWeaponData.AttackRate + 0.15f);
            player.transform.rotation = Quaternion.identity;
            player.Combat.PerformAttack();
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.forward), 0.98f, "Targets outside the displayed radius must not auto-aim.");
            Object.Destroy(outside);
            player.SetInputActive(false);
            yield return null;

            foreach (GameObject prefab in pool)
            {
                var type = prefab.GetComponent<SpecialEnemy>().Kind;
                health.ResetHealth();
                bool ranged = type == SpecialEnemyKind.EmberMage || type == SpecialEnemyKind.FrostMage ||
                    type == SpecialEnemyKind.StormArcher || type == SpecialEnemyKind.LightPriest ||
                    type == SpecialEnemyKind.Phantom || type == SpecialEnemyKind.Charger;
                var spawn = center + Vector3.right * (ranged ? 4f : 1.4f);
                var enemy = Object.Instantiate(prefab, spawn, Quaternion.identity).GetComponent<SpecialEnemy>();
                HealthComponent injuredAlly = null;
                if (type == SpecialEnemyKind.LightPriest)
                {
                    var ally = Object.Instantiate(pool.First(p => p.GetComponent<SpecialEnemy>().Kind == SpecialEnemyKind.StoneGolem),
                        spawn + Vector3.forward * 2f, Quaternion.identity);
                    ally.GetComponent<SpecialEnemy>().enabled = false;
                    injuredAlly = ally.GetComponent<HealthComponent>();
                    injuredAlly.TakeDamage(new DamageInfo(55f, player.gameObject, ally.transform.position));
                }
                if (type == SpecialEnemyKind.BloodLeech)
                    enemy.TakeDamage(new DamageInfo(30f, player.gameObject, enemy.transform.position));
                float enemyHealthBefore = enemy.CurrentHealth;
                float allyHealthBefore = injuredAlly != null ? injuredAlly.CurrentHealth : 0f;
                yield return WaitFor(() => enemy != null && enemy.IsCasting, type + " never cast.");
                Assert.IsTrue(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                    .Any(line => line.name == "EnemyWarning_" + type), type + " had no visual warning.");
                var glyphs = Object.FindObjectsByType<ArcaneTelegraph>().Where(g => g.name == "EnemyWarning_" + type && g.IsVisible).ToArray();
                Assert.IsNotEmpty(glyphs, type + " must display the styled, animated warning.");
                Assert.IsTrue(glyphs.All(g => g.GetComponentInChildren<MeshRenderer>().sharedMaterial == ArcaneVfxMaterials.Glyph));
                if (type == SpecialEnemyKind.LightPriest) Assert.AreEqual(TelegraphStyle.Blessing, glyphs[0].Style);
                if (type == SpecialEnemyKind.ShieldGuard)
                    Assert.Less(enemy.GetComponent<HealthComponent>().DamageReduction, 0.1f, "Guard must lower its shield while casting.");
                yield return WaitFor(() => enemy == null || !enemy.IsCasting, type + " never resolved.");
                Assert.IsFalse(Object.FindObjectsByType<ArcaneTelegraph>().Any(g => g.name == "EnemyWarning_" + type && g.IsVisible), type + " left a stale telegraph.");
                if (type == SpecialEnemyKind.StormArcher) yield return new WaitForSeconds(0.65f);

                if (type == SpecialEnemyKind.LightPriest)
                    Assert.Greater(injuredAlly.CurrentHealth, allyHealthBefore, "Priest should heal its ally.");
                else
                    Assert.Less(health.CurrentHealth, health.MaxHealth, type + " did not damage the stationary player.");
                if (type == SpecialEnemyKind.BloodLeech)
                    Assert.Greater(enemy.CurrentHealth, enemyHealthBefore, "Blood leech should recover health.");
                if (type == SpecialEnemyKind.BombWisp)
                    Assert.IsTrue(enemy == null || enemy.IsDead, "Bomb must be removed from the wave after exploding.");
                if (type == SpecialEnemyKind.FrostMage)
                    Assert.IsTrue(player.GetComponent<StatusEffectReceiver>().IsSlowed);
                if (type == SpecialEnemyKind.EmberMage)
                    Assert.IsTrue(player.GetComponent<StatusEffectReceiver>().IsBurning);

                if (enemy != null) Object.Destroy(enemy.gameObject);
                if (injuredAlly != null) Object.Destroy(injuredAlly.gameObject);
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Object.Destroy(projectile.gameObject);
                yield return new WaitForSeconds(2.7f);
            }
            Object.Destroy(arena);
        }
    }
}

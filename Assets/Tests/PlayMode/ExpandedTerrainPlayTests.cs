using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Art;
using LightSouls.Combat;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Items;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class ExpandedTerrainPlayTests
    {
        private const string CaptureVariable = "LIGHTSOULS_TERRAIN_CAPTURE_DIR";
        private static PlayerController Player() => Object.FindAnyObjectByType<PlayerController>();

        private static IEnumerator WaitFor(Func<bool> ready, string message, float seconds = 15f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ready(), message);
            yield return null;
        }

        private static IEnumerator LoadSeededDungeon(string id, string roomId)
        {
            Time.timeScale = 1f;
            RunState.Reset();
            RunState.SelectedDungeonId = id;
            // Prevent only the automatic Start call; the real generator, scene and bootstrap
            // are used below with a reproducible seed containing the requested terrain.
            void Prepare(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == SceneNames.Game) Object.FindAnyObjectByType<DungeonGenerator>().enabled = false;
            }
            SceneManager.sceneLoaded += Prepare;
            try { yield return SceneManager.LoadSceneAsync(SceneNames.Game); }
            finally { SceneManager.sceneLoaded -= Prepare; }
            var generator = Object.FindAnyObjectByType<DungeonGenerator>();
            var request = generator.ActiveConfig.ToPlanRequest(out _);
            int chosenSeed = -1;
            for (int seed = 530; seed < 650; seed++)
                if (DungeonLayoutPlanner.TryPlan(request, seed, out var plan, out _) && plan.rooms.Any(r => r.templateId == roomId))
                { chosenSeed = seed; break; }
            Assert.GreaterOrEqual(chosenSeed, 0, "No seed with " + roomId);
            Assert.IsTrue(generator.GenerateWithSeed(chosenSeed), generator.LastError);
            yield return WaitFor(() => Player() != null && generator.IsGenerated, "Dungeon did not spawn.");
            Assert.IsTrue(generator.IsNavMeshBaked);
            Assert.IsTrue(generator.IsNavMeshPathReachable);
            Player().SetInputActive(false);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            RunState.Reset();
            yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);
        }

        [UnityTest]
        public IEnumerator AllThreeRegionsHaveReachableSecretLootDistinctCombatTerrainAndGuardianHeraldry()
        {
            string[] ids = { "dungeon.standard", "dungeon.lava", "dungeon.maze" };
            string[] featured = { "terrain.stonebridge", "terrain.emberbridge", "terrain.mosslabyrinth" };
            for (int i = 0; i < ids.Length; i++)
            {
                yield return LoadSeededDungeon(ids[i], featured[i]);
                var generator = Object.FindAnyObjectByType<DungeonGenerator>();
                var player = Player();
                var minimap = Object.FindAnyObjectByType<GameMinimap>();
                var start = generator.SpawnedRooms.Single(r => r.RoomType == RoomType.Start);
                var bossRoom = generator.SpawnedRooms.Single(r => r.RoomType == RoomType.Exit);
                var secret = generator.SpawnedRooms.Single(r => r.GetComponent<RoomTerrainProfile>().HiddenTreasury);
                var combat = generator.SpawnedRooms.Single(r => r.TemplateId == featured[i]);
                foreach (var room in new[] { start, bossRoom, secret, combat })
                    Assert.AreEqual((DungeonTerrainRegion)i, room.GetComponent<RoomTerrainProfile>().Region, room.name);
                Assert.NotNull(minimap);
                Assert.IsTrue(start.VendingMachine.IsShopReady);
                Assert.IsTrue(minimap.HasDiscoveredMarker(start.RoomId));
                Assert.IsFalse(minimap.IsRoomVisible(secret.RoomId));
                Assert.IsFalse(minimap.HasDiscoveredMarker(bossRoom.RoomId));

                var chest = secret.GetComponentsInChildren<TreasureChest>().Single();
                var drops = chest.Slots.Where(s => !s.IsEmpty).ToArray();
                Assert.IsNotEmpty(drops);
                Assert.IsTrue(drops.All(s => s.Item.Rarity >= ItemRarity.Rare && !(s.Item is CraftMaterialSO material && material.BossOnly)));
                Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out var from, 2f, NavMesh.AllAreas));
                var treasurePoint = PointOnNavMesh(secret.transform.position);
                var path = new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(from.position, treasurePoint, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Secret branch is physically unreachable.");

                MovePlayer(treasurePoint);
                yield return WaitFor(() => minimap.HasDiscoveredMarker(secret.RoomId), "The treasure icon was not discovered.");
                Assert.IsTrue(minimap.IsRoomVisible(secret.RoomId));
                var arrival = Object.FindAnyObjectByType<DungeonArrivalBanner>();
                if (arrival != null) yield return WaitFor(() => !arrival.IsShowing, "Arrival card did not close.");
                yield return Capture("0" + i + "-treasury.png");

                MovePlayer(PointOnNavMesh(combat.transform.position + Vector3.back * 2f));
                if (combat.LifecycleController.State == RoomLifecycleState.Unvisited) Assert.IsTrue(combat.LifecycleController.EnterCombat());
                yield return WaitFor(() => combat.LifecycleController.State == RoomLifecycleState.Combat, "Authored encounter did not begin.");
                Assert.IsTrue(combat.DoorSockets.Where(s => s.IsConnected).All(s => s.IsLocked));
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("1" + i + "-" + featured[i].Substring(8) + ".png");
                foreach (var enemy in Object.FindObjectsByType<EnemyBase>())
                    if (!enemy.IsDead) enemy.TakeDamage(new DamageInfo(100000, player.gameObject, enemy.transform.position));
                yield return WaitFor(() => combat.LifecycleController.State == RoomLifecycleState.Cleared, "Authored encounter did not unlock.");
                yield return WaitFor(() => Object.FindAnyObjectByType<CombatToast>() == null, "Previous reward notification did not close.");

                var spawner = Object.FindAnyObjectByType<EnemySpawner>();
                MovePlayer(PointOnNavMesh(bossRoom.transform.position + Vector3.back * 5f));
                if (bossRoom.LifecycleController.State == RoomLifecycleState.Unvisited) Assert.IsTrue(bossRoom.LifecycleController.EnterCombat());
                yield return WaitFor(() => spawner.ActiveBoss != null, "Missing guardian in " + ids[i]);
                var boss = spawner.ActiveBoss;
                var art = bossRoom.GetComponent<BossArenaDressing>();
                Assert.NotNull(art);
                Assert.AreEqual(boss.Kind, art.ActiveTheme);
                var sigil = bossRoom.transform.Find("Art_GuardianSigil");
                Assert.NotNull(sigil);
                Assert.IsEmpty(sigil.GetComponentsInChildren<Collider>(true));
                Assert.IsTrue(boss.GetComponent<NavMeshAgent>().isOnNavMesh);
                yield return WaitFor(() => minimap.HasDiscoveredMarker(bossRoom.RoomId), "Boss icon was not discovered.");
                yield return new WaitForSecondsRealtime(.25f);
                yield return Capture("2" + i + "-boss-arena.png");
                if (i == 0)
                {
                    var mesh = sigil.GetComponent<MeshFilter>().sharedMesh;
                    var shapes = new HashSet<int>();
                    foreach (BossKind kind in Enum.GetValues(typeof(BossKind)))
                    {
                        art.Apply(BossRoster.Get(kind));
                        Assert.AreEqual(kind, art.ActiveTheme);
                        shapes.Add(mesh.vertexCount);
                    }
                    Assert.GreaterOrEqual(shapes.Count, 4, "Guardian heraldry should use distinct geometry.");
                    art.Apply(boss.Definition);
                }
            }
        }

        [UnityTest]
        public IEnumerator CameraFadesTheObstructingWallButNeverItsCollision()
        {
            yield return LoadSeededDungeon("dungeon.standard", "terrain.stonebridge");
            var start = Object.FindAnyObjectByType<DungeonGenerator>().SpawnedRooms.Single(r => r.RoomType == RoomType.Start);
            var p = start.transform.position;
            float edge = start.GetCurrentWorldBounds().min.z;
            var anchor = start.GetComponentsInChildren<WallOcclusionAnchor>()
                .Where(a => a.Group != null && a.TryGetComponent<BoxCollider>(out var b) && b.enabled && b.bounds.center.z < edge + 1f && b.bounds.size.x > 1f)
                .OrderByDescending(a => a.GetComponent<BoxCollider>().bounds.size.x).First();
            var collider = anchor.GetComponent<BoxCollider>();
            var group = anchor.Group;
            var target = collider.bounds.center + Vector3.forward * (collider.bounds.extents.z + .72f);
            target.y = p.y;
            MovePlayer(PointOnNavMesh(target));
            var occlusion = Camera.main.GetComponent<DungeonWallOcclusion>();
            Assert.NotNull(occlusion);
            yield return WaitFor(() => group.Occluded && group.Opacity < .45f, "Camera did not reveal player behind the foreground wall.");
            Assert.IsTrue(collider.enabled && collider.gameObject.activeInHierarchy);
            Assert.IsFalse(collider.isTrigger);
            Assert.Greater(occlusion.OccludedGroupCount, 0);
            var arrival = Object.FindAnyObjectByType<DungeonArrivalBanner>();
            if (arrival != null) yield return WaitFor(() => !arrival.IsShowing, "Arrival card did not close.");
            yield return Capture("30-wall-cutaway.png");
            MovePlayer(PointOnNavMesh(p));
            yield return WaitFor(() => !group.Occluded && group.Opacity > .95f, "Wall did not become opaque again.");
            Assert.IsTrue(collider.enabled);
        }

        private static Vector3 PointOnNavMesh(Vector3 point)
        {
            Assert.IsTrue(NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas), "No walkable ground at " + point);
            return hit.position + Vector3.up * .04f;
        }

        private static void MovePlayer(Vector3 position)
        {
            var player = Player();
            player.Teleport(position);
            Camera.main.GetComponent<GameCameraFollow>().SetTarget(player.transform, true);
        }

        private static IEnumerator Capture(string filename) => DungeonArrivalShopPlayTests.Capture(filename, CaptureVariable);
    }
}

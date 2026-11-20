using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat.Items;
using LightSouls.Common;
using LightSouls.MapGeneration.Hazards;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.MapGeneration.Tests
{
    public class ExpandedTerrainTests
    {
        private const string Templates = "Assets/ScriptableObjects/MapGeneration/Templates";
        private const string Configs = "Assets/ScriptableObjects/MapGeneration/Config/";
        private static readonly string[] ConfigNames = { "DefaultDungeonConfig", "Config_LavaDungeon", "Config_MazeDungeon" };

        private static RoomTemplateSO[] AllTemplates() => AssetDatabase.FindAssets("t:RoomTemplateSO", new[] { Templates })
            .Select(g => AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        private static DungeonGenerationConfigSO GetConfig(string name) => AssetDatabase.LoadAssetAtPath<DungeonGenerationConfigSO>(Configs + name + ".asset");
        private static RoomTerrainProfile Profile(RoomTemplateSO template) => template.Prefab.GetComponent<RoomTerrainProfile>();

        [Test]
        public void ThirtyTemplatesAreUsedAndEachRegionHasItsOwnStartBossAndSecretBranch()
        {
            var templates = AllTemplates();
            Assert.AreEqual(30, templates.Length);
            Assert.AreEqual(30, templates.Select(t => t.TemplateId).Distinct().Count());
            Assert.IsTrue(templates.All(t => Profile(t) != null && !string.IsNullOrEmpty(Profile(t).LayoutNote)));
            Assert.AreEqual(18, templates.Count(t => Profile(t).AuthoredLayout));
            var used = new HashSet<RoomTemplateSO>();
            int[] expectedCombat = { 4, 3, 4 };
            for (int i = 0; i < ConfigNames.Length; i++)
            {
                var config = GetConfig(ConfigNames[i]);
                Assert.NotNull(config);
                var region = (DungeonTerrainRegion)i;
                var start = config.StartRooms.Single();
                var boss = config.ExitRooms.Single();
                var treasury = config.SpecialBranchRooms.Single(t => Profile(t).HiddenTreasury);
                foreach (var template in new[] { start, boss, treasury }) Assert.AreEqual(region, Profile(template).Region);
                Assert.NotNull(start.Prefab.VendingMachine);
                Assert.NotNull(boss.Prefab.BossRewardController);
                Assert.AreEqual(RoomType.Treasure, treasury.RoomType);
                Assert.AreEqual(1, treasury.MinCount);
                Assert.AreEqual(1, treasury.MaxCount);
                Assert.AreEqual(expectedCombat[i], config.NormalRooms.Count(t => Profile(t).AuthoredLayout && Profile(t).Region == region));
                used.UnionWith(config.StartRooms.Concat(config.CorridorRooms).Concat(config.NormalRooms).Concat(config.SpecialBranchRooms).Concat(config.ExitRooms));
            }
            CollectionAssert.AreEquivalent(templates, used, "Every prefab must be reachable through a production dungeon pool.");
            var loot = AssetDatabase.LoadAssetAtPath<LootTableSO>("Assets/ScriptableObjects/Combat/Items/LootTable_SecretTreasury.asset");
            Assert.NotNull(loot);
            Assert.IsNotEmpty(loot.DropEntries);
            Assert.IsTrue(loot.DropEntries.All(e => e.Item != null && e.Item.Rarity >= ItemRarity.Rare && !(e.Item is CraftMaterialSO m && m.BossOnly)));
            foreach (string icon in new[] { "MapShop", "MapTreasure", "MapBoss" }) Assert.NotNull(Resources.Load<Sprite>("Art/DarkFantasy/UI/" + icon));
        }

        [TestCase("DefaultDungeonConfig")]
        [TestCase("Config_LavaDungeon")]
        [TestCase("Config_MazeDungeon")]
        public void RealRegionPoolsRemainConnectedAcrossThirtyTwoSeeds(string name)
        {
            var config = GetConfig(name);
            var request = config.ToPlanRequest(out var lookup);
            string secretId = config.SpecialBranchRooms.Single(t => Profile(t).HiddenTreasury).TemplateId;
            var observed = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                Assert.IsTrue(DungeonLayoutPlanner.TryPlan(request, 531 + 197 * seed, out var layout, out var error), name + ": " + error);
                Assert.IsTrue(DungeonLayoutPlanner.IsGraphValid(layout, out error), name + ": " + error);
                Assert.IsTrue(layout.isReachableFromStartToExit);
                Assert.IsFalse(layout.hasAnyOverlap);
                var secret = layout.rooms.Single(r => r.templateId == secretId);
                Assert.IsFalse(secret.isOnMainPath, "Secret treasure must be a branch, not an ordinary compulsory combat room.");
                Assert.AreEqual(1, DungeonLayoutPlanner.GetRoomDegree(layout, secret.roomId));
                foreach (var room in layout.rooms)
                {
                    Assert.IsTrue(lookup.ContainsKey(room.templateId));
                    observed.Add(room.templateId);
                }
            }
            foreach (var normal in config.NormalRooms.Where(t => Profile(t).AuthoredLayout))
                CollectionAssert.Contains(observed, normal.TemplateId, "New combat template was never produced: " + normal.TemplateId);
        }

        [Test]
        public void AuthoredCombatRoomsHaveDryDoorApproachesAndNavigableSpawns()
        {
            var rooms = AllTemplates().Where(t => Profile(t).AuthoredLayout && (t.RoomType == RoomType.Combat || t.RoomType == RoomType.Normal)).ToArray();
            Assert.AreEqual(11, rooms.Length);
            foreach (var template in rooms)
            {
                var instance = Object.Instantiate(template.Prefab.gameObject, new Vector3(400, 0, 400), Quaternion.identity);
                NavMeshData data = null;
                NavMeshDataInstance link = default;
                try
                {
                    var room = instance.GetComponent<RoomModule>();
                    string label = template.TemplateId;
                    Assert.That(room.EnemySpawnPoints.Count, Is.InRange(3, 5), label);
                    Assert.AreEqual(1, room.LootSpawnPoints.Count, label);
                    foreach (var socket in room.DoorSockets) socket.SetConnectionState(true);
                    Physics.SyncTransforms();
                    var hazards = room.GetComponentsInChildren<HazardZoneBase>().Select(h => h.GetComponent<Collider>()).ToArray();
                    var bounds = room.GetCurrentWorldBounds();
                    var center = room.transform.position;
                    foreach (var socket in room.DoorSockets)
                    {
                        var inside = socket.transform.position - room.transform.TransformDirection(socket.LocalDirection.ToVector3()) * 1.2f;
                        for (float t = 0; t <= 1.001f; t += .05f)
                        {
                            var point = Vector3.Lerp(center, inside, t);
                            foreach (var hazard in hazards)
                            {
                                Assert.IsTrue(hazard.isTrigger, label);
                                Assert.IsFalse(OverlapsXZ(hazard.bounds, point, .38f), label + " hazard covers the dry route at " + point);
                            }
                        }
                    }
                    var sources = new List<NavMeshBuildSource>();
                    NavMeshBuilder.CollectSources(room.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                        new List<NavMeshBuildMarkup>(), sources);
                    bounds.Expand(new Vector3(2, 5, 2));
                    data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
                    Assert.NotNull(data, label);
                    link = NavMesh.AddNavMeshData(data);
                    Assert.IsTrue(NavMesh.SamplePosition(center, out var origin, 1.1f, NavMesh.AllAreas), label + " center is not walkable.");
                    foreach (var socket in room.DoorSockets)
                        AssertPath(origin.position, socket.transform.position - room.transform.TransformDirection(socket.LocalDirection.ToVector3()) * 1.4f, label + " doorway " + socket.SocketId);
                    foreach (var anchor in room.EnemySpawnPoints.Concat(room.LootSpawnPoints))
                    {
                        foreach (var hazard in hazards) Assert.IsFalse(OverlapsXZ(hazard.bounds, anchor.position, .35f), label + " unsafe anchor " + anchor.name);
                        AssertPath(origin.position, anchor.position, label + " anchor " + anchor.name);
                    }
                    if (label.Contains("bridge") || label.Contains("sluicecrossing"))
                    {
                        var ramps = room.GetComponentsInChildren<MeshCollider>().Where(c => c.name == "WalkableRamp").ToArray();
                        Assert.AreEqual(4, ramps.Length, label);
                        Assert.IsNotNull(room.transform.Find("TerrainColliders/BridgeDeck_Collider"), label);
                        Assert.IsTrue(Physics.Raycast(center + Vector3.up, Vector3.down, out var deck, 1f, 1 << 8, QueryTriggerInteraction.Ignore), label);
                        Assert.Greater(deck.point.y - center.y, .12f, label + " bridge must change walking height, not just texture.");
                    }
                }
                finally
                {
                    if (link.valid) link.Remove();
                    if (data != null) Object.DestroyImmediate(data);
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static bool OverlapsXZ(Bounds b, Vector3 p, float radius) => p.x >= b.min.x - radius && p.x <= b.max.x + radius && p.z >= b.min.z - radius && p.z <= b.max.z + radius;

        private static void AssertPath(Vector3 origin, Vector3 target, string label)
        {
            Assert.IsTrue(NavMesh.SamplePosition(target, out var destination, 1.4f, NavMesh.AllAreas), label + " cannot stand here.");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(origin, destination.position, NavMesh.AllAreas, path), label);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, label);
        }
    }
}

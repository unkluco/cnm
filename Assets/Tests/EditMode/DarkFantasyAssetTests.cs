using System.Linq;
using LightSouls.Art;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using LightSouls.MapGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class DarkFantasyAssetTests
    {
        [Test]
        public void AllRoomPrefabsHaveBatchedArchitectureAndSafeVisualOnlyDecor()
        {
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/MapGeneration/Rooms"});
            Assert.GreaterOrEqual(paths.Length,12);
            foreach(string guid in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var room=prefab.GetComponent<RoomModule>(); Assert.NotNull(room,prefab.name);
                var floor=prefab.transform.Find("Art_Flagstones"); var wall=prefab.transform.Find("Art_Masonry");
                Assert.NotNull(floor,prefab.name); Assert.NotNull(wall,prefab.name);
                Assert.That(floor.GetComponentsInChildren<MeshRenderer>(true).Length,Is.InRange(1,7),prefab.name+" floor must be batched.");
                Assert.That(wall.GetComponentsInChildren<MeshRenderer>(true).Length,Is.InRange(1,4),prefab.name+" wall must be batched.");
                int architectureTriangles=floor.GetComponentsInChildren<MeshFilter>(true).Concat(wall.GetComponentsInChildren<MeshFilter>(true)).Sum(m=>m.sharedMesh.triangles.Length/3);
                Assert.Less(architectureTriangles,15000,prefab.name+" architecture must remain low-poly.");
                foreach(var collider in prefab.GetComponentsInChildren<Collider>(true))
                {
                    for(var t=collider.transform;t!=null&&t!=prefab.transform;t=t.parent)
                        Assert.IsFalse(t.name.StartsWith("Art_"),prefab.name+" decoration must not alter collisions.");
                }
                var torches=prefab.GetComponentsInChildren<DarkFantasyTorchLight>(true);
                Assert.That(torches.Length,Is.InRange(1,6),prefab.name);
                Assert.IsTrue(torches.All(t=>t.Source!=null&&t.Source.type==LightType.Point&&t.Source.shadows==LightShadows.None),prefab.name);
                Assert.Greater(room.DoorSockets.Count,0,prefab.name);
                if(room.RoomType==RoomType.Exit)
                {
                    Assert.NotNull(room.ExitPoint);
                    Assert.NotNull(room.ExitPoint.Find("Art_Portal"));
                    Assert.NotNull(room.BossRewardController);
                }
            }
        }

        [Test]
        public void EveryWeaponHasAnAuthoredColliderFreeVisualWithMatchingCombatComponent()
        {
            var guids=AssetDatabase.FindAssets("t:WeaponDataSO",new[]{"Assets/ScriptableObjects/Combat/Weapons"});
            Assert.GreaterOrEqual(guids.Length,14);
            foreach(string guid in guids)
            {
                var weapon=AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.NotNull(weapon.WeaponPrefab,weapon.name);
                Assert.GreaterOrEqual(weapon.WeaponPrefab.GetComponentsInChildren<MeshRenderer>().Length,3,weapon.name);
                Assert.IsEmpty(weapon.WeaponPrefab.GetComponentsInChildren<Collider>(true),weapon.name);
                Assert.IsTrue(weapon.Type==WeaponType.Melee?weapon.WeaponPrefab.GetComponent<MeleeWeapon>()!=null:weapon.WeaponPrefab.GetComponent<RangedWeapon>()!=null,weapon.name);
            }
        }

        [Test]
        public void HeroAndUiUseTheNewSharedArtKit()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/Player_Character.prefab");
            var names=player.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            CollectionAssert.Contains(names,"Art_Helm");CollectionAssert.Contains(names,"Art_Mantle");CollectionAssert.Contains(names,"WeaponSocket");
            Assert.Greater(player.GetComponentsInChildren<MeshRenderer>().Length,25);
            Assert.NotNull(DarkFantasyTheme.PanelFrame);
            Assert.NotNull(DarkFantasyTheme.ButtonFrame);
            Assert.NotNull(DarkFantasyTheme.CircleFrame);
            Assert.NotNull(DarkFantasyTheme.JoystickRing);
            Assert.NotNull(Resources.Load<Mesh>("Art/DarkFantasy/Meshes/BeveledBox"));
            var items=AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/ScriptableObjects/Combat"}).Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g))).Where(i=>i!=null).ToArray();
            Assert.GreaterOrEqual(items.Length,29);
            Assert.IsTrue(items.All(i=>i.Icon!=null&&AssetDatabase.GetAssetPath(i.Icon).StartsWith("Assets/Art/DarkFantasy/Icons/")),"All inventory items need matching 3D rendered icons.");
        }

        [Test]
        public void WayfindingColorsTintOnlyLandmarksAndNotTheStoneArch()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/Rooms/Room_Start.prefab");
            var instance=Object.Instantiate(prefab);
            try
            {
                var door=instance.GetComponent<RoomModule>().DoorSockets[0];
                var open=door.transform.Find("OpenVisual");
                var landmark=open.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Art_Landmark");
                var masonry=open.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("Frame"));
                var block=new MaterialPropertyBlock();
                door.SetDoorFrameColor(Color.magenta);
                landmark.GetPropertyBlock(block);Assert.AreEqual(Color.magenta,block.GetColor("_BaseColor"));
                masonry.GetPropertyBlock(block);Assert.IsTrue(block.isEmpty);
            }
            finally{Object.DestroyImmediate(instance);}
        }
    }
}

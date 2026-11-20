using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Items;
using LightSouls.Combat.Stats;
using LightSouls.Combat.UI;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.Lobby;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class ArsenalPlayTests
    {
        private static PlayerController Player()=>Object.FindAnyObjectByType<PlayerController>();
        private static WeaponDataSO Weapon(string key)=>(WeaponDataSO)CraftingCatalogSO.Load().Recipes.Single(r=>r.Output.ItemId=="weapon.arsenal_"+key).Output;
        private static ConsumableDataSO Potion(string key)=>(ConsumableDataSO)CraftingCatalogSO.Load().Recipes.Single(r=>r.Output.ItemId=="consumable.arsenal_"+key).Output;
        private static IEnumerator WaitFor(Func<bool> predicate,string message,float seconds=20f)
        {
            float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(predicate(),message);yield return null;
        }
        private static IEnumerator LoadLobby()
        {
            Time.timeScale=1f;SceneLoader.StartNewRun();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Lobby&&Player()!=null,"Lobby not ready.");
        }
        private static IEnumerator LoadGame()
        {
            Time.timeScale=1f;RunState.Reset();RunState.SelectedDungeonId="dungeon.standard";
            yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(()=>Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon not ready.");
        }
        private static IEnumerator Capture(string name)=>DungeonArrivalShopPlayTests.Capture(name,"LIGHTSOULS_ARSENAL_CAPTURE_DIR");

        [UnityTearDown]
        public IEnumerator Cleanup(){Time.timeScale=1f;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);}

        [UnityTest]
        public IEnumerator DungeonJournalShowsAllThreeIllustrationsAndAwaitsExplicitConfirmation()
        {
            yield return LoadLobby();var ui=LobbyController.Instance.DungeonSelectUI;Assert.NotNull(ui);
            string previous=RunState.SelectedDungeonId;ui.Open(Player().gameObject);yield return null;Canvas.ForceUpdateCanvases();
            Assert.IsTrue(ui.IsOpen);Assert.IsTrue(BackButtonStack.HasOpenPanel);Assert.AreEqual(3,ui.CardCount);
            var portraits=ui.GetComponentsInChildren<Image>().Where(i=>i.name=="RegionIllustration").ToArray();
            Assert.AreEqual(3,portraits.Length);Assert.IsTrue(portraits.All(i=>i.sprite!=null));
            Assert.IsTrue(ui.SelectDungeon("dungeon.maze"));Assert.AreEqual("dungeon.maze",ui.SelectedDungeonId);
            Assert.AreEqual(previous,RunState.SelectedDungeonId,"Selecting a card should not start travel.");
            yield return Capture("01-dungeon-journal.png");
            Assert.IsTrue(BackButtonStack.TryCloseTop());Assert.IsFalse(ui.IsOpen);
            ui.Open(Player().gameObject);yield return null;Assert.AreEqual(3,ui.CardCount);
            Assert.IsTrue(ui.SelectDungeon("dungeon.lava"));yield return Capture("02-dungeon-journal-lava.png");
            ui.GetComponentsInChildren<Button>().Single(b=>b.name=="EnterDungeonButton").onClick.Invoke();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Game&&Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Confirmation did not enter selected dungeon.");
            Assert.AreEqual("dungeon.lava",RunState.SelectedDungeonId);Assert.AreEqual(1,RunState.DungeonsEntered);
            var generator=Object.FindAnyObjectByType<DungeonGenerator>();var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            var room=generator.SpawnedRooms.First(r=>r.RoomType==RoomType.Normal||r.RoomType==RoomType.Combat);
            Assert.IsTrue(room.LifecycleController.EnterCombat());Assert.Greater(spawner.AliveEnemyCount,0);
            var profile=DungeonChallenge.For("dungeon.lava");float multiplier=profile.EnemyHealth*profile.Progression(1,room.GraphDepth,0);
            foreach(var enemy in Object.FindObjectsByType<EnemyBase>())
            {
                var prefab=spawner.RegularEnemyPrefabs.Concat(spawner.SpecialEnemyPrefabs).First(p=>enemy.name.StartsWith(p.name));
                Assert.AreEqual(prefab.GetComponent<HealthComponent>().MaxHealth*multiplier,enemy.MaxHealth,.2f,"Encounter should use the selected difficulty.");
                enemy.TakeDamage(new DamageInfo(100000,Player().gameObject,enemy.transform.position));
            }
            Assert.AreEqual(RoomLifecycleState.Cleared,room.LifecycleController.State);Assert.AreEqual(1,RunState.CombatRoomsCleared);
        }

        [UnityTest]
        public IEnumerator RecoverySurvivesWeaponSwapAndConsumablesShareCooldown()
        {
            yield return LoadLobby();var player=Player();var combat=player.Combat;player.SetInputActive(true);
            var heavy=Weapon("bastion_hammer");var quick=Weapon("rime_dagger");int attacks=0;combat.OnAttackExecuted+=()=>attacks++;
            combat.EquipWeapon(heavy);combat.PerformAttack();Assert.AreEqual(1,attacks);Assert.Greater(PlayerCombat.CooldownFor(heavy),1f);
            combat.EquipWeapon(quick);combat.PerformAttack();Assert.AreEqual(1,attacks,"Equipping another weapon must not bypass the heavy attack recovery.");
            yield return null;Assert.Greater(combat.AttackCooldownRemaining,0);
            var hud=Object.FindAnyObjectByType<CombatHUD>();Assert.NotNull(hud);Assert.NotNull(hud.AttackCooldownText);Assert.IsTrue(hud.AttackCooldownText.gameObject.activeSelf);
            yield return Capture("03-attack-recovery.png");
            Time.timeScale=3f;yield return WaitFor(()=>combat.AttackCooldownRemaining<=.025f,"Attack did not recover.");
            combat.PerformAttack();Assert.AreEqual(2,attacks);Assert.Greater(PlayerCombat.CooldownFor(quick),0);
            var health=player.GetComponent<HealthComponent>();health.TakeDamage(new DamageInfo(70f,null,player.transform.position));
            float before=health.CurrentHealth;var tonic=Potion("mending");Assert.IsTrue(tonic.Use(player.gameObject));Assert.Greater(health.CurrentHealth,before);
            float healed=health.CurrentHealth;Assert.IsFalse(Potion("renewal").Use(player.gameObject),"Swapping to a different potion must not permit instant healing spam.");
            Assert.AreEqual(healed,health.CurrentHealth);Assert.Greater(ConsumableDataSO.CooldownRemaining,0f);
        }

        // A small independent walkable arena in the real Game scene lets abilities be tested
        // deterministically, without moving the player through a procedurally locked doorway.
        private sealed class Arena:IDisposable
        {
            public readonly Vector3 Center=new Vector3(1000,0,1000);
            public readonly GameObject Root;private readonly NavMeshData data;private NavMeshDataInstance link;
            public Arena()
            {
                Root=new GameObject("ArsenalTestArena");Root.transform.position=Center;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="NavigationFloor";floor.layer=8;floor.transform.SetParent(Root.transform,false);floor.transform.localPosition=Vector3.down*.10f;floor.transform.localScale=new Vector3(32,.20f,32);
                Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(Root.transform,1<<8,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
                data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Center+Vector3.up,new Vector3(34,6,34)),Vector3.zero,Quaternion.identity);
                Assert.NotNull(data);link=NavMesh.AddNavMeshData(data);
                Player().Teleport(Center+Vector3.up*.05f);Player().SetInputActive(false);Player().InLockedCombat=true;
            }
            public EnemyBase Spawn(GameObject prefab,Vector3 offset)
            {Assert.IsTrue(NavMesh.SamplePosition(Center+offset,out var p,1.2f,NavMesh.AllAreas));return Object.Instantiate(prefab,p.position,Quaternion.identity,Root.transform).GetComponent<EnemyBase>();}
            public Transform Marker(Vector3 offset){var go=new GameObject("SpawnMarker");go.transform.SetParent(Root.transform,false);go.transform.localPosition=offset;return go.transform;}
            public void Dispose(){if(link.valid)link.Remove();if(data!=null)Object.Destroy(data);if(Root!=null)Object.Destroy(Root);}
        }

        [UnityTest]
        public IEnumerator PushFreezeChainExposeBlinkWallAndWardWorkWithRealPhysics()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            var normal=spawner.RegularEnemyPrefabs.First(p=>p.GetComponent<EnemyBase>() is MeleeEnemy);
            var pushed=arena.Spawn(normal,Vector3.forward*3.4f);var frozen=arena.Spawn(normal,Vector3.right*4f);
            var chainA=arena.Spawn(normal,new Vector3(-8,0,6));var chainB=arena.Spawn(normal,new Vector3(-6,0,6));
            yield return null;
            foreach(var e in new[]{pushed,frozen,chainA,chainB}){e.enabled=false;e.GetComponent<NavMeshAgent>().isStopped=true;}
            Vector3 before=pushed.transform.position;
            WeaponHitResolver.Apply(Weapon("bastion_hammer"),4f,player.gameObject,pushed,pushed.transform.position+Vector3.up);
            Assert.Greater(Vector3.Distance(before,pushed.transform.position),1.2f,"Push should change the actual NavMesh position.");
            Assert.Greater(RunState.CooldownRemaining("combat.hit.Push"),0);
            float pushedDistance=Vector3.Distance(player.transform.position,pushed.transform.position);
            WeaponHitResolver.Apply(Weapon("gravity_spear"),3f,player.gameObject,pushed,pushed.transform.position+Vector3.up);
            Assert.Less(Vector3.Distance(player.transform.position,pushed.transform.position),pushedDistance-1f,"Pull must move the target toward the knight.");
            WeaponHitResolver.Apply(Weapon("frost_scepter"),4,player.gameObject,frozen,frozen.transform.position+Vector3.up);
            Assert.IsTrue(frozen.IsStunned);Assert.IsTrue(frozen.GetComponent<StatusEffectReceiver>().IsSlowed);
            Assert.Less(frozen.GetComponent<NavMeshAgent>().speed,.4f);
            float otherHealth=chainB.CurrentHealth;
            WeaponHitResolver.Apply(Weapon("arcane_bow"),3,player.gameObject,chainA,chainA.transform.position+Vector3.up);
            Assert.Less(chainB.CurrentHealth,otherHealth,"Lightning should damage another visible target.");
            WeaponHitResolver.Apply(Weapon("thorn_sword"),3,player.gameObject,chainA,chainA.transform.position+Vector3.up);
            float hp=chainA.CurrentHealth;var health=chainA.GetComponent<HealthComponent>();
            chainA.TakeDamage(new DamageInfo(10f,player.gameObject,chainA.transform.position));
            Assert.Greater(hp-chainA.CurrentHealth,10f*(1-health.DamageReduction)+.3f,"Expose must increase actual damage.");
            Time.timeScale=3f;yield return WaitFor(()=>!frozen.IsStunned&&!frozen.GetComponent<StatusEffectReceiver>().IsSlowed,"Freeze did not expire.");
            foreach(var e in new[]{pushed,frozen,chainA,chainB})if(e!=null)Object.Destroy(e.gameObject);yield return null;

            player.Teleport(arena.Center+Vector3.up*.05f);var blink=Weapon("mirage_sabre");var start=player.transform.position;
            Assert.IsTrue(WeaponTactics.TryCast(blink,player.gameObject,Vector3.forward));Assert.Greater(Vector3.Distance(start,player.transform.position),2f);
            Assert.IsFalse(WeaponTactics.TryCast(blink,player.gameObject,Vector3.forward));
            player.Teleport(arena.Center+Vector3.up*.05f);
            var blocked=GameObject.CreatePrimitive(PrimitiveType.Cube);blocked.layer=8;blocked.transform.SetParent(arena.Root.transform);blocked.transform.position=arena.Center+Vector3.forward*.88f+Vector3.up;blocked.transform.localScale=new Vector3(4,2,.3f);Physics.SyncTransforms();
            Assert.IsFalse(WeaponTactics.TryFindTravel(player.transform.position,Vector3.forward,4,.46f,true,out _),"Blink must not cross a sealed physical barrier.");Object.Destroy(blocked);yield return null;
            yield return WaitFor(()=>RunState.CooldownRemaining("combat.cast.global")<=0,"Cast common recovery.");
            // The real room entry trigger must never prevent the wall from being placed.
            var entranceTrigger=new GameObject("RoomEntryTrigger",typeof(BoxCollider));entranceTrigger.layer=8;entranceTrigger.transform.SetParent(arena.Root.transform,false);
            var entryCollider=entranceTrigger.GetComponent<BoxCollider>();entryCollider.isTrigger=true;entryCollider.size=new Vector3(24,3,24);entryCollider.center=Vector3.up*1.4f;Physics.SyncTransforms();
            var wallWeapon=Weapon("guardian_maul");Assert.IsTrue(WeaponTactics.TryCast(wallWeapon,player.gameObject,Vector3.forward));var wall=TemporaryRuneWall.Active;
            Assert.NotNull(wall);Assert.IsTrue(wall.GetComponent<BoxCollider>().enabled);Assert.IsTrue(wall.GetComponent<NavMeshObstacle>().carving);
            Physics.SyncTransforms();Vector3 p=wall.transform.position+Vector3.up*.65f,d=wall.transform.forward;
            Assert.IsTrue(Physics.Linecast(p-d*2,p+d*2,1<<8,QueryTriggerInteraction.Ignore));Assert.IsFalse(WeaponTactics.TryCast(wallWeapon,player.gameObject,Vector3.forward));
            yield return WaitFor(()=>TemporaryRuneWall.Active==null,"Terrain did not expire after its duration.");
            Assert.IsFalse(WeaponTactics.TryCast(Weapon("ice_totem"),player.gameObject,Vector3.forward),"Stone and ice walls share a cooldown.");
            Time.timeScale=4f;yield return WaitFor(()=>WeaponTactics.CastCooldownRemaining(Weapon("ice_totem"))<=0,"Wall recovery.");
            Assert.IsTrue(WeaponTactics.TryCast(Weapon("ice_totem"),player.gameObject,Vector3.forward));Assert.IsTrue(TemporaryRuneWall.Active.name.Contains("Ice"));
            player.InLockedCombat=false;yield return WaitFor(()=>TemporaryRuneWall.Active==null,"Terrain did not dissolve outside combat.");player.InLockedCombat=true;
            yield return WaitFor(()=>RunState.CooldownRemaining("combat.cast.global")<=0,"Cast common recovery.");
            Assert.IsTrue(WeaponTactics.TryCast(Weapon("solar_wand"),player.gameObject,Vector3.forward));Assert.LessOrEqual(player.Stats.Get(StatType.DamageTaken),.73f);
        }

        [UnityTest]
        public IEnumerator CharmedEnemyFightsActualHostilesAndCannotTrapTheWave()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            var normal=spawner.RegularEnemyPrefabs.First(p=>p.GetComponent<EnemyBase>() is MeleeEnemy);
            spawner.SetSpecialEnemyPrefabs(Array.Empty<GameObject>());spawner.SetEnemyPrefabs(new[]{normal});
            spawner.SpawnWave(new[]{arena.Marker(new Vector3(-1,0,4)),arena.Marker(new Vector3(1,0,4))});
            yield return null;Assert.AreEqual(2,spawner.AliveEnemyCount);
            var enemies=Object.FindObjectsByType<EnemyBase>().Where(e=>!e.IsDead&&Vector3.Distance(e.transform.position,arena.Center)<16).OrderBy(e=>e.transform.position.x).ToArray();
            Assert.AreEqual(2,enemies.Length);var ally=enemies[0];var hostile=enemies[1];hostile.enabled=false;hostile.GetComponent<NavMeshAgent>().isStopped=true;
            ally.TakeDamage(new DamageInfo(ally.MaxHealth*.66f,player.gameObject,ally.transform.position));
            WeaponHitResolver.Apply(Weapon("dream_scepter"),1,player.gameObject,ally,ally.transform.position+Vector3.up);
            Assert.IsTrue(ally.IsCharmed);Assert.IsFalse(hostile.TryCharm(player.gameObject,5f,1f));
            float friendlyHp=ally.CurrentHealth;Assert.AreEqual(0,WeaponHitResolver.Apply(Weapon("bastion_hammer"),10,player.gameObject,ally,ally.transform.position));Assert.AreEqual(friendlyHp,ally.CurrentHealth);
            float before=hostile.CurrentHealth;Time.timeScale=2f;
            yield return WaitFor(()=>hostile.CurrentHealth<before,"Charmed ally never fought a hostile.",5f);
            int cleared=0;spawner.AllEnemiesDefeated+=()=>cleared++;
            if(!hostile.IsDead)hostile.TakeDamage(new DamageInfo(10000f,player.gameObject,hostile.transform.position));
            Assert.AreEqual(0,spawner.AliveEnemyCount);Assert.AreEqual(1,cleared,"Charmed survivor should not soft-lock the room or clear it twice.");
        }

        [UnityTest]
        public IEnumerator EveryAddedWeaponAttacksAndRestoresItsAuthoredPose()
        {
            yield return LoadLobby();var player=Player();Time.timeScale=3f;
            var weapons=CraftingCatalogSO.Load().Recipes.Select(r=>r.Output).OfType<WeaponDataSO>().Where(w=>w.ItemId.StartsWith("weapon.arsenal_")).OrderBy(w=>w.ItemId).ToArray();
            Assert.AreEqual(30,weapons.Length);
            foreach(var data in weapons)
            {
                player.Combat.EquipWeapon(data);yield return null;
                var instances=player.GetComponentsInChildren<WeaponBase>();Assert.AreEqual(1,instances.Length,data.name+" duplicated active weapon");
                var weapon=instances[0];Quaternion rest=weapon.transform.localRotation;Vector3 position=weapon.transform.localPosition;
                Assert.IsTrue(weapon.ExecuteAttack(player.transform,player.transform.forward,1<<7,player.gameObject),data.name);
                yield return new WaitForSeconds(.75f);
                Assert.Less(Quaternion.Angle(rest,weapon.transform.localRotation),.6f,data.name+" did not reset its pose");
                Assert.Less(Vector3.Distance(position,weapon.transform.localPosition),.025f,data.name+" did not reset recoil");
            }
            player.Combat.EquipWeapon(Weapon("twin_shade"));yield return null;
            var twin=player.GetComponentInChildren<WeaponBase>();Assert.IsTrue(twin.ExecuteAttack(player.transform,player.transform.forward,1<<7,player.gameObject));
            player.Combat.EquipWeapon(Weapon("prism_crossbow"));Assert.IsFalse(twin.gameObject.activeSelf,"Unequipped weapon should stop instantly.");
            yield return new WaitForSeconds(.6f);Assert.AreEqual(1,player.GetComponentsInChildren<WeaponBase>().Length);
        }

        [UnityTest]
        public IEnumerator StunCancelsNormalMeleeAndRangedWindupsWithoutGhostHits()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var pool=Object.FindAnyObjectByType<EnemySpawner>().RegularEnemyPrefabs;
            Time.timeScale=2f;
            foreach(bool ranged in new[]{false,true})
            {
                var prefab=pool.First(p=>ranged?p.GetComponent<RangedEnemy>()!=null:p.GetComponent<MeleeEnemy>()!=null);
                var enemy=arena.Spawn(prefab,Vector3.forward*(ranged?5f:1.65f));
                string warning=ranged?"EnemyWarning_Cultist":"EnemyWarning_Melee";
                yield return WaitFor(()=>Object.FindObjectsByType<ArcaneTelegraph>().Any(w=>w.name==warning&&w.IsVisible),"Attack did not telegraph: "+warning,8f);
                float hp=player.CurrentHealth;Assert.IsTrue(enemy.TryStun(1.2f,out _));
                yield return new WaitForSeconds(.58f);
                Assert.AreEqual(hp,player.CurrentHealth,.01f,"Cancelled windup damaged player.");
                Assert.IsFalse(Object.FindObjectsByType<ArcaneTelegraph>().Any(w=>w.name==warning&&w.IsVisible));
                Object.Destroy(enemy.gameObject);yield return null;
            }
        }
    }
}

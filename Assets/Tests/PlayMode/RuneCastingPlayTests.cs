using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Items;
using LightSouls.Combat.Runes;
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
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class RuneCastingPlayTests
    {
        private static PlayerController Player()=>Object.FindAnyObjectByType<PlayerController>();
        private static RuneCastingHud Hud()=>Object.FindAnyObjectByType<RuneCastingHud>();
        private static ItemDatabaseSO Database()=>Resources.FindObjectsOfTypeAll<ItemDatabaseSO>().First(db=>db.AllItems.OfType<WeaponDataSO>().Count()==45);
        private static WeaponDataSO Weapon(string id)=>(WeaponDataSO)Database().GetItem("weapon."+id);
        private static IEnumerator WaitFor(Func<bool> predicate,string message,float seconds=30f)
        {float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(predicate(),message);yield return null;}
        private static IEnumerator LoadGame()
        {
            Time.timeScale=1f;RunState.Reset();RunState.SelectedDungeonId="dungeon.standard";
            yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(()=>Player()!=null&&Hud()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon/rune HUD not ready");
        }
        private static IEnumerator Capture(string name)
        {
            // For optional screenshots, wait for the unrelated scene-arrival
            // toast to leave so it does not obscure the new skill hints.
            if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIGHTSOULS_RUNE_CAPTURE_DIR")))
            {
                var arrival=Object.FindAnyObjectByType<DungeonArrivalBanner>();
                if(arrival!=null&&arrival.IsShowing)yield return WaitFor(()=>!arrival.IsShowing,"Arrival overlay did not finish",DungeonArrivalBanner.Duration+1f);
            }
            yield return DungeonArrivalShopPlayTests.Capture(name,"LIGHTSOULS_RUNE_CAPTURE_DIR");
        }
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1f;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);}

        [UnityTest]
        public IEnumerator TouchDrawingRunsActualOnnxOnlyOnReleaseAndBlocksInvalidOrUnavailableRunes()
        {
            yield return LoadGame();var player=Player();var combat=player.Combat;var hud=Hud();
            combat.EquipWeapon(Weapon("iron_sword"));player.SetInputActive(true);yield return null;
            Assert.IsTrue(combat.RuneInputMode);Assert.IsTrue(player.Movement.RuneDashMode);Assert.AreEqual(5,hud.VisibleSkillCount);Assert.IsTrue(hud.Panel.gameObject.activeInHierarchy);
            Assert.AreEqual(420,((RectTransform)hud.Pad.transform).rect.width,.01f);Assert.AreEqual(420,((RectTransform)hud.Pad.transform).rect.height,.01f);
            var actionPanel=hud.transform.parent.Find("ActionPanel");var attack=actionPanel.Find("Button_Attack");Assert.IsFalse(attack.gameObject.activeSelf);
            Assert.IsFalse(actionPanel.Find("Button_Skill_Dash").gameObject.activeSelf,"Dodge must be drawn, not clicked");
            int casts=0;RuneSkill last=null;combat.OnRuneCast+=s=>{casts++;last=s;};
            Canvas.ForceUpdateCanvases();yield return Capture("01-rune-hud.png");

            // The same pointer callbacks as the Input System's UI module, including a second touch.
            var rect=(RectTransform)hud.Pad.transform;var path=RuneGlyphs.Example(RuneGlyph.Slash);
            Vector2 Screen(Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMin+p.x*rect.rect.width,rect.rect.yMax-p.y*rect.rect.height,0)));
            var touch=new PointerEventData(EventSystem.current){pointerId=17,button=PointerEventData.InputButton.Left,position=Screen(path[0])};
            hud.Pad.OnInitializePotentialDrag(touch);Assert.IsFalse(touch.useDragThreshold);hud.Pad.OnPointerDown(touch);Assert.IsTrue(hud.Pad.IsDrawing);
            for(int i=1;i<path.Length;i++){touch.position=Screen(path[i]);hud.Pad.OnDrag(touch);}
            hud.Pad.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=18,button=PointerEventData.InputButton.Left,position=touch.position});
            Assert.IsTrue(hud.Pad.IsDrawing);Assert.AreEqual(0,casts,"Drawing alone must not attack");
            yield return null;
            var ink=hud.Pad.GetComponentInChildren<RuneInkGraphic>();Assert.NotNull(ink);Assert.IsTrue(ink.HasInk);
            Assert.IsTrue(((Texture2D)ink.texture).GetPixels32().Any(p=>p.a>150),"A drawn stroke must produce visible ink pixels");
            yield return Capture("02-rune-drawing.png");
            touch.position=Screen(path[^1]);hud.Pad.OnPointerUp(touch);
            yield return WaitFor(()=>!hud.IsProcessing,"Inference never completed");
            Assert.IsTrue(hud.LastCastSucceeded,hud.LastMatch.Status+" "+hud.LastMatch.Score);Assert.AreEqual(1,casts);Assert.AreEqual(RuneGlyph.Slash,last.Glyph);
            Assert.Greater(combat.RuneCooldownRemaining(last),0);Assert.IsFalse(combat.TryCastRune(RuneGlyph.Slash,out _));
            yield return Capture("03-rune-cast.png");

            Assert.IsFalse(hud.SubmitStroke(new[]{Vector2.one*.5f}));Assert.AreEqual(RuneRecognitionStatus.TooShort,hud.LastMatch.Status);Assert.AreEqual(1,casts);
            combat.EquipWeapon(Weapon("pistol"));yield return null;Assert.AreEqual(5,hud.VisibleSkillCount);
            Assert.IsTrue(hud.SubmitStroke(path));yield return WaitFor(()=>!hud.IsProcessing,"Unsupported drawing inference timeout");
            Assert.AreEqual(RuneRecognitionStatus.Unsupported,hud.LastMatch.Status);Assert.AreEqual(1,casts);
            Assert.IsFalse(hud.LastCastSucceeded);

            // Do not execute a recognized skill from the previous weapon after a swap mid-stroke.
            touch.position=Screen(RuneGlyphs.Example(RuneGlyph.Thrust)[0]);hud.Pad.OnPointerDown(touch);Assert.IsTrue(hud.Pad.IsDrawing);
            combat.EquipWeapon(Weapon("arsenal_frost_scepter"));yield return null;Assert.IsFalse(hud.Pad.IsDrawing);
            hud.Pad.OnPointerUp(touch);Assert.AreEqual(1,casts);

            var inv=player.Inventory;int potion=Enumerable.Range(0,inv.HotbarSlots.Count).Where(i=>inv.HotbarSlots[i].Item is ConsumableDataSO).DefaultIfEmpty(-1).First();
            Assert.GreaterOrEqual(potion,0,"Default hotbar should include consumables");inv.SelectHotbarSlot(potion);yield return null;
            Assert.IsTrue(hud.Panel.gameObject.activeSelf);Assert.AreEqual(1,hud.VisibleSkillCount,"Only dodge remains while holding a potion");
            Assert.IsTrue(attack.gameObject.activeSelf,"Consumable still needs a use button");
            inv.SelectHotbarSlot(0);yield return null;Assert.AreEqual(5,hud.VisibleSkillCount);Assert.IsFalse(attack.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator DrawnDodgeMovesThePlayerDuringAttackRecoveryAndKeepsCooldownAcrossSwaps()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var combat=player.Combat;var hud=Hud();
            combat.EquipWeapon(Weapon("iron_sword"));yield return null;
            var dodge=RuneSkillCatalog.DashSkill;int dashes=0;player.Movement.OnDashExecuted+=()=>dashes++;
            Assert.IsTrue(combat.TryCastRune(RuneGlyph.Slash,out var feedback),feedback);
            Assert.Greater(combat.AttackCooldownRemaining,0);Assert.AreEqual(0,combat.RuneCooldownRemaining(dodge),.01f);
            Vector3 before=player.transform.position;float released=Time.realtimeSinceStartup;
            Assert.IsTrue(hud.SubmitStroke(RuneGlyphs.Example(dodge.Glyph)));yield return WaitFor(()=>!hud.IsProcessing,"Dodge recognition timeout");
            Assert.IsTrue(hud.LastCastSucceeded,hud.LastMatch.Status+" "+hud.LastMatch.Score);Assert.AreEqual(1,dashes);
            Debug.Log($"RUNE_DODGE recognition={hud.LastMatch.Milliseconds:F1}ms end_to_end_with_frame={(Time.realtimeSinceStartup-released)*1000:F1}ms");
            yield return WaitFor(()=>!player.Movement.IsDashing,"Dodge did not finish");
            Assert.Greater(Vector3.Distance(before,player.transform.position),1f,"Dodge rune must move the actual knight");
            float cooldown=combat.RuneCooldownRemaining(dodge);Assert.Greater(cooldown,0);
            combat.EquipWeapon(Weapon("pistol"));Assert.AreEqual(cooldown,combat.RuneCooldownRemaining(dodge),.05f);
            Assert.IsFalse(combat.TryCastRune(dodge.Glyph,out _),"Swapping cannot reset dodge cooldown");
            int potion=Enumerable.Range(0,player.Inventory.HotbarSlots.Count).Where(i=>player.Inventory.HotbarSlots[i].Item is ConsumableDataSO).DefaultIfEmpty(-1).First();
            Assert.GreaterOrEqual(potion,0);player.Inventory.SelectHotbarSlot(potion);yield return null;Assert.AreEqual(1,hud.VisibleSkillCount);
            Time.timeScale=3f;yield return WaitFor(()=>combat.RuneCooldownRemaining(dodge)<=0,"Shared dodge cooldown did not expire");
            Assert.IsFalse(combat.TryCastRune(RuneGlyph.Slash,out _),"Cannot attack with a potion selected");
            before=player.transform.position;Assert.IsTrue(hud.SubmitStroke(RuneGlyphs.Example(dodge.Glyph)));yield return WaitFor(()=>!hud.IsProcessing,"Potion dodge recognition timeout");
            Assert.IsTrue(hud.LastCastSucceeded);Assert.AreEqual(2,dashes);yield return WaitFor(()=>!player.Movement.IsDashing,"Potion dodge did not finish");
            Assert.Greater(Vector3.Distance(before,player.transform.position),1f);
        }

        [UnityTest]
        public IEnumerator GpuComputeExecutesTheSameModelWithAsyncReadback()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||!SystemInfo.supportsComputeShaders)Assert.Ignore("GPU compute is unavailable on this runner");
            using var gpu=new RuneClassifier(RuneBackend.Gpu);using var cpu=new RuneClassifier(RuneBackend.Cpu);
            foreach(var glyph in new[]{RuneGlyph.Slash,RuneGlyph.Diagonal,RuneGlyph.Ring,RuneGlyph.Lightning,RuneGlyph.Wall})
            {
                var path=RuneGlyphs.Example(glyph);var expected=cpu.RecognizeSync(path,RuneReferenceBank.AllMask,.80f,.05f);
                bool complete=false;RuneMatch result=default;
                async void Request(){result=await gpu.RecognizeAsync(path,RuneReferenceBank.AllMask,.80f,.05f);complete=true;}
                Request();yield return WaitFor(()=>complete,"GPU inference readback timeout");
                Assert.AreEqual(RuneBackend.Gpu,gpu.Backend,"GPU fallback is not a GPU parity test");
                Assert.AreEqual(expected.Glyph,result.Glyph);Assert.AreEqual(expected.Status,result.Status);Assert.AreEqual(expected.Score,result.Score,.015f);
                Debug.Log($"RUNE_GPU {glyph} score={result.Score:F4} elapsed_ms={result.Milliseconds:F1} CPU={expected.Milliseconds:F1}");
            }
        }

        [UnityTest]
        public IEnumerator ShopAndBackpackAllowReadingEverySkillWithoutClipping()
        {
            Time.timeScale=1;SceneLoader.StartNewRun();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Lobby&&Player()!=null&&LobbyController.Instance!=null,"Lobby not ready");
            var player=Player();var weapon=Weapon("arsenal_dream_scepter");var shop=LobbyController.Instance.ShopUI;
            shop.Open(player.gameObject);shop.SelectItem(weapon);yield return null;Canvas.ForceUpdateCanvases();
            var shopScroll=shop.GetComponentsInChildren<ScrollRect>(true).Single(s=>s.name=="DescriptionScroll");
            var detail=shopScroll.content.GetComponent<Text>();
            foreach(var skill in RuneSkillCatalog.For(weapon))StringAssert.Contains(skill.Name,detail.text);
            Assert.GreaterOrEqual(shopScroll.content.rect.height,detail.preferredHeight);
            Assert.IsTrue(shopScroll.vertical);Assert.NotNull(shopScroll.viewport.GetComponent<RectMask2D>());
            yield return Capture("04-rune-shop.png");shop.Close();

            var backpack=Object.FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);Assert.NotNull(backpack);
            backpack.Open();yield return null;backpack.OnSlotClicked("hotbar/0");yield return null;Canvas.ForceUpdateCanvases();
            var bagScroll=backpack.GetComponentsInChildren<ScrollRect>(true).Single(s=>s.name=="ItemDetailScroll");
            var bagText=bagScroll.content.GetComponent<Text>();var starter=player.Inventory.HotbarSlots[0].Item as WeaponDataSO;
            Assert.NotNull(starter);foreach(var skill in RuneSkillCatalog.For(starter))StringAssert.Contains(skill.Name,bagText.text);
            Assert.GreaterOrEqual(bagScroll.content.rect.height,bagText.preferredHeight);Assert.IsTrue(bagScroll.vertical);
            yield return Capture("05-rune-backpack.png");backpack.Close();
        }

        private sealed class Arena:IDisposable
        {
            public readonly Vector3 Center=new Vector3(1000,0,1000);
            public readonly GameObject Root;private readonly NavMeshData data;private NavMeshDataInstance link;
            public Arena()
            {
                Root=new GameObject("RuneTestArena");Root.transform.position=Center;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=8;floor.transform.SetParent(Root.transform,false);floor.transform.localPosition=Vector3.down*.10f;floor.transform.localScale=new Vector3(32,.20f,32);
                Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(Root.transform,1<<8,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
                data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Center+Vector3.up,new Vector3(34,6,34)),Vector3.zero,Quaternion.identity);
                Assert.NotNull(data);link=NavMesh.AddNavMeshData(data);ResetPlayer();
            }
            public void ResetPlayer(){var player=Player();player.Teleport(Center+Vector3.up*.05f);player.transform.rotation=Quaternion.identity;player.SetInputActive(true);player.InLockedCombat=true;Physics.SyncTransforms();}
            public EnemyBase Spawn(GameObject prefab,Vector3 offset)
            {Assert.IsTrue(NavMesh.SamplePosition(Center+offset,out var p,1.2f,NavMesh.AllAreas));return Object.Instantiate(prefab,p.position,Quaternion.identity,Root.transform).GetComponent<EnemyBase>();}
            public void Dispose(){if(link.valid)link.Remove();if(data!=null)Object.Destroy(data);if(Root!=null)Object.Destroy(Root);}
        }

        [UnityTest,Timeout(240000)]
        public IEnumerator EveryAuthoredSkillDispatchesAndReturnsWeaponToRestPose()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var combat=player.Combat;
            var weapons=Database().AllItems.OfType<WeaponDataSO>().ToArray();int casts=0;combat.OnRuneCast+=_=>casts++;
            Time.timeScale=5f;
            foreach(var weapon in weapons)
            foreach(var skill in RuneSkillCatalog.For(weapon))
            {
                RunState.Reset();arena.ResetPlayer();combat.EquipWeapon((WeaponDataSO)null);combat.EquipWeapon(weapon);yield return null;
                var instance=player.GetComponentInChildren<WeaponBase>();Assert.NotNull(instance,weapon.ItemId);
                Quaternion rest=instance.transform.localRotation;Vector3 position=instance.transform.localPosition;
                Assert.IsTrue(combat.TryCastRune(skill.Glyph,out var feedback),weapon.ItemId+" / "+skill.Name+" / "+feedback);
                Assert.Greater(combat.RuneCooldownRemaining(skill),0,skill.Name);
                Assert.IsFalse(combat.TryCastRune(skill.Glyph,out _),"Immediate reuse: "+skill.Name);
                float duration=weapon.Type==WeaponType.Melee&&skill.Form==RuneForm.Weapon?Mathf.Max(.85f,(weapon.StrikeCount+skill.ExtraHits-1)*weapon.StrikeInterval+.65f):.8f;
                yield return new WaitForSeconds(duration);
                Assert.Less(Quaternion.Angle(rest,instance.transform.localRotation),.65f,skill.Name+" stuck in attack pose");
                Assert.Less(Vector3.Distance(position,instance.transform.localPosition),.035f,skill.Name+" stuck in recoil");
                if(TemporaryRuneWall.Active!=null){Object.Destroy(TemporaryRuneWall.Active.gameObject);yield return null;}
            }
            Assert.AreEqual(225,casts);
            Debug.Log("RUNE_DISPATCH verified 225/225 casts (180 weapon skills + 45 shared dodge slots) and pose recovery.");
        }

        [UnityTest]
        public IEnumerator RuneSkillsApplyRealDamageCrowdControlAndTemporaryTerrain()
        {
            yield return LoadGame();using var arena=new Arena();var player=Player();var combat=player.Combat;
            var prefab=Object.FindAnyObjectByType<EnemySpawner>().RegularEnemyPrefabs.First(p=>p.GetComponent<EnemyBase>() is MeleeEnemy);
            var target=arena.Spawn(prefab,Vector3.forward*2.3f);yield return null;
            target.enabled=false;target.GetComponent<NavMeshAgent>().isStopped=true;
            void Prepare(string weapon){RunState.Reset();arena.ResetPlayer();combat.EquipWeapon(Weapon(weapon));Physics.SyncTransforms();}
            Prepare("arsenal_bastion_hammer");float hp=target.CurrentHealth;Vector3 before=target.transform.position;
            Assert.IsTrue(combat.TryCastRune(RuneGlyph.Ring,out var feedback),feedback);Assert.Less(target.CurrentHealth,hp);Assert.Greater(Vector3.Distance(before,target.transform.position),1f);
            var frozen=arena.Spawn(prefab,Vector3.right*3f);yield return null;frozen.enabled=false;frozen.GetComponent<NavMeshAgent>().isStopped=true;
            Prepare("arsenal_frost_scepter");if(target!=null)Object.Destroy(target.gameObject);yield return null;hp=frozen.CurrentHealth;
            Assert.IsTrue(combat.TryCastRune(RuneGlyph.Ring,out feedback),feedback);Assert.Less(frozen.CurrentHealth,hp);Assert.IsTrue(frozen.IsStunned);Assert.IsTrue(frozen.GetComponent<StatusEffectReceiver>().IsSlowed);
            Object.Destroy(frozen.gameObject);yield return null;

            var charmed=arena.Spawn(prefab,Vector3.forward*4f);var remainingHostile=arena.Spawn(prefab,new Vector3(7,0,4));yield return null;
            charmed.enabled=false;charmed.GetComponent<NavMeshAgent>().isStopped=true;remainingHostile.enabled=false;remainingHostile.GetComponent<NavMeshAgent>().isStopped=true;
            charmed.TakeDamage(new DamageInfo(charmed.MaxHealth*.59f,player.gameObject,charmed.transform.position));
            Prepare("arsenal_dream_scepter");Assert.IsTrue(combat.TryCastRune(RuneGlyph.Vortex,out feedback),feedback);
            Assert.IsTrue(charmed.IsCharmed&&!charmed.IsDead,"Mộng Lệnh must convert a weakened living enemy");Object.Destroy(charmed.gameObject);Object.Destroy(remainingHostile.gameObject);yield return null;

            Prepare("arsenal_guardian_maul");Assert.IsTrue(combat.TryCastRune(RuneGlyph.Wall,out feedback),feedback);
            Assert.NotNull(TemporaryRuneWall.Active);Assert.IsTrue(TemporaryRuneWall.Active.GetComponent<BoxCollider>().enabled);Assert.IsTrue(TemporaryRuneWall.Active.GetComponent<NavMeshObstacle>().carving);
            combat.EquipWeapon(Weapon("arsenal_ice_totem"));Assert.IsFalse(combat.TryCastRune(RuneGlyph.Wall,out _),"Swap must not bypass a shared wall cooldown");
            Object.Destroy(TemporaryRuneWall.Active.gameObject);yield return null;
            Prepare("arsenal_mirage_sabre");before=player.transform.position;Assert.IsTrue(combat.TryCastRune(RuneGlyph.Vortex,out feedback),feedback);Assert.Greater(Vector3.Distance(before,player.transform.position),2f);
            Prepare("arsenal_solar_wand");Assert.IsTrue(combat.TryCastRune(RuneGlyph.Ward,out feedback),feedback);Assert.LessOrEqual(player.Stats.Get(StatType.DamageTaken),.73f);
        }
    }
}

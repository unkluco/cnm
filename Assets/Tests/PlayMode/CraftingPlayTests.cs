using System;
using System.Collections;
using System.IO;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.Lobby;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class CraftingPlayTests
    {
        private static PlayerController Player()=>Object.FindAnyObjectByType<PlayerController>();
        private static IEnumerator WaitFor(Func<bool> ready,string message)
        {
            float limit=Time.realtimeSinceStartup+30f;while(!ready()&&Time.realtimeSinceStartup<limit)yield return null;
            Assert.IsTrue(ready(),message);yield return null;
        }
        private static IEnumerator StartRun()
        {
            SceneLoader.StartNewRun();yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Lobby&&Player()!=null,"Lobby not ready.");
        }
        private static string[] Snapshot()=>RunState.InventorySlots.Select(s=>$"{s.SlotKey}:{s.ItemId}:{s.Count}").OrderBy(s=>s).ToArray();
        private static Button ButtonNamed(Transform root,string name)=>root.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);

        [UnityTearDown]
        public IEnumerator Cleanup(){Time.timeScale=1;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);}

        [UnityTest]
        public IEnumerator ForgeInteractionCraftsUpgradesAndPersistsMaterialsBetweenScenes()
        {
            yield return StartRun();var player=Player();var bag=player.Inventory;var catalog=CraftingCatalogSO.Load();
            var station=Object.FindAnyObjectByType<CraftingStation>();Assert.NotNull(station);
            var wood=catalog.FindMaterial("material.wood");var iron=catalog.FindMaterial("material.iron");var fireStone=catalog.FindMaterial("material.fire_stone");
            var sword=catalog.FindRecipe("forge.iron_sword");var fire=catalog.FindRecipe("upgrade.fire_sword");
            Assert.AreEqual(2,bag.GetCount(wood));Assert.AreEqual(3,bag.GetCount(iron));Assert.AreEqual(catalog.Materials.Count,bag.MaterialSlots.Count);
            Assert.IsFalse(bag.HotbarSlots.Concat(bag.BackpackSlots).Any(s=>!s.IsEmpty&&s.Item.Category==ItemCategory.Material));

            Vector3 approach=station.transform.position+Vector3.back*2.05f;approach.y=player.transform.position.y;player.Teleport(approach);
            yield return WaitFor(()=>ReferenceEquals(player.Interaction.CurrentInteractable,station),"Forge must be reachable via the actual interaction system.");
            yield return new WaitForSecondsRealtime(.5f);yield return Capture("01-forge-lobby.png");
            player.Interaction.PerformInteract();yield return null;var panel=Object.FindAnyObjectByType<CraftingPanelUI>();Assert.NotNull(panel);
            Assert.IsTrue(panel.IsOpen);Assert.IsTrue(BackButtonStack.HasOpenPanel);Assert.AreEqual(sword,panel.SelectedRecipe);
            Assert.IsTrue(ButtonNamed(panel.transform,"Craft").interactable);yield return Capture("02-forge-recipes.png");

            int swordCount=bag.GetCount(sword.Output),events=0;void Changed()=>events++;bag.Changed+=Changed;
            ButtonNamed(panel.transform,"Craft").onClick.Invoke();
            Assert.AreEqual(1,events);Assert.AreEqual(swordCount+1,bag.GetCount(sword.Output));
            Assert.AreEqual(0,bag.GetCount(wood));Assert.AreEqual(0,bag.GetCount(iron));Assert.IsFalse(ButtonNamed(panel.transform,"Craft").interactable);
            var snapshot=Snapshot();Assert.AreEqual(CraftResult.MissingIngredients,panel.CraftSelected());Assert.AreEqual(1,events);CollectionAssert.AreEqual(snapshot,Snapshot());

            Assert.IsTrue(bag.TryAdd(fireStone));Assert.IsTrue(panel.SelectRecipe(fire.Id));
            Assert.IsTrue(ButtonNamed(panel.transform,"Craft").interactable);yield return Capture("03-upgrade-fire-sword.png");
            events=0;ButtonNamed(panel.transform,"Craft").onClick.Invoke();
            Assert.AreEqual(1,events);Assert.AreEqual(swordCount,bag.GetCount(sword.Output));Assert.AreEqual(1,bag.GetCount(fire.Output));Assert.AreEqual(0,bag.GetCount(fireStone));
            Assert.AreSame(fire.Output,player.CurrentWeapon,"Upgrading a selected weapon must replace it in the same hotbar slot.");
            yield return new WaitForSecondsRealtime(.12f);yield return Capture("06-craft-success.png");
            bag.Changed-=Changed;

            // A fresh pouch contains one independent stack per material; no generic inventory cells are used.
            for(int i=0;i<catalog.Materials.Count;i++)Assert.IsTrue(bag.TryAdd(catalog.Materials[i],1+i%5));
            ButtonNamed(panel.transform,"Tab_Materials").onClick.Invoke();yield return Capture("04-material-guide.png");
            Assert.IsTrue(BackButtonStack.TryCloseTop());Assert.IsFalse(panel.IsOpen);Assert.IsFalse(BackButtonStack.HasOpenPanel);
            yield return null;
            var backpack=Object.FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);Assert.NotNull(backpack);
            backpack.Open();backpack.ShowMaterialPouch();Assert.IsTrue(backpack.IsMaterialPouchVisible);
            backpack.OnSlotClicked(PlayerInventory.MaterialSlotKey(fireStone));yield return null;yield return Capture("05-material-pouch.png");
            backpack.Close();Assert.IsFalse(BackButtonStack.HasOpenPanel);

            var shop=Object.FindAnyObjectByType<LobbyShopUI>(FindObjectsInactive.Include);RunState.AddCoins(30);
            int coins=RunState.Coins,ownedWood=bag.GetCount(wood);shop.Open(player.gameObject);shop.TryBuyItem(wood);shop.Close();
            Assert.AreEqual(ownedWood+1,bag.GetCount(wood));Assert.AreEqual(coins-wood.Price,RunState.Coins);

            snapshot=Snapshot();SceneLoader.LoadGame();yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Game&&Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon not ready.");
            CollectionAssert.AreEqual(snapshot,Snapshot(),"Crafted items and material pouch must survive loading a dungeon.");
            SceneLoader.LoadLobby();yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Lobby&&Player()!=null,"Lobby did not reload.");
            CollectionAssert.AreEqual(snapshot,Snapshot(),"Returning must not duplicate starting materials or lose upgrades.");
            Assert.AreEqual(1,Player().Inventory.GetCount(fire.Output));
        }

        [UnityTest]
        public IEnumerator FailedAndFullBagCraftsAreAtomicAndWornGearIsProtected()
        {
            yield return StartRun();var bag=Player().Inventory;var catalog=CraftingCatalogSO.Load();
            var sword=catalog.FindRecipe("forge.iron_sword");var fire=catalog.FindRecipe("upgrade.fire_sword");
            var dagger=catalog.FindRecipe("forge.dagger").Output;var wood=catalog.FindMaterial("material.wood");var steel=catalog.FindMaterial("material.steel");
            var fireStone=catalog.FindMaterial("material.fire_stone");var coal=catalog.FindMaterial("material.coal");
            var general=bag.HotbarSlots.Concat(bag.BackpackSlots).ToArray();Assert.IsTrue(bag.TryAdd(dagger,general.Count(s=>s.IsEmpty)));
            Assert.IsTrue(general.All(s=>!s.IsEmpty));Assert.IsTrue(bag.HasSpaceFor(fireStone));Assert.IsTrue(bag.TryAdd(fireStone));
            var snapshot=Snapshot();Assert.AreEqual(CraftResult.NoSpace,bag.EvaluateCraft(sword));Assert.AreEqual(CraftResult.NoSpace,bag.TryCraft(sword));CollectionAssert.AreEqual(snapshot,Snapshot());
            var duplicate=new CraftRecipe{Id="test.duplicate",Output=dagger,Ingredients=new(){new CraftIngredient(wood,2),new CraftIngredient(wood,1)}};
            Assert.AreEqual(CraftResult.MissingIngredients,bag.TryCraft(duplicate));CollectionAssert.AreEqual(snapshot,Snapshot());

            Assert.AreEqual(CraftResult.Success,bag.TryCraft(fire));Assert.AreSame(fire.Output,bag.HotbarSlots[0].Item);Assert.IsTrue(general.All(s=>!s.IsEmpty));
            Assert.AreEqual(0,bag.GetCount(sword.Output));Assert.AreEqual(0,bag.GetCount(fireStone));
            Assert.IsFalse(bag.SwapOrMoveSlots(PlayerInventory.MaterialSlotKey(wood),"hotbar/0"));

            var potion=catalog.FindRecipe("alchemy.health_potion");var herb=catalog.FindMaterial("material.herb");var glass=catalog.FindMaterial("material.glass");
            Assert.IsTrue(bag.TryAdd(herb,2));Assert.IsTrue(bag.TryAdd(glass));int potions=bag.GetCount(potion.Output);
            snapshot=Snapshot();Assert.AreEqual(CraftResult.MissingIngredients,bag.TryCraft(potion,3));CollectionAssert.AreEqual(snapshot,Snapshot());
            Assert.AreEqual(CraftResult.Success,bag.TryCraft(potion,2));Assert.AreEqual(potions+2,bag.GetCount(potion.Output));Assert.AreEqual(0,bag.GetCount(herb));Assert.AreEqual(0,bag.GetCount(glass));

            var refine=catalog.FindRecipe("refine.steel");Assert.IsTrue(bag.TryAdd(steel,998));Assert.IsTrue(bag.TryAdd(coal));
            snapshot=Snapshot();Assert.AreEqual(CraftResult.NoSpace,bag.TryCraft(refine));CollectionAssert.AreEqual(snapshot,Snapshot());
            Assert.IsTrue(bag.TryRemove(steel,1));Assert.AreEqual(CraftResult.Success,bag.TryCraft(refine));Assert.AreEqual(999,bag.GetCount(steel));Assert.IsFalse(bag.TryAdd(steel));

            var leather=catalog.FindRecipe("forge.leather_vest").Output;var chainmail=catalog.FindRecipe("upgrade.chainmail");
            var slot=bag.BackpackSlots[0];Assert.IsTrue(bag.TryRemoveFromSlot(slot.SlotKey,slot.Count));Assert.IsTrue(bag.TryAdd(leather));
            Assert.AreSame(leather,slot.Item);Assert.IsTrue(bag.SwapOrMoveSlots(slot.SlotKey,"equip/armor"));Assert.IsTrue(bag.TryAdd(catalog.FindMaterial("material.copper")));
            Assert.AreEqual(1,bag.GetCount(leather));Assert.AreEqual(0,bag.GetCraftableCount(leather));snapshot=Snapshot();
            Assert.AreEqual(CraftResult.MissingIngredients,bag.TryCraft(chainmail));CollectionAssert.AreEqual(snapshot,Snapshot());Assert.AreSame(leather,bag.EquipmentSlots["equip/armor"].Item);
            Assert.IsTrue(bag.SwapOrMoveSlots("equip/armor",slot.SlotKey));Assert.AreEqual(CraftResult.Success,bag.TryCraft(chainmail));Assert.AreSame(chainmail.Output,slot.Item);
        }

        [UnityTest]
        public IEnumerator DungeonRoomsAndBossDropCollectibleMaterialsAndChestCanStoreThem()
        {
            yield return StartRun();RunState.SelectedDungeonId="dungeon.standard";SceneLoader.LoadGame();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Game&&Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon not ready.");
            var player=Player();var generator=Object.FindAnyObjectByType<DungeonGenerator>();var bag=player.Inventory;var catalog=CraftingCatalogSO.Load();
            var normal=generator.SpawnedRooms.First(r=>r.RoomType==RoomType.Normal||r.RoomType==RoomType.Combat);
            var bounds=normal.GetCurrentWorldBounds();var center=bounds.center;center.y=normal.transform.position.y;
            Assert.IsTrue(NavMesh.SamplePosition(center,out var nav,4f,NavMesh.AllAreas));player.SetInputActive(false);
            Assert.IsTrue(normal.LifecycleController.EnterCombat());player.Teleport(nav.position);yield return null;
            KillEnemies(player);yield return null;Assert.AreEqual(RoomLifecycleState.Cleared,normal.LifecycleController.State);
            var drops=Object.FindObjectsByType<ItemPickup>().Where(p=>p.Item is CraftMaterialSO).ToArray();Assert.IsNotEmpty(drops);
            Assert.IsFalse(drops.Any(p=>p.Item.ItemId=="material.boss_soul"));
            var first=drops[0];var mat=first.Item;int previous=bag.GetCount(mat),reward=first.Count;
            Assert.IsTrue(NavMesh.SamplePosition(first.transform.position,out _,1.5f,NavMesh.AllAreas),"Room rewards should be near walkable ground.");
            first.Interact(player.gameObject);yield return null;Assert.IsTrue(first==null);Assert.AreEqual(previous+reward,bag.GetCount(mat));
            Assert.IsFalse(normal.LifecycleController.EnterCombat(),"A cleared room cannot farm its guaranteed reward twice.");

            var bossRoom=generator.SpawnedRooms.Single(r=>r.RoomType==RoomType.Exit);var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            Assert.IsTrue(bossRoom.LifecycleController.EnterCombat());var boss=spawner.ActiveBoss;Assert.NotNull(boss);
            var expected=boss.Kind switch{BossKind.Varkhan=>"fire_stone",BossKind.Nhalis=>"frost_crystal",BossKind.Orgrim=>"steel",BossKind.Noctyr=>"void_shard",_=>"storm_shard"};
            Vector3 safe=generator.LevelExitPoint.position;safe.y=bossRoom.transform.position.y;player.Teleport(safe);yield return null;
            boss.TakeDamage(new DamageInfo(boss.MaxHealth*5f,player.gameObject,boss.transform.position));yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GameBootstrap>().IsExitPortalOpen);Assert.IsTrue(bossRoom.BossRewardController.AreRewardsSpawned);
            var bossDrops=Object.FindObjectsByType<ItemPickup>().Where(p=>p.Item is CraftMaterialSO).ToArray();
            var soulDrop=bossDrops.Single(p=>p.Item.ItemId=="material.boss_soul");Assert.AreEqual(1,soulDrop.Count);
            var portalOffset=soulDrop.transform.position-generator.LevelExitPoint.position;portalOffset.y=0;Assert.GreaterOrEqual(portalOffset.magnitude,2.6f,"Boss materials must not require entering the mystery portal.");
            Assert.IsTrue(bossDrops.Any(p=>p.Item.ItemId=="material."+expected));
            var soul=catalog.FindMaterial("material.boss_soul");soulDrop.Interact(player.gameObject);yield return null;Assert.AreEqual(1,bag.GetCount(soul));

            var chest=Object.FindObjectsByType<TreasureChest>().First();chest.Container.Clear();var chestUI=Object.FindAnyObjectByType<TreasureChestUI>(FindObjectsInactive.Include);Assert.NotNull(chestUI);
            chestUI.Open(chest,player.gameObject);yield return null;Assert.IsTrue(chestUI.IsOpen);ButtonNamed(chestUI.transform,"ToggleMaterialPouch").onClick.Invoke();
            int materialIndex=Array.FindIndex(bag.MaterialSlots.Values.Where(s=>!s.IsEmpty).OrderBy(s=>s.Item.DisplayName).ToArray(),s=>s.Item==soul);
            Assert.That(materialIndex,Is.InRange(0,19));string cell=materialIndex<5?$"Inv_hotbar_{materialIndex}":$"Inv_backpack_{materialIndex-5}";
            ButtonNamed(chestUI.transform,cell).onClick.Invoke();Assert.AreEqual(0,bag.GetCount(soul));Assert.AreEqual(1,chest.Slots.Single(s=>!s.IsEmpty&&s.Item==soul).Count);
            int index=Array.FindIndex(chest.Slots.ToArray(),s=>!s.IsEmpty&&s.Item==soul);ButtonNamed(chestUI.transform,"Chest_"+index).onClick.Invoke();
            Assert.AreEqual(1,bag.GetCount(soul));Assert.IsTrue(chest.IsEmpty);chestUI.Close();
        }

        private static void KillEnemies(PlayerController player)
        {
            foreach(var enemy in Object.FindObjectsByType<EnemyBase>())enemy.TakeDamage(new DamageInfo(100000,player.gameObject,enemy.transform.position));
        }

        private static IEnumerator Capture(string filename)
        {
            string folder=Environment.GetEnvironmentVariable("LIGHTSOULS_CRAFT_CAPTURE_DIR");if(string.IsNullOrWhiteSpace(folder)||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)yield break;
            Directory.CreateDirectory(folder);var cam=Camera.main;var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);rt.Create();
            var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;float scale=Time.timeScale;
            var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            Time.timeScale=0;cam.targetTexture=rt;
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=cam.nearClipPlane+.8f;}
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=rt};if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
                RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,filename),image.EncodeToPNG());Object.Destroy(image);
                Debug.Log("CRAFT_CAPTURE "+Path.Combine(folder,filename));
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
                Time.timeScale=scale;cam.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();Object.Destroy(rt);
            }
        }
    }
}

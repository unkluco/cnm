using System;
using System.Collections;
using System.IO;
using System.Linq;
using LightSouls.Art;
using LightSouls.Combat;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Weapons;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.Lobby;
using LightSouls.MapGeneration;
using LightSouls.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Stability.Tests
{
    public class DarkFantasyPlayTests
    {
        private static PlayerController Player()=>Object.FindAnyObjectByType<PlayerController>();
        private static IEnumerator WaitFor(Func<bool> predicate,string message)
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!predicate()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(predicate(),message);yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale=1f;RunState.Reset();SceneLoader.LoadMainMenu();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.MainMenu,"Main menu should remain playable.");
        }

        [UnityTest]
        public IEnumerator StyledScenesGenerateAndBudgetLightsWhilePreservingCombatAndBossPortal()
        {
            SceneLoader.LoadMainMenu();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.MainMenu,"Menu did not load.");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.NotNull(GameObject.Find("Art_Knight"));
            yield return Capture("01-main-menu.png");

            SceneLoader.StartNewRun();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Lobby&&Player()!=null,"Lobby did not load.");
            yield return new WaitForSecondsRealtime(.35f);
            AssertBudget();
            yield return Capture("02-lobby.png");
            var shop=Object.FindAnyObjectByType<LobbyShopUI>(FindObjectsInactive.Include);Assert.NotNull(shop);
            shop.Open(Player().gameObject);yield return null;Canvas.ForceUpdateCanvases();yield return null;
            AssertShopGridFits(shop);
            if(WantsCapture()){shop.SelectItem(Player().CurrentWeapon);yield return Capture("06-shop.png");}
            shop.Close();
            if(WantsCapture())
            {
                var backpack=Object.FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);
                if(backpack!=null){backpack.Open();yield return null;backpack.OnSlotClicked(Player().Inventory.HotbarSlots[0].SlotKey);yield return Capture("07-backpack.png");backpack.Close();}
            }

            SceneLoader.LoadGame();
            yield return WaitFor(()=>SceneManager.GetActiveScene().name==SceneNames.Game&&Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon did not load.");
            yield return new WaitForSecondsRealtime(.35f);
            var generator=Object.FindAnyObjectByType<DungeonGenerator>();
            Assert.IsTrue(generator.IsNavMeshPathReachable,"Art must not block procedural routing.");
            Assert.IsTrue(generator.SpawnedRooms.All(room=>room.transform.Find("Art_Flagstones")!=null));
            Assert.NotNull(Player().GetComponentInChildren<WeaponBase>(),"Starting weapon should instantiate from the authored prefab.");
            AssertBudget();
            yield return Capture("03-dungeon.png");

            var boss=generator.SpawnedRooms.First(room=>room.RoomType==RoomType.Exit);
            Assert.IsFalse(generator.LevelExitPoint.gameObject.activeSelf);
            Assert.IsTrue(boss.LifecycleController.EnterCombat());
            Vector3 center=boss.GetCurrentWorldBounds().center;center.y=0;
            Player().Teleport(center+new Vector3(-3,0,-2));
            yield return new WaitForSecondsRealtime(.6f);
            AssertBudget();
            yield return Capture("04-boss-combat.png");
            foreach(var enemy in Object.FindObjectsByType<EnemyBase>()) if(!enemy.IsDead) enemy.TakeDamage(new DamageInfo(100000f,Player().gameObject,enemy.transform.position));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.IsTrue(Object.FindAnyObjectByType<GameBootstrap>().IsExitPortalOpen);
            Assert.IsTrue(generator.LevelExitPoint.gameObject.activeInHierarchy);
            Assert.NotNull(generator.LevelExitPoint.Find("Art_Portal"));
            Assert.IsTrue(string.IsNullOrEmpty(RunState.PendingPortalArrivalMessage),"Portal art must not reveal the destination.");
            if(WantsCapture())yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("05-boss-mystery-portal.png");
            if(WantsCapture())
            {
                RunState.SelectedDungeonId="dungeon.lava";
                SceneLoader.LoadGame();
                yield return WaitFor(()=>Object.FindAnyObjectByType<DungeonGenerator>() is DungeonGenerator g&&g!=generator&&g.IsGenerated&&Player()!=null,"Lava dungeon did not load.");
                var lavaRoom=Object.FindAnyObjectByType<DungeonGenerator>().SpawnedRooms.FirstOrDefault(r=>r.GetComponent<ProceduralRoomApplicator>()?.Theme==TerrainTheme.Lava);
                if(lavaRoom!=null)
                {
                    Transform spawn=lavaRoom.EnemySpawnPoints.FirstOrDefault();Vector3 pos=spawn!=null?spawn.position:lavaRoom.GetCurrentWorldBounds().center;pos.y=0;
                    Player().Teleport(pos);yield return new WaitForSecondsRealtime(.55f);AssertBudget();yield return Capture("08-volcanic-dungeon.png");
                }
            }
        }

        private static void AssertBudget()
        {
            var budget=Object.FindAnyObjectByType<DarkFantasyLightBudget>();Assert.NotNull(budget);
            budget.Refresh(Player().transform.position);
            var active=DarkFantasyTorchLight.ActiveLights.Where(t=>t!=null&&t.Source!=null&&t.Source.enabled).ToArray();
            Assert.Greater(active.Length,0,"At least one nearby light should contribute.");
            Assert.LessOrEqual(active.Length,budget.MaximumActiveLights);
            Assert.IsTrue(active.All(t=>t.Source.shadows==LightShadows.None));
        }

        private static void AssertShopGridFits(LobbyShopUI shop)
        {
            var scroll=shop.GridScroll;Assert.NotNull(scroll);
            var grid=scroll.content.GetComponent<GridLayoutGroup>();Assert.NotNull(grid);
            float used=grid.padding.horizontal+grid.constraintCount*grid.cellSize.x+(grid.constraintCount-1)*grid.spacing.x;
            Assert.LessOrEqual(used,scroll.viewport.rect.width+1f,"Shop must fit all columns within its viewport.");
        }

        private static bool WantsCapture()=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIGHTSOULS_ART_CAPTURE_DIR"))&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;

        // Optional render captures for CLI visual QA. Does nothing during ordinary test runs.
        private static IEnumerator Capture(string filename)
        {
            string dir=Environment.GetEnvironmentVariable("LIGHTSOULS_ART_CAPTURE_DIR");
            if(string.IsNullOrWhiteSpace(dir)||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)yield break;
            Directory.CreateDirectory(dir);
            var cam=Camera.main;Assert.NotNull(cam);
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){name="ArtPreview"};target.Create();
            var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;
            var overlays=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras=overlays.Select(c=>c.worldCamera).ToArray();
            var oldDistances=overlays.Select(c=>c.planeDistance).ToArray();
            cam.targetTexture=target;
            foreach(Canvas canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=cam.nearClipPlane+.8f;}
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
                RenderTexture.active=target;
                var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(dir,filename),texture.EncodeToPNG());Object.Destroy(texture);
                Debug.Log("ART_CAPTURE "+Path.Combine(dir,filename));
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=oldCameras[i];overlays[i].planeDistance=oldDistances[i];}
                cam.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Object.Destroy(target);
            }
        }
    }
}

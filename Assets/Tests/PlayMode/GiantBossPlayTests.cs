using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.MapGeneration;
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
    public class GiantBossPlayTests
    {
        private static PlayerController Player()=>Object.FindAnyObjectByType<PlayerController>();
        private static IEnumerator WaitFor(Func<bool> ready,string message,float max=15f)
        {float until=Time.realtimeSinceStartup+max;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(ready(),message);}

        private static IEnumerator LoadDungeon()
        {
            Time.timeScale=1f;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(()=>Player()!=null&&Object.FindAnyObjectByType<DungeonGenerator>()?.IsGenerated==true,"Dungeon not ready.",35);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup(){Time.timeScale=1f;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);}

        [UnityTest]
        public IEnumerator RoomSpawnsExactlyOneGiantAndOnlyItsDeathUnlocksRewards()
        {
            yield return LoadDungeon();
            var generator=Object.FindAnyObjectByType<DungeonGenerator>();var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            var spawner=Object.FindAnyObjectByType<EnemySpawner>();var player=Player();player.SetInputActive(false);
            var room=generator.SpawnedRooms.Single(r=>r.RoomType==RoomType.Exit);
            Bounds bounds=room.GetCurrentWorldBounds();Vector3 center=bounds.center;center.y=room.transform.position.y;
            Assert.IsNull(spawner.ActiveBoss);Assert.IsTrue(room.LifecycleController.EnterCombat());
            Assert.AreEqual(1,spawner.AliveEnemyCount);var boss=spawner.ActiveBoss;Assert.NotNull(boss);
            Assert.IsFalse(bootstrap.IsExitPortalOpen);Assert.IsFalse(room.BossRewardController.AreRewardsSpawned);
            player.Teleport(center+Vector3.back*5f);yield return null;
            Assert.That(boss.GetComponent<CapsuleCollider>().height,Is.GreaterThan(4f));
            Assert.IsTrue(boss.GetComponent<NavMeshAgent>().isOnNavMesh,"The larger collider still needs to stand on the generated NavMesh.");
            var hud=Object.FindAnyObjectByType<BossEncounterHud>();Assert.NotNull(hud);Assert.AreSame(boss,hud.Boss);
            Assert.AreEqual(1f,hud.HealthRatio,.001f);Assert.IsFalse(hud.TryGetComponent<GraphicRaycaster>(out _),"HUD must not block touch controls.");
            // Verify the actual starting sword can hit the giant (not just direct test damage).
            player.SetInputActive(true);
            player.Teleport(boss.transform.position+Vector3.back*(boss.Definition.Radius+1.18f));
            player.transform.rotation=Quaternion.LookRotation(Vector3.forward);Physics.SyncTransforms();yield return null;
            float before=boss.CurrentHealth;player.Combat.PerformAttack();
            Assert.Less(boss.CurrentHealth,before,"The large hit collider must remain reachable by real melee attacks.");
            player.SetInputActive(false);player.Teleport(center+Vector3.back*5f);
            boss.TakeDamage(new DamageInfo(boss.CurrentHealth-boss.MaxHealth*.45f,player.gameObject,boss.transform.position));
            Assert.AreEqual(.45f,hud.HealthRatio,.01f);Assert.IsFalse(bootstrap.IsExitPortalOpen);
            yield return WaitFor(()=>boss.Phase==2,"Phase II didn't begin at half HP.");
            yield return WaitFor(()=>boss.IsCasting&&Object.FindObjectsByType<ArcaneTelegraph>().Any(m=>m.name=="BossWarning_"+boss.Kind&&m.IsVisible),"Boss did not warn before its attack.");
            boss.TakeDamage(new DamageInfo(boss.MaxHealth*5f,player.gameObject,boss.transform.position));
            yield return null;
            Assert.AreEqual(0,spawner.AliveEnemyCount);Assert.IsNull(spawner.ActiveBoss);
            Assert.AreEqual(RoomLifecycleState.Cleared,room.LifecycleController.State);
            Assert.IsTrue(bootstrap.IsExitPortalOpen);Assert.IsTrue(room.BossRewardController.AreRewardsSpawned);
            Assert.That(room.BossRewardController.SpawnedChestCount,Is.InRange(1,2));
            Assert.IsFalse(Object.FindObjectsByType<ArcaneTelegraph>().Any(m=>m.name.StartsWith("BossWarning_")&&m.IsVisible));
            Assert.IsNull(Object.FindAnyObjectByType<BossEncounterHud>());
        }

        [UnityTest]
        public IEnumerator EveryGiantExecutesAllFiveEnhancedAbilitiesWithWarningsAndCleanup()
        {
            yield return LoadDungeon();
            var player=Player();player.SetInputActive(false);var hp=player.GetComponent<HealthComponent>();
            var generator=Object.FindAnyObjectByType<DungeonGenerator>();var room=generator.SpawnedRooms.Single(r=>r.RoomType==RoomType.Exit);
            var spawner=Object.FindAnyObjectByType<EnemySpawner>();Assert.IsTrue(room.LifecycleController.EnterCombat());spawner.ClearEnemies();yield return null;
            var bounds=room.GetCurrentWorldBounds();Vector3 center=bounds.center;center.y=room.transform.position.y;
            var camera=Object.FindAnyObjectByType<GameCameraFollow>();
            foreach(var prefab in spawner.BossPrefabs)
            {
                Time.timeScale=4f;hp.ResetHealth();
                player.Teleport(center+Vector3.back*5.1f);
                Assert.IsTrue(NavMesh.SamplePosition(center+Vector3.forward*1.4f,out var nav,2f,NavMesh.AllAreas));
                var boss=Object.Instantiate(prefab,nav.position,Quaternion.LookRotation(Vector3.back)).GetComponent<BossEnemy>();
                boss.ConfigureEncounter(bounds,center.y,player);camera.SetBossFocus(boss);
                var resolved=new Dictionary<int,int>();int warnings=0;bool allWarningsMatched=true;bool hitAny=false,safeRingMiss=false;
                int previewSkill=boss.Kind==BossKind.Orgrim?4:boss.Kind==BossKind.Noctyr?1:0;
                int previewStage=boss.Kind==BossKind.Orgrim?2:1;
                bool previewPending=false,previewShown=false,releasePending=false,releaseShown=false;
                boss.StageWarned+=(b,plan)=>
                {
                    warnings++;
                    var markers=Object.FindObjectsByType<ArcaneTelegraph>().Where(m=>m.name=="BossWarning_"+b.Kind&&m.IsVisible).ToArray();
                    allWarningsMatched&=markers.Length==plan.Zones.Count && plan.Zones.Count>0;
                    if(WantsCapture()&&b.ActiveSkillIndex==previewSkill&&b.ActiveStage==previewStage){previewPending=true;Time.timeScale=1f;}
                };
                boss.StageResolved+=(b,plan,hit)=>
                {
                    int id=b.ActiveSkillIndex;resolved.TryGetValue(id,out int count);resolved[id]=count+1;hitAny|=hit;
                    if(b.Kind==BossKind.Noctyr&&id==1)safeRingMiss=!hit;
                    if(previewShown&&!releaseShown&&id==previewSkill&&b.ActiveStage==previewStage)releasePending=true;
                };
                yield return null;
                boss.TakeDamage(new DamageInfo(boss.MaxHealth*.54f,player.gameObject,boss.transform.position));
                float deadline=Time.realtimeSinceStartup+40f;
                while(Time.realtimeSinceStartup<deadline && (resolved.Count<5 || boss.IsCasting))
                {
                    if(hp.CurrentHealth<45f)hp.ResetHealth();
                    if(previewPending&&!previewShown&&Object.FindObjectsByType<ArcaneTelegraph>().Any(m=>m.name=="BossWarning_"+boss.Kind&&m.IsVisible&&m.Progress>=.48f))
                    {
                        previewShown=true;yield return Capture(boss.Kind+"-01-warning.png");
                    }
                    if(releasePending&&!releaseShown)
                    {
                        releaseShown=true;yield return new WaitForSeconds(.10f);yield return Capture(boss.Kind+"-02-release.png");Time.timeScale=4f;
                    }
                    yield return null;
                }
                Assert.AreEqual(2,boss.Phase,boss.Kind+" never entered its second phase.");
                Assert.AreEqual(5,resolved.Count,boss.Kind+" timed out before all skills resolved.");
                for(int i=0;i<5;i++)Assert.AreEqual(boss.Definition.Skills[i].StageCount(2),resolved[i],boss.Kind+" / "+i);
                Assert.AreEqual(resolved.Values.Sum(),warnings,boss.Kind+" released an unannounced stage.");
                Assert.IsTrue(allWarningsMatched,boss.Kind+" warning geometry count did not match its attack.");
                Assert.IsTrue(hitAny,boss.Kind+" did not actually damage a stationary player during its entire ability cycle.");
                if(boss.Kind==BossKind.Noctyr)Assert.IsTrue(safeRingMiss,"Standing in the safe heart of an eclipse must avoid damage.");
                camera.SetBossFocus(null);Object.Destroy(boss.gameObject);yield return null;
                yield return new WaitForSeconds(.6f);
                Assert.IsFalse(Object.FindObjectsByType<ArcaneTelegraph>().Any(m=>m.name=="BossWarning_"+boss.Kind&&m.IsVisible));
                Assert.IsNull(Object.FindAnyObjectByType<BossEncounterHud>());
            }
            Time.timeScale=1f;
        }

        [UnityTest]
        public IEnumerator RegularHealthBarsUseStyledFillAndDelayedDamageThenHideOnDeath()
        {
            yield return LoadDungeon();
            var player=Player();player.SetInputActive(false);var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            var origin=player.transform.position;
            var list=new List<EnemyBase>();
            var kinds=new[]{SpecialEnemyKind.StoneGolem,SpecialEnemyKind.Charger,SpecialEnemyKind.FrostMage};
            for(int i=0;i<kinds.Length;i++)
            {
                var prefab=spawner.SpecialEnemyPrefabs.Single(p=>p.GetComponent<SpecialEnemy>().Kind==kinds[i]);
                Vector3 point=origin+new Vector3((i-1)*2.25f,0,3.35f);
                Assert.IsTrue(NavMesh.SamplePosition(point,out var nav,1.5f,NavMesh.AllAreas));
                var enemy=Object.Instantiate(prefab,nav.position,Quaternion.LookRotation(Vector3.back)).GetComponent<EnemyBase>();list.Add(enemy);
                yield return null;enemy.enabled=false;
                var bar=enemy.GetComponentInChildren<EnemyWorldHealthBar>();Assert.NotNull(bar);
                var fill=bar.transform.Find("Vitality_Ornament/Vitality_Fill").GetComponent<Image>();
                var chip=bar.transform.Find("Vitality_Ornament/Vitality_DelayedDamage").GetComponent<Image>();
                Assert.IsFalse(fill.raycastTarget);Assert.AreEqual(1f,fill.fillAmount,.01f);
                float ratio=1f-i*.21f;
                if(i>0)enemy.TakeDamage(new DamageInfo(enemy.MaxHealth*(1-ratio),player.gameObject,enemy.transform.position));
                Assert.AreEqual(ratio,bar.HealthRatio,.015f);Assert.GreaterOrEqual(chip.fillAmount,fill.fillAmount);
            }
            yield return new WaitForSeconds(.8f);yield return Capture("Regular-Mob-HealthBars.png");
            foreach(var enemy in list)
            {
                var bar=enemy.GetComponentInChildren<EnemyWorldHealthBar>();
                enemy.TakeDamage(new DamageInfo(5000,player.gameObject,enemy.transform.position));
                Assert.IsFalse(bar.GetComponent<Canvas>().enabled);
            }
        }

        private static bool WantsCapture()=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIGHTSOULS_BOSS_CAPTURE_DIR"))&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
        private static IEnumerator Capture(string file)
        {
            if(!WantsCapture())yield break;
            string dir=Environment.GetEnvironmentVariable("LIGHTSOULS_BOSS_CAPTURE_DIR");Directory.CreateDirectory(dir);
            var cam=Camera.main;var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);rt.Create();
            var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;float scale=Time.timeScale;
            var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
            Time.timeScale=0;cam.targetTexture=rt;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=cam.nearClipPlane+.8f;}
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=rt};
                if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
                RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());Object.Destroy(image);
                Debug.Log("BOSS_CAPTURE "+Path.Combine(dir,file));
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
                Time.timeScale=scale;cam.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();Object.Destroy(rt);
            }
        }
    }
}

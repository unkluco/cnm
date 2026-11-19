using System;
using System.Collections;
using System.IO;
using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace LightSouls.Combat.Tests
{
    public class ArcaneVfxPlayTests
    {
        private static IEnumerator WaitFor(Func<bool> condition,string message,float seconds=10f)
        {float stop=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<stop)yield return null;Assert.IsTrue(condition(),message);}

        [UnityTearDown]
        public IEnumerator Cleanup(){Time.timeScale=1f;RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu);}

        [UnityTest]
        public IEnumerator RealFireSwordCreatesRibbonAndBurnWithoutChangingDamageOrStackingEffects()
        {
            RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(()=>Object.FindAnyObjectByType<PlayerController>()!=null,"Player did not spawn.");
            var player=Object.FindAnyObjectByType<PlayerController>();
            var spawner=Object.FindAnyObjectByType<EnemySpawner>();Assert.NotNull(spawner);
            var database=Resources.FindObjectsOfTypeAll<ItemDatabaseSO>().FirstOrDefault(d=>d.GetItem("weapon.fire_sword")!=null);
            Assert.NotNull(database);var fire=database.GetItem("weapon.fire_sword") as WeaponDataSO;Assert.NotNull(fire);
            var enemy=SpawnEnemy(spawner,SpecialEnemyKind.StoneGolem,player.transform.position+Vector3.forward*1.7f);
            yield return null;enemy.enabled=false;
            var health=enemy.GetComponent<HealthComponent>();float before=health.CurrentHealth;
            Assert.IsTrue(player.Inventory.TryAdd(fire));
            int fireSlot=player.Inventory.HotbarSlots.ToList().FindIndex(s=>s.Item==fire);Assert.GreaterOrEqual(fireSlot,0);
            player.Inventory.SelectHotbarSlot(fireSlot);player.SetInputActive(true);Physics.SyncTransforms();
            player.Combat.PerformAttack();
            yield return WaitFor(()=>enemy.GetComponent<StatusEffectReceiver>()?.IsBurning==true,"Fire sword's actual burn effect did not apply.");
            Assert.Less(health.CurrentHealth,before);
            yield return new WaitForSeconds(.09f);
            var blade=player.GetComponentInChildren<BladeRibbonTrail>();Assert.NotNull(blade);
            Assert.IsTrue(blade.IsVisible,"The actual sampled blade needs a visible wake.");Assert.That(blade.SampleCount,Is.InRange(2,18));
            Assert.IsNotNull(Object.FindAnyObjectByType<SwordSlashWave>());
            yield return Capture("01-fire-sword-slash.png");
            var receiver=enemy.GetComponent<StatusEffectReceiver>();var aura=enemy.transform.Find("VFX_Burn");Assert.NotNull(aura);
            Assert.NotNull(aura.Find("VFX_Burn_Embers"));Assert.LessOrEqual(aura.GetComponent<ParticleSystem>().main.maxParticles,30);
            receiver.ApplyBurn(1f,.75f,player.gameObject);Assert.AreSame(aura,enemy.transform.Find("VFX_Burn"));
            yield return new WaitForSeconds(.43f);Assert.IsFalse(blade.IsVisible);
            yield return Capture("02-burning-target.png");
            Object.Destroy(enemy.gameObject);yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<SwordSlashWave>());

            // Trails finish as visual-only objects and are removed as soon as their last samples fade.
            var sample=new GameObject("TrailTest");sample.transform.position=player.transform.position+Vector3.up;
            var trail=VfxFactory.AttachTrail(sample,Color.cyan,.16f,.18f);Assert.NotNull(trail);
            VfxFactory.ReleaseTrail(sample);Assert.IsNull(trail.transform.parent);Object.Destroy(sample);
            yield return new WaitForSeconds(.3f);Assert.IsTrue(trail==null);

            if(WantsCapture())
            {
                player.SetInputActive(false);
                yield return Showcase(SpecialEnemyKind.EmberMage,spawner,player,"03-ember-cast.png","04-fire-eruption.png");
                yield return Showcase(SpecialEnemyKind.FrostMage,spawner,player,"05-frost-cast.png","06-frost-impact.png");
                yield return Showcase(SpecialEnemyKind.Charger,spawner,player,"07-charge-direction.png",null);
                yield return Showcase(SpecialEnemyKind.StormArcher,spawner,player,"08-storm-volley.png",null);
            }
        }

        [UnityTest]
        public IEnumerator IceProjectileAndSoulDashUseSharedMaterialsAndCleanUp()
        {
            RunState.Reset();yield return SceneManager.LoadSceneAsync(SceneNames.Game);
            yield return WaitFor(()=>Object.FindAnyObjectByType<PlayerController>()!=null,"Player did not spawn.");
            var player=Object.FindAnyObjectByType<PlayerController>();var spawner=Object.FindAnyObjectByType<EnemySpawner>();
            var db=Resources.FindObjectsOfTypeAll<ItemDatabaseSO>().First(d=>d.GetItem("weapon.ice_staff")!=null);
            var ice=db.GetItem("weapon.ice_staff") as WeaponDataSO;Assert.NotNull(ice);
            var enemy=SpawnEnemy(spawner,SpecialEnemyKind.StoneGolem,player.transform.position+Vector3.forward*3.35f);
            yield return null;enemy.enabled=false;
            Assert.IsTrue(player.Inventory.TryAdd(ice));int slot=player.Inventory.HotbarSlots.ToList().FindIndex(s=>s.Item==ice);Assert.GreaterOrEqual(slot,0);
            player.Inventory.SelectHotbarSlot(slot);player.SetInputActive(true);Physics.SyncTransforms();player.Combat.PerformAttack();
            var projectile=Object.FindAnyObjectByType<Projectile>();Assert.NotNull(projectile);
            var trail=projectile.GetComponentInChildren<TrailRenderer>();Assert.NotNull(trail);Assert.AreSame(ArcaneVfxMaterials.Ribbon,trail.sharedMaterial);
            yield return new WaitForSeconds(.065f);yield return Capture("09-ice-staff-flight.png");
            yield return WaitFor(()=>enemy.GetComponent<StatusEffectReceiver>()?.IsSlowed==true,"Ice projectile did not apply its original slow.");
            yield return new WaitForSeconds(.17f);yield return Capture("10-ice-staff-impact.png");
            Object.Destroy(enemy.gameObject);yield return null;
            player.transform.rotation=Quaternion.LookRotation(Vector3.right,Vector3.up);player.Movement.PerformDash();
            yield return new WaitForSeconds(.085f);
            var ghosts=Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name=="DashGhost_Afterimage").ToArray();
            Assert.That(ghosts.Length,Is.InRange(1,4));
            Assert.IsTrue(ghosts.All(g=>g.sharedMaterial==ArcaneVfxMaterials.Ghost&&g.GetComponentsInChildren<Renderer>().Length==1));
            yield return Capture("11-soul-dash.png");
            yield return new WaitForSeconds(.75f);
            Assert.IsFalse(Object.FindObjectsByType<MeshRenderer>().Any(r=>r.name=="DashGhost_Afterimage"));
        }

        private static SpecialEnemy SpawnEnemy(EnemySpawner spawner,SpecialEnemyKind kind,Vector3 position)
        {
            var prefab=spawner.SpecialEnemyPrefabs.First(p=>p.GetComponent<SpecialEnemy>().Kind==kind);
            if(NavMesh.SamplePosition(position,out var hit,2f,NavMesh.AllAreas))position=hit.position;
            return Object.Instantiate(prefab,position,Quaternion.identity).GetComponent<SpecialEnemy>();
        }

        private static IEnumerator Showcase(SpecialEnemyKind kind,EnemySpawner spawner,PlayerController player,string warningFile,string impactFile)
        {
            var hp=player.GetComponent<HealthComponent>();hp.ResetHealth();
            var enemy=SpawnEnemy(spawner,kind,player.transform.position+Vector3.forward*4.1f);
            string markerName="EnemyWarning_"+kind;
            bool MarkersVisible()=>Object.FindObjectsByType<ArcaneTelegraph>().Any(g=>g.name==markerName&&g.IsVisible);
            yield return WaitFor(()=>Object.FindObjectsByType<ArcaneTelegraph>().Any(g=>g.name==markerName&&g.IsVisible&&g.Progress>.43f),kind+" cast did not display the styled warning.");
            yield return Capture(warningFile);
            if(!string.IsNullOrEmpty(impactFile))
            {
                yield return WaitFor(()=>!MarkersVisible(),kind+" warning was not cleared.");
                yield return new WaitForSeconds(.10f);
                yield return Capture(impactFile);
            }
            if(enemy!=null)Object.Destroy(enemy.gameObject);
            yield return new WaitForSeconds(1.05f);
            Assert.IsFalse(MarkersVisible(),kind+" marker survived enemy removal.");
        }

        private static bool WantsCapture()=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIGHTSOULS_VFX_CAPTURE_DIR"))&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;

        private static IEnumerator Capture(string file)
        {
            if(!WantsCapture())yield break;
            string dir=Environment.GetEnvironmentVariable("LIGHTSOULS_VFX_CAPTURE_DIR");Directory.CreateDirectory(dir);
            var camera=Camera.main;Assert.NotNull(camera);
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){name="CombatVfxPreview"};target.Create();
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;float previousTime=Time.timeScale;
            var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras=canvases.Select(c=>c.worldCamera).ToArray();var oldDistances=canvases.Select(c=>c.planeDistance).ToArray();
            // Freeze the precise wind-up or release frame while resolving the screenshot canvas layout.
            Time.timeScale=0f;camera.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.8f;}
            yield return null;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();
                RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,file),image.EncodeToPNG());Object.Destroy(image);
                Debug.Log("VFX_CAPTURE "+Path.Combine(dir,file));
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=oldCameras[i];canvases[i].planeDistance=oldDistances[i];}
                Time.timeScale=previousTime;camera.targetTexture=previousTarget;RenderTexture.active=previousActive;target.Release();Object.Destroy(target);
            }
        }
    }
}

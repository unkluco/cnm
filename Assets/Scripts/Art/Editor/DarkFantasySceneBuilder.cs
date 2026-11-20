using System.Linq;
using LightSouls.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private static VolumeProfile LightingProfile()
        {
            string path=ArtRoot+"/LightSoulsLighting.asset";
            var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(p==null){p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,path);}
            if(!p.TryGet<Bloom>(out var bloom)){bloom=p.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,p);}
            bloom.threshold.Override(1.18f);bloom.intensity.Override(.23f);bloom.scatter.Override(.48f);bloom.highQualityFiltering.Override(false);bloom.maxIterations.Override(4);
            if(!p.TryGet<Vignette>(out var vignette)){vignette=p.Add<Vignette>(true);AssetDatabase.AddObjectToAsset(vignette,p);}
            vignette.color.Override(C("141b28"));vignette.intensity.Override(.19f);vignette.smoothness.Override(.53f);
            if(!p.TryGet<Tonemapping>(out var tone)){tone=p.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,p);}
            tone.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(vignette);EditorUtility.SetDirty(tone);EditorUtility.SetDirty(p);
            return p;
        }

        private static void BuildScenes()
        {
            var profile=LightingProfile();
            foreach(string name in new[]{"MainMenu","Lobby","Game"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Single);
                var oldArt=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Art_DarkFantasyScene");if(oldArt!=null)Object.DestroyImmediate(oldArt);
                Transform art=Group(null,"Art_DarkFantasyScene");
                var camera=scene.GetRootGameObjects().Select(g=>g.GetComponentInChildren<Camera>(true)).FirstOrDefault(c=>c!=null);
                var key=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
                ConfigureLightAndCamera(camera,key,profile,scene,name);
                if(name=="MainMenu") {art.gameObject.AddComponent<DarkFantasyLightBudget>();BuildMenuDiorama(art,camera);}
                else
                {
                    var budget=art.gameObject.AddComponent<DarkFantasyLightBudget>();
                    if(name=="Lobby") {budget.ConfigureFixedSceneLighting(true);BuildLobbyEnvironment(scene,art);}
                    foreach(var root in scene.GetRootGameObjects().Where(g=>g.name=="UI_CoinCounter"||g.name=="Canvas_LobbyUI")) StyleUi(root.transform);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            SetPipelineBudget("Assets/Settings/Mobile_RPAsset.asset",29f,2);
            SetPipelineBudget("Assets/Settings/PC_RPAsset.asset",42f,3);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
        }

        private static void ConfigureLightAndCamera(Camera cam,Light key,VolumeProfile profile,Scene scene,string name)
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=C(name=="Lobby"?"718298":"627188");RenderSettings.ambientIntensity=1f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=C("131c2c");
            RenderSettings.fogStartDistance=name=="MainMenu"?15:21;RenderSettings.fogEndDistance=name=="MainMenu"?35:57;
            if(key!=null) {key.color=C("cddcf0");key.intensity=1.0f;key.shadows=LightShadows.Soft;key.shadowStrength=.88f;key.transform.rotation=Q(42,-43,0);RenderSettings.sun=key;}
            if(cam!=null)
            {
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C("131c2c");cam.allowHDR=true;
                var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.renderShadows=true;
                data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                if(name=="Game")
                {
                    cam.fieldOfView=45f;
                    var follow=cam.GetComponent<LightSouls.Game.GameCameraFollow>();
                    if(follow!=null){var so=new SerializedObject(follow);so.FindProperty("distance").floatValue=16f;so.ApplyModifiedPropertiesWithoutUndo();}
                }
            }
            var vol=scene.GetRootGameObjects().Select(g=>g.GetComponent<Volume>()).FirstOrDefault(v=>v!=null);
            if(vol==null) vol=new GameObject("Art_LightingVolume").AddComponent<Volume>();
            vol.isGlobal=true;vol.priority=1;vol.sharedProfile=profile;
        }

        private static void SetPipelineBudget(string path,float distance,int cascades)
        {
            var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);if(asset==null)return;
            var so=new SerializedObject(asset);so.FindProperty("m_ShadowDistance").floatValue=distance;
            so.FindProperty("m_ShadowCascadeCount").intValue=cascades;so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=4;
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);
        }

        private static void BuildLobbyEnvironment(Scene scene,Transform art)
        {
            var floor=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="LobbyFloor")?.transform;
            var walls=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="LobbyWalls")?.transform;
            StyleFloor(art,floor,"Lobby",soul,true);
            if(walls!=null)
            {
                var solids=walls.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_")).ToArray();
                StyleWalls(art,solids,"Lobby");PlaceWallTorches(art,Group(art,"Art_Torches"),solids,4);StyleWallBanners(art,solids,cloak,2);
            }
            var shelf=Group(art,"Art_MerchantShelf",V(-4.65f,1.35f,9.52f));
            Part(shelf,"ShelfBack",box,wood,V(0,.55f,0),V(3.0f,1.5f,.21f));
            foreach(float y in new[]{.02f,.78f})Part(shelf,"Shelf",box,darkIron,V(0,y,-.29f),V(3.05f,.13f,.69f));
            for(int i=0;i<7;i++)
            {
                float x=-1.2f+i*.4f;float y=i%2==0 ? .22f : 1f;
                Part(shelf,"Bottle",plateMesh,i%3==0?soul:i%3==1?purple:red,V(x,y,-.3f),V(.23f,.27f,.23f));
                Part(shelf,"Stopper",prism,bronze,V(x,y+.17f,-.3f),V(.11f,.09f,.11f));
            }
            var shrine=Group(art,"Art_Sanctuary",V(0,0,0));
            for(int i=0;i<4;i++)
            {
                float a=(i+.5f)*Mathf.PI*.5f;float x=Mathf.Sin(a)*6.1f,z=Mathf.Cos(a)*6.1f;
                var p=Group(shrine,"FloorInlay",V(x,.01f,z));Part(p,"RuneDiamond",ring,bronze,Vector3.zero,V(.85f,.18f,.85f),Q(0,45,0),false);
            }
        }

        private static void BuildMenuDiorama(Transform art,Camera camera)
        {
            if(camera!=null){camera.transform.position=V(0,3.58f,-9.5f);camera.transform.LookAt(V(0,2.0f,0));camera.fieldOfView=40f;camera.orthographic=false;}
            var floor=Part(art,"CourtFoundation",plainCube,mortar,V(0,-.21f,2.0f),V(21f,.42f,18f));StyleFloor(art,floor,"TitleCourt",bronze,false);
            var back=Part(art,"Wall_Back",plainCube,wall0,V(0,3.65f,5.5f),V(21,7.3f,.76f));
            var left=Part(art,"Wall_Left",plainCube,wall0,V(-10f,2.35f,.8f),V(.7f,4.7f,9.7f));
            var right=Part(art,"Wall_Right",plainCube,wall0,V(10f,2.35f,.8f),V(.7f,4.7f,9.7f));
            StyleWalls(art,new[]{back,left,right},"TitleCourt");
            for(int i=0;i<5;i++)
            {
                float x=-8.8f+i*4.4f;
                Part(art,"PillarFoot",box,wall3,V(x,.26f,4.96f),V(1.12f,.52f,.88f));
                Part(art,"AncientColumn",prism,wall1,V(x,3.2f,4.92f),V(.68f,5.75f,.75f));
                Part(art,"ColumnCapital",box,trim,V(x,6.1f,4.92f),V(1.08f,.29f,.96f));
            }
            var portal=Group(art,"Art_AncientGate",V(4.1f,0,3.95f));portal.localScale=Vector3.one*1.3f;
            BuildPortal(portal,soul,true);
            var pedestal=Group(art,"HeroDais",V(3.08f,0,-.12f));
            Part(pedestal,"Base",prism,wall3,V(0,.15f,0),V(3.1f,.29f,3.1f));
            Part(pedestal,"Edge",prism,bronze,V(0,.31f,0),V(2.85f,.08f,2.85f));
            Part(pedestal,"Top",prism,wall1,V(0,.42f,0),V(2.73f,.17f,2.73f));
            Part(pedestal,"Sigil",ring,soul,V(0,.513f,0),V(2.58f,.12f,2.58f),shadows:false);
            GameObject player=AssetDatabase.LoadAssetAtPath<GameObject>(CombatRoot+"/Player_Character.prefab");
            Transform rig=Find(player.transform,"HumanoidFigure");var knight=Object.Instantiate(rig.gameObject,art);knight.name="Art_Knight";knight.transform.localPosition=V(3.08f,.51f,-.12f);knight.transform.localRotation=Q(0,196,0);knight.transform.localScale=Vector3.one*1.93f;
            foreach(var mb in knight.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(mb);
            foreach(var collider in knight.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            var arm=Find(knight.transform,"RightArmPivot");if(arm!=null)arm.localRotation=Q(-6,-9,-18);
            var leftArm=Find(knight.transform,"LeftArm");if(leftArm!=null)leftArm.localRotation=Q(7,0,11);
            var leftLeg=Find(knight.transform,"LeftLeg");if(leftLeg!=null){leftLeg.localPosition+=V(-.045f,0,0);leftLeg.localRotation=Q(0,0,-4);}
            var rightLeg=Find(knight.transform,"RightLeg");if(rightLeg!=null){rightLeg.localPosition+=V(.045f,0,0);rightLeg.localRotation=Q(0,0,4);}
            // Warm foreground and cool magical rim make the silhouette read at mobile size.
            Torch(art,"Art_LeftBrazier",V(1.05f,1.02f,.8f),Q(0,180,0),true);
            Torch(art,"Art_RightBrazier",V(6.04f,1.02f,2.0f),Q(0,180,0),true);
            foreach(float x in new[]{1.05f,6.04f})
            {
                float z=x<3f ? .8f : 2f;
                Part(art,"BrazierBase",prism,wall3,V(x,.51f,z),V(.74f,1.02f,.74f));
                Part(art,"BrazierCap",box,bronze,V(x,1.02f,z),V(.83f,.10f,.8f));
            }
            var fill=Group(art,"Art_MoonRim",V(4.9f,3.2f,-2.0f)).gameObject.AddComponent<Light>();fill.type=LightType.Point;fill.color=C("97d7e9");fill.range=8f;fill.intensity=4.4f;fill.shadows=LightShadows.None;
            // Low poly banners on the back wall add quiet vertical rhythm.
            foreach(float x in new[]{-6.7f,-2.2f,8.15f})
            {
                Part(art,"BannerRod",box,bronze,V(x,4.16f,5f),V(1.5f,.095f,.10f));
                Part(art,"Banner",box,cloak,V(x,3.23f,5f),V(1.22f,1.8f,.075f));
                Part(art,"BannerCrest",gem,gold,V(x,3.58f,4.94f),V(.4f,.58f,.05f),shadows:false);
            }
        }
    }
}

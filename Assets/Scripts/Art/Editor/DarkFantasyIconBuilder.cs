using System;
using System.IO;
using System.Linq;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        // Render the same faceted models that are used in-world into clean transparent item sprites.
        // The unsaved studio scene is disposable; the three playable scenes are not touched here.
        private static void BuildItemIcons()
        {
            string folder=ArtRoot+"/Icons";EnsureFolder(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=C("b7becb");
            var cam=new GameObject("Art_IconCamera").AddComponent<Camera>();
            cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.magenta;cam.allowHDR=false;cam.allowMSAA=false;
            cam.nearClipPlane=.05f;cam.farClipPlane=20f;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var studioKey=new GameObject("Key").AddComponent<Light>();studioKey.type=LightType.Directional;studioKey.color=C("fff0d2");studioKey.intensity=1.35f;studioKey.transform.rotation=Q(36,-37,0);RenderSettings.sun=studioKey;
            var fill=new GameObject("Rim").AddComponent<Light>();fill.type=LightType.Directional;fill.color=C("a1c9e8");fill.intensity=.65f;fill.transform.rotation=Q(18,145,0);
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();cam.targetTexture=target;
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/ScriptableObjects/Combat"}))
                {
                    var item=AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));if(item==null)continue;
                    GameObject model;
                    if(item is WeaponDataSO weapon&&weapon.WeaponPrefab!=null)
                    {
                        model=Object.Instantiate(weapon.WeaponPrefab);model.transform.position=Vector3.zero;
                        model.transform.rotation=weapon.Type==WeaponType.Melee||weapon.name.Contains("Staff")?Q(0,25,-32):Q(12,-74,20);
                    }
                    else
                    {
                        model=new GameObject("IconModel_"+item.name);BuildInventoryModel(model.transform,item.name);
                        model.transform.rotation=Q(8,-22,-8);
                    }
                    try
                    {
                        Sprite sprite=RenderItemIcon(model,cam,target,folder+"/"+item.name+".png");
                        if(sprite!=null){var so=new SerializedObject(item);so.FindProperty("icon").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();}
                    }
                    finally{Object.DestroyImmediate(model);}
                }
                var bag=new GameObject("TravelSatchel");BuildInventoryModel(bag.transform,"Backpack");bag.transform.rotation=Q(6,-18,-7);
                RenderItemIcon(bag,cam,target,folder+"/Control_Backpack.png");Object.DestroyImmediate(bag);
            }
            finally{cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
        }

        private static Sprite RenderItemIcon(GameObject model,Camera cam,RenderTexture target,string path)
        {
            var rs=model.GetComponentsInChildren<MeshRenderer>();if(rs.Length==0)return null;
            Bounds b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
            cam.transform.position=b.center+Vector3.back*6f;cam.transform.rotation=Quaternion.identity;cam.orthographicSize=Mathf.Max(.1f,Mathf.Max(b.size.x,b.size.y)*.61f);
            var request=new RenderPipeline.StandardRequest{destination=target};
            if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
            RenderTexture previous=RenderTexture.active;RenderTexture.active=target;
            var input=new Texture2D(256,256,TextureFormat.RGB24,false);input.ReadPixels(new Rect(0,0,256,256),0,0);input.Apply();RenderTexture.active=previous;
            Color32[] pixels=input.GetPixels32();Object.DestroyImmediate(input);
            Color32 background=pixels[0];
            var result=new Color32[128*128];int opaque=0;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                int r=0,g=0,bv=0,n=0;
                for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                {
                    Color32 c=pixels[(y*2+dy)*256+x*2+dx];
                    if(Mathf.Abs(c.r-background.r)+Mathf.Abs(c.g-background.g)+Mathf.Abs(c.b-background.b)<=9)continue;
                    // Full coverage samples composited as straight-alpha to avoid a colored border.
                    r+=c.r;g+=c.g;bv+=c.b;n++;
                }
                if(n>0){result[y*128+x]=new Color32((byte)(r/n),(byte)(g/n),(byte)(bv/n),(byte)(n*255/4));opaque++;}
            }
            if(opaque<45||opaque>128*128*.96f){Debug.LogWarning("Art icon capture skipped (invalid silhouette): "+path);return null;}
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.SetPixels32(result);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildInventoryModel(Transform t,string id)
        {
            if(id.Contains("Potion"))
            {
                Material liquid=id.Contains("Rage")?fireOuter:red;
                Part(t,"Bottle",plateMesh,liquid,V(0,.39f,0),V(.65f,.65f,.50f));
                Part(t,"BottleNeck",prism,ivory,V(0,.77f,0),V(.2f,.22f,.20f));
                Part(t,"Cork",prism,wood,V(0,.91f,0),V(.25f,.12f,.24f));
                Part(t,"BottleBand",prism,bronze,V(0,.27f,0),V(.67f,.08f,.52f));
                Part(t,"HealerMark",gem,id.Contains("Rage")?flame:ivory,V(0,.47f,-.28f),V(.18f,.22f,.075f));
                if(id.Contains("Large"))Part(t,"Collar",prism,gold,V(0,.66f,0),V(.44f,.08f,.39f));
            }
            else if(id.Contains("Boots"))
            {
                Material material=id.Contains("Spiked")?steel:id.Contains("Wading")?soul:id.Contains("Swift")?cloak:leather;
                foreach(int s in new[]{-1,1})
                {
                    var boot=Group(t,"Boot",V(s*.29f,0,s*.055f));
                    Part(boot,"Foot",box,material,V(0,.15f,-.12f),V(.42f,.26f,.65f));
                    Part(boot,"Upper",limbMesh,material,V(0,.43f,.01f),V(.39f,.62f,.39f));
                    Part(boot,"Sole",box,darkIron,V(0,.04f,-.11f),V(.44f,.1f,.67f));
                    Part(boot,"Cuff",prism,bronze,V(0,.66f,.01f),V(.44f,.075f,.43f));
                    if(id.Contains("Spiked"))Part(boot,"Crampon",cone,steel,V(0,-.09f,-.15f),V(.17f,.18f,.17f),Q(0,0,180));
                    if(id.Contains("Swift")||id.Contains("Wading"))Part(boot,"Rune",gem,soul,V(.09f,.38f,-.22f),V(.13f,.21f,.08f));
                    if(id.Contains("Spring"))Part(boot,"Coil",prism,gold,V(0,.11f,-.08f),V(.50f,.20f,.69f));
                }
            }
            else if(id.Contains("Armor"))
            {
                Material m=id.Contains("Chainmail")?steel:id.Contains("Heat")?red:id.Contains("Shadow")?cloth:leather;
                Part(t,"Vest",chestMesh,m,V(0,.47f,0),V(.70f,.91f,.48f));
                for(int s=-1;s<=1;s+=2)Part(t,"Shoulder",plateMesh,m,V(s*.35f,.73f,0),V(.33f,.34f,.39f));
                Part(t,"Belt",box,leather,V(0,.18f,0),V(.68f,.11f,.48f));
                Part(t,"Badge",gem,id.Contains("Heat")?flame:id.Contains("Shadow")?purple:gold,V(0,.54f,-.26f),V(.21f,.29f,.06f));
                if(id.Contains("Shadow"))Part(t,"Mantle",cape,cloak,V(0,.42f,-.13f),V(.81f,.98f,.30f),Q(0,180,0));
            }
            else if(id.Contains("Scroll"))
            {
                Part(t,"Parchment",box,ivory,V(0,.4f,0),V(.64f,.75f,.16f));
                foreach(float y in new[]{.03f,.77f})Part(t,"Roll",prism,wood,V(0,y,0),V(.21f,.78f,.21f),Q(0,0,90));
                Part(t,"Seal",gem,purple,V(0,.43f,-.10f),V(.28f,.32f,.085f));
            }
            else if(id.Contains("Feather"))
            {
                Part(t,"Quill",blade,flame,V(0,.55f,0),V(.39f,1.0f,.11f),Q(0,0,-20));
                foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)Part(t,"Barb",blade,gold,V(s*(.17f-i*.03f),.35f+i*.18f,0),V(.15f,.36f,.08f),Q(0,0,s*25-20));
                Part(t,"Shaft",prism,bronze,V(-.09f,.22f,0),V(.065f,.56f,.065f),Q(0,0,-20));
            }
            else if(id.Contains("Bomb"))
            {
                Part(t,"Core",plateMesh,darkIron,V(0,.36f,0),V(.70f,.70f,.70f));
                Part(t,"Ring",prism,bronze,V(0,.34f,0),V(.72f,.12f,.72f));
                Part(t,"Rune",gem,flame,V(0,.37f,-.37f),V(.18f,.29f,.065f));
                Part(t,"Cap",prism,bronze,V(0,.73f,0),V(.23f,.13f,.23f));
                Part(t,"Ember",fireMesh,flame,V(0,.87f,0),V(.14f,.24f,.14f));
            }
            else // Travel satchel / backpack button.
            {
                Part(t,"Satchel",box,wood,V(0,.43f,0),V(.68f,.78f,.43f));
                Part(t,"Lid",box,leather,V(0,.69f,-.01f),V(.71f,.27f,.47f));
                foreach(int s in new[]{-1,1})Part(t,"Strap",box,bronze,V(s*.20f,.43f,-.24f),V(.09f,.74f,.065f));
                Part(t,"Clasp",gem,gold,V(0,.52f,-.28f),V(.19f,.20f,.085f));
                Part(t,"Bottom",box,darkIron,V(0,.08f,0),V(.70f,.13f,.45f));
            }
        }
    }
}

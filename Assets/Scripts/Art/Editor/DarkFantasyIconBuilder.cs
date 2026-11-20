using System;
using System.Collections.Generic;
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
        private const int IconSize=192;
        private const int CaptureSize=IconSize*2;

        // Render the same faceted models that are used in-world into clean transparent item sprites.
        // The unsaved studio scene is disposable; the three playable scenes are not touched here.
        private static void BuildItemIcons(bool materialsOnly=false,ISet<string> onlyItems=null)
        {
            string folder=ArtRoot+"/Icons";EnsureFolder(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=C("a4adbc");
            var cam=new GameObject("Art_IconCamera").AddComponent<Camera>();
            cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowHDR=false;cam.allowMSAA=false;
            cam.nearClipPlane=.05f;cam.farClipPlane=20f;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var studioKey=new GameObject("Key").AddComponent<Light>();studioKey.type=LightType.Directional;studioKey.color=C("fff0d2");studioKey.intensity=1.2f;studioKey.transform.rotation=Q(35,-43,0);RenderSettings.sun=studioKey;
            var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=C("b4d2f2");fill.intensity=.60f;fill.transform.rotation=Q(18,46,0);
            var target=new RenderTexture(CaptureSize,CaptureSize,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();cam.targetTexture=target;
            var maskShader=Shader.Find("Universal Render Pipeline/Unlit");
            if(maskShader==null)throw new InvalidOperationException("Icon silhouette shader is unavailable.");
            var maskMaterial=new Material(maskShader){hideFlags=HideFlags.DontSave};maskMaterial.SetColor("_BaseColor",Color.white);
            try
            {
                var iconItems=AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/ScriptableObjects/Combat"})
                    .Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(item=>item!=null&&(!materialsOnly||item is CraftMaterialSO)&&(onlyItems==null||onlyItems.Contains(item.name))).ToArray();
                // A cold graphics device can still be compiling a variant during its first frame.
                // Recapture the first model after the rest of the studio has warmed up.
                foreach(var item in iconItems.Concat(iconItems.Take(1)))
                {
                    GameObject model;
                    if(item is WeaponDataSO weapon&&weapon.WeaponPrefab!=null)
                    {
                        model=Object.Instantiate(weapon.WeaponPrefab);model.transform.position=Vector3.zero;
                        model.transform.rotation=IconRotation(weapon);
                    }
                    else
                    {
                        model=new GameObject("IconModel_"+item.name);BuildInventoryModel(model.transform,item.name);
                        model.transform.rotation=Q(8,-22,-8);
                    }
                    try
                    {
                        Sprite sprite=RenderItemIcon(model,cam,target,maskMaterial,folder+"/"+item.name+".png");
                        if(sprite!=null){var so=new SerializedObject(item);so.FindProperty("icon").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();}
                    }
                    finally{Object.DestroyImmediate(model);}
                }
                if(!materialsOnly&&onlyItems==null)
                {
                    var bag=new GameObject("TravelSatchel");BuildInventoryModel(bag.transform,"Backpack");bag.transform.rotation=Q(6,-18,-7);
                    RenderItemIcon(bag,cam,target,maskMaterial,folder+"/Control_Backpack.png");Object.DestroyImmediate(bag);
                }
            }
            finally{cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(maskMaterial);}
        }

        private static Quaternion IconRotation(WeaponDataSO weapon)
        {
            if(weapon.name.StartsWith("Weapon_Arsenal_",StringComparison.Ordinal))
            {
                var spec=NewWeapons.FirstOrDefault(w=>weapon.name=="Weapon_Arsenal_"+w.Key);
                if(spec!=null&&spec.IsRanged)
                    return spec.Shape=="Crossbow"?Q(-59,-17,-28):Q(8,-22,-28);
            }
            // Front three-quarter for edged weapons, top three-quarter for the crossbow's limbs.
            if(weapon.name.Contains("Crossbow"))return Q(-59,-17,-28);
            if(weapon.name.Contains("Staff"))return Q(-39,10,-34);
            if(weapon.Type==WeaponType.Melee)return Q(0,10,-34);
            return Q(12,-74,20);
        }

        private static Color32[] ReadStudio(Camera cam,RenderTexture target)
        {
            var request=new RenderPipeline.StandardRequest{destination=target};
            if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
            RenderTexture previous=RenderTexture.active;RenderTexture.active=target;
            var input=new Texture2D(CaptureSize,CaptureSize,TextureFormat.RGB24,false);
            try{input.ReadPixels(new Rect(0,0,CaptureSize,CaptureSize),0,0);input.Apply();return input.GetPixels32();}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(input);}
        }

        private static Sprite RenderItemIcon(GameObject model,Camera cam,RenderTexture target,Material maskMaterial,string path)
        {
            var rs=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            if(rs.Length==0)throw new InvalidOperationException("No visible geometry for "+path);
            Bounds b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
            cam.transform.position=b.center+Vector3.back*6f;cam.transform.rotation=Quaternion.identity;cam.orthographicSize=Mathf.Max(.1f,Mathf.Max(b.size.x,b.size.y)*.61f);

            // Render color and a separate opaque geometry mask. No colour key can erase a
            // valid dark/purple/glowing facet; the exported transparent pixels follow the mesh.
            Material[][] original=rs.Select(r=>r.sharedMaterials).ToArray();
            var studioMaterials=new Dictionary<Material,Material>();
            Color32[] pixels,mask;
            try
            {
                for(int i=0;i<rs.Length;i++)
                {
                    var toned=new Material[original[i].Length];
                    for(int j=0;j<toned.Length;j++)
                    {
                        Material mat=original[i][j];toned[j]=mat;
                        if(mat==null||!mat.HasProperty("_EmissionColor")||mat.GetColor("_EmissionColor").maxColorComponent<=.50f)continue;
                        // Limit emission for the small UI preview only; keep the actual in-game material untouched.
                        if(!studioMaterials.TryGetValue(mat,out var clone))
                        {
                            clone=new Material(mat){hideFlags=HideFlags.DontSave};Color e=mat.GetColor("_EmissionColor");
                            clone.SetColor("_EmissionColor",e*(.50f/e.maxColorComponent));studioMaterials.Add(mat,clone);
                        }
                        toned[j]=clone;
                    }
                    rs[i].sharedMaterials=toned;
                }
                pixels=ReadStudio(cam,target);
                for(int i=0;i<rs.Length;i++)rs[i].sharedMaterials=Enumerable.Repeat(maskMaterial,original[i].Length).ToArray();
                mask=ReadStudio(cam,target);
            }
            finally
            {
                for(int i=0;i<rs.Length;i++)rs[i].sharedMaterials=original[i];
                foreach(var clone in studioMaterials.Values)Object.DestroyImmediate(clone);
            }

            var result=new Color32[IconSize*IconSize];int opaque=0,minX=IconSize,minY=IconSize,maxX=-1,maxY=-1;
            for(int y=0;y<IconSize;y++)for(int x=0;x<IconSize;x++)
            {
                int r=0,g=0,bv=0,n=0;
                for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                {
                    int index=(y*2+dy)*CaptureSize+x*2+dx;
                    if(mask[index].r<128)continue;
                    Color32 c=pixels[index];
                    r+=c.r;g+=c.g;bv+=c.b;n++;
                }
                if(n>0)
                {
                    result[y*IconSize+x]=new Color32((byte)(r/n),(byte)(g/n),(byte)(bv/n),(byte)(n*255/4));opaque++;
                    minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
                }
            }
            if(opaque<96||opaque>IconSize*IconSize*.95f||minX<4||minY<4||maxX>IconSize-5||maxY>IconSize-5)
                throw new InvalidOperationException($"Invalid or cropped item silhouette {path}: {opaque} px, {minX},{minY}–{maxX},{maxY}");
            var texture=new Texture2D(IconSize,IconSize,TextureFormat.RGBA32,false);texture.SetPixels32(result);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            Debug.Log($"ITEM_ICON_COMPLETE {Path.GetFileNameWithoutExtension(path)} opaque={opaque} bounds={minX},{minY}-{maxX},{maxY}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildInventoryModel(Transform t,string id)
        {
            if(TryBuildArsenalInventoryModel(t,id))return;
            if(id.StartsWith("Material_",StringComparison.Ordinal))
            {
                BuildMaterialIconModel(t,id.Substring("Material_".Length));
            }
            else if(id.Contains("Potion"))
            {
                Material liquid=id.Contains("Rage")?amber:red;
                Part(t,"Bottle",plateMesh,liquid,V(0,.39f,0),V(.65f,.65f,.50f));
                Part(t,"BottleNeck",prism,ivory,V(0,.77f,0),V(.2f,.22f,.20f));
                Part(t,"Cork",prism,wood,V(0,.91f,0),V(.25f,.12f,.24f));
                Part(t,"BottleBand",prism,bronze,V(0,.27f,0),V(.67f,.08f,.52f));
                Part(t,"HealerMark",gem,id.Contains("Rage")?flame:ivory,V(0,.47f,-.28f),V(.18f,.22f,.075f));
                if(id.Contains("Large"))Part(t,"Collar",prism,gold,V(0,.66f,0),V(.44f,.08f,.39f));
            }
            else if(id.Contains("Boots"))
            {
                Material material=id.Contains("Spiked")?steel:id.Contains("Wading")?runeTeal:id.Contains("Swift")?cloak:leather;
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

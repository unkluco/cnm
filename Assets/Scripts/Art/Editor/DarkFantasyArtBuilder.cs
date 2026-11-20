using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    /// <summary>Rebuilds the original, repository-native art kit without replacing any gameplay anchors, scripts or colliders.</summary>
    public static partial class DarkFantasyArtBuilder
    {
        private const string ArtRoot = "Assets/Art/DarkFantasy";
        private const string ResourceRoot = "Assets/Resources/Art/DarkFantasy";
        private const string CombatRoot = "Assets/Prefabs/Combat";
        private const string RoomRoot = "Assets/Prefabs/MapGeneration/Rooms";
        private static Mesh box, block, cap, prism, gem, blade, ring, broadRing, cape, cone, helmMesh, chestMesh, plateMesh, limbMesh, fireMesh, plainCube;
        private static Material mortar, floor0, floor1, floor2, floor3, wall0, wall1, wall2, wall3, trim, darkIron, steel, silver, bronze, gold, leather, wood, cloth, cloak, flame, fireOuter, amber, soul, poison, frost, lava, water, red, purple, ivory, landmark, runeTeal, runeViolet, runeGold;
        private static Material[] floorStones, wallStones, volcanicStones, drownedStones, cryptStones, treasuryStones;
        private static readonly HashSet<string> authoredBatches=new HashSet<string>();

        [MenuItem("Light Souls/Art/Rebuild Dark Fantasy art")]
        public static void Build()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            authoredBatches.Clear();
            BuildKit();
            BuildTorchPrefab();
            BuildUiSprites();
            BuildRooms();
            BuildHazards();
            BuildPlayersAndEnemies();
            BuildWeapons();
            BuildChestAndPickups();
            BuildLobbyPrefabs();
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null) BuildItemIcons();
            BuildUiPrefabs();
            BuildScenes();
            PruneOldBatches();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LIGHT_SOULS_DARK_FANTASY_ART_COMPLETE");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');
            string current=parts[0];
            foreach (string part in parts.Skip(1))
            {
                string next=current+"/"+part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,part);
                current=next;
            }
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));
            T previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous != null) { EditorUtility.CopySerialized(asset,previous); Object.DestroyImmediate(asset); EditorUtility.SetDirty(previous); return previous; }
            AssetDatabase.CreateAsset(asset,path);
            return asset;
        }

        private static Mesh MeshAsset(Mesh mesh)
        {
            string path=ResourceRoot+"/Meshes/"+mesh.name+".asset";EnsureFolder(ResourceRoot+"/Meshes");
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            DarkFantasyMeshKit.CopyGeometry(existing,mesh);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
        private static Color C(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out Color c); return c; }
        private static Material Mat(string name, string color, float smooth = .18f, float metal = 0f, string emission = null, float strength = 1f)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name="DF_"+name};
            m.SetColor("_BaseColor",C(color)); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metal);
            if (emission!=null) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",C(emission)*strength); m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            return Save(m,ArtRoot+"/Materials/"+m.name+".mat");
        }

        private static void BuildMeshes()
        {
            box=MeshAsset(DarkFantasyMeshKit.Box("BeveledBox",.09f)); block=MeshAsset(DarkFantasyMeshKit.Box("CutStone",.035f));
            cap=MeshAsset(DarkFantasyMeshKit.Cap("StoneFace",.024f));
            prism=MeshAsset(DarkFantasyMeshKit.Prism("OctagonalPrism",8)); gem=MeshAsset(DarkFantasyMeshKit.Gem("Crystal"));
            cone=MeshAsset(DarkFantasyMeshKit.Prism("TaperedSpire",6,.018f)); blade=MeshAsset(DarkFantasyMeshKit.Blade("ForgedBlade"));
            ring=MeshAsset(DarkFantasyMeshKit.Ring("RuneRing",32,.481f)); broadRing=MeshAsset(DarkFantasyMeshKit.Ring("BroadRing",16,.38f)); cape=MeshAsset(DarkFantasyMeshKit.Cape("ClothMantle"));
            helmMesh=MeshAsset(DarkFantasyMeshKit.Profile("KnightHelm",8,V(.29f,-.5f,.35f),V(.43f,-.30f,.46f),V(.5f,.07f,.49f),V(.42f,.34f,.42f),V(.22f,.5f,.25f)));
            chestMesh=MeshAsset(DarkFantasyMeshKit.Profile("SculptedCuirass",8,V(.34f,-.5f,.4f),V(.39f,-.26f,.44f),V(.49f,.19f,.50f),V(.44f,.38f,.43f),V(.25f,.5f,.3f)));
            plateMesh=MeshAsset(DarkFantasyMeshKit.Profile("RoundedPauldron",8,V(.40f,-.5f,.41f),V(.5f,-.25f,.5f),V(.45f,.16f,.44f),V(.24f,.5f,.27f)));
            limbMesh=MeshAsset(DarkFantasyMeshKit.Profile("TaperedArmor",6,V(.34f,-.5f,.36f),V(.47f,-.19f,.48f),V(.45f,.35f,.47f),V(.36f,.5f,.37f)));
            fireMesh=MeshAsset(DarkFantasyMeshKit.Profile("TorchFlame",5,V(.21f,-.5f,.21f),V(.47f,-.19f,.45f),V(.30f,.12f,.28f),V(.14f,.35f,.11f),V(.018f,.50f,.018f)));
            plainCube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if(plainCube==null)
            {
                var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube); plainCube=primitive.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(primitive);
            }
        }

        private static void BuildKit()
        {
            BuildMeshes();
            mortar=Mat("Mortar","252932"); floor0=Mat("FlagstoneAsh","505460"); floor1=Mat("FlagstoneSlate","4b505b"); floor2=Mat("FlagstoneBlue","565f6c"); floor3=Mat("FlagstoneWeathered","5b5a61");
            wall0=Mat("WallBasalt","303541"); wall1=Mat("MasonryCold","454b59"); wall2=Mat("MasonryAsh","505461"); wall3=Mat("MasonryDark","363d49"); trim=Mat("ArchitectureTrim","686c76",.25f);
            floorStones=new []{floor0,floor1,floor2,floor3}; wallStones=new []{wall1,wall2,wall3};
            volcanicStones=new[]{Mat("VolcanicAsh","514547"),Mat("VolcanicBasalt","3f3c43"),Mat("VolcanicSlate","554846"),Mat("VolcanicWeathered","4b4348")};
            drownedStones=new[]{Mat("DrownedStone","45575c"),Mat("DrownedSlate","384a51"),Mat("DrownedMoss","4d5e60"),Mat("DrownedAsh","46525b")};
            cryptStones=new[]{Mat("CryptStone","494659"),Mat("CryptSlate","414353"),Mat("CryptAsh","55515e"),Mat("CryptWeathered","4b4756")};
            treasuryStones=new[]{Mat("TreasuryStone","5b5447"),Mat("TreasurySlate","4c4945"),Mat("TreasuryAsh","58524c"),Mat("TreasuryWeathered","4d5151")};
            darkIron=Mat("BlackIron","2f3847",.36f,.35f); steel=Mat("TemperedSteel","788b9c",.4f,.4f); silver=Mat("PolishedEdge","c6d5da",.48f,.42f);
            bronze=Mat("AntiqueBronze","936840",.35f,.3f); gold=Mat("RoyalGold","e1b466",.38f,.25f); leather=Mat("Leather","59412f"); wood=Mat("SmokedOak","765039");
            cloth=Mat("InkCloth","222e45"); cloak=Mat("IndigoMantle","2e4e6e"); red=Mat("Oxblood","873f45"); ivory=Mat("BoneIvory","c8c1a8");
            flame=Mat("FlameHeart","ffcc66",.2f,0,"ff962d",2.3f); fireOuter=Mat("FlameTongue","da6834",.18f,0,"ff6c25",1.0f); amber=Mat("EmberRune","b57b3a",.2f,0,"ee9c49",.65f);
            soul=Mat("SoulLight","48d3c9",.23f,.05f,"32bdbb",1.1f); frost=Mat("IceCrystal","87c9ef",.30f,.05f,"5cbcf0",.7f);
            poison=Mat("VenomGem","79bd71",.22f,0,"54c85c",.7f); purple=Mat("VoidCrystal","a77ae2",.23f,0,"9d5bd6",.9f);
            lava=Mat("LavaFlow","9b3820",.23f,0,"ff501a",.68f); water=Mat("DeepWater","39737b",.46f,.1f,"21838d",.18f);
            landmark=Mat("LandmarkInlay","d7adc2",.2f,0,"533b51",.2f);
            runeTeal=Mat("EngravedTeal","648c8c",.24f); runeViolet=Mat("EngravedViolet","77687d",.22f);runeGold=Mat("EngravedGold","93744c",.26f,.05f);
        }

        // Targeted repair: refresh existing mesh assets in place, fix the mislabeled
        // hammer prefab, and recapture icons without regenerating scenes or materials.
        [MenuItem("Light Souls/Art/Repair item meshes and icons")]
        public static void RepairItemArt()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Item icons require a graphics device.");
            string scene=EditorSceneManager.GetActiveScene().path;
            BuildMeshes();BossForgeBuilder.RebuildProfileMeshes();LoadItemMaterials();
            BuildWeapons("Weapon_Sledgehammer");
            // Mesh CopySerialized updates the saved data, but the renderer can retain its
            // previously uploaded buffer until the asset is imported again.
            AssetDatabase.SaveAssets();
            foreach(var meshPath in AssetDatabase.FindAssets("t:Mesh",new[]{ResourceRoot+"/Meshes","Assets/Art/DarkFantasy/Bosses/Meshes"})
                .Select(AssetDatabase.GUIDToAssetPath))
                AssetDatabase.ImportAsset(meshPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            LoadItemMeshes();
            BuildItemIcons();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(scene))EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            Debug.Log("LIGHT_SOULS_ITEM_ART_REPAIR_COMPLETE");
        }

        [MenuItem("Light Souls/Art/Recapture item icons")]
        public static void RecaptureItemIcons()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Item icons require a graphics device.");
            string scene=EditorSceneManager.GetActiveScene().path;
            LoadItemMeshes();LoadItemMaterials();BuildItemIcons();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(scene))EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            Debug.Log("LIGHT_SOULS_ITEM_ICON_RECAPTURE_COMPLETE");
        }

        private static void LoadItemMeshes()
        {
            Mesh Existing(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(ResourceRoot+"/Meshes/"+name+".asset")
                ??throw new InvalidOperationException("Missing authored item mesh: "+name);
            box=Existing("BeveledBox");block=Existing("CutStone");cap=Existing("StoneFace");
            prism=Existing("OctagonalPrism");gem=Existing("Crystal");cone=Existing("TaperedSpire");blade=Existing("ForgedBlade");
            ring=Existing("RuneRing");broadRing=Existing("BroadRing");
            cape=Existing("ClothMantle");helmMesh=Existing("KnightHelm");chestMesh=Existing("SculptedCuirass");
            plateMesh=Existing("RoundedPauldron");limbMesh=Existing("TaperedArmor");fireMesh=Existing("TorchFlame");
        }

        private static void LoadItemMaterials()
        {
            Material Existing(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ArtRoot+"/Materials/DF_"+name+".mat")
                ??throw new InvalidOperationException("Missing authored item material: "+name);
            darkIron=Existing("BlackIron");steel=Existing("TemperedSteel");silver=Existing("PolishedEdge");
            bronze=Existing("AntiqueBronze");gold=Existing("RoyalGold");leather=Existing("Leather");wood=Existing("SmokedOak");
            cloth=Existing("InkCloth");cloak=Existing("IndigoMantle");red=Existing("Oxblood");ivory=Existing("BoneIvory");
            flame=Existing("FlameHeart");fireOuter=Existing("FlameTongue");amber=Existing("EmberRune");
            soul=Existing("SoulLight");frost=Existing("IceCrystal");poison=Existing("VenomGem");purple=Existing("VoidCrystal");runeTeal=Existing("EngravedTeal");
        }

        private static Transform Group(Transform parent,string name,Vector3 position=default,Quaternion rotation=default)
        {
            var go=new GameObject(name); var t=go.transform; t.SetParent(parent,false); t.localPosition=position; t.localRotation=rotation.Equals(default(Quaternion)) ? Quaternion.identity : rotation; return t;
        }

        private static Transform Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale,Quaternion rotation=default,bool shadows=true)
        {
            Transform t=Group(parent,name,position,rotation); t.localScale=scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return t;
        }

        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static Quaternion Q(float x,float y,float z)=>Quaternion.Euler(x,y,z);
        private static Transform Find(Transform root,string name)=>root==null?null:root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        private static void DeleteArt(Transform root,string name)
        {
            var old=root.Find(name); if(old!=null) Object.DestroyImmediate(old.gameObject);
        }
        private static void Restyle(Transform t,Material mat,Mesh mesh=null)
        {
            if(t==null) return;
            var mf=t.GetComponent<MeshFilter>(); if(mf!=null && mesh!=null) mf.sharedMesh=mesh;
            var mr=t.GetComponent<MeshRenderer>(); if(mr!=null) mr.sharedMaterial=mat;
        }
        private static void Restyle(Transform root,string name,Material mat,Mesh mesh=null)=>Restyle(Find(root,name),mat,mesh);
        private static void EditPrefab(string path,Action<Transform> action)
        {
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try { action(root.transform); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static string[] Prefabs(string folder)=>AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();

        private static void PruneOldBatches()
        {
            string folder=ArtRoot+"/BatchedMeshes";
            if(!AssetDatabase.IsValidFolder(folder)) return;
            // Only old generated meshes without references in any authored prefab or scene are removed.
            var owners=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}).Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets"})).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var used=new HashSet<string>(AssetDatabase.GetDependencies(owners,true));
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(path.StartsWith(folder+"/",StringComparison.Ordinal)&&!authoredBatches.Contains(path)&&!used.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        // Combines static decoration by material; one renderer per material instead of one per tile/brick.
        private sealed class MasonryBatch
        {
            private readonly Dictionary<Material,List<CombineInstance>> parts=new Dictionary<Material,List<CombineInstance>>();
            public void Add(Mesh mesh,Material mat,Matrix4x4 transform)
            {
                if(!parts.TryGetValue(mat,out var list)) parts[mat]=list=new List<CombineInstance>();
                list.Add(new CombineInstance {mesh=mesh,transform=transform});
            }
            public void Add(Mesh mesh,Material mat,Vector3 pos,Vector3 scale,Quaternion rot=default)=>Add(mesh,mat,Matrix4x4.TRS(pos,rot.Equals(default(Quaternion))?Quaternion.identity:rot,scale));
            public void Finish(Transform parent,string group,string key,bool shadows)
            {
                DeleteArt(parent,group); Transform holder=Group(parent,group);
                foreach(var entry in parts)
                {
                    string name=key+"_"+entry.Key.name;
                    var mesh=new Mesh{name=name,indexFormat=entry.Value.Sum(p=>p.mesh.vertexCount)<=65535?IndexFormat.UInt16:IndexFormat.UInt32}; mesh.CombineMeshes(entry.Value.ToArray(),true,true);
                    string path=ArtRoot+"/BatchedMeshes/"+name+".asset";authoredBatches.Add(path);
                    mesh=Save(mesh,path);
                    Part(holder,entry.Key.name,mesh,entry.Key,Vector3.zero,Vector3.one,shadows:shadows);
                }
            }
        }
    }
}

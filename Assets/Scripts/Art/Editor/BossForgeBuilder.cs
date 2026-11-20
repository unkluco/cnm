using System;
using System.Collections.Generic;
using LightSouls.Combat;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    /// <summary>Authors five original, low-poly silhouettes. Run with Unity CLI or the editor menu.</summary>
    public static class BossForgeBuilder
    {
        public const string PrefabRoot="Assets/Resources/Combat/Bosses";
        private const string MeshRoot="Assets/Resources/Art/DarkFantasy/Meshes/";
        private const string MatRoot="Assets/Art/DarkFantasy/Materials/DF_";
        private const string BossMeshRoot="Assets/Art/DarkFantasy/Bosses/Meshes";
        private static Mesh box,stone,plate,limb,helm,cuirass,gem,prism,spire,blade,cape,ring,robe,wingLeft,wingRight,boulder,muzzle;
        private static Material iron,steel,bronze,gold,bone,ember,fire,hide,ice,frost,stoneDark,stoneLight,voidLight,spectral,ink,storm,stormSkin,wing;

        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static Quaternion Q(float x,float y,float z)=>Quaternion.Euler(x,y,z);
        private static Mesh Mesh(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot+name+".asset")??throw new InvalidOperationException("Missing art mesh: "+name);
        private static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(MatRoot+name+".mat")??throw new InvalidOperationException("Missing art material: "+name);

        [MenuItem("Light Souls/Art/Build giant boss roster")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring bosses.");
            EnsureFolder(PrefabRoot);EnsureFolder(BossMeshRoot);
            box=Mesh("BeveledBox");stone=Mesh("CutStone");plate=Mesh("RoundedPauldron");limb=Mesh("TaperedArmor");
            helm=Mesh("KnightHelm");cuirass=Mesh("SculptedCuirass");gem=Mesh("Crystal");prism=Mesh("OctagonalPrism");
            spire=Mesh("TaperedSpire");blade=Mesh("ForgedBlade");cape=Mesh("ClothMantle");ring=Mesh("BroadRing");
            iron=Mat("BlackIron");steel=Mat("TemperedSteel");bronze=Mat("AntiqueBronze");gold=Mat("RoyalGold");bone=Mat("BoneIvory");
            ember=BossMaterial("Magma",new Color(.91f,.17f,.055f),new Color(1f,.14f,.025f)*.82f);
            fire=BossMaterial("CinderHeart",new Color(1f,.48f,.08f),new Color(1f,.26f,.025f)*1.18f);
            hide=Mat("EmberHide");ice=Mat("IceCrystal");frost=Mat("FrostRobes");
            stoneDark=Mat("MasonryDark");stoneLight=Mat("ArchitectureTrim");voidLight=Mat("VoidCrystal");spectral=Mat("SpectralCloth");ink=Mat("InkCloth");
            storm=BossMaterial("ThunderRune",new Color(.92f,.70f,.16f),new Color(1f,.73f,.13f)*.70f);
            stormSkin=Mat("HunterCloth");wing=BossMaterial("StormMembrane",new Color(.19f,.26f,.39f),Color.black);
            robe=SaveMesh(DarkFantasyMeshKit.Profile("BossRobe",10,V(.48f,-.5f,.45f),V(.34f,-.15f,.32f),V(.24f,.29f,.25f),V(.28f,.5f,.26f)));
            boulder=SaveMesh(DarkFantasyMeshKit.Profile("BossBasaltBoulder",7,V(.32f,-.5f,.35f),V(.48f,-.23f,.45f),V(.50f,.10f,.5f),V(.41f,.35f,.42f),V(.23f,.5f,.29f)));
            muzzle=SaveMesh(DarkFantasyMeshKit.Profile("BossDrakeMuzzle",6,V(.46f,-.5f,.43f),V(.49f,-.25f,.43f),V(.39f,.22f,.35f),V(.27f,.5f,.28f)));
            wingLeft=SaveMesh(DragonWing(-1));wingRight=SaveMesh(DragonWing(1));
            foreach(var definition in BossRoster.All)BuildPrefab(definition);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("LIGHT_SOULS_GIANT_BOSS_ROSTER_COMPLETE");
        }

        private static void EnsureFolder(string path)
        {
            string[] bits=path.Split('/');string current=bits[0];
            for(int i=1;i<bits.Length;i++){string next=current+"/"+bits[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,bits[i]);current=next;}
        }
        private static Mesh SaveMesh(Mesh mesh)
        {
            string path=BossMeshRoot+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old!=null){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        private static Material BossMaterial(string name,Color bodyColor,Color emission)
        {
            string folder="Assets/Art/DarkFantasy/Bosses/Materials";EnsureFolder(folder);
            string path=folder+"/DF_Boss"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="DF_Boss"+name};AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",bodyColor);mat.SetFloat("_Smoothness",.28f);mat.SetFloat("_Metallic",.10f);
            mat.SetColor("_EmissionColor",emission);
            if(emission.maxColorComponent>.01f)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);return mat;
        }

        private static Transform Joint(Transform parent,string name,Vector3 pos)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}

        private static Transform Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 pos,Vector3 scale,Quaternion rot=default,bool shadows=true)
        {
            var t=Joint(parent,name,pos);t.localScale=scale;t.localRotation=rot==default?Quaternion.identity:rot;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;return t;
        }

        private static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat,Mesh shape=null)
        {
            Vector3 d=b-a;Part(parent,name,shape??limb,mat,(a+b)*.5f,V(width,d.magnitude,width),Quaternion.FromToRotation(Vector3.up,d.normalized));
        }

        private sealed class Rig
        {
            public Transform Root,Body,L,R,LegL,LegR;
        }

        private static void BuildPrefab(BossDefinition def)
        {
            var root=new GameObject("Boss_"+def.Kind);root.tag="Enemy";root.layer=7;
            try
            {
                var health=root.AddComponent<HealthComponent>();var hp=new SerializedObject(health);
                hp.FindProperty("maxHealth").floatValue=def.Health;hp.FindProperty("currentHealth").floatValue=def.Health;hp.ApplyModifiedPropertiesWithoutUndo();
                var collider=root.AddComponent<CapsuleCollider>();collider.height=def.Height;collider.radius=def.Radius;collider.center=V(0,def.Height*.5f,0);
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=def.Radius;agent.height=def.Height;agent.speed=def.Speed;agent.acceleration=9;agent.angularSpeed=220;agent.stoppingDistance=def.PreferredDistance;
                var boss=root.AddComponent<BossEnemy>();var so=new SerializedObject(boss);so.FindProperty("kind").enumValueIndex=(int)def.Kind;so.FindProperty("enemyName").stringValue=def.Name;so.ApplyModifiedPropertiesWithoutUndo();
                Rig rig=def.Kind switch
                {
                    BossKind.Varkhan=>MakeVarkhan(root.transform),BossKind.Nhalis=>MakeNhalis(root.transform),
                    BossKind.Orgrim=>MakeOrgrim(root.transform),BossKind.Noctyr=>MakeNoctyr(root.transform),_=>MakeAurex(root.transform)
                };
                boss.SetArtRig(rig.Root,rig.Body,rig.L,rig.R,rig.LegL,rig.LegR);
                var canvas=new GameObject("WorldHealthBarCanvas",typeof(RectTransform),typeof(Canvas),typeof(EnemyWorldHealthBar));
                canvas.transform.SetParent(root.transform,false);canvas.transform.localPosition=Vector3.up*(def.Height+.36f);canvas.transform.localScale=Vector3.one*.01f;
                canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                var bar=new SerializedObject(canvas.GetComponent<EnemyWorldHealthBar>());
                bar.FindProperty("healthComponent").objectReferenceValue=health;
                bar.FindProperty("popupPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/Combat_DamagePopup.prefab");
                bar.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabRoot+"/"+root.name+".prefab");
            }
            finally {Object.DestroyImmediate(root);}
        }

        private static Rig Humanoid(Transform root,float torsoY,float shoulderX,float shoulderY,float legX,float legY,bool legs)
        {
            var rig=new Rig{Root=Joint(root,"Art_BossSilhouette",Vector3.zero)};
            rig.Body=Joint(rig.Root,"Torso",V(0,torsoY,0));rig.L=Joint(rig.Root,"LeftArm",V(-shoulderX,shoulderY,0));rig.R=Joint(rig.Root,"RightArm",V(shoulderX,shoulderY,0));
            if(legs){rig.LegL=Joint(rig.Root,"LeftLeg",V(-legX,legY,0));rig.LegR=Joint(rig.Root,"RightLeg",V(legX,legY,0));}
            return rig;
        }

        private static void Eyes(Transform head,float y,float depth,float spacing,Material glow,float size=.20f)
        {
            Part(head,"Visor",box,iron,V(0,y,depth-.035f),V(spacing*2.8f,size*1.0f,.13f));
            for(int s=-1;s<=1;s+=2)Part(head,"EyeGlow",box,glow,V(s*spacing,y,depth+.05f),V(size*1.5f,size*.45f,.045f),Q(0,0,-s*8),false);
        }

        private static Rig MakeVarkhan(Transform root)
        {
            Rig r=Humanoid(root,2.50f,1.12f,3.05f,.53f,1.45f,true);
            Part(r.Body,"ObsidianCuirass",cuirass,iron,V(0,.0f,0),V(1.9f,1.72f,1.43f));
            Part(r.Body,"MoltenBreastplate",cuirass,hide,V(0,.13f,.48f),V(1.51f,1.28f,.55f));
            Part(r.Body,"BurningCoreSetting",gem,bronze,V(0,.18f,.81f),V(.62f,.77f,.22f));
            Part(r.Body,"BurningCore",gem,fire,V(0,.20f,.97f),V(.36f,.52f,.14f),default,false);
            for(int s=-1;s<=1;s+=2)
            {
                for(int i=0;i<3;i++)Part(r.Body,"MoltenRib",box,ember,V(s*(.27f+i*.16f),-.19f+i*.22f,.80f),V(.34f,.065f,.08f),Q(0,0,s*27),false);
                Part(r.Body,"HipGuard",limb,iron,V(s*.58f,-.80f,0),V(.66f,.70f,.94f),Q(0,0,-s*10));
            }
            Part(r.Body,"SootCape",cape,hide,V(0,-.30f,-.65f),V(2.1f,2.36f,.8f));
            var head=Joint(r.Body,"CinderHelm",V(0,1.20f,0));Part(head,"Helm",helm,iron,Vector3.zero,V(1.04f,1.04f,.95f));
            Part(head,"Faceplate",helm,bronze,V(0,-.15f,.28f),V(.81f,.67f,.55f));Eyes(head,.13f,.47f,.23f,fire,.22f);
            foreach(int s in new[]{-1,1})
            {
                Beam(head,"Horns",V(s*.40f,.25f,0),V(s*.94f,.92f,-.03f),.26f,bronze,spire);
                Beam(head,"FlameHorn",V(s*.68f,.57f,0),V(s*.89f,.93f,0),.13f,ember,spire);
                Transform arm=s<0?r.L:r.R,leg=s<0?r.LegL:r.LegR;
                Part(arm,"HeavyPauldrons",plate,iron,V(0,.03f,0),V(1.25f,.86f,1.30f));
                Part(arm,"BronzeRim",prism,bronze,V(0,-.28f,0),V(1.08f,.12f,1.20f));
                Beam(arm,"ShoulderSpike",V(s*.31f,.26f,0),V(s*.72f,.84f,-.04f),.34f,ember,spire);
                Part(arm,"Forearm",limb,hide,V(0,-.72f,.06f),V(.76f,1.12f,.81f));
                Part(arm,"Fist",box,iron,V(0,-1.35f,.06f),V(.77f,.63f,.79f));
                Part(leg,"Shin",limb,iron,V(0,-.65f,0),V(.82f,1.34f,.88f));
                Part(leg,"Knee",plate,bronze,V(0,-.15f,.36f),V(.73f,.45f,.51f));
                Part(leg,"Sabaton",box,iron,V(0,-1.21f,.23f),V(.93f,.45f,1.36f));
                Part(leg,"HeatInlay",gem,ember,V(0,-.70f,.44f),V(.23f,.82f,.13f),default,false);
            }
            var sword=Joint(r.R,"CinderGreatsword",V(0,-1.38f,.20f));sword.localRotation=Q(18,0,-16);
            Part(sword,"Hilt",prism,bronze,V(0,-.14f,0),V(.23f,.88f,.24f));Part(sword,"Crossguard",box,bronze,V(0,.26f,0),V(1.15f,.20f,.31f));
            Part(sword,"ForgedBlade",blade,iron,V(0,1.67f,0),V(.88f,2.72f,.38f));
            Part(sword,"MoltenEdge",blade,ember,V(0,1.67f,.13f),V(.55f,2.58f,.15f),default,false);
            return r;
        }

        private static Rig MakeNhalis(Transform root)
        {
            Rig r=Humanoid(root,2.54f,.78f,2.94f,0,0,false);
            Part(r.Root,"FrozenGown",robe,frost,V(0,1.02f,0),V(2.35f,1.82f,1.65f));
            Part(r.Root,"FrozenHem",prism,ice,V(0,.20f,0),V(2.20f,.17f,1.58f),default,false);
            Part(r.Body,"CrystalArmor",cuirass,steel,V(0,-.03f,0),V(1.25f,1.36f,1.0f));
            Part(r.Body,"DiamondBodice",gem,ice,V(0,-.09f,.57f),V(.56f,1.05f,.23f),default,false);
            Part(r.Body,"Collar",plate,steel,V(0,.46f,0),V(1.58f,.40f,.98f));
            Part(r.Body,"GlacialMantle",cape,frost,V(0,-.33f,-.55f),V(2.30f,2.7f,1.10f));
            var head=Joint(r.Body,"IceCrown",V(0,1.02f,0));Part(head,"Cowl",helm,frost,Vector3.zero,V(.78f,.86f,.77f));
            Part(head,"Mask",helm,bone,V(0,-.06f,.19f),V(.59f,.70f,.59f));Eyes(head,.1f,.39f,.16f,ice,.16f);
            Part(head,"Diadem",prism,steel,V(0,.37f,0),V(.88f,.20f,.88f));
            for(int i=-2;i<=2;i++)Part(head,"CrownCrystal",gem,ice,V(i*.21f,.68f+(.22f-Mathf.Abs(i)*.08f),Mathf.Abs(i)*-.04f),V(.25f,.74f-Mathf.Abs(i)*.14f,.32f),Q(0,0,-i*13),false);
            foreach(int s in new[]{-1,1})
            {
                Transform arm=s<0?r.L:r.R;
                Part(arm,"IcePauldron",gem,ice,V(s*.08f,.16f,0),V(.70f,1.05f,.67f),Q(0,0,-s*37),false);
                Part(arm,"SilkSleeve",limb,frost,V(0,-.56f,0),V(.48f,1.02f,.48f));
                Part(arm,"SilverCuff",prism,steel,V(0,-1.06f,.02f),V(.51f,.24f,.51f));
                Part(arm,"Fingers",box,bone,V(0,-1.23f,.05f),V(.32f,.34f,.32f));
                for(int i=0;i<3;i++)Part(r.Root,"FloatingIceShard",gem,ice,V(s*(1.09f+i*.37f),2.10f+i*.64f,-.42f),V(.35f,.76f+i*.12f,.34f),Q(9,s*15,s*(16+i*13)),false);
            }
            var staff=Joint(r.R,"SceptreOfWinter",V(0,-1.15f,.18f));staff.localRotation=Q(12,0,-13);
            Part(staff,"Shaft",prism,steel,V(0,.35f,0),V(.16f,2.84f,.16f));
            Part(staff,"IcePrism",gem,ice,V(0,1.96f,0),V(.74f,1.36f,.71f),default,false);
            Part(staff,"CrownRing",ring,bronze,V(0,1.58f,0),V(1.05f,.12f,1.05f));
            for(int s=-1;s<=1;s+=2)Beam(staff,"Prong",V(s*.36f,1.40f,0),V(s*.43f,2.23f,0),.12f,steel,spire);
            return r;
        }

        private static Rig MakeOrgrim(Transform root)
        {
            Rig r=Humanoid(root,2.41f,1.43f,2.86f,.69f,1.35f,true);
            Part(r.Body,"BasaltCore",cuirass,stoneDark,V(0,.0f,0),V(2.44f,1.96f,1.69f));
            Part(r.Body,"ChestMonolith",stone,stoneLight,V(0,.08f,.51f),V(1.96f,1.58f,.73f));
            Part(r.Body,"RuneSocket",gem,iron,V(0,.08f,.92f),V(1.0f,1.06f,.21f));
            Part(r.Body,"RuneCore",gem,storm,V(0,.08f,1.07f),V(.68f,.81f,.16f),default,false);
            for(int s=-1;s<=1;s+=2)
            {
                Part(r.Body,"BrokenTablet",stone,stoneDark,V(s*.73f,-.63f,.54f),V(.8f,.66f,.43f),Q(0,0,s*18));
                Part(r.Body,"GoldFault",box,storm,V(s*.52f,.43f,.93f),V(.75f,.08f,.09f),Q(0,0,s*32),false);
                Part(r.Body,"BackSpine",spire,stoneLight,V(s*.69f,.73f,-.58f),V(.62f,1.24f,.72f),Q(-24,0,-s*13));
            }
            var head=Joint(r.Body,"StoneMask",V(0,1.20f,.05f));Part(head,"AncientMask",stone,stoneLight,Vector3.zero,V(1.08f,1.03f,.98f));
            Eyes(head,.10f,.49f,.25f,storm,.24f);Part(head,"Crest",gem,storm,V(0,.39f,.42f),V(.24f,.43f,.14f),default,false);
            foreach(int s in new[]{-1,1})
            {
                Transform arm=s<0?r.L:r.R,leg=s<0?r.LegL:r.LegR;
                Part(arm,"BoulderShoulder",boulder,stoneDark,V(s*.12f,.15f,0),V(1.50f,1.38f,1.49f),Q(7,13,s*19));
                Part(arm,"GraniteCollar",gem,stoneLight,V(s*.25f,.47f,.06f),V(1.17f,.92f,1.16f),Q(-8,s*18,-s*18));
                Part(arm,"RuneFault",gem,storm,V(s*.17f,.13f,.67f),V(.39f,.62f,.18f),Q(0,0,-s*24),false);
                Part(arm,"StoneForearm",boulder,stoneLight,V(s*.08f,-.64f,.05f),V(1.03f,1.13f,1.05f),Q(-6,9,0));
                Part(arm,"TitanFist",box,stoneDark,V(s*.13f,-1.32f,.12f),V(1.18f,.87f,1.18f));
                for(int i=0;i<3;i++)Part(arm,"Knuckle",stone,stoneLight,V(-.36f+i*.36f+s*.13f,-1.54f,.60f),V(.31f,.43f,.42f));
                Part(arm,"RunicBracelet",prism,storm,V(0,-.99f,.05f),V(1.14f,.14f,1.14f),default,false);
                Part(leg,"BoulderLeg",boulder,stoneDark,V(0,-.54f,0),V(1.09f,1.50f,1.09f));
                Part(leg,"Heel",stone,stoneLight,V(0,-1.14f,.24f),V(1.17f,.37f,1.40f));
                Part(leg,"KneeTablet",box,stoneLight,V(0,-.2f,.55f),V(.79f,.66f,.35f));
            }
            return r;
        }

        private static Rig MakeNoctyr(Transform root)
        {
            Rig r=Humanoid(root,2.53f,1.27f,2.92f,0,0,false);
            Part(r.Root,"FloatingLowerMantle",robe,ink,V(0,1.06f,0),V(2.12f,2.0f,1.60f));
            Part(r.Body,"ShadowTorso",cuirass,spectral,Vector3.zero,V(1.61f,1.55f,1.18f));
            Part(r.Body,"EmptyHeart",gem,ink,V(0,.03f,.46f),V(.96f,1.1f,.45f));
            Part(r.Body,"VoidCore",gem,voidLight,V(0,.06f,.76f),V(.47f,.74f,.32f),default,false);
            Part(r.Body,"AstralRing",ring,bronze,V(0,.10f,.58f),V(1.34f,.13f,1.34f),Q(90,0,0));
            Part(r.Body,"TornMantle",cape,spectral,V(0,-.30f,-.56f),V(2.65f,3.16f,.9f));
            var head=Joint(r.Body,"CrownOfNothing",V(0,1.28f,0));Part(head,"HollowHelm",helm,ink,Vector3.zero,V(.85f,.96f,.87f));
            Part(head,"GoldenMask",helm,bronze,V(0,-.12f,.26f),V(.72f,.69f,.55f));Eyes(head,.10f,.46f,.19f,voidLight,.21f);
            Part(head,"Halo",ring,voidLight,V(0,.22f,-.35f),V(1.50f,.13f,1.50f),Q(90,0,0),false);
            for(int s=-1;s<=1;s+=2)
            {
                Beam(head,"HollowCrown",V(s*.29f,.33f,0),V(s*.73f,.89f,0),.23f,bronze,spire);
                Transform arm=s<0?r.L:r.R;
                Part(arm,"FloatingShoulder",gem,ink,V(s*.12f,.24f,0),V(1.03f,1.08f,.96f),Q(0,0,-s*32));
                Part(arm,"InscribedShoulder",gem,voidLight,V(s*.12f,.23f,.42f),V(.38f,.69f,.21f),Q(0,0,-s*32),false);
                Part(arm,"SpectralBracer",limb,spectral,V(s*.14f,-.70f,.06f),V(.58f,1.06f,.62f));
                Part(arm,"FloatingHand",box,bronze,V(s*.14f,-1.37f,.07f),V(.59f,.59f,.59f));
                var sickle=Joint(arm,"VoidBlade",V(s*.15f,-1.46f,.26f));sickle.localRotation=Q(20,0,-s*35);
                Part(sickle,"DarkEdge",blade,iron,V(0,.81f,0),V(.61f,1.96f,.34f));
                Part(sickle,"AstralEdge",blade,voidLight,V(0,.91f,.13f),V(.38f,1.76f,.18f),default,false);
                for(int i=0;i<3;i++)Part(r.Root,"FracturedOrbit",gem,voidLight,V(s*(1.17f+i*.39f),.85f+i*.97f,-.46f),V(.25f,.60f,.27f),Q(0,0,s*(25+i*9)),false);
            }
            return r;
        }

        private static Rig MakeAurex(Transform root)
        {
            var r=new Rig{Root=Joint(root,"Art_BossSilhouette",Vector3.zero)};
            r.Body=Joint(r.Root,"DrakeBody",V(0,1.52f,.0f));
            Part(r.Body,"DrakeChest",cuirass,stormSkin,V(0,.37f,.40f),V(2.06f,2.26f,1.77f),Q(31,0,0));
            Part(r.Body,"Belly",cuirass,bronze,V(0,.27f,.90f),V(1.52f,1.62f,.99f),Q(30,0,0));
            Part(r.Body,"Haunch",cuirass,iron,V(0,-.08f,-.78f),V(2.19f,1.60f,2.08f),Q(90,0,0));
            Beam(r.Body,"LongNeck",V(0,.53f,.68f),V(0,1.72f,1.25f),1.0f,stormSkin);
            Part(r.Body,"ThroatScale",gem,storm,V(0,1.02f,1.23f),V(.49f,.92f,.22f),Q(-20,0,0),false);
            var head=Joint(r.Body,"DrakeHead",V(0,1.80f,1.47f));
            Part(head,"Skull",helm,stormSkin,V(0,.05f,.12f),V(1.34f,1.02f,1.27f),Q(22,0,0));
            Part(head,"Snout",muzzle,stormSkin,V(0,-.13f,.76f),V(1.02f,1.30f,.76f),Q(90,0,0));
            Part(head,"MouthShadow",muzzle,iron,V(0,-.34f,.80f),V(.92f,1.18f,.25f),Q(90,0,0));
            Part(head,"Jaw",muzzle,bronze,V(0,-.50f,.78f),V(.89f,1.14f,.32f),Q(98,0,0));
            for(int s=-1;s<=1;s+=2)
            {
                Part(head,"EyeRidge",box,iron,V(s*.45f,.27f,.53f),V(.44f,.19f,.52f),Q(0,s*21,-s*16));
                Part(head,"LuminousEye",gem,storm,V(s*.46f,.13f,.72f),V(.25f,.19f,.20f),Q(0,0,s*22),false);
                Beam(head,"CrownHorn",V(s*.42f,.28f,-.07f),V(s*.87f,1.02f,-.52f),.28f,gold,spire);
                for(int i=0;i<3;i++)Beam(head,"Fang",V(s*(.36f-.04f*i),-.25f,.62f+i*.28f),V(s*(.36f-.04f*i),-.45f-(i==2?.06f:0),.64f+i*.28f),.12f,bone,spire);
            }
            Part(head,"CrownGem",gem,storm,V(0,.58f,.18f),V(.32f,.68f,.31f),Q(-22,0,0),false);
            for(int i=0;i<5;i++)Part(r.Body,"DorsalSpine",spire,gold,V(0,.81f-i*.08f,-.42f-i*.35f),V(.34f,.58f,.45f),Q(-19,0,0));
            foreach(int s in new[]{-1,1})
            {
                Transform wingRoot=Joint(r.Root,s<0?"Wing_L":"Wing_R",V(s*.78f,2.40f,-.36f));
                wingRoot.localScale=V(1.12f,1,1.28f);
                if(s<0)r.L=wingRoot;else r.R=wingRoot;
                Part(wingRoot,"FacetedMembrane",s<0?wingLeft:wingRight,wing,Vector3.zero,Vector3.one);
                Vector3 shoulder=Vector3.zero,elbow=V(s*.84f,.50f,.18f),tip=V(s*2.92f,.80f,.05f),tip2=V(s*2.47f,.06f,-1.51f),tip3=V(s*1.48f,-.20f,-1.69f);
                Beam(wingRoot,"WingArm",shoulder,elbow,.32f,stormSkin);Beam(wingRoot,"LeadingEdge",elbow,tip,.21f,gold);
                Beam(wingRoot,"WingRib",elbow,tip2,.15f,bronze);Beam(wingRoot,"WingRib",elbow,tip3,.14f,bronze);
                foreach(Vector3 p in new[]{tip,tip2,tip3})Part(wingRoot,"WingTalon",gem,storm,p,V(.19f,.40f,.18f),Q(0,0,-s*38),false);
                Transform leg=Joint(r.Root,s<0?"LeftLeg":"RightLeg",V(s*.80f,1.18f,.66f));if(s<0)r.LegL=leg;else r.LegR=leg;
                Part(leg,"Foreleg",limb,stormSkin,V(0,-.38f,.12f),V(.69f,.98f,.76f),Q(-12,0,0));
                Part(leg,"ClawFoot",box,iron,V(0,-1.02f,.29f),V(.85f,.32f,1.12f));
                for(int i=-1;i<=1;i++)Part(leg,"Talons",spire,bone,V(i*.24f,-1.02f,.87f),V(.18f,.52f,.22f),Q(91,0,0));
                Transform rear=Joint(r.Root,"RearLeg",V(s*.89f,.98f,-1.14f));
                Part(rear,"HindQuarter",plate,stormSkin,V(0,-.17f,0),V(.94f,1.05f,1.02f),Q(18,0,0));
                Part(rear,"HindFoot",box,iron,V(0,-.79f,.18f),V(.84f,.31f,1.04f));
            }
            Vector3[] tail={V(0,1.42f,-1.38f),V(.06f,1.12f,-2.38f),V(.28f,.79f,-3.19f),V(.7f,.55f,-4.0f),V(1.14f,.88f,-4.63f)};
            for(int i=0;i<tail.Length-1;i++)
            {
                Beam(r.Root,"SegmentedTail",tail[i],tail[i+1],.82f-i*.18f,i%2==0?stormSkin:iron);
                Part(r.Root,"TailSpine",spire,gold,tail[i]+V(0,.38f-i*.05f,0),V(.28f-i*.035f,.48f-i*.05f,.29f),Q(-25,0,0));
            }
            Part(r.Root,"TailLightning",gem,storm,tail[tail.Length-1],V(.34f,.73f,.32f),Q(0,0,-40),false);
            return r;
        }

        private static Mesh DragonWing(int s)
        {
            // Triangular facets with a raised spar and scalloped trailing edge; duplicated reverse faces are visible from below.
            Vector3[] p={V(0,0,0),V(s*.84f,.50f,.18f),V(s*2.92f,.80f,.05f),V(s*2.55f,.19f,-.83f),
                V(s*2.47f,.06f,-1.51f),V(s*1.88f,.15f,-.95f),V(s*1.48f,-.20f,-1.69f),V(s*.91f,-.12f,-1.02f),V(s*.15f,-.28f,-.99f)};
            int[] faces={0,1,8,1,7,8,1,6,7,1,5,6,1,4,5,1,3,4,1,2,3};
            var vertices=new List<Vector3>();var triangles=new List<int>();var normals=new List<Vector3>();
            for(int i=0;i<faces.Length;i+=3)
            {
                Vector3 a=p[faces[i]],b=p[faces[i+1]],c=p[faces[i+2]];
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                int start=vertices.Count;vertices.AddRange(new[]{a,b,c,a,c,b});normals.AddRange(new[]{n,n,n,-n,-n,-n});
                for(int j=0;j<6;j++)triangles.Add(start+j);
            }
            var mesh=new Mesh{name=s<0?"BossDragonWing_L":"BossDragonWing_R"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}

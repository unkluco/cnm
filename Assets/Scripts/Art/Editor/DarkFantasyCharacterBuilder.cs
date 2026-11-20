using System;
using System.Linq;
using LightSouls.Combat.Weapons;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private static void BuildPlayersAndEnemies()
        {
            EditPrefab(CombatRoot+"/Player_Character.prefab",StyleHero);
            Material deadSkin=Mat("RevenantSkin","91a49a"), goblinSkin=Mat("GoblinSkin","79945d"), rusty=Mat("RustArmor","796054",.24f,.15f);
            Material demon=Mat("EmberHide","80504b"), ice=Mat("FrostRobes","426d89"), specter=Mat("SpectralCloth","476b7f"), robes=Mat("CultistRobes","62394f"), priest=Mat("PriestVestment","bba782"), green=Mat("HunterCloth","465e53");
            foreach(string path in Prefabs(CombatRoot).Where(p=>System.IO.Path.GetFileName(p).StartsWith("Enemy_")&&!p.Contains("Projectile"))) EditPrefab(path,root=>
            {
                Material armor=rusty,skin=deadSkin,accent=amber;
                string n=root.name;
                if(n.Contains("Goblin")){armor=green;skin=goblinSkin;accent=poison;}
                else if(n.Contains("Cultist")){armor=robes;skin=cloth;accent=purple;}
                else if(n.Contains("Charger")){armor=red;skin=demon;accent=flame;}
                else if(n.Contains("StoneGolem")){armor=wall2;skin=wall1;accent=amber;}
                else if(n.Contains("ShieldGuard")){armor=darkIron;skin=steel;accent=gold;}
                else if(n.Contains("EmberMage")){armor=demon;skin=rusty;accent=flame;}
                else if(n.Contains("FrostMage")){armor=ice;skin=steel;accent=frost;}
                else if(n.Contains("StormArcher")){armor=green;skin=deadSkin;accent=soul;}
                else if(n.Contains("BloodLeech")){armor=red;skin=robes;accent=flame;}
                else if(n.Contains("Phantom")){armor=specter;skin=ice;accent=soul;}
                else if(n.Contains("LightPriest")){armor=priest;skin=ivory;accent=gold;}
                else if(n.Contains("BombWisp")){armor=darkIron;skin=demon;accent=flame;}

                Transform rig=Find(root,"Visual_Zombie")??Find(root,"Visual_Goblin")??Find(root,"Visual_Cultist")??Find(root,"Visual");
                if(rig==null) return;
                if(n.Contains("PracticeDummy")){Restyle(rig,wood,box);return;}
                var head=Find(rig,"Head")??Find(rig,"Hood"); var torso=Find(rig,"Torso")??Find(rig,"Robe");
                bool caster=n.Contains("Mage")||n.Contains("Priest")||n.Contains("Cultist"), armored=n.Contains("Golem")||n.Contains("Guard")||n.Contains("Charger");
                Restyle(head,skin,helmMesh); Restyle(torso,armor,caster?prism:chestMesh);
                foreach(string limb in new[]{"LeftArm","RightArm","LeftLeg","RightLeg"})
                {
                    var bone=Find(rig,limb); if(bone==null) continue;
                    Restyle(bone,limb.Contains("Leg")?armor:skin,limbMesh);
                    DeleteArt(bone,"Art_Armor"); var a=Group(bone,"Art_Armor");
                    if(limb.Contains("Leg"))
                    {
                        Part(a,"Boot",box,leather,V(0,-.35f,.13f),V(1.15f,.32f,1.35f));
                        if(armored) Part(a,"Greave",box,armor,V(0,.03f,.46f),V(.87f,.43f,.24f));
                    }
                    else
                    {
                        Part(a,"Shoulder",plateMesh,armor,V(0,.36f,0),V(armored?1.8f:1.25f,.38f,1.4f));
                        Part(a,"Bracer",limbMesh,armored?darkIron:leather,V(0,-.21f,0),V(1.13f,.31f,1.16f));
                    }
                }
                if(head!=null)
                {
                    DeleteArt(head,"Art_Face"); var a=Group(head,"Art_Face");
                    var eyes=Find(head,"Eyes"); if(eyes!=null) Restyle(eyes,darkIron,box);
                    foreach(int s in new[]{-1,1}) Part(a,"Eye",box,accent,V(s*.22f,.05f,.57f),V(.23f,.16f,.09f),shadows:false);
                    if(caster||armored) Part(a,"Brow",box,armor,V(0,.31f,.37f),V(1.08f,.24f,.42f));
                    else Part(a,"Jaw",box,skin,V(0,-.36f,.26f),V(.72f,.31f,.58f));
                    if(n.Contains("Goblin")) {Restyle(rig,"LeftEar",skin,cone);Restyle(rig,"RightEar",skin,cone);}
                }
                if(torso!=null)
                {
                    DeleteArt(torso,"Art_Vestment"); var a=Group(torso,"Art_Vestment");
                    Part(a,"Belt",box,leather,V(0,-.37f,0),V(1.08f,.15f,1.10f));
                    Part(a,"Buckle",box,bronze,V(0,-.37f,.58f),V(.26f,.18f,.12f));
                    if(armored) Part(a,"ChestPlate",gem,armor,V(0,.09f,.52f),V(.92f,.85f,.35f));
                    else Part(a,"Sigil",gem,accent,V(0,.13f,.52f),V(.30f,.38f,.19f),shadows:false);
                }
                DeleteArt(rig,"Art_Mantle");
                if(caster||n.Contains("Phantom"))
                {
                    var a=Group(rig,"Art_Mantle"); Part(a,"RaggedCape",cape,armor,V(0,.86f,-.19f),V(.76f,1.2f,.46f));
                    if(caster) Part(a,"Cowl",prism,armor,V(0,1.32f,0),V(.57f,.17f,.53f));
                }
                foreach(Transform t in rig.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    string name=t.name;
                    if(name.StartsWith("Horn_")||name.StartsWith("Fang_")) Restyle(t,ivory,cone);
                    else if(name.StartsWith("Rock")) Restyle(t,wall2,box);
                    else if(name=="TowerShield") Restyle(t,darkIron,box);
                    else if(name=="ShieldBoss") Restyle(t,bronze,gem);
                    else if(name=="StaffCrystal"||name=="Crystal"||name=="VolatileCore"||name=="SpiritLight"||name=="Halo") Restyle(t,accent,gem);
                    else if(name=="Staff"||name=="Rod"||name=="Handle") Restyle(t,wood,prism);
                    else if(name=="ClubHead") Restyle(t,darkIron,prism);
                    else if(name=="Dagger") Restyle(t,silver,blade);
                    else if(name.StartsWith("Bow_")) Restyle(t,wood,box);
                    else if(name=="Crest") Restyle(t,accent,gem);
                    else if(name=="SpectralMantle") Restyle(t,specter,cape);
                    else if(name=="Fuse") Restyle(t,flame,gem);
                }
                var shield=Find(rig,"TowerShield");
                if(shield!=null)
                {
                    DeleteArt(shield,"Art_Shield"); var a=Group(shield,"Art_Shield");
                    foreach(int s in new[]{-1,1}) Part(a,"Edge",box,bronze,V(s*.43f,0,.51f),V(.10f,.92f,.12f));
                    Part(a,"Seal",gem,accent,V(0,.14f,.59f),V(.45f,.42f,.11f),shadows:false);
                }
                var rock=Find(rig,"RockPlate");
                if(rock!=null){DeleteArt(rock,"Art_Core"); Part(rock,"Art_Core",gem,accent,V(0,.1f,.56f),V(.38f,.53f,.2f),shadows:false);}
                StyleEnemyHealth(root);
            });
        }

        private static void StyleHero(Transform root)
        {
            Transform rig=Find(root,"HumanoidFigure"); if(rig==null) return;
            var torso=Find(rig,"Torso"); var head=Find(rig,"Head");
            Restyle(torso,steel,chestMesh); Restyle(head,steel,helmMesh);
            var oldVisor=Find(rig,"Visor_Eyes")?.GetComponent<Renderer>();if(oldVisor!=null)oldVisor.enabled=false;
            DeleteArt(torso,"Art_Plate"); var plate=Group(torso,"Art_Plate");
            Part(plate,"BreastplateTrim",chestMesh,darkIron,V(0,.035f,.33f),V(.91f,.80f,.32f));
            Part(plate,"Cuirass",chestMesh,steel,V(0,.05f,.45f),V(.80f,.72f,.28f));
            Part(plate,"Suncrest",gem,gold,V(0,.15f,.60f),V(.28f,.30f,.13f));
            Part(plate,"SoulJewel",gem,soul,V(0,.17f,.69f),V(.13f,.15f,.085f),shadows:false);
            for(int i=0;i<2;i++) Part(plate,"Lame",limbMesh,steel,V(0,-.27f-i*.11f,.36f),V(.80f,.15f,.30f));
            Part(plate,"Belt",box,leather,V(0,-.47f,0),V(1.12f,.17f,1.13f));
            Part(plate,"Buckle",box,gold,V(0,-.47f,.61f),V(.28f,.19f,.1f));
            DeleteArt(head,"Art_Helm"); var helm=Group(head,"Art_Helm");
            Part(helm,"BrowBand",prism,bronze,V(0,.20f,.0f),V(.94f,.095f,.96f));
            Part(helm,"VisorOpening",box,darkIron,V(0,.065f,.458f),V(.79f,.19f,.17f));
            Part(helm,"FaceGuard",helmMesh,steel,V(0,-.23f,.35f),V(.72f,.39f,.37f));
            foreach(int s in new[]{-1,1})
            {
                Part(helm,"VisorGlow",box,soul,V(s*.19f,.073f,.548f),V(.25f,.055f,.05f),shadows:false);
                Part(helm,"CheekGuard",limbMesh,bronze,V(s*.37f,-.13f,.38f),V(.115f,.36f,.16f));
            }
            Part(helm,"HelmRidge",gem,bronze,V(0,.44f,-.04f),V(.125f,.24f,.67f));
            Part(helm,"NasalGuard",blade,bronze,V(0,-.11f,.554f),V(.088f,.32f,.10f),Q(0,0,180));
            foreach(string limb in new[]{"LeftArm","RightArm","LeftLeg","RightLeg"})
            {
                var t=Find(rig,limb); if(t==null) continue;
                Restyle(t,darkIron,limbMesh); DeleteArt(t,"Art_Plate"); var a=Group(t,"Art_Plate");
                if(limb.Contains("Arm"))
                {
                    Part(a,"Pauldron",plateMesh,steel,V(0,.39f,0),V(2.03f,.54f,1.75f));
                    Part(a,"PauldronEdge",prism,bronze,V(0,.17f,0),V(1.93f,.095f,1.65f));
                    Part(a,"Vambrace",limbMesh,steel,V(0,-.10f,0),V(1.31f,.46f,1.29f));
                    Part(a,"Gauntlet",limbMesh,darkIron,V(0,-.45f,.03f),V(1.35f,.24f,1.35f));
                }
                else
                {
                    Part(a,"Greave",limbMesh,steel,V(0,-.04f,.21f),V(1.12f,.62f,.89f));
                    Part(a,"Knee",plateMesh,bronze,V(0,.17f,.58f),V(.85f,.20f,.28f));
                    Part(a,"Sabatons",box,darkIron,V(0,-.40f,.34f),V(1.32f,.25f,1.52f));
                }
            }
            DeleteArt(rig,"Art_Mantle"); var mantle=Group(rig,"Art_Mantle");
            Part(mantle,"Cape",cape,cloak,V(0,.83f,-.16f),V(.76f,1.17f,.62f));
            Part(mantle,"CapeClasp",gem,gold,V(0,1.27f,-.25f),V(.15f,.17f,.08f));
            Part(mantle,"Tabard",blade,cloak,V(0,.59f,.20f),V(.26f,.59f,.075f),Q(0,0,180));
            Part(mantle,"TabardCrest",gem,gold,V(0,.65f,.247f),V(.10f,.14f,.035f));
            Restyle(rig,"Blade",silver,blade); Restyle(rig,"Guard",gold,box); Restyle(rig,"Hilt",leather,prism);
        }

        private static Transform Link(Transform parent,string name,Mesh mesh,Material mat,Vector3 from,Vector3 to,float width,float depth)
            =>Part(parent,name,mesh,mat,(from+to)*.5f,V(width,Vector3.Distance(from,to),depth),Quaternion.FromToRotation(Vector3.up,to-from));

        private static void BuildWeapons(string onlyWeapon=null)
        {
            string folder=ArtRoot+"/Weapons"; EnsureFolder(folder);
            foreach(string guid in AssetDatabase.FindAssets("t:WeaponDataSO",new[]{"Assets/ScriptableObjects/Combat/Weapons"}))
            {
                var data=AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guid)); if(data==null) continue;
                if(onlyWeapon!=null&&data.name!=onlyWeapon)continue;
                var o=new GameObject("Model_"+data.name); Transform t=o.transform;
                string id=data.name;
                if(data.Type==WeaponType.Melee)
                {
                    if(id.IndexOf("hammer",System.StringComparison.OrdinalIgnoreCase)>=0)
                    {
                        Part(t,"Haft",prism,wood,V(0,.40f,0),V(.085f,.97f,.085f));
                        Part(t,"HammerHead",box,darkIron,V(0,.89f,0),V(.63f,.29f,.30f));
                        foreach(int s in new[]{-1,1}) Part(t,"Striker",box,steel,V(s*.28f,.89f,0),V(.13f,.31f,.32f));
                        Part(t,"Binding",prism,bronze,V(0,.70f,0),V(.15f,.16f,.15f));
                    }
                    else if(id.Contains("Axe")||id.Contains("Scythe"))
                    {
                        bool scythe=id.Contains("Scythe"); float len=scythe?1.22f:.86f;
                        Part(t,"Haft",prism,wood,V(0,len*.45f,0),V(.077f,len,.077f));
                        Part(t,"Collar",prism,bronze,V(0,len*.82f,0),V(.16f,.20f,.16f));
                        if(scythe)
                        {
                            Link(t,"Sickle",blade,steel,V(0,len*.82f,0),V(.52f,len*.91f,0),.23f,.065f);
                            Link(t,"SickleTip",blade,purple,V(.38f,len*.91f,0),V(.71f,len*.46f,0),.18f,.06f);
                        }
                        else
                        {
                            Part(t,"AxeHead",gem,steel,V(.16f,.77f,0),V(.49f,.57f,.14f),Q(0,0,-28));
                            Part(t,"ForgedEdge",blade,silver,V(.34f,.77f,0),V(.19f,.51f,.065f),Q(0,0,-4));
                        }
                    }
                    else if(id.Contains("Spear"))
                    {
                        Part(t,"Shaft",prism,wood,V(0,.42f,0),V(.067f,1.20f,.067f));
                        Part(t,"Head",blade,silver,V(0,1.06f,0),V(.21f,.57f,.08f));
                        Part(t,"Socket",prism,bronze,V(0,.83f,0),V(.12f,.18f,.12f));
                    }
                    else
                    {
                        bool twin=id.Contains("Twin"),dagger=id.Contains("Dagger"),great=id.Contains("Great"),fire=id.Contains("Fire");
                        float length=dagger ? .57f : great ? 1.29f : 1.02f, width=dagger ? .12f : great ? .215f : .135f;
                        Sword(t,Vector3.zero,length,width,fire?flame:silver,fire?bronze:gold);
                        if(twin) Sword(t,V(.23f,-.07f,.06f),length*.88f,width,soul,bronze);
                    }
                    o.AddComponent<MeleeWeapon>();
                }
                else
                {
                    // Fantasy silhouettes only. Existing projectile origins, statistics and attack logic are unchanged.
                    t.localRotation=Q(-45,0,0);
                    if(id.Contains("Staff"))
                    {
                        var staff=Group(t,"Staff",Vector3.zero,Q(45,0,0));
                        Part(staff,"Shaft",prism,wood,V(0,.4f,0),V(.075f,1.34f,.075f));
                        Part(staff,"Crown",prism,bronze,V(0,.98f,0),V(.23f,.14f,.23f));
                        Part(staff,"IceGem",gem,frost,V(0,1.19f,0),V(.30f,.48f,.30f),shadows:false);
                    }
                    else if(id.Contains("Crossbow"))
                    {
                        Part(t,"Stock",box,wood,V(0,0,.13f),V(.16f,.17f,.74f));
                        Part(t,"Rail",box,steel,V(0,.105f,.20f),V(.10f,.05f,.66f));
                        foreach(int s in new[]{-1,1})
                        {
                            Link(t,"Limbs",box,bronze,V(0,.01f,.38f),V(s*.37f,.02f,.25f),.10f,.08f);
                            Link(t,"LimbTips",box,wood,V(s*.37f,.02f,.25f),V(s*.50f,.02f,.04f),.07f,.07f);
                            Link(t,"Cord",box,ivory,V(s*.50f,.02f,.04f),V(0,.04f,.09f),.016f,.016f);
                        }
                    }
                    else
                    {
                        bool shotgun=id.Contains("Shotgun"),launcher=id.Contains("Grenade"),smg=id.Contains("SMG");
                        float len=shotgun ? .85f : launcher ? .73f : smg ? .60f : .39f;
                        Part(t,"RunicReceiver",box,darkIron,V(0,.045f,.13f),V(launcher ? .25f : .17f,.22f,len*.58f));
                        Part(t,"Grip",box,leather,V(0,-.10f,-.05f),V(.13f,.24f,.16f),Q(-14,0,0));
                        Part(t,"AncientBarrel",prism,steel,V(0,.088f,len*.62f),V(launcher ? .20f : .10f,len*.60f,launcher ? .20f : .10f),Q(90,0,0));
                        Part(t,"DecorativeMuzzle",prism,bronze,V(0,.088f,len*.89f),V(launcher ? .26f : .15f,.11f,launcher ? .26f : .15f),Q(90,0,0));
                        Part(t,"PowerRune",gem,launcher?poison:shotgun?flame:soul,V(0,.18f,.17f),V(.10f,.12f,.12f),shadows:false);
                        if(shotgun||launcher) Part(t,"Stock",box,wood,V(0,.015f,-.20f),V(.16f,.18f,.30f));
                        if(smg) Part(t,"RuneMagazine",box,bronze,V(0,-.13f,.18f),V(.13f,.25f,.15f),Q(8,0,0));
                    }
                    o.AddComponent<RangedWeapon>();
                }
                string path=folder+"/"+o.name+".prefab";
                GameObject prefab=PrefabUtility.SaveAsPrefabAsset(o,path); Object.DestroyImmediate(o);
                var so=new SerializedObject(data); so.FindProperty("weaponPrefab").objectReferenceValue=prefab; so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Sword(Transform parent,Vector3 p,float length,float width,Material edge,Material guard)
        {
            Part(parent,"Grip",prism,leather,p+V(0,-.02f,0),V(.065f,.20f,.068f));
            Part(parent,"Pommel",gem,guard,p+V(0,-.15f,0),V(.105f,.13f,.10f));
            Part(parent,"Crossguard",box,guard,p+V(0,.12f,0),V(width*2.25f,.075f,.095f));
            Part(parent,"ForgedBlade",blade,edge,p+V(0,.15f+length*.5f,0),V(width,length,.055f));
            Part(parent,"Fuller",box,darkIron,p+V(0,.15f+length*.31f,.029f),V(width*.17f,length*.57f,.007f),shadows:false);
        }

        private static void BuildChestAndPickups()
        {
            EditPrefab(CombatRoot+"/Item_TreasureChest.prefab",root=>
            {
                Restyle(root,"Body",wood,box); Restyle(root,"LidTop",wood,box); Restyle(root,"Lock",gold,box);
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true)) if(t.name.StartsWith("Band_")||t.name=="LidBand") Restyle(t,bronze,box);
                DeleteArt(root,"Art_ChestBody"); var art=Group(root,"Art_ChestBody");
                Part(art,"Plinth",box,darkIron,V(0,.10f,0),V(1.25f,.16f,.87f));
                foreach(int s in new[]{-1,1})
                {
                    for(int i=0;i<3;i++) Part(art,"Rivet",gem,gold,V(s*.43f,.22f+i*.16f,-.431f),V(.075f,.075f,.055f));
                    Part(art,"CornerProtector",box,bronze,V(s*.53f,.35f,-.37f),V(.11f,.62f,.12f));
                }
                var lid=Find(root,"Lid"); DeleteArt(lid,"Art_Lid"); var a=Group(lid,"Art_Lid");
                foreach(float x in new[]{-.42f,.42f}) Part(a,"LidStrap",box,bronze,V(x,.24f,-.40f),V(.10f,.065f,.82f));
                Part(a,"Crest",gem,gold,V(0,.27f,-.42f),V(.29f,.10f,.30f));
                Part(a,"LockGem",gem,soul,V(0,.01f,-.87f),V(.105f,.13f,.06f),shadows:false);
            });
            EditPrefab(CombatRoot+"/Item_Pickup.prefab",root=>Restyle(root,"Visual",soul,gem));
            foreach(string name in new[]{"Enemy_Projectile","Weapon_Projectile"}) EditPrefab(CombatRoot+"/"+name+".prefab",root=>Restyle(root,name=="Enemy_Projectile"?purple:soul,gem));
        }
    }
}

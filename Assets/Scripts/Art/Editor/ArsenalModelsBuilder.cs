using System;
using System.IO;
using System.Linq;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Weapons;
using LightSouls.MapGeneration;
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
        private static Color ArsenalColor(CombatVfxElement e)=>e switch
        {
            CombatVfxElement.Ember=>C("f28b42"),CombatVfxElement.Frost=>C("85d9f4"),CombatVfxElement.Void=>C("b292f1"),
            CombatVfxElement.Storm=>C("ffe189"),CombatVfxElement.Stone=>C("d4ab75"),CombatVfxElement.Life=>C("83e2be"),
            CombatVfxElement.Blood=>C("f37586"),_=>C("d6dce7")
        };
        private static Material ArsenalMat(CombatVfxElement e)=>e switch
        {
            CombatVfxElement.Ember=>flame,CombatVfxElement.Frost=>frost,CombatVfxElement.Void=>purple,
            CombatVfxElement.Storm=>amber,CombatVfxElement.Stone=>bronze,CombatVfxElement.Life=>soul,CombatVfxElement.Blood=>red,_=>silver
        };

        private static GameObject BuildArsenalWeaponModel(ArsenalWeaponSpec spec,string assetName)
        {
            string folder=ArtRoot+"/Weapons";EnsureFolder(folder);
            var root=new GameObject("Model_"+assetName);var t=root.transform;
            Material energy=ArsenalMat(spec.Element),metal=spec.Element==CombatVfxElement.Stone||spec.Element==CombatVfxElement.Void?darkIron:steel;
            Material trimMat=spec.Rarity>=LightSouls.Common.ItemRarity.Epic?gold:bronze;
            bool dark=spec.Element==CombatVfxElement.Void||spec.Element==CombatVfxElement.Blood;
            Transform P(string name,Mesh mesh,Material mat,Vector3 pos,Vector3 scale,Quaternion rot=default)=>Part(t,name,mesh,mat,pos,scale,rot);
            void Shaft(float length,float grip=.095f)
            {
                P("Haft",prism,wood,V(0,length*.40f,0),V(grip,length,grip));P("Grip",prism,leather,V(0,.02f,0),V(grip*1.3f,.24f,grip*1.3f));
                P("Pommel",gem,trimMat,V(0,-.14f,0),V(grip*1.64f,.15f,grip*1.64f));
                foreach(float y in new[]{.15f,length*.42f,length*.83f})P("Ferrule",prism,trimMat,V(0,y,0),V(grip*1.55f,.08f,grip*1.55f));
            }
            void Crest(Vector3 pos,float scale=.15f)=>P("ElementalSeal",gem,energy,pos,V(scale,scale*1.25f,scale*.6f));
            void Edged(Vector3 offset,float length,float width,bool paired=false)
            {
                var h=Group(t,paired?"OffhandBlade":"MainBlade",offset,paired?Q(0,-10,-14):Quaternion.identity);
                Part(h,"Grip",prism,leather,V(0,0,0),V(.083f,.23f,.083f));Part(h,"Pommel",gem,trimMat,V(0,-.15f,0),V(.13f,.15f,.12f));
                foreach(int s in new[]{-1,1})Link(h,"Quillon",blade,trimMat,V(0,.15f,0),V(s*width*1.65f,.12f+s*.025f,0),.105f,.085f);
                Part(h,"GuardCrest",gem,energy,V(0,.17f,-.055f),V(width*.7f,.18f,.085f));
                Part(h,"ForgedEdge",blade,spec.Element==CombatVfxElement.Frost?frost:silver,V(0,.19f+length*.50f,0),V(width,length,.087f));
                Part(h,"InsetSpine",blade,dark?metal:energy,V(0,.19f+length*.48f,-.055f),V(width*.39f,length*.82f,.043f));
                if(length>1.03f)foreach(int s in new[]{-1,1})Part(h,"RuneTooth",gem,energy,V(s*width*.40f,.41f,-.045f),V(.085f,.22f,.07f),Q(0,0,s*23));
            }

            if(!spec.IsRanged)
            {
                switch(spec.Shape)
                {
                    case "Sword":Edged(Vector3.zero,1.00f,.17f);if(spec.Key=="thorn_sword")for(int i=0;i<4;i++)P("BriarTooth",blade,energy,V((i%2==0?1:-1)*.10f,.38f+i*.15f,0),V(.10f,.22f,.06f),Q(0,0,i%2==0?-28:28));break;
                    case "GreatSword":Edged(Vector3.zero,1.38f,.26f);foreach(int s in new[]{-1,1})P("GlacierShard",gem,energy,V(s*.19f,.60f,.01f),V(.13f,.54f,.12f),Q(0,0,s*18));break;
                    case "Dagger":Edged(Vector3.zero,.57f,.17f);P("FangTip",gem,energy,V(.018f,.67f,-.02f),V(.106f,.27f,.07f),Q(0,0,-8));break;
                    case "Twin":Edged(V(-.10f,0,0),.78f,.13f);Edged(V(.22f,-.085f,.04f),.69f,.14f,true);break;
                    case "Rapier":Edged(Vector3.zero,1.19f,.075f);P("BasketGuard",ring,trimMat,V(0,.15f,0),V(.39f,.19f,.32f));P("DreamEye",gem,energy,V(.17f,.15f,0),V(.13f,.16f,.13f));break;
                    case "Saber":
                        Shaft(.33f,.08f);P("SweptGuard",gem,trimMat,V(.09f,.18f,0),V(.33f,.18f,.16f));
                        Link(t,"SabreLower",blade,silver,V(0,.24f,0),V(.10f,.82f,0),.19f,.075f);
                        Link(t,"SabreTip",blade,energy,V(.10f,.71f,0),V(.35f,1.15f,0),.16f,.07f);Crest(V(.08f,.31f,-.06f));break;
                    case "Cleaver":
                        Shaft(.35f,.09f);P("BladeBody",gem,metal,V(.14f,.67f,0),V(.43f,.84f,.17f),Q(0,0,-13));
                        Link(t,"EmberEdge",blade,energy,V(.29f,.29f,0),V(.31f,1.08f,0),.21f,.095f);P("BladeRune",gem,gold,V(.12f,.64f,-.095f),V(.16f,.30f,.06f));break;
                    case "Hammer":
                        Shaft(.93f,.11f);
                        if(spec.Key=="guardian_maul")
                        {
                            P("BulwarkCore",prism,steel,V(0,.92f,0),V(.47f,.51f,.46f));
                            foreach(int s in new[]{-1,1})
                            {
                                P("WardShield",gem,darkIron,V(s*.25f,.92f,0),V(.25f,.55f,.53f),Q(0,0,s*7));
                                P("WardPlate",box,steel,V(s*.29f,.92f,-.26f),V(.18f,.42f,.085f));
                            }
                            P("Crest",plateMesh,bronze,V(0,.94f,-.25f),V(.30f,.39f,.095f));P("WallInscription",gem,soul,V(0,.94f,-.31f),V(.14f,.29f,.065f));
                            P("CrownSpike",gem,gold,V(0,1.23f,0),V(.23f,.22f,.23f));
                        }
                        else
                        {
                            P("StoneHead",block,metal,V(0,.93f,0),V(.68f,.33f,.40f));
                            foreach(int s in new[]{-1,1}){P("ReinforcedStriker",prism,trimMat,V(s*.36f,.93f,0),V(.34f,.14f,.42f),Q(0,0,90));P("Ridge",box,steel,V(s*.25f,1.08f,0),V(.14f,.09f,.39f));}
                            Crest(V(0,.95f,-.215f),.21f);P("HeadCrown",gem,energy,V(0,1.17f,0),V(.18f,.16f,.2f));
                        }
                        break;
                    case "Mace":
                        Shaft(.85f,.085f);P("FlangedCore",gem,metal,V(0,.89f,0),V(.40f,.42f,.40f));
                        for(int i=0;i<6;i++){float a=i*60*Mathf.Deg2Rad;P("LightningFlange",blade,energy,V(Mathf.Cos(a)*.22f,.90f,Mathf.Sin(a)*.22f),V(.14f,.35f,.075f),Q(0,i*60,0));}P("Finial",gem,gold,V(0,1.17f,0),V(.16f,.18f,.16f));break;
                    case "Axe":
                        Shaft(.95f);P("AxeBody",gem,metal,V(.20f,.87f,0),V(.57f,.62f,.20f),Q(0,0,-28));
                        Link(t,"BrightAxeEdge",blade,energy,V(.42f,.54f,0),V(.43f,1.16f,0),.21f,.08f);P("CounterWeight",gem,trimMat,V(-.195f,.89f,0),V(.25f,.32f,.24f));Crest(V(.15f,.88f,-.11f),.22f);break;
                    case "Spear":
                        Shaft(1.22f,.082f);P("SpearLeaf",blade,energy,V(0,1.20f,0),V(.27f,.68f,.12f));P("SpearSpine",blade,silver,V(0,1.20f,-.06f),V(.083f,.56f,.05f));
                        foreach(int s in new[]{-1,1})Link(t,"GravityFlange",blade,metal,V(0,.97f,0),V(s*.20f,1.16f,0),.13f,.07f);break;
                    case "Halberd":
                        Shaft(1.22f,.088f);P("PikeTip",blade,silver,V(0,1.32f,0),V(.16f,.44f,.09f));
                        P("CrescentHead",gem,energy,V(.24f,1.04f,0),V(.62f,.58f,.16f),Q(0,0,-23));Link(t,"CrescentEdge",blade,silver,V(.46f,.78f,0),V(.43f,1.33f,0),.16f,.08f);
                        P("RearBeak",blade,metal,V(-.22f,1.04f,0),V(.20f,.39f,.12f),Q(0,0,65));Crest(V(.05f,1.04f,-.09f),.18f);break;
                    case "Scythe":
                        Shaft(1.35f,.09f);Link(t,"HookRoot",blade,metal,V(0,1.18f,0),V(.50f,1.29f,0),.25f,.10f);
                        Link(t,"HookArc",blade,energy,V(.38f,1.29f,0),V(.78f,1.02f,0),.20f,.09f);Link(t,"HookTip",blade,silver,V(.70f,1.10f,0),V(.86f,.71f,0),.15f,.07f);Crest(V(.07f,1.17f,-.06f),.21f);break;
                    case "Flail":
                        Shaft(.69f,.09f);
                        Link(t,"ChainTether",prism,steel,V(0,.65f,0),V(.46f,.49f,0),.052f,.052f);
                        for(int i=0;i<4;i++){float x=.07f+i*.10f,y=.65f-i*.045f;P("ChainLink",gem,i%2==0?bronze:steel,V(x,y,0),V(.115f,.095f,.095f),Q(0,0,i%2==0?39:-22));}
                        P("IronStar",gem,metal,V(.48f,.46f,0),V(.39f,.39f,.39f));for(int i=0;i<5;i++){float a=i*72*Mathf.Deg2Rad;P("Spur",cone,energy,V(.48f+Mathf.Cos(a)*.20f,.46f+Mathf.Sin(a)*.20f,0),V(.13f,.19f,.13f),Q(0,0,i*72-90));}break;
                }
                root.AddComponent<MeleeWeapon>();
            }
            else
            {
                t.localRotation=Q(-45,0,0);
                var magic=Group(t,"Arcanum",Vector3.zero,Q(45,0,0));
                void Pm(string name,Mesh mesh,Material mat,Vector3 p,Vector3 s,Quaternion r=default)=>Part(magic,name,mesh,mat,p,s,r);
                switch(spec.Shape)
                {
                    case "Staff":case "Wand":
                        float length=spec.Shape=="Staff"?1.15f:.71f;
                        Pm("StaffShaft",prism,wood,V(0,length*.39f,0),V(.081f,length,.081f));Pm("Pommel",gem,trimMat,V(0,-.12f,0),V(.13f,.19f,.13f));
                        Pm("RuneCollar",prism,trimMat,V(0,length*.83f,0),V(.23f,.18f,.23f));
                        Pm("Focus",gem,energy,V(0,length,0),V(.25f,.40f,.25f));
                        for(int i=0;i<3;i++){float a=i*120*Mathf.Deg2Rad;Pm("Grasp",blade,metal,V(Mathf.Cos(a)*.155f,length*.96f,Mathf.Sin(a)*.155f),V(.12f,.35f,.10f),Q(0,i*120,13));}
                        if(spec.Key=="dream_scepter")Pm("DreamHalo",ring,trimMat,V(0,length,0),V(.46f,.16f,.46f),Q(90,0,0));break;
                    case "Orb":
                        Pm("Handle",prism,leather,V(0,-.03f,0),V(.11f,.24f,.11f));Pm("OrbBasin",prism,metal,V(0,.24f,0),V(.42f,.15f,.42f));
                        Pm("ArcaneCore",gem,energy,V(0,.50f,0),V(.46f,.51f,.46f));
                        foreach(int i in new[]{0,1,2}){Pm("Orbit",ring,trimMat,V(0,.50f,0),V(.68f,.18f,.68f),Q(32+i*41,i*55,0));Pm("Shard",gem,energy,V(Mathf.Cos(i*2.094f)*.31f,.52f,Mathf.Sin(i*2.094f)*.31f),V(.12f,.21f,.11f));}break;
                    case "Lantern":
                        Pm("LanternFoot",prism,metal,V(0,.17f,0),V(.44f,.13f,.43f));Pm("LanternRoof",gem,trimMat,V(0,.75f,0),V(.50f,.24f,.47f));Pm("Mothlight",gem,energy,V(0,.45f,0),V(.31f,.48f,.31f));
                        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Pm("CageRib",prism,metal,V(x*.17f,.46f,z*.17f),V(.065f,.60f,.065f));
                        Pm("CarryLoop",ring,trimMat,V(0,.92f,0),V(.28f,.13f,.28f),Q(90,0,0));break;
                    case "Totem":
                        Pm("StoneGrip",prism,leather,V(0,0,0),V(.14f,.25f,.14f));Pm("WeatheredTotem",prism,metal,V(0,.52f,0),V(.36f,.88f,.36f));
                        Pm("TotemCap",prism,trimMat,V(0,.92f,0),V(.44f,.14f,.44f));Pm("TotemCrown",gem,energy,V(0,1.12f,0),V(.34f,.38f,.33f));
                        for(int i=0;i<3;i++)Pm("SealingRune",gem,energy,V(0,.35f+i*.21f,-.185f),V(.16f,.18f,.06f));break;
                    case "Fan":
                        Pm("FanGrip",prism,leather,V(0,0,0),V(.11f,.27f,.11f));
                        for(int i=-3;i<=3;i++){float a=i*15f*Mathf.Deg2Rad;Vector3 tip=V(Mathf.Sin(a)*.72f,Mathf.Cos(a)*.72f+.12f,0);Link(magic,"WindFeather",blade,i%2==0?energy:metal,V(0,.08f,0),tip,.19f,.055f);}
                        Pm("Rivet",gem,trimMat,V(0,.16f,-.065f),V(.17f,.18f,.09f));break;
                    case "Bow":
                        Pm("BowGrip",prism,leather,V(0,0,0),V(.105f,.28f,.105f));
                        foreach(int s in new[]{-1,1}){Link(magic,"Stave",box,wood,V(0,s*.09f,0),V(.21f,s*.42f,0),.12f,.09f);Link(magic,"ReflexTip",blade,energy,V(.21f,s*.38f,0),V(.10f,s*.74f,0),.14f,.075f);Link(magic,"String",box,ivory,V(.10f,s*.73f,0),V(-.13f,0,0),.021f,.019f);}
                        Pm("BowHeart",gem,trimMat,V(-.025f,0,-.06f),V(.16f,.20f,.11f));break;
                    case "Crossbow":
                        // Fantasy crossbow silhouettes; gameplay is handled by existing spell projectiles.
                        P("Stock",box,wood,V(0,.03f,.16f),V(.18f,.19f,.77f));P("RunicRail",box,metal,V(0,.14f,.20f),V(.12f,.075f,.68f));
                        foreach(int s in new[]{-1,1}){Link(t,"Arm",blade,energy,V(0,.12f,.41f),V(s*.48f,.09f,.24f),.19f,.11f);Link(t,"ArmTip",blade,trimMat,V(s*.38f,.09f,.24f),V(s*.57f,.09f,.08f),.12f,.08f);Link(t,"Tether",box,ivory,V(s*.57f,.09f,.08f),V(0,.10f,.05f),.018f,.018f);}
                        P("FocusGem",gem,energy,V(0,.18f,.34f),V(.18f,.22f,.17f));break;
                }
                root.AddComponent<RangedWeapon>();
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+root.name+".prefab");Object.DestroyImmediate(root);return prefab;
        }

        private static bool TryBuildArsenalInventoryModel(Transform t,string id)
        {
            if(!id.Contains("_Arsenal_",StringComparison.Ordinal))return false;
            string key=id.Substring(id.IndexOf("_Arsenal_",StringComparison.Ordinal)+"_Arsenal_".Length);
            void P(string name,Mesh mesh,Material mat,Vector3 p,Vector3 scale,Quaternion rot=default)=>Part(t,name,mesh,mat,p,scale,rot);
            if(id.StartsWith("Material_"))
            {
                var spec=NewMaterials.First(s=>s.Key==key);Material energy=ArsenalMat(spec.Element);
                switch(spec.Shape)
                {
                    case "Shard":for(int i=0;i<4;i++)P("Obsidian",gem,i==1?energy:darkIron,V((i-1.5f)*.17f,.32f+i*.035f,(i%2)*.12f),V(.32f,.62f-i*.07f,.30f),Q(10,i*40,i*17-26));break;
                    case "Salt":for(int i=0;i<6;i++)P("CrystalSalt",gem,i%2==0?frost:ivory,V((i%3-1)*.24f,.20f+(i/3)*.22f,(i/3)*.12f),V(.21f,.27f,.21f),Q(18,i*29,12*i));break;
                    case "Silk":P("SilkSpool",prism,energy,V(0,.37f,0),V(.56f,.77f,.56f),Q(12,0,-24));foreach(float y in new[]{.035f,.70f})P("SpoolCap",prism,trimForIcons(),V(0,y,0),V(.67f,.11f,.67f),Q(12,0,-24));P("Ribbon",cape,cloth,V(.35f,.30f,.02f),V(.20f,.54f,.16f),Q(12,0,19));break;
                    case "Ingot":for(int i=0;i<3;i++)P("MoonMetal",block,i==1?energy:silver,V((i-1)*.15f,.17f+i*.14f,(i%2)*.09f),V(.63f,.19f,.33f),Q(0,i*22-15,0));break;
                    case "Resin":P("AmberChunk",gem,energy,V(0,.42f,0),V(.65f,.70f,.55f),Q(16,18,22));P("AncientInclusion",gem,wood,V(.17f,.35f,-.23f),V(.20f,.30f,.11f));P("SmallResin",gem,amber,V(-.27f,.14f,.1f),V(.29f,.27f,.29f));break;
                    case "Bone":for(int s=-1;s<=1;s+=2){P("IvoryBone",prism,ivory,V(s*.20f,.40f,0),V(.14f,.74f,.16f),Q(0,0,s*28));foreach(float y in new[]{.14f,.65f})P("Joint",gem,ivory,V(s*(.20f+(y-.4f)*.40f),y,0),V(.23f,.23f,.22f),Q(12,33,0));}P("EchoTie",gem,energy,V(0,.37f,-.12f),V(.17f,.25f,.13f));break;
                    case "Ore":P("MagneticOre",gem,darkIron,V(0,.33f,0),V(.69f,.65f,.55f),Q(13,27,15));for(int i=0;i<4;i++)P("Vein",gem,energy,V((i-1.5f)*.16f,.29f+(i%2)*.19f,-.21f),V(.15f,.23f,.12f),Q(17,i*30,14));break;
                    case "Dust":P("DreamPhial",plateMesh,energy,V(0,.35f,0),V(.55f,.64f,.45f));P("Neck",prism,bronze,V(0,.72f,0),V(.23f,.18f,.22f));P("Stopper",gem,wood,V(0,.85f,0),V(.25f,.15f,.22f));P("Seal",gem,gold,V(0,.35f,-.25f),V(.15f,.27f,.07f));break;
                    case "Root":for(int i=-1;i<=1;i++){Link(t,"Root",prism,wood,V(i*.17f,0,0),V(i*.07f,.73f,0),.15f,.14f);P("Sprout",blade,energy,V(i*.15f,.53f,-.03f),V(.21f,.38f,.10f),Q(0,0,i*30));}P("Knot",prism,leather,V(0,.20f,0),V(.39f,.12f,.22f));break;
                    case "Moss":P("RootStone",gem,darkIron,V(0,.18f,0),V(.72f,.36f,.53f));for(int i=0;i<7;i++)P("MossCluster",gem,i%3==0?energy:poison,V(Mathf.Sin(i*1.3f)*.23f,.37f+(i%3)*.06f,Mathf.Cos(i*1.3f)*.19f),V(.21f,.24f,.21f),Q(11,i*47,24));break;
                    case "Coil":P("CoilCore",prism,metalIcon(),V(0,.39f,0),V(.28f,.78f,.28f));for(int i=0;i<5;i++)P("CopperLoop",prism,i%2==0?bronze:energy,V(0,.12f+i*.135f,0),V(.59f,.087f,.59f));P("Terminal",gem,gold,V(0,.83f,0),V(.22f,.17f,.22f));break;
                    case "Pearl":P("ShellLeft",plateMesh,bronze,V(-.17f,.22f,0),V(.45f,.34f,.56f),Q(0,0,25));P("ShellRight",plateMesh,trimForIcons(),V(.17f,.22f,0),V(.45f,.34f,.56f),Q(0,0,-25));P("AbyssPearl",gem,energy,V(0,.47f,0),V(.44f,.48f,.44f));break;
                }
                return true;
            }
            if(id.StartsWith("Armor_")||id.StartsWith("Boots_"))
            {
                var spec=NewGear.First(s=>s.Key==key);Material energy=ArsenalMat(spec.Element);
                Material baseMat=spec.Key.Contains("coat")||spec.Key.Contains("mantle")?cloth:spec.Element==CombatVfxElement.Stone?darkIron:steel;
                if(spec.Boots)
                {
                    for(int s=-1;s<=1;s+=2)
                    {
                        var foot=Group(t,"Boot",V(s*.28f,0,s*.05f),Q(0,s*10,0));
                        Part(foot,"Sole",box,darkIron,V(0,.06f,-.10f),V(.46f,.15f,.68f));Part(foot,"Foot",box,baseMat,V(0,.19f,-.12f),V(.43f,.25f,.64f));
                        Part(foot,"Greave",limbMesh,baseMat,V(0,.48f,.04f),V(.40f,.65f,.38f));Part(foot,"Cuff",prism,bronze,V(0,.78f,.04f),V(.45f,.11f,.43f));
                        Part(foot,"Rune",gem,energy,V(0,.54f,-.19f),V(.20f,.29f,.08f));
                        if(spec.Key.Contains("wind"))Part(foot,"Wing",blade,energy,V(s*.22f,.50f,.04f),V(.17f,.47f,.11f),Q(0,0,s*30));
                        if(spec.Key.Contains("iron"))foreach(float z in new[]{-.28f,.03f})Part(foot,"Rivet",gem,steel,V(0,.12f,z),V(.16f,.10f,.16f));
                    }
                }
                else
                {
                    P("ArmoredTorso",chestMesh,baseMat,V(0,.46f,0),V(.78f,1f,.54f));
                    foreach(int s in new[]{-1,1}){P("Pauldron",plateMesh,baseMat,V(s*.40f,.75f,0),V(.37f,.38f,.43f));P("Inlay",gem,energy,V(s*.39f,.77f,-.19f),V(.20f,.22f,.09f));}
                    P("Crest",gem,energy,V(0,.56f,-.30f),V(.23f,.32f,.07f));P("Belt",box,leather,V(0,.19f,0),V(.77f,.12f,.51f));P("Buckle",box,bronze,V(0,.19f,-.28f),V(.22f,.15f,.07f));
                    if(spec.Key.Contains("coat")||spec.Key.Contains("mantle"))P("Mantle",cape,energy,V(0,.45f,.21f),V(.9f,1.1f,.28f),Q(0,180,0));
                }
                return true;
            }
            if(id.StartsWith("Consumable_"))
            {
                var spec=NewPotions.First(s=>s.Key==key);int index=Array.IndexOf(NewPotions,spec);Material liquid=ArsenalMat(spec.Element);
                float width=.51f+(index%3)*.08f,height=.59f+(index%2)*.10f;
                P("PhialBody",index%3==0?gem:plateMesh,liquid,V(0,height*.49f,0),V(width,height,width*.77f));
                P("Neck",prism,ivory,V(0,height+.07f,0),V(.20f,.20f,.20f));P("Cork",prism,wood,V(0,height+.21f,0),V(.25f,.12f,.25f));
                P("Band",prism,index%2==0?bronze:gold,V(0,height*.20f,0),V(width*1.04f,.09f,width*.81f));
                P("AlchemySeal",gem,index%2==0?ivory:gold,V(0,height*.52f,-width*.40f),V(.19f,.22f,.075f));
                for(int i=0;i<1+index%3;i++)P("SealMark",gem,liquid,V((i-index%3*.5f)*.085f,height*.51f,-width*.46f),V(.065f,.11f,.04f));
                if(spec.Key=="purity")foreach(int s in new[]{-1,1})P("LeafSeal",blade,energyForPotion(),V(s*.20f,.47f,0),V(.15f,.25f,.08f),Q(0,0,s*30));
                return true;
            }
            return false;
            Material trimForIcons()=>gold;
            Material metalIcon()=>darkIron;
            Material energyForPotion()=>soul;
        }

        private static void BuildDungeonJournalIllustrations()
        {
            var catalog=ExistingArsenalAsset<DungeonCatalogSO>("Assets/ScriptableObjects/MapGeneration/Config/DungeonCatalog.asset");
            var serialized=new SerializedObject(catalog);var entries=serialized.FindProperty("entries");
            var folder=ArtRoot+"/DungeonJournal";EnsureFolder(folder);
            // The first SRP camera submission after opening Unity can render before a shader
            // variant is warm. Render the first region a second time after the other regions.
            for(int pass=0;pass<=entries.arraySize&&entries.arraySize>0;pass++)
            {
                int i=pass%entries.arraySize;
                string dungeon=entries.GetArrayElementAtIndex(i).FindPropertyRelative("dungeonId").stringValue;
                string roomName=dungeon=="dungeon.lava"?"Room_EmberBridge":dungeon=="dungeon.maze"?"Room_MossLabyrinth":"Room_RuinedLibrary";
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=C("868eaa");
                var room=(GameObject)PrefabUtility.InstantiatePrefab(ExistingArsenalAsset<GameObject>(RoomRoot+"/"+roomName+".prefab"));
                var module=room.GetComponent<RoomModule>();var bounds=module.LocalBounds;
                foreach(string side in new[]{"South","East"}){var cut=room.transform.Find("Art_Masonry/Cutaway_"+side);if(cut!=null)cut.gameObject.SetActive(false);}
                foreach(var marker in module.LootSpawnPoints)
                {
                    if(marker==null)continue;
                    Vector3 p=marker.position;var markerRenderer=marker.GetComponent<Renderer>();if(markerRenderer!=null)p.y=markerRenderer.bounds.min.y;
                    marker.gameObject.SetActive(false);
                    var chest=(GameObject)PrefabUtility.InstantiatePrefab(ExistingArsenalAsset<GameObject>(CombatRoot+"/Item_TreasureChest.prefab"));
                    chest.transform.SetPositionAndRotation(p,marker.rotation);
                }
                var hero=(GameObject)PrefabUtility.InstantiatePrefab(ExistingArsenalAsset<GameObject>(CombatRoot+"/Player_Character.prefab"));hero.transform.position=V(-1.1f,.05f,-1.7f);hero.transform.rotation=Q(0,15,0);
                foreach(var canvas in hero.GetComponentsInChildren<Canvas>(true))canvas.enabled=false;
                var sun=new GameObject("JournalKey").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Q(48,-28,0);sun.intensity=1.30f;sun.color=C("f4dabd");sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
                Color tint=dungeon=="dungeon.lava"?C("ff7f45"):dungeon=="dungeon.maze"?C("74d7ca"):C("d5b08a");
                for(int s=-1;s<=1;s+=2){var fill=new GameObject("JournalFill").AddComponent<Light>();fill.type=LightType.Point;fill.color=tint;fill.intensity=6.5f;fill.range=11f;fill.transform.position=V(s*3.8f,3.2f,-s*3.3f);}
                var cam=new GameObject("DungeonJournalCamera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=C("121824");
                cam.transform.position=V(12f,24f,-22f);cam.transform.LookAt(V(0,.1f,.6f));cam.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.z)*.87f+1.0f;cam.allowHDR=false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var target=new RenderTexture(768,512,24,RenderTextureFormat.ARGB32);target.Create();cam.targetTexture=target;
                var previous=RenderTexture.active;
                try
                {
                    var request=new RenderPipeline.StandardRequest{destination=target};if(RenderPipeline.SupportsRenderRequest(cam,request))RenderPipeline.SubmitRenderRequest(cam,request);else cam.Render();
                    RenderTexture.active=target;var tex=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                    tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();string path=folder+"/Region_"+dungeon.Substring("dungeon.".Length)+".png";
                    File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var import=(TextureImporter)AssetImporter.GetAtPath(path);import.textureType=TextureImporterType.Sprite;import.spriteImportMode=SpriteImportMode.Single;
                    import.mipmapEnabled=false;import.wrapMode=TextureWrapMode.Clamp;import.maxTextureSize=1024;import.SaveAndReimport();
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                finally{RenderTexture.active=previous;cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);}
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Light Souls/Art/Refresh Dungeon Journal Illustrations")]
        public static void RefreshDungeonJournalIllustrations()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            BuildDungeonJournalIllustrations();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            Debug.Log("DUNGEON_JOURNAL_ILLUSTRATIONS_COMPLETE");
        }
    }
}

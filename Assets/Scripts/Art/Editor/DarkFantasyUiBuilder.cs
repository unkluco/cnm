using System;
using System.IO;
using System.Linq;
using LightSouls.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private static Sprite panelSkin,buttonSkin,slotSkin,circleSkin,stickSkin,knobSkin,menuShade,emblemSkin,healthSkin;
        private static Sprite SpriteAsset(string name,int w,int h,Func<int,int,Color> pixel,Vector4 border=default)
        {
            string path=ResourceRoot+"/UI/"+name+".png"; EnsureFolder(ResourceRoot+"/UI");
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var pixels=new Color[w*h]; for(int y=0;y<h;y++) for(int x=0;x<w;x++) pixels[y*w+x]=pixel(x,y);
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100f; importer.spriteBorder=border;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float Grain(int x,int y)=>((x*73+y*137+x*y*17)%37)/37f-.5f;
        private static Color RectSkin(int x,int y,int size,bool panel,bool slot)
        {
            float e=Mathf.Min(Mathf.Min(x,size-1-x),Mathf.Min(y,size-1-y));
            int ex=Mathf.Min(x,size-1-x),ey=Mathf.Min(y,size-1-y); float cut=ex+ey;
            if(cut<7f) return Color.clear;
            float edge=Mathf.Min(e,cut-7f); float yf=y/(float)(size-1),g=Grain(x,y)*.028f;
            if(panel)
            {
                if(edge<1.6f) return C("10141d");
                if(edge<3.5f) return Color.Lerp(C("604a37"),C("c5a06b"),yf);
                if(edge<6f) return C("333b46");
                if(edge<7.4f) return C("91734e");
                if((Mathf.Abs(ex-13)+Mathf.Abs(ey-13))<5) return C("c7a369");
                Color c=Color.Lerp(C("171e2a"),C("303947"),yf); return c+new Color(g,g,g,0);
            }
            if(edge<1.5f) return new Color(.31f,.31f,.33f,1);
            if(edge<3.5f) return new Color(1f,.97f,.86f,1);
            if(edge<5.5f) return new Color(.58f,.57f,.57f,1);
            if(edge<7f) return new Color(.88f,.86f,.83f,1);
            float v=(slot ? .72f : .82f)+yf*.13f+g;
            return new Color(v,v,v,1);
        }

        private static void BuildUiSprites()
        {
            panelSkin=SpriteAsset("PanelFrame",96,96,(x,y)=>RectSkin(x,y,96,true,false),new Vector4(17,17,17,17));
            buttonSkin=SpriteAsset("ButtonFrame",64,64,(x,y)=>RectSkin(x,y,64,false,false),new Vector4(10,10,10,10));
            slotSkin=SpriteAsset("SlotFrame",64,64,(x,y)=>RectSkin(x,y,64,false,true),new Vector4(10,10,10,10));
            circleSkin=SpriteAsset("CircleFrame",128,128,(x,y)=>
            {
                float dx=(x-63.5f)/63.5f,dy=(y-63.5f)/63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                if(r>.98f)return Color.clear;
                if(r>.946f)return new Color(.43f,.41f,.38f,.97f);
                if(r>.90f)return new Color(1f,.92f,.78f,1);
                if(r>.85f)return new Color(.54f,.53f,.54f,1);
                float a=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg; float tick=Mathf.Abs(Mathf.Repeat(a+22.5f,45)-22.5f);
                if(r>.75f&&tick<2.2f)return new Color(.98f,.94f,.84f,1);
                if(r>.78f)return new Color(.70f,.69f,.67f,1);
                float v=.78f+dy*.11f-r*.09f+Grain(x,y)*.025f;
                return new Color(v,v,v,.96f);
            });
            stickSkin=SpriteAsset("JoystickRing",128,128,(x,y)=>
            {
                float dx=(x-63.5f)/63.5f,dy=(y-63.5f)/63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                if(r>.99f)return Color.clear;
                if(r>.94f)return C("5d4b3a"); if(r>.88f)return C("d0ae76"); if(r>.83f)return C("363d4b");
                float a=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg; float tick=Mathf.Abs(Mathf.Repeat(a+45,90)-45);
                if(r>.67f&&tick<4f)return C("e0be83"); if(r>.76f)return C("846b49");
                return new Color(.08f,.12f,.17f,.24f);
            });
            knobSkin=SpriteAsset("JoystickKnob",96,96,(x,y)=>
            {
                float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                if(r>.99f)return Color.clear;if(r>.93f)return C("59452e");if(r>.84f)return C("dcb887");
                if(r>.74f)return C("425663");if(Mathf.Abs(dx)+Mathf.Abs(dy)<.33f)return C("c7b58e");
                return Color.Lerp(C("263140"),C("6d8392"),Mathf.Clamp01((dy+1)*.5f));
            });
            menuShade=SpriteAsset("MenuShade",192,96,(x,y)=>
            {
                float u=x/191f,v=y/95f; float a=Mathf.Lerp(.90f,.03f,Mathf.SmoothStep(0,1,(u-.18f)/.62f));
                a=Mathf.Max(a,Mathf.Pow(Mathf.Abs(v*2-1),4)*.55f); return new Color(.045f,.058f,.084f,a);
            });
            emblemSkin=SpriteAsset("SunEmblem",128,128,(x,y)=>
            {
                float dx=(x-63.5f)/63.5f,dy=(y-63.5f)/63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                float a=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg; float delta=Mathf.Abs(Mathf.Repeat(a+22.5f,45)-22.5f);
                bool mark=(r>.58f&&r<.64f)||(r>.75f&&r<.79f)||(r>.8f&&r<.97f&&delta<2.8f)||
                    (Mathf.Abs(dx)<.07f&&dy>-.62f&&dy<.66f)||(Mathf.Abs(dy+.2f)<.04f&&Mathf.Abs(dx)<.26f)||(dy>.66f&&dy<.83f&&Mathf.Abs(dx)<(.83f-dy)*.4f);
                if(!mark)return Color.clear; return Color.Lerp(C("98764a"),C("f0cf92"),Mathf.Clamp01((dy+1)*.5f));
            });
            healthSkin=SpriteAsset("HealthFill",48,16,(x,y)=>
            {
                float v=y/15f; float c=(y==0||y==15) ? .54f : .68f+v*.32f;
                return new Color(c,c,c,1);
            });
        }

        private static void BuildUiPrefabs()
        {
            foreach(string path in Prefabs("Assets/Prefabs/UI")) if(!path.Contains("EventSystem")) EditPrefab(path,root=>
            {
                StyleUi(root);
                if(root.name=="UI_MainMenu") StyleMainMenu(root);
            });
            foreach(string n in new[]{"Combat_TouchControls","Combat_Backpack","Combat_DamagePopup"}) EditPrefab(CombatRoot+"/"+n+".prefab",StyleUi);
        }

        private static void StyleUi(Transform root)
        {
            if(root.name=="Combat_TouchControls")
            {
                var icon=Find(Find(root,"Button_Backpack"),"Icon")?.GetComponent<Image>();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot+"/Icons/Control_Backpack.png");if(icon!=null&&sprite!=null){icon.sprite=sprite;icon.preserveAspect=true;}
            }
            foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                string n=image.name; var button=image.GetComponent<Button>();
                if(n.StartsWith("Slot_")) {DarkFantasyTheme.SetFrame(image,slotSkin,DarkFantasyTheme.Slot,Image.Type.Sliced); continue;}
                if(n=="Highlight") {image.color=DarkFantasyTheme.Gold; continue;}
                if(n=="OnScreenStick") {DarkFantasyTheme.SetFrame(image,stickSkin,new Color(1,1,1,.76f),Image.Type.Simple);continue;}
                if(n=="Handle"&&image.transform.parent!=null&&image.transform.parent.name=="OnScreenStick") {DarkFantasyTheme.SetFrame(image,knobSkin,Color.white,Image.Type.Simple);continue;}
                if(n=="Button_Attack"||n=="Button_Skill_Dash"||n=="Button_Interact")
                {
                    Color tint=n=="Button_Attack"?new Color32(184,99,83,255):n=="Button_Skill_Dash"?new Color32(89,151,160,255):new Color32(178,140,78,255);
                    DarkFantasyTheme.SetFrame(image,circleSkin,tint,Image.Type.Simple); continue;
                }
                if(button!=null)
                {
                    Color tint=n.Contains("Play")||n.Contains("Resume")||n.Contains("Retry")?new Color32(166,124,68,255):n.Contains("Close")||n.Contains("Quit")||n.Contains("Drop")?new Color32(137,77,74,255):new Color32(82,99,118,255);
                    bool circular=n=="PauseButton";
                    DarkFantasyTheme.SetFrame(image,circular?circleSkin:buttonSkin,tint,circular?Image.Type.Simple:Image.Type.Sliced);
                    DarkFantasyTheme.FrameButton(button,tint,circular); continue;
                }
                if(n=="Card"||n=="BackpackWindow"||n=="PlayerHUD_Panel"||n=="Panel_Detail"||n=="Minimap"||n=="Panel_Shop"||n=="Panel_DungeonSelect"||n=="Panel"&&image.rectTransform.anchorMin==image.rectTransform.anchorMax)
                {DarkFantasyTheme.SetFrame(image,panelSkin,Color.white,Image.Type.Sliced);continue;}
                if(n=="DimOverlay"||n=="Panel"||n=="DefeatPanel"||n=="VictoryPanel") {image.color=new Color(.025f,.035f,.05f,.72f);continue;}
                if(n=="PlayerMarker"||n=="Facing") {image.color=DarkFantasyTheme.Gold;continue;}
                if(n=="Background"&&image.transform.parent!=null&&(image.transform.parent.GetComponent<Slider>()!=null||image.transform.parent.GetComponent<Toggle>()!=null))
                {DarkFantasyTheme.SetFrame(image,slotSkin,DarkFantasyTheme.EmptySlot,Image.Type.Sliced);continue;}
                if(n=="Background"&&root.name=="UI_CoinCounter") {DarkFantasyTheme.SetFrame(image,panelSkin,new Color(1,1,1,.93f),Image.Type.Sliced);continue;}
                if(n=="Fill") {image.sprite=healthSkin;image.type=Image.Type.Simple;image.color=new Color32(95,181,137,255);continue;}
                if(n=="Background"&&root.name!="UI_MainMenu") {image.color=new Color32(24,31,44,220);continue;}
            }
            foreach(Text text in root.GetComponentsInChildren<Text>(true))
            {
                if(root.name=="Combat_DamagePopup") {text.color=DarkFantasyTheme.Gold;continue;}
                bool goldText=text.name.Contains("Title")||text.name.Contains("Coins")||text.text.Contains("HIỆP SĨ")||text.text.StartsWith("Xu:");
                text.color=goldText?DarkFantasyTheme.Gold:DarkFantasyTheme.Ivory;
            }
            foreach(TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.color=text.name.Contains("Title")?DarkFantasyTheme.Gold:DarkFantasyTheme.Ivory;
            var minimap=root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m=>m!=null&&m.GetType().Name=="GameMinimap");
            if(minimap!=null)
            {
                var so=new SerializedObject(minimap);
                so.FindProperty("seenColor").colorValue=new Color32(76,83,101,200); so.FindProperty("visitedColor").colorValue=new Color32(155,167,178,245);
                so.FindProperty("combatColor").colorValue=new Color32(205,89,80,245); so.FindProperty("treasureColor").colorValue=new Color32(232,186,93,245); so.FindProperty("exitColor").colorValue=new Color32(174,127,218,245);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void StyleMainMenu(Transform root)
        {
            var bg=Find(root,"Background")?.GetComponent<Image>(); if(bg!=null) {bg.sprite=menuShade;bg.color=Color.white;bg.type=Image.Type.Simple;}
            var card=Find(root,"Card") as RectTransform; if(card==null)return;
            card.anchorMin=card.anchorMax=new Vector2(.29f,.5f);card.anchoredPosition=Vector2.zero;card.sizeDelta=new Vector2(740f,0f);
            var layout=card.GetComponent<VerticalLayoutGroup>(); if(layout!=null) {layout.padding=new RectOffset(54,54,34,44);layout.spacing=17;}
            var cardImage=card.GetComponent<Image>(); DarkFantasyTheme.SetFrame(cardImage,panelSkin,new Color(1,1,1,.89f),Image.Type.Sliced);
            DeleteArt(card,"Art_SunEmblem");
            var mark=new GameObject("Art_SunEmblem",typeof(RectTransform),typeof(Image),typeof(LayoutElement));mark.transform.SetParent(card,false);mark.transform.SetAsFirstSibling();mark.layer=card.gameObject.layer;
            var icon=mark.GetComponent<Image>();icon.sprite=emblemSkin;icon.preserveAspect=true;icon.raycastTarget=false;mark.GetComponent<LayoutElement>().preferredHeight=76;
            var title=Find(card,"Title")?.GetComponent<TMP_Text>(); if(title!=null)
            {
                title.text="LIGHT SOULS";title.fontSize=73;title.characterSpacing=3.3f;title.fontStyle=FontStyles.Bold;title.color=DarkFantasyTheme.Gold;
                var le=title.GetComponent<LayoutElement>()??title.gameObject.AddComponent<LayoutElement>();le.preferredHeight=95;
            }
            var subtitle=Find(card,"Subtitle")?.GetComponent<TMP_Text>(); if(subtitle!=null){subtitle.text="Khám phá hầm ngục • Thắp sáng bóng tối";subtitle.fontSize=27;subtitle.color=DarkFantasyTheme.Muted;}
        }

        private static void StyleEnemyHealth(Transform root)
        {
            var ui=Find(root,"WorldHealthBarCanvas"); if(ui==null)return;
            foreach(Image image in ui.GetComponentsInChildren<Image>(true))
            {
                if(image.name=="Background") {image.sprite=slotSkin;image.type=Image.Type.Sliced;image.color=new Color32(36,40,50,230);}
                else if(image.name=="Fill") {image.sprite=healthSkin;image.type=Image.Type.Simple;image.color=new Color32(162,68,64,255);}
            }
            foreach(Text text in ui.GetComponentsInChildren<Text>(true)) text.color=DarkFantasyTheme.Ivory;
        }
    }
}

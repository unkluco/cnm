using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>Shared antique-metal frame and delayed damage sliver for mob and boss health bars.</summary>
    public sealed class DarkVitalityBar
    {
        private readonly Image fill, chip;
        private readonly Color baseFill, lowFill;
        private bool isAlly;
        private float target=1f, chipAmount=1f, chipDelay;
        private static Font font;
        private static Sprite fillSprite;
        public float Ratio=>target;
        public float ChipRatio=>chipAmount;

        private static Color C(byte r,byte g,byte b,byte a=255)=>new Color32(r,g,b,a);
        private DarkVitalityBar(Image fill,Image chip,Color baseFill)
        {this.fill=fill;this.chip=chip;this.baseFill=baseFill;lowFill=C(236,84,63);}

        public static DarkVitalityBar Create(RectTransform parent,Vector2 position,Vector2 size,Color accent,bool boss)
        {
            RectTransform root=Rect(parent,"Vitality_Ornament",position,size);
            // Fine bronze edge over charcoal, with pointed terminals; avoid the previous chunky green slider.
            Image(root,"Shadow",new Vector2(0,-1),size+new Vector2(4,5),C(8,10,16,220));
            Image(root,"Frame",Vector2.zero,size,C(139,107,74));
            Image(root,"Recess",Vector2.zero,size-new Vector2(3,3),C(25,22,31,250));
            Vector2 inner=size-new Vector2(boss?8:5,boss?6:5);
            var trail=Image(root,"Vitality_DelayedDamage",Vector2.zero,inner,C(228,163,79));
            var front=Image(root,"Vitality_Fill",Vector2.zero,inner,Color.Lerp(C(160,49,64),accent,boss?.17f:.06f));
            FillSprite(trail);FillSprite(front);
            foreach(int s in new[]{-1,1})
            {
                float diamond=boss?13:7;
                Vector2 p=new Vector2(s*(size.x*.5f+1),0);
                var rim=Image(root,"Finial",p,Vector2.one*diamond,C(176,139,90));rim.rectTransform.localRotation=Quaternion.Euler(0,0,45);
                var jewel=Image(root,"Finial_Inlay",p,Vector2.one*(diamond*.53f),boss?accent:C(101,62,65));jewel.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            if(boss)
            {
                // The centre notch marks the phase transition; smaller marks help estimate health.
                for(int i=1;i<10;i++)
                {
                    float h=i==5?size.y-2:size.y*.30f;
                    Image(root,i==5?"PhaseThreshold":"Division",new Vector2((i/10f-.5f)*inner.x,(size.y-h)*.5f-1),
                        new Vector2(i==5?2:1,h),i==5?C(246,218,167,235):C(30,20,25,145));
                }
            }
            return new DarkVitalityBar(front,trail,front.color);
        }

        public void SetHealth(float current,float maximum,bool immediate=false)
        {
            float ratio=maximum>0?Mathf.Clamp01(current/maximum):0f;
            if(immediate || ratio>=target){chipAmount=ratio;chipDelay=0;}
            else{chipAmount=Mathf.Max(chipAmount,target);chipDelay=.17f;}
            target=ratio;fill.fillAmount=ratio;chip.fillAmount=chipAmount;
            UpdateColor();
        }

        public void SetAlly(bool value){isAlly=value;UpdateColor();}
        private void UpdateColor(){fill.color=isAlly?new Color(.23f,.77f,.55f):Color.Lerp(lowFill,baseFill,Mathf.Clamp01(target*1.7f));}

        public void Tick(float dt)
        {
            if(chipDelay>0){chipDelay-=dt;return;}
            if(Mathf.Abs(chipAmount-target)<.001f)return;
            chipAmount=Mathf.MoveTowards(chipAmount,target,dt*.88f);chip.fillAmount=chipAmount;
        }

        private static void FillSprite(Image image)
        {
            if(fillSprite==null)fillSprite=Resources.Load<Sprite>("Art/DarkFantasy/UI/HealthFill");
            image.sprite=fillSprite;image.type=UnityEngine.UI.Image.Type.Filled;
            image.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;image.fillOrigin=0;image.fillAmount=1f;
        }

        public static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }

        public static Image Image(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var rt=Rect(parent,name,position,size);var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }

        public static Text Label(Transform parent,string name,string content,Vector2 position,Vector2 size,int fontSize,Color color,
            TextAnchor alignment=TextAnchor.MiddleCenter,bool bold=false)
        {
            var rt=Rect(parent,name,position,size);var label=rt.gameObject.AddComponent<Text>();
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font=font;label.text=content;label.fontSize=fontSize;label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            label.color=color;label.alignment=alignment;label.raycastTarget=false;label.resizeTextForBestFit=true;
            label.resizeTextMinSize=Mathf.Max(9,fontSize-6);label.resizeTextMaxSize=fontSize;
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=C(12,10,14,235);outline.effectDistance=new Vector2(1,-1);
            return label;
        }
    }
}

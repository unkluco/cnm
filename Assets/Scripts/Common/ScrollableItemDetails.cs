using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Common
{
    /// <summary>Keep long item/skill descriptions readable at mobile sizes.</summary>
    public sealed class ScrollableItemDetails
    {
        private readonly ScrollRect scroll;
        private readonly Text text;
        public ScrollRect Scroll=>scroll;

        private ScrollableItemDetails(ScrollRect scroll,Text text){this.scroll=scroll;this.text=text;}

        public static ScrollableItemDetails Wrap(Text label,string name)
        {
            if(label==null)return null;
            var old=label.rectTransform;
            var host=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(ScrollRect));
            var rect=(RectTransform)host.transform;rect.SetParent(old.parent,false);rect.SetSiblingIndex(old.GetSiblingIndex());
            rect.anchorMin=old.anchorMin;rect.anchorMax=old.anchorMax;rect.pivot=old.pivot;rect.offsetMin=old.offsetMin;rect.offsetMax=old.offsetMax;
            var background=host.GetComponent<Image>();background.color=new Color(.025f,.04f,.065f,.27f);background.raycastTarget=true;
            var viewportObject=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));
            var view=(RectTransform)viewportObject.transform;view.SetParent(rect,false);view.anchorMin=Vector2.zero;view.anchorMax=Vector2.one;view.offsetMin=new Vector2(7,5);view.offsetMax=new Vector2(-17,-5);
            old.SetParent(view,false);old.anchorMin=new Vector2(0,1);old.anchorMax=Vector2.one;old.pivot=new Vector2(.5f,1);
            old.anchoredPosition=Vector2.zero;old.sizeDelta=Vector2.zero;label.verticalOverflow=VerticalWrapMode.Overflow;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.resizeTextForBestFit=false;label.alignment=TextAnchor.UpperLeft;label.raycastTarget=false;

            var barObject=new GameObject("ScrollIndicator",typeof(RectTransform),typeof(Image),typeof(Scrollbar));
            var track=(RectTransform)barObject.transform;track.SetParent(rect,false);track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);
            track.offsetMin=new Vector2(-12,8);track.offsetMax=new Vector2(-5,-8);barObject.GetComponent<Image>().color=new Color32(20,24,33,180);
            var area=new GameObject("SlidingArea",typeof(RectTransform));var slide=(RectTransform)area.transform;slide.SetParent(track,false);slide.anchorMin=Vector2.zero;slide.anchorMax=Vector2.one;slide.offsetMin=slide.offsetMax=Vector2.zero;
            var handleObject=new GameObject("Handle",typeof(RectTransform),typeof(Image));var handle=(RectTransform)handleObject.transform;handle.SetParent(slide,false);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var handleImage=handleObject.GetComponent<Image>();handleImage.color=DarkFantasyTheme.Gold;
            var bar=barObject.GetComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=handleImage;
            var scroll=host.GetComponent<ScrollRect>();scroll.viewport=view;scroll.content=old;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=true;scroll.scrollSensitivity=30f;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return new ScrollableItemDetails(scroll,label);
        }

        public void SetText(string value)
        {
            text.text=value??string.Empty;Canvas.ForceUpdateCanvases();
            var rt=text.rectTransform;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(scroll.viewport.rect.height,text.preferredHeight+8));
            scroll.StopMovement();scroll.verticalNormalizedPosition=1f;
        }
    }
}

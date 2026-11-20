using System.Collections.Generic;
using System.Linq;
using LightSouls.Common;
using LightSouls.MapGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Lobby
{
    /// <summary>Mobile landscape journal: browse illustrated regions, compare difficulty, then confirm travel.</summary>
    public class DungeonSelectUI : MonoBehaviour
    {
        [SerializeField] private DungeonCatalogSO catalog;
        [SerializeField] private GameObject panelRoot;
        // Legacy scene fields remain serialized for backwards compatibility with LobbyTest.
        [SerializeField] private Transform dungeonListContainer;
        [SerializeField] private GameObject dungeonRowPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text statusNotificationText;

        private class Card
        {
            public DungeonEntry Entry;
            public Image Frame, Accent;
            public Text Action;
        }

        private readonly List<Card> cards=new List<Card>();
        private RectTransform cardContent,viewport;
        private ScrollRect scroll;
        private Text selectedTitle,selectedDetail;
        private Image selectedTrim;
        private Button confirmButton;
        private DungeonEntry selected;
        private bool built,confirming;
        public bool IsOpen=>panelRoot!=null&&panelRoot.activeSelf;
        public string SelectedDungeonId=>selected?.dungeonId;
        public int CardCount=>cards.Count;

        private void Awake(){if(panelRoot==null)panelRoot=gameObject;}
        private void OnDisable()=>BackButtonStack.Remove(this);

        public void Open(GameObject interactor)
        {
            if(panelRoot==null)panelRoot=gameObject;
            panelRoot.SetActive(true);confirming=false;
            if(!built)BuildJournal();
            BackButtonStack.Push(this,Close);
            PopulateDungeons();
        }

        public void Close(){BackButtonStack.Remove(this);if(panelRoot!=null)panelRoot.SetActive(false);}
        public void SetCatalog(DungeonCatalogSO newCatalog){catalog=newCatalog;if(IsOpen&&built)PopulateDungeons();}

        private void BuildJournal()
        {
            built=true;
            var panel=(RectTransform)panelRoot.transform;
            // Only replace the legacy children at runtime; no scene or prefab reference is lost.
            foreach(Transform old in panel)old.gameObject.SetActive(false);
            panel.anchorMin=new Vector2(.035f,.055f);panel.anchorMax=new Vector2(.965f,.945f);panel.offsetMin=panel.offsetMax=Vector2.zero;
            var frame=panelRoot.GetComponent<Image>()??panelRoot.AddComponent<Image>();DarkFantasyTheme.FramePanel(frame,Color.white);
            var journal=Rect(panel,"DungeonJournal",new(.02f,.025f),new(.98f,.975f));
            Label(journal,"Eyebrow","CỔNG DỊ GIỚI  ◆  HÀNH TRÌNH TIẾP THEO",new(.025f,.914f),new(.76f,.98f),23,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            Label(journal,"Title","CHỌN HẦM NGỤC",new(.024f,.824f),new(.75f,.92f),49,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft,true);
            Label(journal,"Subtitle","Mỗi vùng có quái, địa hình và phần thưởng riêng. Hãy chuẩn bị trước khi bước vào.",new(.026f,.772f),new(.89f,.828f),23,DarkFantasyTheme.Muted,TextAnchor.MiddleLeft);
            var close=ButtonAt(journal,"CloseJournal","ĐÓNG  ×",new(.855f,.875f),new(.975f,.965f),DarkFantasyTheme.Ink,24);close.onClick.AddListener(Close);
            GraphicAt(journal,"HeaderRule",new(.025f,.769f),new(.975f,.774f),DarkFantasyTheme.Bronze);

            viewport=Rect(journal,"RegionViewport",new(.025f,.205f),new(.975f,.755f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitSurface=viewport.gameObject.AddComponent<Image>();hitSurface.color=new Color(0,0,0,.01f);hitSurface.raycastTarget=true;
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.vertical=false;scroll.horizontal=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            cardContent=Rect(viewport,"RegionCards",Vector2.zero,Vector2.one);cardContent.pivot=new Vector2(0,.5f);scroll.content=cardContent;

            var footer=Rect(journal,"SelectedRegion",new(.025f,.025f),new(.975f,.19f));
            DarkFantasyTheme.FramePanel(footer.gameObject.AddComponent<Image>(),new Color(.89f,.89f,.89f,1f));
            selectedTrim=GraphicAt(footer,"SelectedAccent",new(.012f,.13f),new(.018f,.87f),DarkFantasyTheme.Gold);
            selectedTitle=Label(footer,"SelectedTitle",string.Empty,new(.035f,.51f),new(.69f,.92f),31,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft,true);
            selectedDetail=Label(footer,"SelectedDetail",string.Empty,new(.035f,.08f),new(.69f,.54f),22,DarkFantasyTheme.Muted,TextAnchor.MiddleLeft);
            confirmButton=ButtonAt(footer,"EnterDungeonButton","BƯỚC VÀO  ›",new(.72f,.17f),new(.982f,.83f),DarkFantasyTheme.Action,31);
            confirmButton.onClick.AddListener(ConfirmSelectedDungeon);
        }

        public void PopulateDungeons()
        {
            if(!built||cardContent==null)return;
            foreach(var card in cards){if(card.Frame!=null){card.Frame.gameObject.SetActive(false);Destroy(card.Frame.gameObject);}}
            cards.Clear();selected=null;
            var entries=catalog!=null?catalog.Entries.Where(e=>e!=null&&e.config!=null).ToArray():System.Array.Empty<DungeonEntry>();
            Canvas.ForceUpdateCanvases();
            int count=entries.Length,visible=Mathf.Max(1,Mathf.Min(3,count));
            float extraWidth=viewport.rect.width*Mathf.Max(0,count/(float)visible-1f);
            cardContent.sizeDelta=new Vector2(extraWidth,0);cardContent.anchoredPosition=Vector2.zero;
            scroll.horizontal=count>3;
            for(int i=0;i<count;i++)cards.Add(CreateCard(entries[i],i,count));
            var preferred=entries.FirstOrDefault(e=>e.dungeonId==RunState.SelectedDungeonId)??entries.FirstOrDefault();
            Select(preferred);
            if(count>0&&preferred!=null&&count>3)scroll.horizontalNormalizedPosition=Mathf.Clamp01(System.Array.IndexOf(entries,preferred)/(float)Mathf.Max(1,count-3));
        }

        private Card CreateCard(DungeonEntry entry,int index,int count)
        {
            var challenge=DungeonChallenge.For(entry.dungeonId);
            var rt=Rect(cardContent,"Region_"+entry.dungeonId,new Vector2(index/(float)count+.007f,0),new Vector2((index+1f)/count-.007f,1));
            var frame=rt.gameObject.AddComponent<Image>();DarkFantasyTheme.FramePanel(frame,Color.white);
            var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=frame;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.96f,.84f);colors.pressedColor=new Color(.8f,.8f,.8f);colors.selectedColor=Color.white;button.colors=colors;
            button.onClick.AddListener(()=>Select(entry));
            GraphicAt(rt,"IllustrationBack",new(.035f,.455f),new(.965f,.965f),DarkFantasyTheme.Ink);
            var portrait=GraphicAt(rt,"RegionIllustration",new(.045f,.465f),new(.955f,.955f),Color.white);portrait.sprite=entry.icon;
            portrait.preserveAspect=true;if(entry.icon==null)portrait.color=Color.Lerp(DarkFantasyTheme.Ink,challenge.Accent,.24f);
            var badge=GraphicAt(rt,"DifficultyPlate",new(.058f,.865f),new(.80f,.95f),new Color(.05f,.06f,.10f,.92f));
            Label(badge.rectTransform,"Difficulty",challenge.Tier,new(.04f,.04f),new(.96f,.96f),20,challenge.Accent,TextAnchor.MiddleCenter,true);
            var accent=GraphicAt(rt,"SelectionRune",new(.04f,.435f),new(.96f,.45f),challenge.Accent);
            Label(rt,"RegionName",entry.displayName,new(.065f,.335f),new(.935f,.43f),34,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft,true);
            Label(rt,"RegionTraits",challenge.Traits,new(.065f,.265f),new(.935f,.335f),21,challenge.Accent,TextAnchor.MiddleLeft,true);
            Label(rt,"RegionDescription",entry.description,new(.065f,.10f),new(.935f,.265f),21,DarkFantasyTheme.Muted,TextAnchor.UpperLeft);
            var action=Label(rt,"SelectionAction","CHẠM ĐỂ CHỌN",new(.065f,.025f),new(.935f,.098f),20,challenge.Accent,TextAnchor.MiddleCenter,true);
            return new Card{Entry=entry,Frame=frame,Accent=accent,Action=action};
        }

        public bool SelectDungeon(string id)
        {
            var card=cards.Find(c=>c.Entry.dungeonId==id);if(card==null)return false;Select(card.Entry);return true;
        }
        private void Select(DungeonEntry entry)
        {
            selected=entry;
            foreach(var card in cards)
            {
                bool active=card.Entry==entry;var challenge=DungeonChallenge.For(card.Entry.dungeonId);
                card.Frame.color=active?Color.Lerp(Color.white,challenge.Accent,.22f):new Color(.75f,.78f,.83f,1f);
                card.Accent.color=active?challenge.Accent:Color.Lerp(DarkFantasyTheme.Ink,challenge.Accent,.5f);
                card.Action.text=active?"◆  ĐÃ CHỌN":"CHẠM ĐỂ CHỌN";
            }
            confirmButton.interactable=entry!=null;
            if(entry==null){selectedTitle.text="Không có hầm ngục";selectedDetail.text="Danh mục chưa được cấu hình.";return;}
            var profile=DungeonChallenge.For(entry.dungeonId);selectedTrim.color=profile.Accent;
            selectedTitle.text=entry.displayName+"  ·  CHUẨN BỊ XUẤT PHÁT";
            selectedDetail.text=$"Khoảng {entry.config.TargetRoomCount} phòng  •  Sinh lực quái x{profile.EnemyHealth:0.00}  •  Sát thương x{profile.EnemyDamage:0.00}\nĐộ khó tăng dần theo hành trình · Có kho báu ẩn";
        }

        public void ConfirmSelectedDungeon()
        {
            if(!IsOpen||selected==null||confirming)return;
            confirming=true;string id=selected.dungeonId;Close();LobbyController.SelectDungeon(id);
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
        }
        private static Image GraphicAt(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {var image=Rect(parent,name,min,max).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static Text Label(Transform parent,string name,string caption,Vector2 min,Vector2 max,int size,Color color,TextAnchor alignment,bool bold=false)
        {
            var text=Rect(parent,name,min,max).gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=caption;text.fontSize=size;text.color=color;text.alignment=alignment;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Max(14,size-8);text.resizeTextMaxSize=size;text.raycastTarget=false;return text;
        }
        private static Button ButtonAt(Transform parent,string name,string caption,Vector2 min,Vector2 max,Color tint,int size)
        {
            var rt=Rect(parent,name,min,max);var image=rt.gameObject.AddComponent<Image>();var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;
            DarkFantasyTheme.FrameButton(button,tint);Label(rt,"Label",caption,Vector2.zero,Vector2.one,size,DarkFantasyTheme.Ivory,TextAnchor.MiddleCenter,true);return button;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LightSouls.Combat.Inventory;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    public partial class BackpackUI
    {
        private sealed class MaterialTile { public Image Frame;public Text Count;public InventorySlot Slot; }
        private readonly Dictionary<string,MaterialTile> materialTiles=new();
        private GameObject materialsPane;
        private RectTransform materialContent;
        private Button itemsTabButton,materialsTabButton;
        private Text materialsTabText,materialEmpty;
        private bool materialTab;

        private void BuildMaterialTabs()
        {
            if(materialsPane!=null||backpackSlots.Count==0||backpackSlots[0].button==null)return;
            Transform center=backpackSlots[0].button.transform.parent;
            var label=center.Find("Label_Backpack");if(label!=null)label.gameObject.SetActive(false);
            itemsTabButton=TabButton(center,"Tab_Items","VẬT PHẨM (15 Ô)",new Vector2(-148,100),()=>SetMaterialTab(false),out _);
            materialsTabButton=TabButton(center,"Tab_Materials","NGUYÊN LIỆU (0)",new Vector2(148,100),()=>SetMaterialTab(true),out materialsTabText);
            var pane=DarkVitalityBar.Rect(center,"MaterialsPouch",new Vector2(0,-91),new Vector2(600,340));materialsPane=pane.gameObject;
            var viewport=pane.gameObject.AddComponent<Image>();viewport.color=new Color(.07f,.09f,.13f,.48f);pane.gameObject.AddComponent<RectMask2D>();
            var scroll=pane.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.viewport=pane;
            materialContent=DarkVitalityBar.Rect(pane,"MaterialGrid",Vector2.zero,Vector2.zero);
            materialContent.anchorMin=new Vector2(0,1);materialContent.anchorMax=Vector2.one;materialContent.pivot=new Vector2(.5f,1);materialContent.anchoredPosition=Vector2.zero;materialContent.sizeDelta=Vector2.zero;
            var grid=materialContent.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
            grid.cellSize=new Vector2(138,91);grid.spacing=new Vector2(7,7);grid.padding=new RectOffset(7,14,6,6);
            materialContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=materialContent;
            var track=DarkVitalityBar.Image(pane,"ScrollTrack",new Vector2(294,0),new Vector2(6,324),DarkFantasyTheme.Ink);
            var thumb=DarkVitalityBar.Image(track.transform,"Thumb",Vector2.zero,new Vector2(6,44),DarkFantasyTheme.Bronze);
            thumb.rectTransform.anchorMin=Vector2.zero;thumb.rectTransform.anchorMax=Vector2.one;thumb.rectTransform.offsetMin=thumb.rectTransform.offsetMax=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.targetGraphic=thumb;bar.handleRect=thumb.rectTransform;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            materialEmpty=DarkVitalityBar.Label(pane,"Empty","Chưa có nguyên liệu. Hãy khám phá ngục hoặc mua ở cửa hàng.",Vector2.zero,new Vector2(510,140),20,DarkFantasyTheme.Muted);
            materialsPane.SetActive(false);StyleMaterialTabs();
        }

        private Button TabButton(Transform parent,string name,string label,Vector2 position,UnityEngine.Events.UnityAction action,out Text text)
        {
            var img=DarkVitalityBar.Image(parent,name,position,new Vector2(286,34),Color.white);img.raycastTarget=true;
            var btn=img.gameObject.AddComponent<Button>();btn.targetGraphic=img;btn.onClick.AddListener(action);
            text=DarkVitalityBar.Label(img.transform,"Text",label,Vector2.zero,new Vector2(278,30),18,DarkFantasyTheme.Ivory,bold:true);return btn;
        }

        public void ShowMaterialPouch()=>SetMaterialTab(true);
        public bool IsMaterialPouchVisible=>materialTab;

        private void SetMaterialTab(bool show)
        {
            if(materialsPane==null)return;
            materialTab=show;ClearSelection();
            foreach(var slot in backpackSlots)if(slot.button!=null)slot.button.gameObject.SetActive(!show);
            materialsPane.SetActive(show);StyleMaterialTabs();RefreshMaterialTab();
        }

        private void StyleMaterialTabs()
        {
            if(itemsTabButton!=null)DarkFantasyTheme.FrameButton(itemsTabButton,!materialTab?DarkFantasyTheme.Selected:DarkFantasyTheme.Slot);
            if(materialsTabButton!=null)DarkFantasyTheme.FrameButton(materialsTabButton,materialTab?DarkFantasyTheme.Selected:DarkFantasyTheme.Slot);
        }

        private void RefreshMaterialTab()
        {
            if(inventory==null||materialsPane==null)return;
            var owned=inventory.MaterialSlots.Values.Where(s=>!s.IsEmpty).OrderBy(s=>s.Item.DisplayName).ToArray();
            materialsTabText.text=$"NGUYÊN LIỆU ({owned.Length})";
            if(!materialTab)return;
            materialEmpty.enabled=owned.Length==0;
            if(materialTiles.Count!=owned.Length||owned.Any(s=>!materialTiles.ContainsKey(s.SlotKey)))
            {
                foreach(Transform child in materialContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}materialTiles.Clear();
                foreach(var slot in owned)
                {
                    string key=slot.SlotKey;var frame=DarkVitalityBar.Image(materialContent,"Material_"+slot.Item.ItemId,Vector2.zero,new Vector2(138,91),Color.white);
                    DarkFantasyTheme.FrameSlot(frame,DarkFantasyTheme.Slot);frame.raycastTarget=true;
                    var button=frame.gameObject.AddComponent<Button>();button.targetGraphic=frame;
                    button.onClick.AddListener(()=>{if(_selectedSlotKey==key)ClearSelection();else SelectSlot(key);});
                    var icon=DarkVitalityBar.Image(frame.transform,"Icon",new Vector2(0,12),new Vector2(57,57),Color.white);icon.sprite=slot.Item.Icon;icon.preserveAspect=true;
                    DarkVitalityBar.Label(frame.transform,"Name",slot.Item.DisplayName,new Vector2(0,-30),new Vector2(126,25),16,DarkFantasyTheme.Ivory,bold:true);
                    var count=DarkVitalityBar.Label(frame.transform,"Count",slot.Count.ToString(),new Vector2(43,29),new Vector2(42,24),20,DarkFantasyTheme.Gold,TextAnchor.MiddleRight,true);
                    RarityFrame.Apply(frame.rectTransform,slot.Item);materialTiles.Add(key,new MaterialTile{Frame=frame,Count=count,Slot=slot});
                }
            }
            foreach(var tile in materialTiles.Values)tile.Count.text=tile.Slot.Count.ToString();UpdateMaterialHighlights();
        }

        private void UpdateMaterialHighlights()
        {
            foreach(var tile in materialTiles.Values)if(tile.Frame!=null)tile.Frame.color=tile.Slot.SlotKey==_selectedSlotKey?DarkFantasyTheme.Selected:DarkFantasyTheme.Slot;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Lobby
{
    /// <summary>Mobile forge catalogue: live material counts, one-tap upgrades and atomic batch crafting.</summary>
    public sealed class CraftingPanelUI : MonoBehaviour
    {
        private enum Tab { All, Weapons, Equipment, Consumables, Refine, Materials }
        private sealed class Row
        {
            public CraftRecipe Recipe;public CraftMaterialSO Material;public Image Background;public Text State;
        }
        private static readonly Color Ready=new Color32(135,209,169,255);
        private static readonly Color Missing=new Color32(235,129,106,255);
        private readonly List<Row> rows=new();
        private readonly List<Button> tabs=new();
        private CraftingStation station;
        private ICraftingInventory inventory;
        private CraftingCatalogSO catalog;
        private CraftRecipe selectedRecipe;
        private CraftMaterialSO selectedMaterial;
        private Tab currentTab;
        private int batches=1;
        private RectTransform canvasRect,panel,listContent,ingredientRoot;
        private ScrollRect listScroll;
        private Image outputIcon,outputFrame,craftPulse;
        private Text outputName,outputMeta,outputNote,quantityText,statusText,coinText,materialText;
        private Button craftButton,minus,plus;
        private GameObject actions,recipeDetail,materialDetail;
        private Font font;
        private bool open;
        private string status=string.Empty;
        private Coroutine pulseRoutine;

        public bool IsOpen=>open;
        public CraftRecipe SelectedRecipe=>selectedRecipe;
        public int BatchCount=>batches;
        public ScrollRect RecipeScroll=>listScroll;

        public static CraftingPanelUI Create()
        {
            var root=new GameObject("Crafting_ForgeUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            return root.AddComponent<CraftingPanelUI>();
        }

        private void Awake()
        {
            canvasRect=(RectTransform)transform;var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=14;
            var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1366,768);scaler.matchWidthOrHeight=.5f;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");BuildLayout();gameObject.SetActive(false);
        }

        public void Open(CraftingStation owner,ICraftingInventory bag)
        {
            if(owner==null||bag==null||owner.Catalog==null)return;
            Detach();station=owner;inventory=bag;catalog=owner.Catalog;open=true;batches=1;status=string.Empty;
            gameObject.SetActive(true);inventory.Changed+=Refresh;RunState.CoinsChanged+=OnCoinsChanged;
            BackButtonStack.Push(this,Close);ActivateTab(Tab.All);OnCoinsChanged(RunState.Coins);
        }

        public void Close()
        {
            open=false;Detach();BackButtonStack.Remove(this);
            if(gameObject.activeSelf)gameObject.SetActive(false);
        }

        private void OnDisable(){open=false;Detach();ResetPulse();BackButtonStack.Remove(this);}
        private void OnDestroy(){Detach();BackButtonStack.Remove(this);}
        private void Detach(){if(inventory!=null)inventory.Changed-=Refresh;RunState.CoinsChanged-=OnCoinsChanged;inventory=null;station=null;}
        private void OnCoinsChanged(int value){if(coinText!=null)coinText.text=$"Xu: {value}";}

        private void LateUpdate()
        {
            if(panel!=null&&canvasRect!=null)
            {
                float fit=Mathf.Min(1f,Mathf.Min(canvasRect.rect.width/1220f,canvasRect.rect.height/690f));
                if(fit>.1f)panel.localScale=Vector3.one*fit;
            }
        }

        private void BuildLayout()
        {
            var shade=Picture(transform,"Backdrop",Vector2.zero,Vector2.zero,new Color(.025f,.035f,.06f,.87f));Stretch(shade.rectTransform);shade.raycastTarget=true;
            panel=Rect(transform,"ForgePanel",Vector2.zero,new Vector2(1180,650));
            var frame=Picture(panel,"PanelFrame",Vector2.zero,new Vector2(1180,650),Color.white);DarkFantasyTheme.FramePanel(frame,Color.white);frame.raycastTarget=true;
            var crest=Picture(panel,"ForgeCrest",new Vector2(-525,278),new Vector2(54,54),Color.white);
            crest.sprite=Resources.Load<Sprite>("Art/DarkFantasy/UI/SunEmblem");crest.preserveAspect=true;crest.color=DarkFantasyTheme.Gold;
            Label(panel,"Title","LÒ RÈN ÁNH TÀN",new Vector2(-290,286),new Vector2(410,45),32,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            Label(panel,"Subtitle","Chế tạo trang bị  •  Nâng cấp  •  Tinh luyện",new Vector2(-218,249),new Vector2(555,25),17,DarkFantasyTheme.Muted,TextAnchor.MiddleLeft);
            coinText=Label(panel,"Coins","Xu: 0",new Vector2(425,283),new Vector2(160,32),24,DarkFantasyTheme.Gold,TextAnchor.MiddleRight,true);
            var close=MakeButton(panel,"Close","×",new Vector2(543,280),new Vector2(50,50),DarkFantasyTheme.Danger,34,Close);

            string[] labels={"TẤT CẢ","VŨ KHÍ","TRANG BỊ","TIÊU HAO","TINH LUYỆN","NGUYÊN LIỆU"};
            for(int i=0;i<labels.Length;i++) {Tab tab=(Tab)i;tabs.Add(MakeButton(panel,"Tab_"+tab,labels[i],new Vector2((i-2.5f)*180,211),new Vector2(174,38),DarkFantasyTheme.Slot,18,()=>ActivateTab(tab)));}

            var left=Rect(panel,"RecipePane",new Vector2(-369,-39),new Vector2(404,445));
            DarkFantasyTheme.FramePanel(Picture(left,"Background",Vector2.zero,new Vector2(404,445),Color.white),Color.white);
            var viewport=Rect(left,"Viewport",Vector2.zero,new Vector2(382,423));
            var viewImage=viewport.gameObject.AddComponent<Image>();viewImage.color=new Color(.06f,.075f,.10f,.8f);viewport.gameObject.AddComponent<RectMask2D>();
            listScroll=viewport.gameObject.AddComponent<ScrollRect>();listScroll.horizontal=false;listScroll.vertical=true;listScroll.movementType=ScrollRect.MovementType.Clamped;
            listScroll.scrollSensitivity=35;listScroll.viewport=viewport;
            listContent=Rect(viewport,"Content",Vector2.zero,Vector2.zero);listContent.anchorMin=new Vector2(0,1);listContent.anchorMax=Vector2.one;listContent.pivot=new Vector2(.5f,1);
            listContent.anchoredPosition=Vector2.zero;listContent.sizeDelta=Vector2.zero;
            var layout=listContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(5,5,5,5);layout.spacing=6;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;listScroll.content=listContent;
            var track=Picture(left,"ScrollTrack",new Vector2(193,0),new Vector2(6,414),DarkFantasyTheme.Ink);
            var handle=Picture(track.transform,"Thumb",Vector2.zero,new Vector2(6,50),DarkFantasyTheme.Bronze);
            Stretch(handle.rectTransform);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            listScroll.verticalScrollbar=bar;listScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;

            var detail=Rect(panel,"DetailPane",new Vector2(205,-39),new Vector2(728,445));
            DarkFantasyTheme.FramePanel(Picture(detail,"Background",Vector2.zero,new Vector2(728,445),Color.white),Color.white);
            outputFrame=Picture(detail,"OutputFrame",new Vector2(-285,147),new Vector2(114,114),Color.white);DarkFantasyTheme.FrameSlot(outputFrame,DarkFantasyTheme.Slot);
            outputIcon=Picture(outputFrame.transform,"Output",Vector2.zero,new Vector2(104,104),Color.white);outputIcon.preserveAspect=true;
            craftPulse=Picture(outputFrame.transform,"CraftPulse",Vector2.zero,new Vector2(118,118),DarkFantasyTheme.Gold);
            craftPulse.sprite=crest.sprite;craftPulse.preserveAspect=true;craftPulse.enabled=false;
            outputName=Label(detail,"OutputName","",new Vector2(54,176),new Vector2(530,40),29,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft,true);
            outputMeta=Label(detail,"OutputMeta","",new Vector2(54,139),new Vector2(530,30),18,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            outputNote=Label(detail,"OutputSummary","",new Vector2(54,100),new Vector2(530,57),17,DarkFantasyTheme.Muted,TextAnchor.UpperLeft);

            recipeDetail=Rect(detail,"RecipeDetail",Vector2.zero,new Vector2(700,400)).gameObject;
            Label(recipeDetail.transform,"IngredientHeading","NGUYÊN LIỆU YÊU CẦU",new Vector2(-125,62),new Vector2(410,27),17,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            Label(recipeDetail.transform,"CountHeading","ĐANG CÓ / CẦN",new Vector2(213,62),new Vector2(220,27),17,DarkFantasyTheme.Gold,TextAnchor.MiddleRight,true);
            ingredientRoot=Rect(recipeDetail.transform,"Ingredients",new Vector2(0,-54),new Vector2(666,180));
            actions=Rect(recipeDetail.transform,"Actions",new Vector2(0,-174),new Vector2(666,60)).gameObject;
            minus=MakeButton(actions.transform,"Less","−",new Vector2(-274,0),new Vector2(49,52),DarkFantasyTheme.Slot,28,()=>SetBatchCount(batches-1));
            quantityText=Label(actions.transform,"Quantity","× 1",new Vector2(-212,0),new Vector2(70,45),24,DarkFantasyTheme.Ivory,TextAnchor.MiddleCenter,true);
            plus=MakeButton(actions.transform,"More","+",new Vector2(-150,0),new Vector2(49,52),DarkFantasyTheme.Slot,28,()=>SetBatchCount(batches+1));
            craftButton=MakeButton(actions.transform,"Craft","CHẾ TẠO",new Vector2(122,0),new Vector2(405,56),new Color32(134,77,36,255),23,()=>CraftSelected());

            materialDetail=Rect(detail,"MaterialDetail",Vector2.zero,new Vector2(680,400)).gameObject;
            materialText=Label(materialDetail.transform,"SourcesAndUses","",new Vector2(0,-56),new Vector2(656,273),20,DarkFantasyTheme.Ivory,TextAnchor.UpperLeft);
            materialDetail.SetActive(false);
            statusText=Label(panel,"Status","",new Vector2(0,-293),new Vector2(1090,33),19,DarkFantasyTheme.Gold,TextAnchor.MiddleCenter,true);
        }

        private void ActivateTab(Tab tab)
        {
            ResetPulse();currentTab=tab;batches=1;status=string.Empty;selectedRecipe=null;selectedMaterial=null;
            for(int i=0;i<tabs.Count;i++)DarkFantasyTheme.FrameButton(tabs[i],i==(int)tab?new Color32(126,89,47,255):DarkFantasyTheme.Slot);
            foreach(Transform child in listContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}rows.Clear();
            if(catalog==null)return;
            if(tab==Tab.Materials)
            {
                foreach(var material in catalog.Materials)if(material!=null)rows.Add(CreateRow(null,material));
                selectedMaterial=rows.FirstOrDefault()?.Material;
            }
            else
            {
                foreach(var recipe in catalog.Recipes)if(recipe!=null&&recipe.IsValid&&Matches(recipe,tab))rows.Add(CreateRow(recipe,null));
                selectedRecipe=rows.FirstOrDefault()?.Recipe;
            }
            listContent.anchoredPosition=Vector2.zero;listScroll.verticalNormalizedPosition=1;Refresh();
        }

        private static bool Matches(CraftRecipe recipe,Tab tab)=>tab switch
        {
            Tab.Weapons=>recipe.Output.Category==ItemCategory.Weapon,
            Tab.Equipment=>recipe.Output.Category==ItemCategory.Equipment,
            Tab.Consumables=>recipe.Output.Category==ItemCategory.Consumable,
            Tab.Refine=>recipe.Output.Category==ItemCategory.Material,
            _=>true
        };

        private Row CreateRow(CraftRecipe recipe,CraftMaterialSO material)
        {
            ItemDefinitionSO item=recipe!=null?recipe.Output:material;
            var bg=Picture(listContent,"Row_"+(recipe!=null?recipe.Id:material.ItemId),Vector2.zero,new Vector2(366,72),Color.white);
            DarkFantasyTheme.FrameSlot(bg,DarkFantasyTheme.Slot);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight=74;
            bg.raycastTarget=true;var button=bg.gameObject.AddComponent<Button>();button.targetGraphic=bg;
            var iconFrame=Picture(bg.transform,"IconFrame",new Vector2(-144,0),new Vector2(61,61),Color.white);DarkFantasyTheme.FrameSlot(iconFrame,DarkFantasyTheme.EmptySlot);
            var icon=Picture(iconFrame.transform,"Icon",Vector2.zero,new Vector2(56,56),Color.white);icon.sprite=item.Icon;icon.preserveAspect=true;RarityFrame.Apply(iconFrame.rectTransform,item);
            Label(bg.transform,"Name",item.DisplayName+(recipe!=null&&recipe.OutputCount>1?$" ×{recipe.OutputCount}":""),new Vector2(29,14),new Vector2(277,31),21,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft,true);
            var state=Label(bg.transform,"State","",new Vector2(29,-17),new Vector2(277,27),15,DarkFantasyTheme.Muted,TextAnchor.MiddleLeft);
            button.onClick.AddListener(()=>{ResetPulse();status=string.Empty;batches=1;selectedRecipe=recipe;selectedMaterial=material;Refresh();});
            return new Row{Recipe=recipe,Material=material,Background=bg,State=state};
        }

        public bool SelectRecipe(string recipeId)
        {
            var recipe=catalog!=null?catalog.FindRecipe(recipeId):null;if(recipe==null)return false;
            ActivateTab(Tab.All);selectedRecipe=recipe;selectedMaterial=null;batches=1;Refresh();return true;
        }

        public void SetBatchCount(int count){batches=Mathf.Clamp(count,1,20);status=string.Empty;Refresh();}

        public CraftResult CraftSelected()
        {
            if(!open||inventory==null||selectedRecipe==null)return CraftResult.InvalidRecipe;
            var result=inventory.TryCraft(selectedRecipe,batches);
            status=result==CraftResult.Success?$"ĐÃ CHẾ TẠO  {selectedRecipe.Output.DisplayName} ×{selectedRecipe.OutputCount*batches}":ResultText(result);
            if(result==CraftResult.Success){station?.Celebrate();ResetPulse();pulseRoutine=StartCoroutine(ShineOnCraft());}
            Refresh();return result;
        }

        private IEnumerator ShineOnCraft()
        {
            if(craftPulse==null)yield break;
            craftPulse.enabled=true;craftPulse.transform.SetAsLastSibling();float elapsed=0;
            while(elapsed<.65f)
            {
                float t=elapsed/.65f;float eased=1f-(1f-t)*(1f-t);
                craftPulse.rectTransform.localScale=Vector3.one*Mathf.Lerp(.82f,1.65f,eased);
                craftPulse.color=new Color(1f,.74f,.32f,.8f*(1f-t));
                outputIcon.rectTransform.localScale=Vector3.one*(1f+.10f*Mathf.Sin(t*Mathf.PI));
                elapsed+=Time.unscaledDeltaTime;yield return null;
            }
            craftPulse.enabled=false;outputIcon.rectTransform.localScale=Vector3.one;pulseRoutine=null;
        }

        private void ResetPulse()
        {
            if(pulseRoutine!=null){StopCoroutine(pulseRoutine);pulseRoutine=null;}
            if(craftPulse!=null)craftPulse.enabled=false;
            if(outputIcon!=null)outputIcon.rectTransform.localScale=Vector3.one;
        }

        private static string ResultText(CraftResult result)=>result switch
        {
            CraftResult.MissingIngredients=>"Chưa đủ nguyên liệu",
            CraftResult.NoSpace=>"Túi đồ không đủ chỗ",
            CraftResult.InvalidRecipe=>"Công thức không hợp lệ",
            _=>"Đủ nguyên liệu"
        };

        private void Refresh()
        {
            if(inventory==null||catalog==null)return;
            foreach(var row in rows)
            {
                bool selected=row.Recipe!=null?row.Recipe==selectedRecipe:row.Material==selectedMaterial;
                row.Background.color=selected?DarkFantasyTheme.Selected:DarkFantasyTheme.Slot;
                if(row.Material!=null){int owned=inventory.GetCount(row.Material);row.State.text=$"Đang có: {owned}  •  {ItemRarityStyle.GetDisplayName(row.Material.Rarity)}";row.State.color=owned>0?Ready:DarkFantasyTheme.Muted;continue;}
                var result=inventory.EvaluateCraft(row.Recipe);row.State.text=$"{row.Recipe.KindLabel}  •  {ResultText(result)}";row.State.color=result==CraftResult.Success?Ready:DarkFantasyTheme.Muted;
            }
            statusText.text=!string.IsNullOrEmpty(status)?status:selectedMaterial!=null?"Nguyên liệu được cất riêng • Không chiếm ô vật phẩm":selectedRecipe?.Kind==CraftRecipeKind.Upgrade?"Nâng cấp sẽ sử dụng món gốc • Hãy tháo trang bị đang mặc trước":"Chế tạo không tốn xu • Có thể chế tạo nhiều lượt khi đủ nguyên liệu";
            statusText.color=string.IsNullOrEmpty(status)?DarkFantasyTheme.Gold:status.StartsWith("ĐÃ CHẾ TẠO",StringComparison.Ordinal)?Ready:Missing;
            if(selectedMaterial!=null)
            {
                recipeDetail.SetActive(false);materialDetail.SetActive(true);
                SetOutput(selectedMaterial,$"NGUYÊN LIỆU  •  {ItemRarityStyle.GetDisplayName(selectedMaterial.Rarity)}",selectedMaterial.Description);
                var uses=catalog.Recipes.Where(r=>r!=null&&r.Ingredients.Any(i=>i.Item==selectedMaterial)).Select(r=>r.Output.DisplayName).Distinct().ToArray();
                materialText.text=$"ĐANG CÓ: {inventory.GetCount(selectedMaterial)} / {selectedMaterial.MaxStack}\n\nNGUỒN THU THẬP\n{selectedMaterial.SourceHint}\n\nDÙNG ĐỂ CHẾ TẠO\n"+string.Join("  •  ",uses.Take(9))+(uses.Length>9?$"  +{uses.Length-9} món khác":"");return;
            }
            recipeDetail.SetActive(true);materialDetail.SetActive(false);
            foreach(Transform row in ingredientRoot){row.gameObject.SetActive(false);Destroy(row.gameObject);}
            if(selectedRecipe==null){outputName.text="Chưa có công thức";outputIcon.enabled=false;craftButton.interactable=false;return;}
            string summary=selectedRecipe.Note;
            if(selectedRecipe.Output.Category!=ItemCategory.Material)
            {
                string stats=string.Join("  •  ",selectedRecipe.Output.GetStatsText().Split('\n').Skip(1).Take(2));
                if(!string.IsNullOrEmpty(stats))summary+="\n"+stats;
            }
            SetOutput(selectedRecipe.Output,$"{selectedRecipe.KindLabel}  •  {ItemRarityStyle.GetDisplayName(selectedRecipe.Output.Rarity)}  •  Nhận ×{selectedRecipe.OutputCount*batches}",summary);
            quantityText.text=$"× {batches}";minus.interactable=batches>1;plus.interactable=batches<20;
            for(int i=0;i<selectedRecipe.Ingredients.Count;i++)
            {
                var ingredient=selectedRecipe.Ingredients[i];int need=ingredient.Count*batches;int have=inventory.GetCraftableCount(ingredient.Item);
                float y=70-i*46;
                var row=Rect(ingredientRoot,"Need_"+ingredient.Item.ItemId,new Vector2(0,y),new Vector2(660,41));
                Picture(row,"Inset",Vector2.zero,new Vector2(660,41),new Color32(24,29,39,220));
                var image=Picture(row,"Icon",new Vector2(-300,0),new Vector2(36,36),Color.white);image.sprite=ingredient.Item.Icon;image.preserveAspect=true;
                Label(row,"Ingredient",ingredient.Item.DisplayName,new Vector2(-90,0),new Vector2(360,34),19,DarkFantasyTheme.Ivory,TextAnchor.MiddleLeft);
                string worn=inventory.GetCount(ingredient.Item)>have?"  (đang mặc)":"";
                Label(row,"Count",$"{have} / {need}{worn}",new Vector2(213,0),new Vector2(220,34),18,have>=need?Ready:Missing,TextAnchor.MiddleRight,true);
            }
            var readiness=inventory.EvaluateCraft(selectedRecipe,batches);craftButton.interactable=readiness==CraftResult.Success;
            var text=craftButton.GetComponentInChildren<Text>();text.text=readiness==CraftResult.Success?(selectedRecipe.Kind==CraftRecipeKind.Upgrade?"NÂNG CẤP":"CHẾ TẠO"):ResultText(readiness).ToUpperInvariant();
        }

        private void SetOutput(ItemDefinitionSO item,string meta,string note)
        {
            outputName.text=item.DisplayName;outputMeta.text=meta;outputMeta.color=ItemRarityStyle.GetColor(item.Rarity);
            outputIcon.sprite=item.Icon;outputIcon.enabled=item.Icon!=null;outputNote.text=string.IsNullOrWhiteSpace(note)?item.Description:note;
            RarityFrame.Apply(outputFrame.rectTransform,item);
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        private static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
        private static Image Picture(Transform parent,string name,Vector2 p,Vector2 size,Color color)
        {var r=Rect(parent,name,p,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private Text Label(Transform parent,string name,string value,Vector2 p,Vector2 size,int fontSize,Color color,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false)
        {
            var r=Rect(parent,name,p,size);var text=r.gameObject.AddComponent<Text>();text.font=font;text.fontSize=fontSize;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            text.color=color;text.alignment=align;text.text=value;text.raycastTarget=false;text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Max(12,fontSize-6);text.resizeTextMaxSize=fontSize;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        private Button MakeButton(Transform parent,string name,string caption,Vector2 p,Vector2 size,Color color,int fontSize,Action action)
        {
            var image=Picture(parent,name,p,size,Color.white);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;DarkFantasyTheme.FrameButton(button,color);button.onClick.AddListener(()=>action());
            var text=Label(image.transform,"Text",caption,Vector2.zero,size-new Vector2(8,5),fontSize,DarkFantasyTheme.Ivory,TextAnchor.MiddleCenter,true);return button;
        }
    }
}

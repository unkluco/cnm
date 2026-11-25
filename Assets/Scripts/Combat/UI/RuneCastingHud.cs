using System;
using System.Collections.Generic;
using LightSouls.Combat.Runes;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>One offline model session per game, shared across lobby/dungeon transitions.</summary>
    internal static class RuneInferenceRuntime
    {
        private static RuneClassifier classifier;
        private static bool warmingStarted;
        public static bool Busy=>classifier!=null&&classifier.Busy;
        public static RuneClassifier Get()=>classifier??=new RuneClassifier();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Dispose();warmingStarted=false;
            Application.quitting-=Dispose;Application.quitting+=Dispose;
        }

        private static void Dispose(){classifier?.Dispose();classifier=null;}

        public static async void WarmUp()
        {
            if(warmingStarted||Application.isBatchMode)return;
            warmingStarted=true;
            try
            {
                await Get().RecognizeAsync(RuneGlyphs.Example(RuneGlyph.Slash),RuneReferenceBank.AllMask,.90f,.05f);
            }
            catch(Exception ex){Debug.LogWarning("Rune warm-up: "+ex.Message);}
        }
    }

    /// <summary>Touch/mouse casting surface, four weapon skills and one shared dodge reference.</summary>
    public sealed class RuneCastingHud:MonoBehaviour
    {
        private static readonly Color Teal=new Color32(117,232,217,255);
        private static readonly Color Reject=new Color32(248,133,117,255);
        private static readonly Color Accept=new Color32(176,249,185,255);
        private const string SensitivityKey="lightsouls.rune.flexible";
        private PlayerController player;
        private WeaponDataSO shownWeapon;
        private IReadOnlyList<RuneSkill> shownKit=Array.Empty<RuneSkill>();
        private RectTransform root,interact,useRect;
        private GameObject useButton;
        private Image useIcon;
        private Vector2 interactOriginal;
        private Text useLabel,title,status,sensitivityLabel,padHint;
        private Image padFrame;
        private RuneGesturePad pad;
        private readonly Card[] cards=new Card[RuneSkillCatalog.SkillsPerWeapon];
        private bool flexible,processing,hadSurface;
        private float messageUntil,lastRefresh;
        private int requestToken,pendingRevision;
        private WeaponDataSO pendingWeapon;
        private bool pendingCanceled;

        private sealed class Card
        {
            public Image Frame,Shade;
            public RawImage Glyph;
            public Text Name,Cooldown;
            public RuneSkill Skill;
        }

        private PlayerCombat Combat=>player!=null?player.Combat:null;
        public RectTransform Panel=>root;
        public RuneGesturePad Pad=>pad;
        public int VisibleSkillCount=>shownKit.Count;
        public bool IsProcessing=>processing;
        public RuneMatch LastMatch{get;private set;}
        public bool LastCastSucceeded{get;private set;}
        public float Threshold=>flexible?.80f:.90f;
        public bool CanBeginStroke=>Combat!=null&&Combat.RuneInputReady&&!processing&&!RuneInferenceRuntime.Busy;

        public static RuneCastingHud Create(Transform safeArea,GameObject attack,Text attackLabel,RectTransform dash,RectTransform interact)
        {
            var host=new GameObject("RuneCastingHUD",typeof(RectTransform),typeof(RuneCastingHud));
            host.transform.SetParent(safeArea,false);
            var hud=host.GetComponent<RuneCastingHud>();
            hud.Build(attack,attackLabel,dash,interact);
            return hud;
        }

        private void Build(GameObject attack,Text attackLabel,RectTransform dashButton,RectTransform interactButton)
        {
            useButton=attack;useLabel=attackLabel;interact=interactButton;
            useRect=attack!=null?attack.transform as RectTransform:null;
            useIcon=attack!=null?attack.transform.Find("Icon")?.GetComponent<Image>():null;
            if(dashButton!=null)dashButton.gameObject.SetActive(false);
            if(interact!=null)interactOriginal=interact.anchoredPosition;
            flexible=PlayerPrefs.GetInt(SensitivityKey,0)==1;
            // Keep this on the right thumb side, above and outside the central hotbar.
            root=DarkVitalityBar.Rect(transform,"RuneSurface",Vector2.zero,new Vector2(560,610));
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(1,0);
            root.anchoredPosition=new Vector2(-24,106);
            RectTransform host=(RectTransform)transform;
            host.anchorMin=Vector2.zero;host.anchorMax=Vector2.one;host.offsetMin=host.offsetMax=Vector2.zero;

            var header=DarkVitalityBar.Image(root,"RuneHeader",new Vector2(0,281),new Vector2(552,42),DarkFantasyTheme.Ink);
            DarkFantasyTheme.FramePanel(header,new Color32(26,35,47,242));
            title=DarkVitalityBar.Label(header.transform,"WeaponRunes","ẤN THUẬT",new Vector2(-48,0),new Vector2(430,33),20,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            var sensitivity=DarkVitalityBar.Image(header.transform,"Sensitivity",new Vector2(222,0),new Vector2(96,31),DarkFantasyTheme.Action);
            sensitivity.raycastTarget=true;
            var button=sensitivity.gameObject.AddComponent<Button>();button.targetGraphic=sensitivity;DarkFantasyTheme.FrameButton(button,DarkFantasyTheme.Action);
            sensitivityLabel=DarkVitalityBar.Label(sensitivity.transform,"Mode","",Vector2.zero,new Vector2(94,27),15,DarkFantasyTheme.Ivory,bold:true);
            button.onClick.AddListener(()=>{if(processing||(pad!=null&&pad.IsDrawing))return;flexible=!flexible;PlayerPrefs.SetInt(SensitivityKey,flexible?1:0);UpdateSensitivity();ShowStatus(flexible?"Thoáng · ngưỡng 80%":"Chặt · ngưỡng 90%",Teal,2f);});
            UpdateSensitivity();

            for(int i=0;i<cards.Length;i++)
            {
                var frame=DarkVitalityBar.Image(root,"RuneSkill"+i,new Vector2(-222f+i*111,210),new Vector2(107,99),DarkFantasyTheme.Slot);
                DarkFantasyTheme.FrameSlot(frame,DarkFantasyTheme.Slot);
                var iconRect=DarkVitalityBar.Rect(frame.transform,"Glyph",new Vector2(0,19),new Vector2(53,53));
                var icon=iconRect.gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
                var name=DarkVitalityBar.Label(frame.transform,"Name","",new Vector2(0,-22),new Vector2(98,32),16,DarkFantasyTheme.Ivory,bold:true);
                var shade=DarkVitalityBar.Image(frame.transform,"CooldownShade",new Vector2(0,19),new Vector2(93,51),new Color(.04f,.07f,.10f,.80f));shade.gameObject.SetActive(false);
                var cd=DarkVitalityBar.Label(shade.transform,"Seconds","",Vector2.zero,new Vector2(90,38),22,DarkFantasyTheme.Gold,bold:true);
                cards[i]=new Card{Frame=frame,Glyph=icon,Name=name,Shade=shade,Cooldown=cd};
            }

            padFrame=DarkVitalityBar.Image(root,"RunePad",new Vector2(64,-58),new Vector2(420,420),new Color32(29,50,61,191));
            DarkFantasyTheme.FramePanel(padFrame,new Color32(29,50,61,205));padFrame.raycastTarget=true;
            // Decorative seal is deliberately dim; only the freehand mesh becomes bright.
            var seal=DarkVitalityBar.Image(padFrame.transform,"Seal",Vector2.zero,new Vector2(349,349),new Color(1,.79f,.43f,.29f));
            if(DarkFantasyTheme.JoystickRing!=null)seal.sprite=DarkFantasyTheme.JoystickRing;
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;
                var node=DarkVitalityBar.Image(padFrame.transform,"SealPoint"+i,new Vector2(Mathf.Cos(a)*156,Mathf.Sin(a)*156),new Vector2(10,10),new Color32(218,180,103,163));
                node.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            padHint=DarkVitalityBar.Label(padFrame.transform,"PadHint","VẼ CỔ NGỮ\nThả tay để thi triển",new Vector2(0,-2),new Vector2(310,82),25,new Color32(205,223,223,142),bold:true);
            var inkRect=DarkVitalityBar.Rect(padFrame.transform,"Ink",Vector2.zero,new Vector2(420,420));
            var ink=inkRect.gameObject.AddComponent<RuneInkGraphic>();ink.raycastTarget=false;
            pad=padFrame.gameObject.AddComponent<RuneGesturePad>();pad.Initialize(this,ink);
            status=DarkVitalityBar.Label(root,"RuneStatus","Vẽ một nét theo mẫu",new Vector2(62,-284),new Vector2(420,31),19,DarkFantasyTheme.Muted,bold:true);
            root.gameObject.SetActive(false);
        }

        public void SetPlayer(PlayerController next)
        {
            if(player!=next){requestToken++;pad?.Cancel();player=next;shownWeapon=null;shownKit=Array.Empty<RuneSkill>();}
            if(Combat!=null)Combat.SetRuneInputMode(true);
            Refresh(true);
            if(Combat!=null)RuneInferenceRuntime.WarmUp();
        }

        private void Update()
        {
            if(Combat!=null&&!Combat.RuneInputMode)Combat.SetRuneInputMode(true);
            if(pad.IsDrawing&&!CanContinueStroke(pad.Revision,pad.Weapon))pad.Cancel();
            if(processing&&!pendingCanceled&&!CanContinueStroke(pendingRevision,pendingWeapon))
            {pendingCanceled=true;requestToken++;pad.ClearInk();ShowStatus("Đã hủy cổ ngữ",DarkFantasyTheme.Muted,.7f);}
            Refresh(false);
            if(root.gameObject.activeSelf&&!processing&&!pad.IsDrawing&&Time.unscaledTime>=messageUntil)
            {
                pad.ClearInk();
                status.text=RuneInferenceRuntime.Busy?"Đang chuẩn bị cổ ngữ…":"Vẽ một nét theo mẫu";status.color=DarkFantasyTheme.Muted;
            }
            if(padHint!=null)padHint.enabled=!pad.HasInk&&!processing;
        }

        private void Refresh(bool force)
        {
            if(root==null)return;
            var combat=Combat;var weapon=combat!=null?combat.CurrentWeaponData:null;
            bool consumable=combat!=null&&combat.ConsumableSelected;
            var kit=combat!=null?combat.CurrentRuneSkills:Array.Empty<RuneSkill>();bool show=kit.Count>0;
            bool armed=kit.Count==RuneSkillCatalog.SkillsPerWeapon;
            if(root.gameObject.activeSelf!=show)root.gameObject.SetActive(show);
            if(useButton!=null&&useButton.activeSelf!=consumable)useButton.SetActive(consumable);
            if(useLabel!=null)useLabel.text="DÙNG";
            if(consumable&&useIcon!=null)useIcon.sprite=player.Inventory?.SelectedItem?.Icon;
            if(useRect!=null&&consumable){useRect.anchoredPosition=new Vector2(-520,145);useRect.sizeDelta=new Vector2(150,150);}
            if(force||show!=hadSurface)
            {
                hadSurface=show;
                if(interact!=null)interact.anchoredPosition=show?new Vector2(-520,298):interactOriginal;
            }
            if(force||weapon!=shownWeapon||shownKit.Count!=kit.Count)
            {
                shownWeapon=weapon;shownKit=show?kit:Array.Empty<RuneSkill>();
                if(show)
                {
                    title.text=armed?"ẤN THUẬT · "+weapon.WeaponName:"ẤN THUẬT · LƯỚT NÉ";
                    for(int i=0;i<cards.Length;i++)
                    {
                        var skill=armed?kit[i]:i==cards.Length-1?RuneSkillCatalog.DashSkill:null;
                        var card=cards[i];card.Skill=skill;card.Frame.gameObject.SetActive(skill!=null);
                        if(skill==null)continue;
                        card.Glyph.texture=RuneGlyphs.Icon(skill.Glyph);card.Name.text=skill.Name;
                        card.Frame.color=skill.Form==RuneForm.Dash?DarkFantasyTheme.Action:skill.Basic?new Color32(101,78,48,255):DarkFantasyTheme.Slot;
                        card.Glyph.color=skill.Form==RuneForm.Dash?Teal:Color.white;
                        card.Shade.gameObject.SetActive(false);
                    }
                    if(!processing&&!pad.IsDrawing)ShowStatus("Vẽ một nét theo mẫu",DarkFantasyTheme.Muted,.2f);
                }
            }
            if(!show||combat==null)return;
            if(!force&&Time.unscaledTime<lastRefresh)return;
            lastRefresh=Time.unscaledTime+.08f;
            for(int i=0;i<cards.Length;i++)
            {
                var card=cards[i];if(card.Skill==null)continue;
                float remaining=combat.RuneCooldownRemaining(card.Skill);bool cooling=remaining>.035f;
                card.Shade.gameObject.SetActive(cooling);
                if(cooling)card.Cooldown.text=remaining.ToString("0.0")+"s";
            }
        }

        private void UpdateSensitivity(){if(sensitivityLabel!=null)sensitivityLabel.text=flexible?"THOÁNG":"CHẶT";}
        private void ShowStatus(string message,Color color,float seconds)
        {status.text=message;status.color=color;messageUntil=Time.unscaledTime+seconds;}

        internal bool TryBeginStroke(out int revision,out WeaponDataSO weapon)
        {
            revision=Combat!=null?Combat.RuneContextVersion:-1;weapon=Combat!=null?Combat.CurrentWeaponData:null;
            if(!CanBeginStroke)
            {
                ShowStatus(RuneInferenceRuntime.Busy||processing?"Đang đọc ấn…":"Chưa thể thi triển",DarkFantasyTheme.Muted,.5f);return false;
            }
            LastCastSucceeded=false;ShowStatus("Thả tay để thi triển",Teal,10f);return true;
        }

        internal bool CanContinueStroke(int revision,WeaponDataSO weapon)=>Combat!=null&&Combat.RuneInputReady&&
            revision==Combat.RuneContextVersion&&weapon==Combat.CurrentWeaponData;

        // Also used by automated PlayMode tests. The actual input comes from RuneGesturePad.
        public bool SubmitStroke(IReadOnlyList<Vector2> points)
        {
            if(!TryBeginStroke(out var revision,out var weapon))return false;
            pad.SetInk(points,Teal);return SubmitStroke(points,revision,weapon);
        }

        internal bool SubmitStroke(IReadOnlyList<Vector2> points,int revision,WeaponDataSO weapon)
        {
            LastCastSucceeded=false;
            if(!CanContinueStroke(revision,weapon)||processing||RuneInferenceRuntime.Busy){pad.ClearInk();return false;}
            if(!RuneRasterizer.IsValid(points))
            {
                LastMatch=new RuneMatch(RuneRecognitionStatus.TooShort);pad.Tint(Reject);ShowStatus("Nét vẽ quá ngắn",Reject,1.3f);return false;
            }
            processing=true;pendingCanceled=false;pendingRevision=revision;pendingWeapon=weapon;int token=++requestToken;
            ShowStatus("Đang đọc ấn…",Teal,10f);
            Analyze(points,revision,weapon,token,Threshold,RuneReferenceBank.Mask(Combat.CurrentRuneSkills));return true;
        }

        private async void Analyze(IReadOnlyList<Vector2> points,int revision,WeaponDataSO weapon,int token,float threshold,int allowedMask)
        {
            RuneMatch result;
            try{result=await RuneInferenceRuntime.Get().RecognizeAsync(points,allowedMask,threshold,.05f);}
            catch(Exception ex){Debug.LogWarning("Rune recognition unavailable: "+ex.Message);result=new RuneMatch(RuneRecognitionStatus.Unavailable);}
            if(this==null)return;
            processing=false;
            if(token!=requestToken||!CanContinueStroke(revision,weapon))
            {pad.ClearInk();ShowStatus("Đã hủy cổ ngữ",DarkFantasyTheme.Muted,.7f);return;}
            LastMatch=result;
            if(result.Accepted)
            {
                LastCastSucceeded=Combat.TryCastRune(result.Glyph,out string feedback);
                pad.Tint(LastCastSucceeded?Accept:Reject);
                ShowStatus(LastCastSucceeded?feedback+" · Khớp "+Mathf.RoundToInt(result.Score*100)+"%":feedback,LastCastSucceeded?Accept:Reject,1.35f);
                lastRefresh=0;Refresh(false);
                return;
            }
            string reason=result.Status switch
            {
                RuneRecognitionStatus.TooShort=>"Nét vẽ quá ngắn",
                RuneRecognitionStatus.BelowThreshold=>"Không khớp · "+Mathf.RoundToInt(result.Score*100)+"%",
                RuneRecognitionStatus.Ambiguous=>"Cổ ngữ chưa rõ · hãy vẽ lại",
                RuneRecognitionStatus.Unsupported=>"Vũ khí này không có ấn "+RuneGlyphs.Name(result.Glyph),
                RuneRecognitionStatus.Busy=>"Đang đọc ấn…",
                _=>"Không thể đọc cổ ngữ"
            };
            pad.Tint(Reject);ShowStatus(reason,Reject,1.55f);
        }

        private void OnDestroy(){requestToken++;}
    }

    /// <summary>Capture one pointer by ID; another touch remains free for joystick/dash.</summary>
    public sealed class RuneGesturePad:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler,IInitializePotentialDragHandler
    {
        private readonly List<Vector2> points=new List<Vector2>(256);
        private RuneCastingHud owner;
        private RuneInkGraphic ink;
        private RectTransform rect;
        private int pointerId;
        public bool IsDrawing{get;private set;}
        public bool HasInk=>ink!=null&&ink.HasInk;
        public int Revision{get;private set;}
        public WeaponDataSO Weapon{get;private set;}

        internal void Initialize(RuneCastingHud hud,RuneInkGraphic graphic){owner=hud;ink=graphic;rect=(RectTransform)transform;}
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||IsDrawing||!owner.TryBeginStroke(out int revision,out var weapon))return;
            pointerId=e.pointerId;Revision=revision;Weapon=weapon;IsDrawing=true;points.Clear();
            ink.color=new Color32(117,232,217,255);Append(e);
        }
        public void OnDrag(PointerEventData e)
        {if(IsDrawing&&e.pointerId==pointerId){if(owner.CanContinueStroke(Revision,Weapon))Append(e);else Cancel();}}
        public void OnPointerUp(PointerEventData e)
        {
            if(!IsDrawing||e.pointerId!=pointerId)return;
            Append(e);IsDrawing=false;
            owner.SubmitStroke(points.ToArray(),Revision,Weapon);points.Clear();
        }
        private void Append(PointerEventData e)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local))return;
            var r=rect.rect;var p=new Vector2(Mathf.Clamp01((local.x-r.xMin)/r.width),Mathf.Clamp01(1-(local.y-r.yMin)/r.height));
            if(points.Count>0&&(p-points[^1]).sqrMagnitude<.000008f)return;
            if(points.Count>=RuneRasterizer.MaxPoints){Cancel();return;}
            points.Add(p);ink.SetPoints(points);
        }
        internal void Cancel(){IsDrawing=false;points.Clear();ClearInk();}
        internal void SetInk(IReadOnlyList<Vector2> path,Color tint){ink.color=tint;ink.SetPoints(path);}
        internal void Tint(Color tint){ink.color=tint;}
        internal void ClearInk(){if(ink!=null)ink.SetPoints(null);}
        private void OnDisable()=>Cancel();
    }

    /// <summary>One small reusable translucent ink texture. Incremental CPU drawing;
    /// the standard uGUI quad renders the glow even on low-end mobile renderers.</summary>
    public sealed class RuneInkGraphic:RawImage
    {
        private const int Size=256;
        private const float Radius=7.5f;
        private readonly Color32[] pixels=new Color32[Size*Size];
        private Texture2D inkTexture;
        private Vector2 first,last;
        private int count;
        private bool dirty;
        public bool HasInk=>count>0;

        protected override void Awake()
        {
            base.Awake();
            inkTexture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Rune Ink (runtime)",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            inkTexture.SetPixels32(pixels);inkTexture.Apply(false,false);texture=inkTexture;
        }

        public void SetPoints(IReadOnlyList<Vector2> source)
        {
            int length=source?.Count??0;
            if(length==0)
            {
                if(count==0)return;Array.Clear(pixels,0,pixels.Length);count=0;dirty=true;return;
            }
            if(count==0||length<count||source[0]!=first||source[count-1]!=last||length==count)
            {
                Array.Clear(pixels,0,pixels.Length);first=last=source[0];Draw(first,first);count=1;
            }
            for(int i=count;i<length;i++){Draw(last,source[i]);last=source[i];}
            count=length;dirty=true;
        }

        private void Draw(Vector2 a,Vector2 b)
        {
            a=new Vector2(a.x,1-a.y)*(Size-1);b=new Vector2(b.x,1-b.y)*(Size-1);
            Vector2 d=b-a;float inv=d.sqrMagnitude>0?1/d.sqrMagnitude:0;
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x)-Radius),0,Size-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x)+Radius),0,Size-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y)-Radius),0,Size-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y)+Radius),0,Size-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                Vector2 offset=new Vector2(x,y)-a;float t=Mathf.Clamp01(Vector2.Dot(offset,d)*inv);
                float distance=(offset-d*t).magnitude;
                if(distance>=Radius)continue;
                byte alpha=(byte)Mathf.Clamp(Mathf.RoundToInt(distance<=2.2f?255:distance<3.8f?Mathf.Lerp(255,112,(distance-2.2f)/1.6f):Mathf.Lerp(112,0,(distance-3.8f)/(Radius-3.8f))),0,255);
                int index=y*Size+x;if(alpha>pixels[index].a)pixels[index]=new Color32(255,255,255,alpha);
            }
        }

        private void LateUpdate()
        {
            if(!dirty||inkTexture==null)return;
            inkTexture.SetPixels32(pixels);inkTexture.Apply(false,false);dirty=false;
        }
        protected override void OnDestroy()
        {
            if(inkTexture!=null)Destroy(inkTexture);base.OnDestroy();
        }
    }
}

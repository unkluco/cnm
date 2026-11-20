using LightSouls.Combat.Bosses;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Combat.UI
{
    /// <summary>Large, non-interactive encounter bar below the existing coin panel on landscape mobile screens.</summary>
    public sealed class BossEncounterHud : MonoBehaviour
    {
        private BossEnemy boss;
        private HealthComponent health;
        private DarkVitalityBar bar;
        private Canvas canvas;
        private Text phaseText, skillText, hpText;
        public BossEnemy Boss=>boss;
        public float HealthRatio=>bar!=null?bar.Ratio:0f;

        public static BossEncounterHud Create(BossEnemy boss)
        {
            if(boss==null)return null;
            var go=new GameObject("BossEncounterHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var hud=go.AddComponent<BossEncounterHud>();hud.Build(boss);return hud;
        }

        private void Build(BossEnemy owner)
        {
            boss=owner;health=owner.GetComponent<HealthComponent>();
            canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=16;
            var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1366,768);scaler.matchWidthOrHeight=.5f;

            var panel=DarkVitalityBar.Rect(transform,"BossPlate",Vector2.zero,new Vector2(532,76));
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,1f);panel.pivot=new Vector2(.5f,1f);panel.anchoredPosition=new Vector2(0,-65);
            var back=DarkVitalityBar.Image(panel,"PanelFrame",Vector2.zero,new Vector2(532,76),new Color(1,1,1,.96f));
            DarkFantasyTheme.SetFrame(back,DarkFantasyTheme.PanelFrame,new Color(1,1,1,.96f),Image.Type.Sliced);
            Color accent=owner.Definition.Accent;
            // A restrained elemental crest at both ends of the nameplate.
            foreach(int s in new[]{-1,1})
            {
                var rim=DarkVitalityBar.Image(panel,"Crest",new Vector2(s*239,21),new Vector2(15,15),DarkFantasyTheme.Gold);rim.rectTransform.localRotation=Quaternion.Euler(0,0,45);
                var gem=DarkVitalityBar.Image(panel,"Crest_Gem",new Vector2(s*239,21),new Vector2(9,9),accent);gem.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            DarkVitalityBar.Label(panel,"BossName",owner.DisplayName,new Vector2(0,21),new Vector2(452,25),21,DarkFantasyTheme.Ivory,bold:true);
            bar=DarkVitalityBar.Create(panel,new Vector2(0,-1),new Vector2(478,19),accent,true);
            phaseText=DarkVitalityBar.Label(panel,"Phase","GIAI ĐOẠN I",new Vector2(-162,-23),new Vector2(155,20),13,DarkFantasyTheme.Gold,TextAnchor.MiddleLeft,true);
            skillText=DarkVitalityBar.Label(panel,"CastName","",new Vector2(0,-23),new Vector2(220,20),15,Color.Lerp(accent,DarkFantasyTheme.Ivory,.27f),bold:true);
            hpText=DarkVitalityBar.Label(panel,"HitPoints","",new Vector2(176,-23),new Vector2(128,20),13,DarkFantasyTheme.Ivory,TextAnchor.MiddleRight);

            health.OnHealthChanged+=HandleHealth;health.Died+=HandleDied;boss.StateChanged+=HandleState;
            bar.SetHealth(health.CurrentHealth,health.MaxHealth,true);HandleHealth(health.CurrentHealth,health.MaxHealth);HandleState(boss);
        }

        private void HandleHealth(float current,float max)
        {bar.SetHealth(current,max);hpText.text=$"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";}

        private void HandleState(BossEnemy current)
        {
            phaseText.text=current.Phase==2?"GIAI ĐOẠN II":"GIAI ĐOẠN I";
            phaseText.color=current.Phase==2?Color.Lerp(current.Definition.Accent,DarkFantasyTheme.Ivory,.24f):DarkFantasyTheme.Gold;
            skillText.text=current.IsCasting
                ? current.CurrentSkillName+(current.CurrentStageCount>1?$" · {current.ActiveStage}/{current.CurrentStageCount}":"")
                : current.Phase==2?"Thức tỉnh":"Đang quan sát";
        }

        private void HandleDied(IDamageable _){canvas.enabled=false;Destroy(gameObject);}
        private void Update()
        {
            if(boss==null || boss.IsDead || !boss.gameObject.activeInHierarchy){Destroy(gameObject);return;}
            bar?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if(health!=null){health.OnHealthChanged-=HandleHealth;health.Died-=HandleDied;}
            if(boss!=null)boss.StateChanged-=HandleState;
        }
    }
}

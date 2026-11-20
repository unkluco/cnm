using UnityEngine;
using UnityEngine.UI;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Enemies;
using LightSouls.Common;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Thanh máu lơ lửng trên đầu quái vật (World-Space) và tự động kích hoạt
    /// hiệu ứng số sát thương bay lên (Damage Popup) khi quái bị đánh.
    /// </summary>
    public class EnemyWorldHealthBar : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject popupPrefab;

        [Header("Tùy biến hiển thị")]
        [SerializeField] private Vector3 offsetAboveHead = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private bool hideWhenFull = false;

        private Camera _mainCamera;
        private Canvas _worldCanvas;
        private float _lastHealth = -1f;
        private DarkVitalityBar view;
        private BossEnemy boss;
        private EnemyBase enemy;
        private Text nameLabel;
        private bool allyShown;
        public float HealthRatio=>view!=null?view.Ratio:0f;

        private void Awake()
        {
            if (healthComponent == null)
            {
                healthComponent = GetComponentInParent<HealthComponent>();
            }

            _worldCanvas = GetComponentInChildren<Canvas>();
            if(_worldCanvas==null || healthComponent==null)return;
            var owner=healthComponent.GetComponent<EnemyBase>();
            enemy=owner;
            boss=owner as BossEnemy;
            // The large encounter HUD replaces the small floating boss bar, but damage popups still work.
            if(boss!=null){_worldCanvas.enabled=false;return;}
            var previousLabel=GetComponentInChildren<Text>(true);
            string caption=owner!=null?owner.DisplayName:previousLabel!=null?previousLabel.text:"Bù nhìn tập luyện";
            var root=_worldCanvas.GetComponent<RectTransform>();
            foreach(Transform old in root)old.gameObject.SetActive(false);
            root.sizeDelta=new Vector2(190,44);root.localScale=Vector3.one*.0105f;
            var capsule=healthComponent.GetComponent<CapsuleCollider>();
            if(capsule!=null)root.localPosition=Vector3.up*(capsule.height+.38f);
            nameLabel=DarkVitalityBar.Label(root,"EnemyName_Styled",caption,new Vector2(0,11),new Vector2(190,25),17,DarkFantasyTheme.Ivory,bold:true);
            view=DarkVitalityBar.Create(root,new Vector2(0,-9),new Vector2(174,13),new Color(.9f,.34f,.20f),false);
        }

        private void Start()
        {
            _mainCamera = Camera.main;

            if (healthComponent != null)
            {
                _lastHealth = healthComponent.CurrentHealth;
                healthComponent.OnHealthChanged += HandleHealthChanged;
                healthComponent.Died += HandleDied;
                UpdateHealthBar(healthComponent.CurrentHealth, healthComponent.MaxHealth);
                view?.SetHealth(healthComponent.CurrentHealth,healthComponent.MaxHealth,true);
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged -= HandleHealthChanged;
                healthComponent.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            view?.Tick(Time.deltaTime);
            if(enemy!=null&&boss==null&&allyShown!=enemy.IsCharmed)
            {
                allyShown=enemy.IsCharmed;
                if(nameLabel!=null){nameLabel.text=allyShown?"ĐỒNG MINH · "+enemy.DisplayName:enemy.DisplayName;nameLabel.color=allyShown?new Color(.42f,1f,.76f):DarkFantasyTheme.Ivory;}
                view?.SetAlly(allyShown);
            }
            if (_mainCamera != null && transform != null && boss==null)
            {
                // Luôn quay mặt về Camera
                transform.rotation = _mainCamera.transform.rotation;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastHealth > current)
            {
                float damageTaken = _lastHealth - current;
                Vector3 spawnPos = boss!=null?boss.transform.position+Vector3.up*(boss.Definition.Height*.78f):transform.position + Vector3.up * 0.3f;
                DamagePopup.Create(popupPrefab, spawnPos, damageTaken);
            }

            _lastHealth = current;
            UpdateHealthBar(current, max);
        }

        private void UpdateHealthBar(float current, float max)
        {
            view?.SetHealth(current,max);
            if(_worldCanvas!=null)_worldCanvas.enabled=boss==null && current>0 && (!hideWhenFull || current<max);
        }

        private void HandleDied(IDamageable damageable)
        {
            if (_worldCanvas != null)
            {
                _worldCanvas.enabled = false;
            }
        }
    }
}

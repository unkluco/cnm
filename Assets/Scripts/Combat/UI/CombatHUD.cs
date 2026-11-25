using UnityEngine;
using UnityEngine.UI;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using LightSouls.Combat.Items;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Hiển thị tên người chơi, thanh máu, thông tin vũ khí,
    /// cập nhật icon động (Kiếm / Súng, Attack, Dash Skill) trên các nút bấm cảm ứng.
    /// Kiểm tra vũ khí tiếp theo trong danh sách: nếu tiếp theo là Kiếm thì hiện icon Kiếm, nếu là Súng thì hiện icon Súng.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        [Header("Tham chiếu Player (Tùy chọn)")]
        [SerializeField] private PlayerController player;

        [Header("UI Người chơi & Máu (Top-Left)")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthText;

        [Header("UI Thông tin Vũ khí")]
        [SerializeField] private Text weaponNameText;
        [SerializeField] private Text weaponTypeText;
        [SerializeField] private Image weaponColorBadge;

        [Header("Nút bấm cảm ứng tròn (Action Buttons)")]
        [Tooltip("Image Icon trên nút Tấn công")]
        [SerializeField] private Image attackButtonIconImage;
        [Tooltip("Text Icon trên nút Tấn công (dự phòng)")]
        [SerializeField] private Text attackButtonIconText;

        [Tooltip("Image Icon trên nút Đổi vũ khí")]
        [SerializeField] private Image switchButtonIconImage;
        [Tooltip("Text Icon trên nút Đổi vũ khí (dự phòng)")]
        [SerializeField] private Text switchButtonIconText;
        [Tooltip("Sprite hình Kiếm cho nút Đổi")]
        [SerializeField] private Sprite swordIconSprite;
        [Tooltip("Sprite hình Súng cho nút Đổi")]
        [SerializeField] private Sprite gunIconSprite;
        [Tooltip("Nhãn chữ nhỏ dưới icon đổi vũ khí")]
        [SerializeField] private Text switchButtonLabelText;

        [Header("Nút Kỹ năng Lướt (Skill Dash Button)")]
        [SerializeField] private Image skillButtonIconImage;
        [SerializeField] private Text skillButtonIconText;
        [SerializeField] private Text skillButtonLabelText;
        [SerializeField] private Image skillCooldownOverlay;

        private PlayerController _subscribedPlayer;
        private Image attackCooldownShade;
        private Text attackCooldownText,tacticalCooldownText;
        private RuneCastingHud runeHud;
        public Text AttackCooldownText=>attackCooldownText;
        public RuneCastingHud RuneHud=>runeHud;

        private void Start()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (playerNameText != null && string.IsNullOrEmpty(playerNameText.text))
            {
                playerNameText.text = "HIỆP SĨ ÁNH SÁNG";
            }

            if (attackButtonIconText != null)
            {
                attackButtonIconText.text = "ĐÁNH";
            }

            if (skillButtonIconText != null && string.IsNullOrEmpty(skillButtonIconText.text))
            {
                skillButtonIconText.text = "»";
            }

            if (skillButtonLabelText != null && string.IsNullOrEmpty(skillButtonLabelText.text))
            {
                skillButtonLabelText.text = "LƯỚT";
            }

            Subscribe();
            BuildAttackCooldownUI();
            BuildRuneUI();
        }

        private void BuildRuneUI()
        {
            if(runeHud!=null)return;
            var attack=attackButtonIconImage!=null?attackButtonIconImage.transform.parent:attackButtonIconText!=null?attackButtonIconText.transform.parent:null;
            if(attack==null||attack.parent==null||attack.parent.parent==null)return;
            runeHud=RuneCastingHud.Create(attack.parent.parent,attack.gameObject,attackButtonIconText,
                attack.parent.Find("Button_Skill_Dash") as RectTransform,attack.parent.Find("Button_Interact") as RectTransform);
            runeHud.SetPlayer(player);
        }

        private void BuildAttackCooldownUI()
        {
            if(attackCooldownShade!=null)return;
            var anchor=attackButtonIconImage!=null?attackButtonIconImage.transform.parent:attackButtonIconText!=null?attackButtonIconText.transform.parent:null;
            if(anchor==null)return;
            var shade=new GameObject("AttackCooldownShade",typeof(RectTransform),typeof(Image));shade.transform.SetParent(anchor,false);
            var rect=(RectTransform)shade.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            attackCooldownShade=shade.GetComponent<Image>();attackCooldownShade.sprite=DarkFantasyTheme.CircleFrame;attackCooldownShade.type=Image.Type.Filled;
            attackCooldownShade.fillMethod=Image.FillMethod.Radial360;attackCooldownShade.fillOrigin=2;attackCooldownShade.fillClockwise=false;
            attackCooldownShade.color=new Color(.06f,.08f,.13f,.85f);attackCooldownShade.raycastTarget=false;shade.SetActive(false);
            attackCooldownText=DarkVitalityBar.Label((RectTransform)anchor,"AttackCooldownText",string.Empty,Vector2.zero,new Vector2(122,46),30,DarkFantasyTheme.Ivory,bold:true);
            attackCooldownText.gameObject.SetActive(false);
            tacticalCooldownText=DarkVitalityBar.Label((RectTransform)anchor,"TacticalCooldownText",string.Empty,new Vector2(0,87),new Vector2(190,30),19,DarkFantasyTheme.Gold,bold:true);
            tacticalCooldownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Gắn HUD với người chơi được sinh ra lúc chạy (GameBootstrap gọi sau khi đặt người chơi vào màn).
        /// </summary>
        public void SetPlayer(PlayerController newPlayer)
        {
            if (newPlayer == player)
            {
                return;
            }

            Unsubscribe();
            player = newPlayer;
            Subscribe();
            runeHud?.SetPlayer(player);
        }

        private void Subscribe()
        {
            if (player == null || _subscribedPlayer == player)
            {
                return;
            }

            _subscribedPlayer = player;
            player.OnWeaponChanged += HandleWeaponChanged;
            player.OnHealthChanged += HandleHealthChanged;

            if (player.CurrentWeapon != null)
            {
                HandleWeaponChanged(player.CurrentWeapon);
            }
            HandleHealthChanged(player.CurrentHealth, player.MaxHealth);
        }

        private void Unsubscribe()
        {
            if (_subscribedPlayer == null)
            {
                return;
            }

            _subscribedPlayer.OnWeaponChanged -= HandleWeaponChanged;
            _subscribedPlayer.OnHealthChanged -= HandleHealthChanged;
            _subscribedPlayer = null;
        }

        private void Update()
        {
            if(attackCooldownShade==null)BuildAttackCooldownUI();
            if(attackCooldownShade!=null&&player!=null&&player.Combat!=null)
            {
                float remaining=player.Combat.AttackCooldownRemaining;
                bool cooling=remaining>.035f;
                attackCooldownShade.gameObject.SetActive(cooling);attackCooldownShade.fillAmount=player.Combat.AttackCooldownRatio;
                attackCooldownText.gameObject.SetActive(cooling);attackCooldownText.text=cooling?remaining.ToString("0.0"):string.Empty;
                var w=player.Combat.CurrentWeaponData;
                bool hasTactic=runeHud==null&&w!=null&&w.HasTacticalSkill;
                if(tacticalCooldownText!=null)
                {
                    tacticalCooldownText.gameObject.SetActive(hasTactic);
                    float special=player.Combat.TacticalCooldownRemaining;
                    tacticalCooldownText.text=special>.05f?"KỸ NĂNG · "+special.ToString("0.0")+"s":"KỸ NĂNG SẴN SÀNG";
                    tacticalCooldownText.color=special>.05f?DarkFantasyTheme.Muted:DarkFantasyTheme.Gold;
                }
            }
            // Cập nhật hiển thị thời gian hồi chiêu của nút Skill nếu có
            if (skillCooldownOverlay != null && player != null && player.Movement != null)
            {
                float ratio = player.Movement.DashCooldownRatio;
                skillCooldownOverlay.fillAmount = ratio;
                skillCooldownOverlay.enabled = ratio > 0.001f;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void HandleWeaponChanged(WeaponDataSO weapon)
        {
            if (weapon == null)
            {
                if (weaponNameText != null) weaponNameText.text = "Tay không";
                if (weaponTypeText != null)
                {
                    weaponTypeText.text = "[TRỐNG]";
                    weaponTypeText.color = Color.gray;
                }
                if (weaponColorBadge != null) weaponColorBadge.color = Color.clear;
                return;
            }

            if (weaponNameText != null)
            {
                weaponNameText.text = weapon.WeaponName;
            }

            if (weaponTypeText != null)
            {
                weaponTypeText.text = weapon.Type == WeaponType.Melee ? "[CẬN CHIẾN]" : "[TẦM XA]";
                weaponTypeText.color = weapon.Type == WeaponType.Melee ? new Color(1f, 0.45f, 0.35f) : new Color(0.35f, 0.8f, 1f);
            }

            if (weaponColorBadge != null)
            {
                weaponColorBadge.color = weapon.WeaponColor;
            }

            // Nếu còn dùng nút đổi vũ khí cũ (dự phòng tương thích ngược)
            WeaponDataSO nextWeapon = player != null ? player.NextWeapon : null;
            bool isNextSword = false;

            if (nextWeapon != null)
            {
                isNextSword = nextWeapon.Type == WeaponType.Melee;
            }
            else
            {
                isNextSword = weapon.Type != WeaponType.Melee;
            }

            if (switchButtonIconImage != null)
            {
                switchButtonIconImage.sprite = isNextSword ? swordIconSprite : gunIconSprite;
            }

            if (switchButtonIconText != null)
            {
                switchButtonIconText.text = isNextSword ? "KIẾM" : "SÚNG";
            }

            if (switchButtonLabelText != null)
            {
                switchButtonLabelText.text = isNextSword ? "KIẾM" : "SÚNG";
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (max <= 0f) return;

            float ratio = Mathf.Clamp01(current / max);

            if (healthSlider != null)
            {
                healthSlider.value = ratio;
            }

            if (healthFillImage != null)
            {
                // Chuyển màu từ xanh ngọc sang vàng sang đỏ
                if (ratio > 0.5f)
                {
                    healthFillImage.color = Color.Lerp(new Color32(230, 177, 76, 255), new Color32(103, 195, 153, 255), (ratio - 0.5f) * 2f);
                }
                else
                {
                    healthFillImage.color = Color.Lerp(new Color32(204, 77, 69, 255), new Color32(230, 177, 76, 255), ratio * 2f);
                }
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        public void SetPlayerName(string name)
        {
            if (playerNameText != null)
            {
                playerNameText.text = name;
            }
        }
    }
}

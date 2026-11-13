using UnityEngine;
using UnityEngine.UI;
using LightSouls.Combat.Weapons;

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
                attackButtonIconText.text = "⚔️\n<size=22>ĐÁNH</size>";
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
            if (weapon == null) return;

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

            // Quy tắc: Nút Đổi vũ khí kiểm tra vũ khí TIẾP THEO để đổi icon:
            // Nếu vũ khí tiếp theo là Kiếm -> hiển thị icon Kiếm.
            // Nếu vũ khí tiếp theo là Súng -> hiển thị icon Súng.
            WeaponDataSO nextWeapon = player != null ? player.NextWeapon : null;
            bool isNextSword = false;

            if (nextWeapon != null)
            {
                isNextSword = nextWeapon.Type == WeaponType.Melee;
            }
            else
            {
                // Dự phòng nếu inventory chỉ có 1 vũ khí hoặc chưa nạp xong:
                // Nếu hiện tại là Kiếm thì tiếp theo sẽ là Súng, và ngược lại.
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
                    healthFillImage.color = Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(0.18f, 0.8f, 0.44f), (ratio - 0.5f) * 2f);
                }
                else
                {
                    healthFillImage.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.1f), ratio * 2f);
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

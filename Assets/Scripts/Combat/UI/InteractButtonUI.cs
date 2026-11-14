using UnityEngine;
using UnityEngine.UI;
using LightSouls.Common;
using LightSouls.Combat.Items;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Nút cảm ứng Tương tác (Interact) ở góc phải dưới màn hình trong Combat_TouchControls.
    /// Nút chỉ hiện khi có vật thể tương tác (IInteractable) trong tầm quét 2m của PlayerInteraction.
    /// Nếu vật thể là vũ khí hoặc vật phẩm rơi (ItemPickup), icon nút tự động đổi sang icon của vũ khí/vật phẩm đó!
    /// </summary>
    public class InteractButtonUI : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private Button button;
        [SerializeField] private Text promptText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite defaultInteractIcon;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(OnClickInteract);
            }
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (interaction == null && player != null)
            {
                interaction = player.Interaction;
            }

            if (interaction != null)
            {
                interaction.OnInteractableFound += HandleInteractableFound;
                interaction.OnInteractableLost += HandleInteractableLost;

                if (interaction.HasInteractable)
                {
                    HandleInteractableFound(interaction.CurrentInteractable);
                }
                else
                {
                    HandleInteractableLost();
                }
            }
            else
            {
                // Mặc định ẩn nút nếu chưa có vật tương tác
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (interaction != null)
            {
                interaction.OnInteractableFound -= HandleInteractableFound;
                interaction.OnInteractableLost -= HandleInteractableLost;
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnClickInteract);
            }
        }

        public void SetPlayer(PlayerController newPlayer)
        {
            if (player == newPlayer) return;

            if (interaction != null)
            {
                interaction.OnInteractableFound -= HandleInteractableFound;
                interaction.OnInteractableLost -= HandleInteractableLost;
            }

            player = newPlayer;
            interaction = player != null ? player.Interaction : null;

            if (interaction != null)
            {
                interaction.OnInteractableFound += HandleInteractableFound;
                interaction.OnInteractableLost += HandleInteractableLost;

                if (interaction.HasInteractable)
                {
                    HandleInteractableFound(interaction.CurrentInteractable);
                }
                else
                {
                    HandleInteractableLost();
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void HandleInteractableFound(IInteractable interactable)
        {
            gameObject.SetActive(true);

            Sprite iconSprite = defaultInteractIcon;
            string prompt = interactable != null ? interactable.Prompt : "TƯƠNG TÁC";

            // Nếu vật thể là ItemPickup -> đổi icon sang icon của vũ khí / vật phẩm rơi đó!
            ItemPickup pickup = null;
            if (interactable is ItemPickup directPickup)
            {
                pickup = directPickup;
            }
            else if (interactable is Component comp)
            {
                pickup = comp.GetComponent<ItemPickup>() ?? comp.GetComponentInParent<ItemPickup>();
            }

            if (pickup != null && pickup.Item != null)
            {
                if (pickup.Item.Icon != null)
                {
                    iconSprite = pickup.Item.Icon;
                }
                prompt = pickup.Count > 1 
                    ? $"Nhặt {pickup.Item.DisplayName} x{pickup.Count}"
                    : $"Nhặt {pickup.Item.DisplayName}";
            }

            if (iconImage != null)
            {
                if (iconSprite != null)
                {
                    iconImage.gameObject.SetActive(true);
                    iconImage.sprite = iconSprite;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (promptText != null)
            {
                promptText.text = prompt;
            }
        }

        private void HandleInteractableLost()
        {
            gameObject.SetActive(false);
        }

        private void OnClickInteract()
        {
            if (interaction != null)
            {
                interaction.PerformInteract();
            }
        }
    }
}

using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Quầy hàng tại Sảnh, cài đặt IInteractable để người chơi mở bảng Cửa hàng mua vật phẩm.
    /// Vùng collider trigger được đặt ở Layer 12 (Interactable).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LobbyShopCounter : MonoBehaviour, IInteractable
    {
        public const int InteractableLayer = 12;

        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Cửa hàng";
        [SerializeField] private bool autoConfigureLayer = true;

        public string Prompt => promptText;

        private void Awake()
        {
            if (autoConfigureLayer)
            {
                gameObject.layer = InteractableLayer;
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = true;
                }
            }
        }

        private void Reset()
        {
            gameObject.layer = InteractableLayer;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return interactor != null && interactor.CompareTag("Player");
        }

        public void Interact(GameObject interactor)
        {
            if (LobbyController.Instance != null)
            {
                LobbyController.Instance.OpenShop(interactor);
            }
            else
            {
                LobbyShopUI shopUI = FindAnyObjectByType<LobbyShopUI>(FindObjectsInactive.Include);
                if (shopUI != null)
                {
                    shopUI.Open(interactor);
                }
            }
        }
    }
}

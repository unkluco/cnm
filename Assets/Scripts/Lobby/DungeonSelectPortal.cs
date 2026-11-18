using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Cổng chọn ngục tại Sảnh, cài đặt IInteractable để người chơi mở bảng chọn các loại ngục khác nhau.
    /// Vùng collider trigger được đặt ở Layer 12 (Interactable).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DungeonSelectPortal : MonoBehaviour, IInteractable
    {
        public const int InteractableLayer = 12;

        [Header("Portal Settings")]
        [SerializeField] private string promptText = "Chọn ngục";
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
                LobbyController.Instance.OpenDungeonSelect(interactor);
            }
            else
            {
                DungeonSelectUI selectUI = FindAnyObjectByType<DungeonSelectUI>();
                if (selectUI != null)
                {
                    selectUI.Open(interactor);
                }
            }
        }
    }
}

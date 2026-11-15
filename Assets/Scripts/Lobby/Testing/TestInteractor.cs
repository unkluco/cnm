using LightSouls.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LightSouls.Lobby.Testing
{
    /// <summary>
    /// Component thử nghiệm tương tác IInteractable phía người chơi (phím E hoặc nút trên màn hình).
    /// Quét các collider ở Layer 12 (Interactable) trong bán kính tương tác (2m).
    /// Dùng trong Scene thử nghiệm khi phần tương tác của Khải chưa merge.
    /// </summary>
    public class TestInteractor : MonoBehaviour
    {
        public const int InteractableLayer = 12;

        [Header("Detection Settings")]
        [SerializeField, Min(0.5f)] private float interactionRadius = 2.5f;

        private readonly Collider[] hitColliders = new Collider[16];
        private IInteractable closestInteractable;
        private string currentPrompt = string.Empty;

        public IInteractable ClosestInteractable => closestInteractable;
        public string CurrentPrompt => currentPrompt;

        private void Update()
        {
            FindClosestInteractable();

            // Phím E để tương tác nhanh trong Editor
            bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (ePressed && closestInteractable != null)
            {
                TriggerInteract();
            }
        }

        public void TriggerInteract()
        {
            if (closestInteractable != null && closestInteractable.CanInteract(gameObject))
            {
                closestInteractable.Interact(gameObject);
            }
        }

        private void FindClosestInteractable()
        {
            int mask = 1 << InteractableLayer;
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, hitColliders, mask, QueryTriggerInteraction.Collide);

            closestInteractable = null;
            currentPrompt = string.Empty;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = hitColliders[i];
                if (col == null)
                {
                    continue;
                }

                IInteractable interactable = col.GetComponent<IInteractable>();
                if (interactable == null)
                {
                    interactable = col.GetComponentInParent<IInteractable>();
                }

                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestInteractable = interactable;
                        currentPrompt = interactable.Prompt;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (closestInteractable != null && !string.IsNullOrEmpty(currentPrompt))
            {
                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };

                float btnWidth = 320f;
                float btnHeight = 60f;
                float x = (Screen.width - btnWidth) * 0.5f;
                float y = Screen.height - 180f;

                if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), $"[E] {currentPrompt}", style))
                {
                    TriggerInteract();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}

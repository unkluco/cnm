using System;
using UnityEngine;
using UnityEngine.InputSystem;
using LightSouls.Common;
using LightSouls.Combat.Items;
using LightSouls.Combat.Weapons;

namespace LightSouls.Combat
{
    /// <summary>
    /// Quản lý tương tác phía Người chơi với các đối tượng IInteractable (vật phẩm rơi, rương, cổng, NPC).
    /// Quét các vật thể trong bán kính 2m trên Layer Interactable (12) và kích hoạt khi bấm nút Interact.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Thông số tương tác")]
        [Tooltip("Bán kính tối đa để phát hiện vật tương tác (quy ước 2m).")]
        [SerializeField] private float interactRadius = 2.0f;
        [SerializeField] private LayerMask interactableLayer;

        [Header("Input System")]
        [Tooltip("Action Player/Interact từ InputSystem_Actions (Gamepad buttonNorth / Keyboard E).")]
        [SerializeField] private InputActionReference interactAction;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private IInteractable _currentInteractable;
        private GameObject _currentInteractableObj;
        private bool _isInputActive = true;

        public IInteractable CurrentInteractable => _currentInteractable;
        public bool HasInteractable => _currentInteractable != null;

        /// <summary>Phát khi phát hiện vật tương tác mới (để hiện nút kèm Prompt).</summary>
        public event Action<IInteractable> OnInteractableFound;

        /// <summary>Phát khi rời khỏi vùng tương tác (để ẩn nút).</summary>
        public event Action OnInteractableLost;

        private void Awake()
        {
            if (interactableLayer.value == 0)
            {
                interactableLayer = 1 << LayerMask.NameToLayer("Interactable");
            }
        }

        private void OnEnable()
        {
            if (interactAction != null && interactAction.action != null)
            {
                interactAction.action.Enable();
                interactAction.action.performed += OnInteractInput;
            }
        }

        private void OnDisable()
        {
            if (interactAction != null && interactAction.action != null)
            {
                interactAction.action.performed -= OnInteractInput;
                interactAction.action.Disable();
            }
        }

        private void Update()
        {
            if (!_isInputActive)
            {
                if (_currentInteractable != null)
                {
                    ClearInteractable();
                }
                return;
            }

            ScanForInteractables();
        }

        private void ScanForInteractables()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, _hitBuffer, interactableLayer, QueryTriggerInteraction.Collide);

            IInteractable bestCandidate = null;
            GameObject bestObj = null;
            float closestDistSq = float.MaxValue;
            bool foundWeapon = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitBuffer[i];
                if (col == null) continue;

                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    bool isWeapon = false;
                    ItemPickup pickup = col.GetComponentInParent<ItemPickup>();
                    if (pickup != null && pickup.Item is WeaponDataSO)
                    {
                        isWeapon = true;
                    }

                    float distSq = (col.transform.position - transform.position).sqrMagnitude;

                    // Ưu tiên hiển thị và nhặt vũ khí trước nếu có vũ khí trong tầm quét
                    if (isWeapon && !foundWeapon)
                    {
                        foundWeapon = true;
                        closestDistSq = distSq;
                        bestCandidate = interactable;
                        bestObj = col.gameObject;
                    }
                    else if (isWeapon == foundWeapon && distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        bestCandidate = interactable;
                        bestObj = col.gameObject;
                    }
                }
            }

            if (bestCandidate != _currentInteractable)
            {
                _currentInteractable = bestCandidate;
                _currentInteractableObj = bestObj;

                if (_currentInteractable != null)
                {
                    OnInteractableFound?.Invoke(_currentInteractable);
                }
                else
                {
                    OnInteractableLost?.Invoke();
                }
            }
            else if (_currentInteractable != null && !_currentInteractable.CanInteract(gameObject))
            {
                ClearInteractable();
            }
        }

        private void ClearInteractable()
        {
            _currentInteractable = null;
            _currentInteractableObj = null;
            OnInteractableLost?.Invoke();
        }

        private void OnInteractInput(InputAction.CallbackContext context)
        {
            PerformInteract();
        }

        /// <summary>
        /// Thực hiện hành vi tương tác với vật thể gần nhất (gọi được từ cả Action Input lẫn Nút cảm ứng OnScreen).
        /// </summary>
        public void PerformInteract()
        {
            if (!_isInputActive || _currentInteractable == null) return;

            if (_currentInteractable.CanInteract(gameObject))
            {
                _currentInteractable.Interact(gameObject);
                // Sau khi tương tác (ví dụ nhặt xong), quét lại ngay lập tức
                ScanForInteractables();
            }
        }

        public void SetInputActive(bool active)
        {
            _isInputActive = active;
            if (!active)
            {
                ClearInteractable();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}

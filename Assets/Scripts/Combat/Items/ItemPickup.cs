using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Vật phẩm nằm trên mặt đất có thể nhặt được. Cài đặt IInteractable chuẩn của LightSouls.Common.
    /// Gắn trên layer Interactable (12).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [Header("Thông tin vật phẩm")]
        [SerializeField] private ItemDefinitionSO item;
        [SerializeField, Min(1)] private int count = 1;

        [Header("Hiển thị & Hoạt ảnh")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private float bobbingSpeed = 3f;
        [SerializeField] private float bobbingHeight = 0.12f;
        [SerializeField] private float rotateSpeed = 60f;

        private Vector3 _startPosition;
        private SphereCollider _collider;

        public ItemDefinitionSO Item => item;
        public int Count => count;

        public string Prompt
        {
            get
            {
                if (item == null) return "Nhặt";
                return count > 1 ? $"Nhặt {item.DisplayName} (x{count})" : $"Nhặt {item.DisplayName}";
            }
        }

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<SphereCollider>();
                _collider.radius = 0.6f;
            }
            _collider.isTrigger = true;

            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }

            _startPosition = transform.position;

            if (iconRenderer == null)
            {
                iconRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            UpdateVisual();
        }

        private void Update()
        {
            // Hiệu ứng bồng bềnh và xoay nhẹ nhàng trên mặt sàn
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        public void SetItem(ItemDefinitionSO newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
            {
                iconRenderer.sprite = item.Icon;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (item == null || count <= 0) return false;
            if (interactor == null) return false;

            IInventory inv = interactor.GetComponent<IInventory>();
            return inv != null;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            IInventory inv = interactor.GetComponent<IInventory>();
            if (inv == null) return;

            if (inv.TryAdd(item, count))
            {
                Debug.Log($"[ItemPickup] Người chơi đã nhặt: {item.DisplayName} x{count}");
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("[ItemPickup] Túi đồ đã đầy! Không thể nhặt thêm.");
                // Có thể hiển thị text thông báo "Túi đầy" trên UI
            }
        }

        /// <summary>
        /// Sinh một vật phẩm rơi tại toạ độ chỉ định.
        /// </summary>
        public static ItemPickup Spawn(ItemDefinitionSO item, int count, Vector3 position)
        {
            if (item == null || count <= 0) return null;

            GameObject pickupObj = new GameObject($"Item_Pickup_{item.ItemId}");
            pickupObj.transform.position = position + Vector3.up * 0.4f;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                pickupObj.layer = interactableLayer;
            }

            SphereCollider sc = pickupObj.AddComponent<SphereCollider>();
            sc.radius = 0.6f;
            sc.isTrigger = true;

            ItemPickup pickup = pickupObj.AddComponent<ItemPickup>();

            // Tạo visual billboard icon
            GameObject visualObj = new GameObject("Icon");
            visualObj.transform.SetParent(pickupObj.transform, false);
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localScale = Vector3.one * 0.6f;

            SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
            sr.sprite = item.Icon;
            pickup.iconRenderer = sr;

            pickup.SetItem(item, count);

            return pickup;
        }
    }
}

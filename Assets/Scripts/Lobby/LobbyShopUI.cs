using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Giao diện bảng Cửa hàng tại Sảnh (Canvas sort order 8).
    /// Tích hợp BackButtonStack để nút Back đóng bảng, SafeAreaFitter, cập nhật số xu theo RunState.
    /// Quy trình mua: HasSpaceFor -> TrySpendCoins -> TryAdd.
    /// </summary>
    public class LobbyShopUI : MonoBehaviour
    {
        [Header("Catalog & References")]
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text statusNotificationText;

        [Header("Item List Container")]
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private GameObject itemRowPrefab;
        [SerializeField] private Button closeButton;

        private GameObject currentInteractor;
        private readonly List<GameObject> spawnedRows = new List<GameObject>();

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }
        }

        private void OnEnable()
        {
            RunState.CoinsChanged += HandleCoinsChanged;
            UpdateCoinsDisplay(RunState.Coins);
        }

        private void OnDisable()
        {
            RunState.CoinsChanged -= HandleCoinsChanged;
            BackButtonStack.Remove(this);
        }

        public void Open(GameObject interactor)
        {
            currentInteractor = interactor != null ? interactor : GameObject.FindWithTag("Player");

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            BackButtonStack.Push(this, Close);
            UpdateCoinsDisplay(RunState.Coins);
            ClearStatusNotification();
            PopulateItems();
        }

        public void Close()
        {
            BackButtonStack.Remove(this);

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            currentInteractor = null;
        }

        public void SetCatalog(ShopCatalogSO newCatalog)
        {
            catalog = newCatalog;
            if (IsOpen)
            {
                PopulateItems();
            }
        }

        private void HandleCoinsChanged(int newAmount)
        {
            UpdateCoinsDisplay(newAmount);
        }

        private void UpdateCoinsDisplay(int amount)
        {
            if (coinsText != null)
            {
                coinsText.text = $"Xu: {amount}";
            }
        }

        private void ShowStatus(string message)
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = message;
            }
            Debug.Log($"[LobbyShopUI] {message}");
        }

        private void ClearStatusNotification()
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = string.Empty;
            }
        }

        public void PopulateItems()
        {
            // Xóa các dòng hàng cũ
            foreach (GameObject row in spawnedRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }
            spawnedRows.Clear();

            if (catalog == null || catalog.Items == null || itemsContainer == null)
            {
                return;
            }

            foreach (ItemDefinitionSO item in catalog.Items)
            {
                if (item == null)
                {
                    continue;
                }

                GameObject rowObj = CreateItemRow(item);
                if (rowObj != null)
                {
                    spawnedRows.Add(rowObj);
                }
            }
        }

        private GameObject CreateItemRow(ItemDefinitionSO item)
        {
            GameObject row;
            if (itemRowPrefab != null)
            {
                row = Instantiate(itemRowPrefab, itemsContainer);
            }
            else
            {
                // Fallback runtime UI row nếu prefab chưa gán
                row = new GameObject($"ShopRow_{item.ItemId}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(itemsContainer, false);
                var rowImg = row.GetComponent<Image>();
                rowImg.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

                var rt = (RectTransform)row.transform;
                rt.sizeDelta = new Vector2(0f, 100f);

                // Text thông tin
                var infoGo = new GameObject("InfoText", typeof(RectTransform), typeof(Text));
                infoGo.transform.SetParent(row.transform, false);
                var infoText = infoGo.GetComponent<Text>();
                infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                infoText.fontSize = 24;
                infoText.color = Color.white;
                infoText.alignment = TextAnchor.MiddleLeft;
                infoText.text = $"{item.DisplayName} - {item.Price} Xu\n<size=18>{item.Description}</size>";
                var infoRt = (RectTransform)infoGo.transform;
                infoRt.anchorMin = new Vector2(0.05f, 0f);
                infoRt.anchorMax = new Vector2(0.7f, 1f);
                infoRt.offsetMin = Vector2.zero;
                infoRt.offsetMax = Vector2.zero;

                // Nút Mua (cao tối thiểu 96 px)
                var btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(row.transform, false);
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.color = new Color(0.2f, 0.65f, 0.3f, 1f);
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.anchorMin = new Vector2(0.72f, 0.05f);
                btnRt.anchorMax = new Vector2(0.98f, 0.95f);
                btnRt.offsetMin = Vector2.zero;
                btnRt.offsetMax = Vector2.zero;

                var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnText = btnTextGo.GetComponent<Text>();
                btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnText.fontSize = 28;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                btnText.text = "Mua";
                var btnTextRt = (RectTransform)btnTextGo.transform;
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.offsetMin = Vector2.zero;
                btnTextRt.offsetMax = Vector2.zero;

                var button = btnGo.GetComponent<Button>();
                button.onClick.AddListener(() => TryBuyItem(item));
                return row;
            }

            // Gán thông tin nếu dùng prefab có component cấu hình sẵn
            var rowButton = row.GetComponentInChildren<Button>();
            if (rowButton != null)
            {
                rowButton.onClick.AddListener(() => TryBuyItem(item));
            }

            var texts = row.GetComponentsInChildren<Text>();
            foreach (Text t in texts)
            {
                if (t.name.Contains("Name") || t.name.Contains("Info"))
                {
                    t.text = $"{item.DisplayName} - {item.Price} Xu";
                }
            }

            return row;
        }

        public void TryBuyItem(ItemDefinitionSO item)
        {
            if (item == null)
            {
                return;
            }

            // 1. Tìm IInventory trên người chơi (tag Player)
            GameObject player = currentInteractor != null ? currentInteractor : GameObject.FindWithTag("Player");
            if (player == null)
            {
                ShowStatus("Lỗi: Không tìm thấy người chơi!");
                return;
            }

            IInventory inventory = player.GetComponent<IInventory>();
            if (inventory == null)
            {
                inventory = player.GetComponentInParent<IInventory>();
            }

            if (inventory == null)
            {
                ShowStatus("Lỗi: Người chơi không có túi đồ (IInventory)!");
                return;
            }

            // 2. Kiểm tra HasSpaceFor
            if (!inventory.HasSpaceFor(item, 1))
            {
                ShowStatus("Túi đồ đã đầy, không thể mua thêm!");
                return;
            }

            // 3. RunState.TrySpendCoins(Price)
            if (!RunState.TrySpendCoins(item.Price))
            {
                ShowStatus($"Không đủ xu! Cần {item.Price} xu (hiện có: {RunState.Coins} xu).");
                return;
            }

            // 4. TryAdd vào túi đồ
            if (!inventory.TryAdd(item, 1))
            {
                // Hoàn lại xu nếu add thất bại bất ngờ
                RunState.AddCoins(item.Price);
                ShowStatus("Lỗi khi thêm vật phẩm vào túi đồ!");
                return;
            }

            ShowStatus($"Đã mua thành công: {item.DisplayName} (-{item.Price} xu).");
        }
    }
}

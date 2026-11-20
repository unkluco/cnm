using System.Collections.Generic;
using LightSouls.Common;
using LightSouls.MapGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Giao diện bảng Chọn ngục tại Sảnh (Canvas sort order 8).
    /// Đọc danh mục DungeonCatalogSO, hiển thị các ngục (Tên, mô tả, config).
    /// Khi chọn: ghi RunState.SelectedDungeonId và phát sự kiện LobbyController.DungeonChosen.
    /// Không tự nạp Scene: nhóm trưởng sẽ nghe sự kiện này để nạp Game.
    /// </summary>
    public class DungeonSelectUI : MonoBehaviour
    {
        [Header("Catalog & References")]
        [SerializeField] private DungeonCatalogSO catalog;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform dungeonListContainer;
        [SerializeField] private GameObject dungeonRowPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text statusNotificationText;

        private readonly List<GameObject> spawnedRows = new List<GameObject>();
        private GameObject currentInteractor;

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

        private void OnDisable()
        {
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
            ClearStatusNotification();
            PopulateDungeons();
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

        public void SetCatalog(DungeonCatalogSO newCatalog)
        {
            catalog = newCatalog;
            if (IsOpen)
            {
                PopulateDungeons();
            }
        }

        private void ClearStatusNotification()
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = string.Empty;
            }
        }

        private void ShowStatus(string message)
        {
            if (statusNotificationText != null)
            {
                statusNotificationText.text = message;
            }
            Debug.Log($"[DungeonSelectUI] {message}");
        }

        public void PopulateDungeons()
        {
            foreach (GameObject row in spawnedRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }
            spawnedRows.Clear();

            if (catalog == null || catalog.Entries == null || dungeonListContainer == null)
            {
                return;
            }

            foreach (DungeonEntry entry in catalog.Entries)
            {
                if (entry == null)
                {
                    continue;
                }

                GameObject rowObj = CreateDungeonRow(entry);
                if (rowObj != null)
                {
                    spawnedRows.Add(rowObj);
                }
            }
        }

        private GameObject CreateDungeonRow(DungeonEntry entry)
        {
            GameObject row;
            if (dungeonRowPrefab != null)
            {
                row = Instantiate(dungeonRowPrefab, dungeonListContainer);
            }
            else
            {
                row = new GameObject($"DungeonRow_{entry.dungeonId}", typeof(RectTransform), typeof(Image));
                row.transform.SetParent(dungeonListContainer, false);
                var rowImg = row.GetComponent<Image>();
                DarkFantasyTheme.FramePanel(rowImg, Color.white);

                var rt = (RectTransform)row.transform;
                rt.sizeDelta = new Vector2(0f, 110f);

                // Text thông tin ngục
                var infoGo = new GameObject("InfoText", typeof(RectTransform), typeof(Text));
                infoGo.transform.SetParent(row.transform, false);
                var infoText = infoGo.GetComponent<Text>();
                infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                infoText.fontSize = 24;
                infoText.color = DarkFantasyTheme.Ivory;
                infoText.alignment = TextAnchor.MiddleLeft;
                infoText.text = $"<b>{entry.displayName}</b> ({entry.dungeonId})\n<size=18>{entry.description}</size>";
                var infoRt = (RectTransform)infoGo.transform;
                infoRt.anchorMin = new Vector2(0.05f, 0f);
                infoRt.anchorMax = new Vector2(0.7f, 1f);
                infoRt.offsetMin = Vector2.zero;
                infoRt.offsetMax = Vector2.zero;

                // Nút Chọn (cao tối thiểu 96 px)
                var btnGo = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(row.transform, false);
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.color = DarkFantasyTheme.Action;
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
                btnText.text = "Chọn";
                var btnTextRt = (RectTransform)btnTextGo.transform;
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.offsetMin = Vector2.zero;
                btnTextRt.offsetMax = Vector2.zero;

                var button = btnGo.GetComponent<Button>();
                DarkFantasyTheme.FrameButton(button, DarkFantasyTheme.Action);
                button.onClick.AddListener(() => OnSelectDungeon(entry));
                return row;
            }

            var rowButton = row.GetComponentInChildren<Button>();
            if (rowButton != null)
            {
                rowButton.onClick.AddListener(() => OnSelectDungeon(entry));
            }

            return row;
        }

        private void OnSelectDungeon(DungeonEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            ShowStatus($"Đã chọn: {entry.displayName} ({entry.dungeonId})");
            LobbyController.SelectDungeon(entry.dungeonId);
            Close();
        }
    }
}

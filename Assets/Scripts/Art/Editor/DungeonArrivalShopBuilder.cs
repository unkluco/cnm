using System;
using System.Linq;
using LightSouls.Common;
using LightSouls.Game;
using LightSouls.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    /// <summary>Extract the production shop once so both scenes share its visual prefab and catalog.</summary>
    public static class DungeonArrivalShopBuilder
    {
        public const string ShopPrefabPath = "Assets/Prefabs/Lobby/Panel_Shop.prefab";

        [MenuItem("Light Souls/Art/Build Shared Dungeon Shop")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = SceneManager.GetActiveScene().path;
            var lobby = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            var lighting = Object.FindAnyObjectByType<DarkFantasyLightBudget>();
            if (lighting == null) throw new InvalidOperationException("Missing Lobby lighting budget.");
            lighting.ConfigureFixedSceneLighting(true);
            EditorUtility.SetDirty(lighting);

            var lobbyShop = Object.FindAnyObjectByType<LobbyShopUI>(FindObjectsInactive.Include);
            if (lobbyShop == null || lobbyShop.Catalog == null) throw new InvalidOperationException("Missing authored Lobby shop.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null || PrefabUtility.GetCorrespondingObjectFromSource(lobbyShop.gameObject) != prefab)
            {
                var title = lobbyShop.transform.Find("TitleText")?.GetComponent<Text>();
                if (title != null) title.text = "CỬA HÀNG ÁNH TÀN";
                lobbyShop.gameObject.SetActive(false);
                prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(lobbyShop.gameObject, ShopPrefabPath, InteractionMode.AutomatedAction);
                if (prefab == null) throw new InvalidOperationException("Could not author the shared shop prefab.");
            }
            var controller = Object.FindAnyObjectByType<LobbyController>();
            if (controller == null) throw new InvalidOperationException("Missing LobbyController.");
            SetReference(controller, "shopUI", lobbyShop);
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);

            var game = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            var root = game.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas_DungeonShop");
            if (root == null) root = new GameObject("Canvas_DungeonShop", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = root.transform.Find("SafeArea") as RectTransform;
            if (safeArea == null)
            {
                safeArea = (RectTransform)new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter)).transform;
                safeArea.SetParent(root.transform, false);
                safeArea.anchorMin = Vector2.zero;
                safeArea.anchorMax = Vector2.one;
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            }
            var shop = root.GetComponentInChildren<LobbyShopUI>(true);
            if (shop == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safeArea);
                shop = instance.GetComponent<LobbyShopUI>();
                // Prefab's panel anchors, rect and catalog are identical to the sanctuary shop.
                instance.SetActive(false);
            }
            if (shop == null || shop.Catalog == null) throw new InvalidOperationException("Shared shop has no catalog.");
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null) throw new InvalidOperationException("Missing GameBootstrap.");
            SetReference(bootstrap, "dungeonShop", shop);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previousScene) && previousScene != game.path)
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            Debug.Log("LIGHT_SOULS_SHARED_DUNGEON_SHOP_COMPLETE");
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing serialized field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

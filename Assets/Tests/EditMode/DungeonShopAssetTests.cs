using LightSouls.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class DungeonShopAssetTests
    {
        [Test]
        public void LobbyAndDungeonReferenceTheSameAuthoredShopPrefabAndCatalog()
        {
            const string path = "Assets/Prefabs/Lobby/Panel_Shop.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.NotNull(prefab);
            Assert.IsFalse(prefab.activeSelf, "Shop must not be open when either scene loads.");
            var ui = prefab.GetComponent<LobbyShopUI>();
            Assert.NotNull(ui);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<ShopCatalogSO>("Assets/ScriptableObjects/Lobby/ShopCatalog_Main.asset"), ui.Catalog);
            foreach (var scene in new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Game.unity" })
                CollectionAssert.Contains(AssetDatabase.GetDependencies(scene, false), path, scene + " must use the shared prefab.");
        }
    }
}

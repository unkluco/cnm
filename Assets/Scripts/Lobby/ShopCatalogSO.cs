using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby
{
    /// <summary>
    /// Danh mục các vật phẩm được bày bán tại cửa hàng ở Sảnh.
    /// Giá mua lấy trực tiếp từ thuộc tính ItemDefinitionSO.Price.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "LightSouls/Lobby/Shop Catalog")]
    public class ShopCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ItemDefinitionSO> items = new List<ItemDefinitionSO>();

        public IReadOnlyList<ItemDefinitionSO> Items => items;

        public void SetItems(IEnumerable<ItemDefinitionSO> newItems)
        {
            items.Clear();
            if (newItems != null)
            {
                items.AddRange(newItems);
            }
        }
    }
}

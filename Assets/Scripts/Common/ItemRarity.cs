using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>Độ hiếm vật phẩm. Chỉ thêm giá trị mới vào cuối, không đổi thứ tự (Unity lưu enum dạng số).</summary>
    public enum ItemRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>Màu và tên hiển thị của từng độ hiếm, dùng chung cho mọi giao diện và hiệu ứng.</summary>
    public static class ItemRarityStyle
    {
        public static Color GetColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color(0.25f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.74f, 0.38f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.72f, 0.15f);
                default: return new Color(0.72f, 0.75f, 0.8f);
            }
        }

        public static string GetDisplayName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return "Hiếm";
                case ItemRarity.Epic: return "Sử thi";
                case ItemRarity.Legendary: return "Huyền thoại";
                default: return "Thường";
            }
        }

        /// <summary>Độ hiếm từ Sử thi trở lên thì viền phát sáng nhấp nháy.</summary>
        public static bool Glows(ItemRarity rarity)
        {
            return rarity >= ItemRarity.Epic;
        }

        /// <summary>Chuỗi rich-text "&lt;color=#...&gt;Tên&lt;/color&gt;" cho Text có bật richText.</summary>
        public static string ColoredName(ItemRarity rarity)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(GetColor(rarity)) + ">" + GetDisplayName(rarity) + "</color>";
        }
    }
}

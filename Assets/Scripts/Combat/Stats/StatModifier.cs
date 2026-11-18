using System;

namespace LightSouls.Combat.Stats
{
    /// <summary>
    /// Chỉ số của người chơi có thể bị trang bị, thuốc, hiệu ứng thay đổi.
    /// Mỗi chỉ số là một HỆ SỐ NHÂN (1 = không đổi). Chỉ thêm giá trị mới vào cuối (Unity lưu enum dạng số).
    /// </summary>
    public enum StatType
    {
        /// <summary>Sát thương gây ra (1.3 = mạnh hơn 30%).</summary>
        DamageDealt,
        /// <summary>Sát thương nhận vào (0.8 = giảm 20%).</summary>
        DamageTaken,
        MoveSpeed,
        /// <summary>Thời gian hồi lướt (0.7 = hồi nhanh hơn 30%).</summary>
        DashCooldown,
        /// <summary>Quãng đường lướt (1.5 = xa hơn 50%).</summary>
        DashDistance,
        /// <summary>Sát thương từ dung nham (0 = miễn nhiễm).</summary>
        LavaDamage,
        /// <summary>Mức làm chậm của nước (0 = không bị chậm).</summary>
        WaterSlow,
        /// <summary>Sát thương bẫy gai.</summary>
        SpikeDamage,
        /// <summary>Sát thương nổ (thùng nổ, bom, lựu đạn).</summary>
        ExplosionDamage,
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public float Multiplier;

        public StatModifier(StatType stat, float multiplier)
        {
            Stat = stat;
            Multiplier = multiplier;
        }

        /// <summary>Mô tả ngắn cho bảng chi tiết, ví dụ "Sát thương nhận vào -20%".</summary>
        public string Describe()
        {
            return StatNames.Describe(Stat, Multiplier);
        }
    }

    public static class StatNames
    {
        public static string Describe(StatType stat, float multiplier)
        {
            if (multiplier <= 0f)
            {
                switch (stat)
                {
                    case StatType.LavaDamage: return "Miễn nhiễm dung nham";
                    case StatType.WaterSlow: return "Không bị nước làm chậm";
                    case StatType.SpikeDamage: return "Miễn nhiễm bẫy gai";
                    case StatType.ExplosionDamage: return "Miễn nhiễm vụ nổ";
                }
            }

            int percent = UnityEngine.Mathf.RoundToInt((multiplier - 1f) * 100f);
            string sign = percent >= 0 ? "+" : "";
            return GetName(stat) + " " + sign + percent + "%";
        }

        public static string GetName(StatType stat)
        {
            switch (stat)
            {
                case StatType.DamageDealt: return "Sát thương";
                case StatType.DamageTaken: return "Sát thương nhận vào";
                case StatType.MoveSpeed: return "Tốc độ chạy";
                case StatType.DashCooldown: return "Thời gian hồi lướt";
                case StatType.DashDistance: return "Quãng đường lướt";
                case StatType.LavaDamage: return "Sát thương dung nham";
                case StatType.WaterSlow: return "Mức chậm khi lội nước";
                case StatType.SpikeDamage: return "Sát thương bẫy gai";
                case StatType.ExplosionDamage: return "Sát thương vụ nổ";
                default: return stat.ToString();
            }
        }
    }
}

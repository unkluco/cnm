using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>Loại nguy hiểm của môi trường. Chỉ thêm giá trị mới vào cuối.</summary>
    public enum HazardKind
    {
        Lava,
        Water,
        Spike,
        Explosion,
    }

    /// <summary>
    /// Đối tượng có khả năng kháng nguy hiểm môi trường (người chơi mặc giáp chịu nhiệt, ủng lội nước...).
    /// Bẫy và địa hình (assembly MapGeneration) chỉ hỏi qua interface này, không cần biết hệ thống trang bị.
    /// </summary>
    public interface IHazardResistant
    {
        /// <summary>Hệ số tác động: 1 = bình thường, 0.5 = giảm một nửa, 0 = miễn nhiễm.</summary>
        float GetHazardMultiplier(HazardKind kind);
    }

    public static class HazardResistance
    {
        /// <summary>Hệ số tác động của nguy hiểm lên đối tượng chứa component này (1 nếu không có kháng).</summary>
        public static float GetMultiplier(Component target, HazardKind kind)
        {
            if (target == null)
            {
                return 1f;
            }

            var resistant = target.GetComponentInParent<IHazardResistant>();
            return resistant == null ? 1f : Mathf.Max(0f, resistant.GetHazardMultiplier(kind));
        }
    }
}

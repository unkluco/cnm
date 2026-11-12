using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>
    /// Thông tin của một lần gây sát thương. Thêm trường mới vào đây thay vì đổi chữ ký của IDamageable.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Lượng máu bị trừ. Luôn không âm.</summary>
        public float Amount;

        /// <summary>Đối tượng gây sát thương (người chơi, quái, viên đạn). Có thể null.</summary>
        public GameObject Source;

        /// <summary>Điểm trúng trong không gian thế giới, dùng cho hiệu ứng.</summary>
        public Vector3 HitPoint;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint)
        {
            Amount = Mathf.Max(0f, amount);
            Source = source;
            HitPoint = hitPoint;
        }
    }
}

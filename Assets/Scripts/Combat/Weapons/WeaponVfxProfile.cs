using System;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Hiệu ứng hình ảnh riêng của một vũ khí, chỉnh trong asset WeaponDataSO.
    /// Màu có alpha = 0 nghĩa là tắt hiệu ứng đó.
    /// </summary>
    [Serializable]
    public class WeaponVfxProfile
    {
        [Tooltip("Màu vệt chém (cận chiến).")]
        public Color slashColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Hào quang bao quanh vũ khí khi cầm trên tay (alpha 0 = không có).")]
        public Color auraColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("Số hạt hào quang mỗi giây.")]
        [Min(0f)] public float auraRate = 14f;

        [Tooltip("Màu tia lửa khi trúng mục tiêu (alpha 0 = không có).")]
        public Color hitColor = new Color(1f, 0.95f, 0.8f, 1f);

        [Tooltip("Màu vệt sáng sau viên đạn (alpha 0 = không có).")]
        public Color trailColor = new Color(1f, 0.9f, 0.5f, 0.8f);

        [Tooltip("Màu chớp sáng đầu nòng khi bắn (alpha 0 = không có).")]
        public Color muzzleColor = new Color(1f, 0.85f, 0.4f, 1f);

        [Tooltip("Tạo vòng sóng trên mặt đất mỗi nhát đánh (búa, rìu).")]
        public bool groundShockwave;
    }
}

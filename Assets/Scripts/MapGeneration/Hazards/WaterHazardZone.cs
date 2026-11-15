using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Vùng nước: làm chậm tốc độ di chuyển (mặc định 0.5x) của người chơi và quái vật thông qua ISpeedModifiable.
    /// Khi rời khỏi vùng hoặc khi vùng bị tắt/hủy, hệ số làm chậm được gỡ bỏ ngay lập tức.
    /// </summary>
    public class WaterHazardZone : HazardZoneBase
    {
        [Header("Water Slow Settings")]
        [Tooltip("Hệ số làm chậm (ví dụ 0.5 là còn một nửa tốc độ).")]
        [SerializeField, Range(0.05f, 0.95f)] private float speedMultiplier = 0.5f;

        private readonly HashSet<ISpeedModifiable> activeSlowed = new HashSet<ISpeedModifiable>();

        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                speedMultiplier = Mathf.Clamp(value, 0.05f, 0.95f);
                // Cập nhật lại cho các đối tượng đang trong vùng nếu hệ số thay đổi
                foreach (ISpeedModifiable modifiable in activeSlowed)
                {
                    if (modifiable != null)
                    {
                        modifiable.SetSpeedMultiplier(this, speedMultiplier);
                    }
                }
            }
        }

        public int ActiveSlowedCount => activeSlowed.Count;

        protected override void OnEntityEntered(Collider other)
        {
            ISpeedModifiable modifiable = FindSpeedModifiable(other);
            if (modifiable == null)
            {
                return;
            }

            if (activeSlowed.Add(modifiable))
            {
                modifiable.SetSpeedMultiplier(this, speedMultiplier);
            }
        }

        protected override void OnEntityExited(Collider other)
        {
            ISpeedModifiable modifiable = FindSpeedModifiable(other);
            if (modifiable == null)
            {
                return;
            }

            if (activeSlowed.Remove(modifiable))
            {
                modifiable.RemoveSpeedMultiplier(this);
            }
        }

        protected override void CleanupAllTracked()
        {
            foreach (ISpeedModifiable modifiable in activeSlowed)
            {
                if (modifiable != null)
                {
                    modifiable.RemoveSpeedMultiplier(this);
                }
            }

            activeSlowed.Clear();
            trackedColliders.Clear();
        }
    }
}

using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Vùng dung nham: gây sát thương định kỳ (mặc định 8 sát thương mỗi 0.5s) cho bất kỳ ai đứng trong vùng.
    /// Áp dụng cho cả người chơi lẫn quái vật thông qua IDamageable.
    /// </summary>
    public class LavaHazardZone : HazardZoneBase
    {
        [Header("Lava Damage Settings")]
        [Tooltip("Lượng sát thương trừ mỗi chu kỳ.")]
        [SerializeField, Min(0.1f)] private float damagePerTick = 8f;

        [Tooltip("Khoảng thời gian giữa 2 lần trừ máu (giây).")]
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        private class VictimRecord
        {
            public IDamageable Damageable;
            public Collider Collider;
            public float NextTickTime;
        }

        private readonly Dictionary<IDamageable, VictimRecord> activeVictims = new Dictionary<IDamageable, VictimRecord>();
        private readonly List<IDamageable> victimsToRemove = new List<IDamageable>();

        public float DamagePerTick
        {
            get => damagePerTick;
            set => damagePerTick = Mathf.Max(0.1f, value);
        }

        public float TickInterval
        {
            get => tickInterval;
            set => tickInterval = Mathf.Max(0.1f, value);
        }

        public int ActiveVictimCount => activeVictims.Count;

        private void Update()
        {
            if (activeVictims.Count == 0)
            {
                return;
            }

            float currentTime = Time.time;
            victimsToRemove.Clear();

            foreach (var kvp in activeVictims)
            {
                IDamageable damageable = kvp.Key;
                VictimRecord record = kvp.Value;

                if (damageable == null || damageable.IsDead || record.Collider == null || !record.Collider.gameObject.activeInHierarchy)
                {
                    victimsToRemove.Add(damageable);
                    continue;
                }

                if (currentTime >= record.NextTickTime)
                {
                    Vector3 hitPoint = record.Collider.bounds.center;
                    // Kháng dung nham (Giáp Chịu Nhiệt) giảm hoặc miễn sát thương.
                    float lavaDamage = damagePerTick * HazardResistance.GetMultiplier(record.Collider, HazardKind.Lava);
                    if (lavaDamage > 0f)
                    {
                        damageable.TakeDamage(new DamageInfo(lavaDamage, gameObject, hitPoint));
                    }
                    record.NextTickTime = currentTime + tickInterval;

                    if (damageable.IsDead)
                    {
                        victimsToRemove.Add(damageable);
                    }
                }
            }

            for (int i = 0; i < victimsToRemove.Count; i++)
            {
                activeVictims.Remove(victimsToRemove[i]);
            }
        }

        protected override void OnEntityEntered(Collider other)
        {
            IDamageable damageable = FindDamageable(other);
            if (damageable == null || damageable.IsDead)
            {
                return;
            }

            if (!activeVictims.ContainsKey(damageable))
            {
                // Gây sát thương ngay tick đầu tiên khi bước vào
                Vector3 hitPoint = other.bounds.center;
                float lavaDamage = damagePerTick * HazardResistance.GetMultiplier(other, HazardKind.Lava);
                if (lavaDamage > 0f)
                {
                    damageable.TakeDamage(new DamageInfo(lavaDamage, gameObject, hitPoint));
                }

                activeVictims[damageable] = new VictimRecord
                {
                    Damageable = damageable,
                    Collider = other,
                    NextTickTime = Time.time + tickInterval
                };
            }
        }

        protected override void OnEntityExited(Collider other)
        {
            IDamageable damageable = FindDamageable(other);
            if (damageable != null)
            {
                activeVictims.Remove(damageable);
            }
        }

        protected override void CleanupAllTracked()
        {
            activeVictims.Clear();
            trackedColliders.Clear();
        }
    }
}

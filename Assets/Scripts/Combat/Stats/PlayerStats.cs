using System;
using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Stats
{
    /// <summary>
    /// Nơi gom mọi hệ số chỉ số của nhân vật: trang bị, thuốc tăng lực, hiệu ứng tạm thời...
    /// Mỗi NGUỒN (khóa là object bất kỳ: ô trang bị, asset thuốc...) đăng ký một danh sách StatModifier,
    /// có thể kèm thời hạn. Giá trị một chỉ số = tích mọi hệ số của chỉ số đó.
    ///
    /// Thêm chỉ số mới: thêm vào StatType, rồi nơi cần dùng gọi Get(StatType.X). Không cần sửa trang bị hay thuốc.
    /// Các bẫy môi trường hỏi qua IHazardResistant (Common), nên MapGeneration không phụ thuộc Combat.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStats : MonoBehaviour, IHazardResistant
    {
        private class SourceEntry
        {
            public readonly List<StatModifier> Modifiers = new List<StatModifier>();
            public float ExpiresAt; // <= 0: vĩnh viễn
        }

        private readonly Dictionary<object, SourceEntry> sources = new Dictionary<object, SourceEntry>();
        private readonly List<object> expiredBuffer = new List<object>();
        private HealthComponent health;
        private ISpeedModifiable speedModifiable;

        /// <summary>Phát mỗi khi có nguồn được thêm, đổi, gỡ hoặc hết hạn.</summary>
        public event Action Changed;

        public static PlayerStats GetOrAdd(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            var stats = owner.GetComponent<PlayerStats>();
            return stats != null ? stats : owner.AddComponent<PlayerStats>();
        }

        /// <summary>Hệ số sát thương của kẻ tấn công (1 nếu không có PlayerStats, ví dụ quái).</summary>
        public static float DamageMultiplierOf(GameObject attacker)
        {
            if (attacker == null)
            {
                return 1f;
            }

            var stats = attacker.GetComponentInParent<PlayerStats>();
            return stats == null ? 1f : stats.Get(StatType.DamageDealt);
        }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            speedModifiable = GetComponent<ISpeedModifiable>();
        }

        private void Update()
        {
            if (sources.Count == 0)
            {
                return;
            }

            expiredBuffer.Clear();
            foreach (KeyValuePair<object, SourceEntry> pair in sources)
            {
                if (pair.Value.ExpiresAt > 0f && Time.time >= pair.Value.ExpiresAt)
                {
                    expiredBuffer.Add(pair.Key);
                }
            }

            if (expiredBuffer.Count == 0)
            {
                return;
            }

            foreach (object key in expiredBuffer)
            {
                sources.Remove(key);
            }

            NotifyChanged();
        }

        /// <summary>Đặt (hoặc thay) các hệ số của một nguồn. duration &gt; 0 thì tự gỡ sau duration giây.</summary>
        public void SetModifiers(object source, IEnumerable<StatModifier> modifiers, float duration = 0f)
        {
            if (source == null)
            {
                return;
            }

            var entry = new SourceEntry { ExpiresAt = duration > 0f ? Time.time + duration : 0f };
            if (modifiers != null)
            {
                entry.Modifiers.AddRange(modifiers);
            }

            sources[source] = entry;
            NotifyChanged();
        }

        public void RemoveSource(object source)
        {
            if (source != null && sources.Remove(source))
            {
                NotifyChanged();
            }
        }

        public bool HasSource(object source)
        {
            return source != null && sources.ContainsKey(source);
        }

        /// <summary>Thời gian còn lại của nguồn có hạn (0 nếu không có hoặc vĩnh viễn).</summary>
        public float GetRemaining(object source)
        {
            SourceEntry entry;
            if (source == null || !sources.TryGetValue(source, out entry) || entry.ExpiresAt <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, entry.ExpiresAt - Time.time);
        }

        public float Get(StatType stat)
        {
            float value = 1f;
            foreach (SourceEntry entry in sources.Values)
            {
                foreach (StatModifier modifier in entry.Modifiers)
                {
                    if (modifier.Stat == stat)
                    {
                        value *= Mathf.Max(0f, modifier.Multiplier);
                    }
                }
            }

            return value;
        }

        public float GetHazardMultiplier(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Lava: return Get(StatType.LavaDamage);
                case HazardKind.Water: return Get(StatType.WaterSlow);
                case HazardKind.Spike: return Get(StatType.SpikeDamage);
                case HazardKind.Explosion: return Get(StatType.ExplosionDamage);
                default: return 1f;
            }
        }

        private void NotifyChanged()
        {
            if (health != null)
            {
                health.DamageReduction = 1f - Get(StatType.DamageTaken);
            }

            if (speedModifiable != null)
            {
                float speed = Get(StatType.MoveSpeed);
                if (Mathf.Approximately(speed, 1f))
                {
                    speedModifiable.RemoveSpeedMultiplier(this);
                }
                else
                {
                    speedModifiable.SetSpeedMultiplier(this, speed);
                }
            }

            Changed?.Invoke();
        }
    }
}

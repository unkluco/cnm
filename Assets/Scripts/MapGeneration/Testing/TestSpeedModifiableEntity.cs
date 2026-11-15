using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Testing
{
    /// <summary>
    /// Component hỗ trợ kiểm thử tính năng làm chậm (ISpeedModifiable) trên nhân vật hoặc dummy thử nghiệm.
    /// Giúp xác nhận tính năng nước làm chậm hoạt động chính xác khi chưa có bản hoàn thiện từ nhánh của Khải.
    /// </summary>
    public class TestSpeedModifiableEntity : MonoBehaviour, ISpeedModifiable
    {
        [SerializeField] private float baseSpeed = 5.5f;

        private readonly Dictionary<object, float> multipliers = new Dictionary<object, float>();

        public float BaseSpeed => baseSpeed;
        public float CurrentSpeedMultiplier { get; private set; } = 1f;
        public float EffectiveSpeed => baseSpeed * CurrentSpeedMultiplier;

        public void SetSpeedMultiplier(object source, float multiplier)
        {
            if (source == null)
            {
                return;
            }

            multipliers[source] = Mathf.Max(0f, multiplier);
            Recalculate();
        }

        public void RemoveSpeedMultiplier(object source)
        {
            if (source == null)
            {
                return;
            }

            if (multipliers.Remove(source))
            {
                Recalculate();
            }
        }

        private void Recalculate()
        {
            float product = 1f;
            foreach (float m in multipliers.Values)
            {
                product *= m;
            }

            CurrentSpeedMultiplier = product;
        }
    }
}

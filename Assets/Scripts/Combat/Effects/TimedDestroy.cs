using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Hủy đối tượng khi tới mốc thời gian; ExtendTo dời mốc ra sau (dùng cho hào quang có hạn).</summary>
    public class TimedDestroy : MonoBehaviour
    {
        private float destroyAt = float.MaxValue;

        public void ExtendTo(float time)
        {
            destroyAt = destroyAt == float.MaxValue ? time : Mathf.Max(destroyAt, time);
        }

        private void Update()
        {
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }
    }
}

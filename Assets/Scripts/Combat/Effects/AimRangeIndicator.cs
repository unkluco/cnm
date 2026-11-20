using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Shows the actual target-selection radius briefly whenever the player presses attack.</summary>
    public sealed class AimRangeIndicator : MonoBehaviour
    {
        private ArcaneTelegraph marker;
        private float remaining;
        private float duration;
        private float radius;
        private static readonly Color RingColor = new Color(0.65f, 0.91f, 1f, 0.55f);

        public float VisibleRadius => marker != null && marker.IsVisible ? radius : 0f;

        public void Show(float range, float seconds = 0.65f)
        {
            if (range <= 0f) { Hide(); return; }
            if (marker == null)
            {
                marker = ArcaneTelegraph.Circle("VFX_AutoAimRange", transform.position, range, RingColor, 0f,
                    CombatVfxElement.Steel, TelegraphStyle.Aim, null, transform);
            }
            if (!Mathf.Approximately(range, radius))
                marker.SetCircle(transform.position, range);
            radius = range;
            duration = remaining = Mathf.Max(0.05f, seconds);
            marker.SetProgress(1f);
            marker.SetOpacity(1f);
            marker.SetVisible(true);
        }

        private void LateUpdate()
        {
            if (marker == null || !marker.IsVisible) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Hide(); return; }
            marker.SetOpacity(Mathf.Clamp01(remaining / Mathf.Min(duration, .3f)));
        }

        public void Hide()
        {
            remaining = 0f;
            if (marker != null) marker.SetVisible(false);
        }

        private void OnDisable() => Hide();
    }
}

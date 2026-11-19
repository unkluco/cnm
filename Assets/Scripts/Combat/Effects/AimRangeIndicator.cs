using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Shows the actual target-selection radius briefly whenever the player presses attack.</summary>
    public sealed class AimRangeIndicator : MonoBehaviour
    {
        private LineRenderer line;
        private float remaining;
        private float duration;
        private float radius;
        private static readonly Color RingColor = new Color(0.65f, 0.91f, 1f, 0.55f);

        public float VisibleRadius => line != null && line.enabled ? radius : 0f;

        public void Show(float range, float seconds = 0.65f)
        {
            if (range <= 0f) { Hide(); return; }
            if (line == null)
            {
                line = GroundCombatMarker.Create("VFX_AutoAimRange", transform);
                line.transform.localPosition = new Vector3(0f, 0.065f, 0f);
            }
            if (!Mathf.Approximately(range, radius))
                GroundCombatMarker.Circle(line, range, Mathf.Clamp(range * 0.025f, 0.07f, 0.14f), RingColor);
            radius = range;
            duration = remaining = Mathf.Max(0.05f, seconds);
            GroundCombatMarker.Tint(line, RingColor);
            line.enabled = true;
        }

        private void LateUpdate()
        {
            if (line == null || !line.enabled) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Hide(); return; }
            var color = RingColor;
            color.a *= Mathf.Clamp01(remaining / Mathf.Min(duration, 0.3f));
            GroundCombatMarker.Tint(line, color);
        }

        public void Hide()
        {
            remaining = 0f;
            if (line != null) line.enabled = false;
        }

        private void OnDisable() => Hide();
    }
}

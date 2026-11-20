using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>A gentle, inexpensive pulse on a portal's energy-only child, never on its trigger or anchor.</summary>
    public sealed class DarkFantasyPortalPulse : MonoBehaviour
    {
        [SerializeField] private Transform energy;
        [SerializeField, Range(0f, 0.15f)] private float amount = 0.045f;
        private Vector3 baseScale;

        public void Configure(Transform visual) => energy = visual;

        private void OnEnable()
        {
            if (energy != null) baseScale = energy.localScale;
        }

        private void Update()
        {
            if (energy != null) energy.localScale = baseScale * (1f + Mathf.Sin(Time.time * 2.3f) * amount);
        }

        private void OnDisable()
        {
            if (energy != null) energy.localScale = baseScale;
        }
    }
}

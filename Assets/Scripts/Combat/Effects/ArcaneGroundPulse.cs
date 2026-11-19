using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>A brief expanding shock ring with etched fragments; no damage volume or light source.</summary>
    public sealed class ArcaneGroundPulse : MonoBehaviour
    {
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private float radius, duration, elapsed;
        private Color color;
        private CombatVfxElement element;

        public static ArcaneGroundPulse Spawn(Vector3 ground, float radius, Color color, float lifetime,
            CombatVfxElement element = CombatVfxElement.Steel)
        {
            var go = new GameObject("VFX_ArcaneShockwave");
            go.transform.position = ground + Vector3.up * .095f;
            var pulse = go.AddComponent<ArcaneGroundPulse>();
            pulse.radius = Mathf.Max(.08f, radius);
            pulse.duration = Mathf.Max(.08f, lifetime);
            pulse.color = color;
            pulse.element = element;
            pulse.surface = ArcaneVfxMaterials.AddSurface(go, ArcaneVfxMaterials.Plane, ArcaneVfxMaterials.Glyph);
            pulse.properties = new MaterialPropertyBlock();
            pulse.Apply(0f);
            return pulse;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Destroy(gameObject); return; }
            Apply(elapsed / duration);
        }

        private void Apply(float progress)
        {
            float ease = 1f - (1f - progress) * (1f - progress);
            float diameter = radius * 2f * Mathf.Lerp(.24f, 1f, ease);
            transform.localScale = new Vector3(diameter, 1f, diameter);
            properties.SetColor("_Accent", color);
            properties.SetFloat("_Style", (float)TelegraphStyle.Impact);
            properties.SetFloat("_Shape", (float)TelegraphShape.Circle);
            properties.SetFloat("_Progress", progress);
            properties.SetFloat("_Opacity", 1f - Mathf.SmoothStep(0f, 1f, progress));
            properties.SetFloat("_Element", (float)element);
            surface.SetPropertyBlock(properties);
        }
    }
}

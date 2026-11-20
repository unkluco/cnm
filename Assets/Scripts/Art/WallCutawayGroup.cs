using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Art
{
    /// <summary>Fade only authored upper wall meshes, never their colliders or low stone plinths.</summary>
    [DisallowMultipleComponent]
    public sealed class WallCutawayGroup : MonoBehaviour
    {
        [SerializeField] private Renderer[] upperRenderers;
        [SerializeField, Range(0.05f, 0.5f)] private float fadedOpacity = 0.16f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 5.5f;

        private sealed class Visual
        {
            public Renderer Renderer;
            public Material[] Opaque, Transparent;
            public ShadowCastingMode Shadows;
            public Color[] Colors;
        }
        private static readonly Dictionary<Material, Material> FadeMaterials = new Dictionary<Material, Material>();
        private readonly List<Visual> visuals = new List<Visual>();
        private MaterialPropertyBlock block;
        private bool initialized, transparent;
        public bool Occluded { get; private set; }
        public float Opacity { get; private set; } = 1f;
        public IReadOnlyList<Renderer> UpperRenderers => upperRenderers;

        public void Configure(Renderer[] renderers) => upperRenderers = renderers;

        public void SetOccluded(bool value) => Occluded = value;

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            block = new MaterialPropertyBlock();
            if (upperRenderers == null) return;
            foreach (var renderer in upperRenderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                var visual = new Visual
                {
                    Renderer = renderer, Opaque = materials, Transparent = new Material[materials.Length],
                    Colors = new Color[materials.Length], Shadows = renderer.shadowCastingMode
                };
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    visual.Transparent[i] = GetFadeMaterial(material);
                    visual.Colors[i] = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                }
                visuals.Add(visual);
            }
        }

        private void Update()
        {
            float next = Mathf.MoveTowards(Opacity, Occluded ? fadedOpacity : 1f, fadeSpeed * Time.unscaledDeltaTime);
            if (Mathf.Approximately(next, Opacity)) return;
            Opacity = next;
            Initialize();
            if (Opacity >= 0.999f)
            {
                Restore();
                return;
            }
            if (!transparent)
            {
                foreach (var visual in visuals)
                {
                    if (visual.Renderer == null) continue;
                    visual.Renderer.sharedMaterials = visual.Transparent;
                    visual.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                transparent = true;
            }
            foreach (var visual in visuals)
            {
                if (visual.Renderer == null) continue;
                for (int index = 0; index < visual.Colors.Length; index++)
                {
                    visual.Renderer.GetPropertyBlock(block, index);
                    var color = visual.Colors[index]; color.a *= Opacity;
                    block.SetColor("_BaseColor", color);
                    visual.Renderer.SetPropertyBlock(block, index);
                }
            }
        }

        private void Restore()
        {
            if (!transparent) return;
            foreach (var visual in visuals)
            {
                if (visual.Renderer == null) continue;
                visual.Renderer.sharedMaterials = visual.Opaque;
                visual.Renderer.shadowCastingMode = visual.Shadows;
                for (int i = 0; i < visual.Opaque.Length; i++) visual.Renderer.SetPropertyBlock(null, i);
            }
            transparent = false;
        }

        private void OnDisable() { Restore(); Occluded = false; Opacity = 1f; }

        private static Material GetFadeMaterial(Material source)
        {
            if (source == null || !source.HasProperty("_Surface")) return source;
            if (FadeMaterials.TryGetValue(source, out var existing) && existing != null) return existing;
            var mat = new Material(source) { name = source.name + "_Cutaway", hideFlags = HideFlags.DontSave };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            FadeMaterials[source] = mat;
            return mat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            foreach (var material in FadeMaterials.Values) if (material != null) Destroy(material);
            FadeMaterials.Clear();
        }
    }
}

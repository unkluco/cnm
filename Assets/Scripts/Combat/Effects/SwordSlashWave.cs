using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Feathered elemental crescent with a bright cutting edge, animated sweep and short lifetime.</summary>
    public class SwordSlashWave : MonoBehaviour
    {
        [SerializeField] private float lifetime = .42f;
        [SerializeField] private float forwardSpeed = 1.1f;
        [SerializeField] private Vector3 startScale = new Vector3(.84f, .84f, .84f);
        [SerializeField] private Vector3 endScale = new Vector3(1.14f, 1.14f, 1.14f);
        [SerializeField] private Renderer waveRenderer;

        private static readonly Dictionary<int, Mesh> unitArcs = new();
        private MaterialPropertyBlock properties;
        private Color elementColor = Color.white;
        private CombatVfxElement element;
        private float elapsed;
        private Vector3 origin;

        public static SwordSlashWave Spawn(Vector3 position, Vector3 direction, float outerRadius, float arcAngle,
            Color color, CombatVfxElement element, bool mirrored = false)
        {
            if (color.a <= 0f) return null;
            direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) direction = Vector3.forward;
            var root = new GameObject("SwordSlashWave_Instance");
            root.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, mirrored ? 13f : -8f, mirrored ? -14f : 11f));
            int key = Mathf.RoundToInt(Mathf.Clamp(arcAngle, 45f, 360f));
            if (!unitArcs.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = CreateSlashArcMesh(.43f, 1f, key, 36);
                mesh.hideFlags = HideFlags.DontSave;
                unitArcs[key] = mesh;
            }
            ArcaneVfxMaterials.AddSurface(root, mesh, ArcaneVfxMaterials.Blade);
            var wave = root.AddComponent<SwordSlashWave>();
            wave.Configure(outerRadius, color, element);
            return wave;
        }

        private void Awake()
        {
            if (waveRenderer == null) waveRenderer = GetComponentInChildren<Renderer>();
            if (waveRenderer != null)
            {
                var old = waveRenderer.sharedMaterial;
                if (old != null && old.HasProperty("_BaseColor")) elementColor = old.GetColor("_BaseColor");
                else if (old != null && old.HasProperty("_Color")) elementColor = old.GetColor("_Color");
                waveRenderer.sharedMaterial = ArcaneVfxMaterials.Blade;
                waveRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                waveRenderer.receiveShadows = false;
            }
            properties = new MaterialPropertyBlock();
            origin = transform.position;
            transform.localScale = startScale;
            Apply(0f);
        }

        public void Configure(float outerRadius, Color tint, CombatVfxElement theme)
        {
            elementColor = tint;
            element = theme;
            float radius = Mathf.Max(.5f, outerRadius);
            startScale = Vector3.one * (radius * .91f);
            endScale = Vector3.one * (radius * 1.11f);
            transform.localScale = startScale;
            Apply(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / Mathf.Max(.05f, lifetime));
            transform.position = origin + transform.forward * (forwardSpeed * lifetime * p * (1f - .38f * p));
            transform.localScale = Vector3.Lerp(startScale, endScale, 1f - (1f - p) * (1f - p));
            Apply(p);
            if (p >= 1f) Destroy(gameObject);
        }

        private void Apply(float p)
        {
            if (waveRenderer == null || properties == null) return;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.16f, 1f, p));
            properties.SetColor("_Tint", elementColor);
            properties.SetFloat("_Fade", fade);
            properties.SetFloat("_Progress", p);
            properties.SetFloat("_TrailMode", 0f);
            properties.SetFloat("_Element", (float)element);
            waveRenderer.SetPropertyBlock(properties);
        }

        /// <summary>Tapered arc used by the runtime cache and the legacy editor preview.</summary>
        public static Mesh CreateSlashArcMesh(float innerRadius = .8f, float outerRadius = 1.6f, float arcAngle = 120f, int segments = 24)
        {
            segments = Mathf.Clamp(segments, 8, 64);
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float rad = (-arcAngle * .5f + arcAngle * t) * Mathf.Deg2Rad;
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), .72f);
                Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                float inner = Mathf.Lerp(outerRadius * .83f, innerRadius, taper);
                float outer = Mathf.Lerp(outerRadius * .83f, outerRadius, taper);
                int v = i * 2;
                vertices[v] = dir * inner + Vector3.up * (-.035f * taper);
                vertices[v + 1] = dir * outer + Vector3.up * (.035f * taper);
                uv[v] = new Vector2(t, 0f); uv[v + 1] = new Vector2(t, 1f);
                colors[v] = colors[v + 1] = new Color(1f, 1f, 1f, Mathf.Lerp(.15f, 1f, taper));
                if (i == segments) continue;
                int n = i * 6;
                triangles[n] = v; triangles[n + 1] = v + 1; triangles[n + 2] = v + 2;
                triangles[n + 3] = v + 1; triangles[n + 4] = v + 3; triangles[n + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Arcane Crescent" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

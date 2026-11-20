using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Actual blade afterimage sampled between hilt and tip. One reusable mesh, no cloned materials.</summary>
    [DisallowMultipleComponent]
    public sealed class BladeRibbonTrail : MonoBehaviour
    {
        private const int MaxSamples = 18;
        private const float Lifetime = .23f;
        private struct Sample { public Vector3 Base, Tip; public float Time; }
        private readonly List<Sample> samples = new(MaxSamples);
        private readonly List<Vector3> vertices = new(MaxSamples * 2);
        private readonly List<Vector2> uv = new(MaxSamples * 2);
        private readonly List<Color> colors = new(MaxSamples * 2);
        private readonly List<int> triangles = new((MaxSamples - 1) * 6);
        private Transform bladeRoot;
        private Vector3 baseLocal, tipLocal;
        private Mesh mesh;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private float emitUntil = -1f;

        public bool IsVisible => surface != null && surface.enabled;
        public int SampleCount => samples.Count;

        public void Configure(Transform blade)
        {
            bladeRoot = blade != null ? blade : transform;
            FindBladeEndpoints();
            if (surface != null) return;
            var go = new GameObject("VFX_RealBladeRibbon");
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Dynamic Blade Ribbon", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            surface = ArcaneVfxMaterials.AddSurface(go, mesh, ArcaneVfxMaterials.Ribbon);
            surface.enabled = false;
            properties = new MaterialPropertyBlock();
        }

        private void FindBladeEndpoints()
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            Vector3 farTip = Vector3.up;
            foreach (var filter in bladeRoot.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.name.StartsWith("VFX_")) continue;
                var bounds = filter.sharedMesh.bounds;
                var m = bladeRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3 axisTip = m.MultiplyPoint3x4(bounds.center + Vector3.up * bounds.extents.y);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 p = m.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                            min = Mathf.Min(min, p.y);
                            if (p.y > max) { max = p.y; farTip = new Vector3(axisTip.x, max, axisTip.z); }
                        }
            }
            if (float.IsInfinity(min) || max - min < .05f) { min = -.1f; max = .95f; farTip = Vector3.up * max; }
            baseLocal = new Vector3(0, Mathf.Lerp(min, max, .25f), 0);
            tipLocal = farTip;
        }

        public void Begin(Color color, CombatVfxElement element, float swingSeconds = .14f)
        {
            if (surface == null) Configure(transform);
            Clear();
            if (color.a <= 0f) return;
            properties.SetColor("_Tint", color);
            properties.SetFloat("_Fade", 1f);
            properties.SetFloat("_TrailMode", 1f);
            properties.SetFloat("_Element", (float)element);
            surface.SetPropertyBlock(properties);
            emitUntil = Time.time + swingSeconds;
            SampleNow();
        }

        public void Clear()
        {
            samples.Clear(); emitUntil = -1f;
            if (surface != null) surface.enabled = false;
            if (mesh != null) mesh.Clear();
        }

        private void SampleNow()
        {
            var sample = new Sample { Base = bladeRoot.TransformPoint(baseLocal), Tip = bladeRoot.TransformPoint(tipLocal), Time = Time.time };
            if (samples.Count > 0 && (sample.Tip - samples[^1].Tip).sqrMagnitude < .00005f) return;
            if (samples.Count == MaxSamples) samples.RemoveAt(0);
            samples.Add(sample);
        }

        private void LateUpdate()
        {
            if (surface == null || bladeRoot == null) return;
            float now = Time.time;
            if (now <= emitUntil) SampleNow();
            while (samples.Count > 0 && now - samples[0].Time >= Lifetime) samples.RemoveAt(0);
            if (samples.Count < 2) { surface.enabled = false; return; }
            vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
            var local = surface.transform.worldToLocalMatrix;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                float alpha = Mathf.Clamp01(1f - (now - s.Time) / Lifetime);
                float u = i / (float)(samples.Count - 1);
                vertices.Add(local.MultiplyPoint3x4(s.Base)); vertices.Add(local.MultiplyPoint3x4(s.Tip));
                uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, 1));
                colors.Add(new Color(1, 1, 1, alpha * .48f)); colors.Add(new Color(1, 1, 1, alpha * .94f));
                if (i == samples.Count - 1) continue;
                int v = i * 2;
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                triangles.Add(v + 1); triangles.Add(v + 3); triangles.Add(v + 2);
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); surface.enabled = true;
        }

        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}

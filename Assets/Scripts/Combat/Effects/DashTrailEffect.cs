using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Effects
{
    /// <summary>Cool soul-light afterimages. Each ghost uses a single mesh and a shared material.</summary>
    public class DashTrailEffect : MonoBehaviour
    {
        [SerializeField] private TrailRenderer ribbonTrail;
        [SerializeField] private Material ghostMaterial; // Retained for compatibility with older prototype prefabs.
        [SerializeField] private float ghostLifetime = .35f;
        [SerializeField] private float spawnInterval = .075f;
        private static readonly Color SoulTrail = new Color(.46f, .88f, .88f, .64f);
        private Transform modelRoot;
        private Transform trailHost;
        private Coroutine routine;

        public void Initialize(Transform model, Material matGhost, TrailRenderer trail = null)
        { modelRoot = model; ghostMaterial = matGhost; ribbonTrail = trail; }

        public void StartDashTrail(float duration)
        {
            StopDashTrail();
            if (modelRoot == null) modelRoot = transform.Find("HumanoidFigure");
            if (modelRoot == null) modelRoot = transform;
            VfxFactory.Burst(transform.position + Vector3.up * .15f, SoulTrail, 10, 2.5f, .17f, .34f, .35f);
            if (ribbonTrail != null) { ribbonTrail.Clear(); ribbonTrail.emitting = true; }
            else
            {
                trailHost = new GameObject("VFX_DashRibbonAnchor").transform;
                trailHost.SetParent(transform, false); trailHost.localPosition = Vector3.up * .76f;
                VfxFactory.AttachTrail(trailHost.gameObject, SoulTrail, .49f, .24f);
            }
            routine = StartCoroutine(DashRoutine(duration));
        }

        public void StopDashTrail()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (ribbonTrail != null) ribbonTrail.emitting = false;
            if (trailHost != null)
            {
                if (gameObject.activeInHierarchy) VfxFactory.ReleaseTrail(trailHost.gameObject);
                Destroy(trailHost.gameObject); trailHost = null;
            }
        }

        private IEnumerator DashRoutine(float duration)
        {
            float interval = Mathf.Max(.075f, spawnInterval);
            float end = Time.time + duration;
            int ghosts = 0;
            while (Time.time < end && ghosts < 4)
            {
                SpawnGhostAfterimage(); ghosts++;
                yield return new WaitForSeconds(interval);
            }
            routine = null;
        }

        private void SpawnGhostAfterimage()
        {
            if (modelRoot == null) return;
            var ghost = new GameObject("DashGhost_Afterimage");
            ghost.transform.SetPositionAndRotation(modelRoot.position, modelRoot.rotation);
            var local = ghost.transform.worldToLocalMatrix;
            var parts = new List<CombineInstance>();
            int vertexCount = 0;
            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || mf.sharedMesh == null || !mf.sharedMesh.isReadable || mf.name.StartsWith("VFX_")) continue;
                parts.Add(new CombineInstance { mesh = mf.sharedMesh, transform = local * mf.transform.localToWorldMatrix });
                vertexCount += mf.sharedMesh.vertexCount;
            }
            if (parts.Count == 0) { Destroy(ghost); return; }
            var mesh = new Mesh { name = "Arcane Dash Snapshot", indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            var renderer = ArcaneVfxMaterials.AddSurface(ghost, mesh, ArcaneVfxMaterials.Ghost);
            ghost.AddComponent<GhostFadeDestroyer>().Initialize(mesh, renderer, Mathf.Clamp(ghostLifetime, .15f, .42f));
        }

        private void OnDisable() => StopDashTrail();

        private sealed class GhostFadeDestroyer : MonoBehaviour
        {
            private Mesh mesh; private MeshRenderer surface; private MaterialPropertyBlock properties;
            private float lifetime, elapsed;
            public void Initialize(Mesh snapshot, MeshRenderer renderer, float seconds)
            { mesh = snapshot; surface = renderer; lifetime = seconds; properties = new MaterialPropertyBlock(); Apply(0f); }
            private void Update()
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / lifetime); Apply(p);
                if (p >= 1f) Destroy(gameObject);
            }
            private void Apply(float p)
            {
                var tint = SoulTrail; tint.a *= .63f * (1f - p) * (1f - p);
                properties.SetColor("_Tint", tint); surface.SetPropertyBlock(properties);
            }
            private void OnDestroy() { if (mesh != null) Destroy(mesh); }
        }
    }
}

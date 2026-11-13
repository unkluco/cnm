using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>
    /// Hiệu ứng vệt lướt trắng (White Dash Trail & Ghost Afterimages).
    /// Kích hoạt dải vệt trắng TrailRenderer và phát sinh các bóng mờ trắng (Afterimages)
    /// dọc theo quãng đường lướt của người chơi.
    /// </summary>
    public class DashTrailEffect : MonoBehaviour
    {
        [Header("Cấu hình dải vệt trắng (Trail)")]
        [SerializeField] private TrailRenderer ribbonTrail;

        [Header("Cấu hình bóng mờ (Ghost Afterimage)")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private float ghostLifetime = 0.35f;
        [SerializeField] private float spawnInterval = 0.04f;

        private Transform _modelRoot;
        private Coroutine _dashEffectCoroutine;

        public void Initialize(Transform modelRoot, Material matGhost, TrailRenderer trail = null)
        {
            _modelRoot = modelRoot;
            ghostMaterial = matGhost;
            ribbonTrail = trail;
        }

        public void StartDashTrail(float duration)
        {
            if (_dashEffectCoroutine != null)
            {
                StopCoroutine(_dashEffectCoroutine);
            }

            _dashEffectCoroutine = StartCoroutine(DashTrailRoutine(duration));
        }

        public void StopDashTrail()
        {
            if (_dashEffectCoroutine != null)
            {
                StopCoroutine(_dashEffectCoroutine);
                _dashEffectCoroutine = null;
            }

            if (ribbonTrail != null)
            {
                ribbonTrail.emitting = false;
            }
        }

        private IEnumerator DashTrailRoutine(float duration)
        {
            if (ribbonTrail != null)
            {
                ribbonTrail.Clear();
                ribbonTrail.emitting = true;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                SpawnGhostAfterimage();
                yield return new WaitForSeconds(spawnInterval);
                elapsed += spawnInterval;
            }

            if (ribbonTrail != null)
            {
                ribbonTrail.emitting = false;
            }
            _dashEffectCoroutine = null;
        }

        private void SpawnGhostAfterimage()
        {
            if (_modelRoot == null) _modelRoot = transform;

            GameObject ghostObj = new GameObject("DashGhost_Afterimage");
            ghostObj.transform.position = _modelRoot.position;
            ghostObj.transform.rotation = _modelRoot.rotation;

            // Thu thập các MeshFilter / Renderer từ mô hình nhân vật để tạo bóng mờ
            MeshFilter[] meshFilters = _modelRoot.GetComponentsInChildren<MeshFilter>();
            Material sharedGhostMat = ghostMaterial;

            if (sharedGhostMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                sharedGhostMat = new Material(shader);
                sharedGhostMat.color = new Color(1f, 1f, 1f, 0.65f);
            }

            List<Material> instancedMats = new List<Material>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                GameObject subGhost = new GameObject(mf.name + "_Ghost");
                subGhost.transform.SetParent(ghostObj.transform, false);
                subGhost.transform.position = mf.transform.position;
                subGhost.transform.rotation = mf.transform.rotation;
                subGhost.transform.localScale = mf.transform.lossyScale;

                MeshFilter newMf = subGhost.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                MeshRenderer mr = subGhost.AddComponent<MeshRenderer>();
                Material instMat = new Material(sharedGhostMat);
                mr.material = instMat;
                instancedMats.Add(instMat);
            }

            // Gắn component tự hủy và fade out
            GhostFadeDestroyer fader = ghostObj.AddComponent<GhostFadeDestroyer>();
            fader.Initialize(instancedMats, ghostLifetime);
        }

        private class GhostFadeDestroyer : MonoBehaviour
        {
            private List<Material> _materials;
            private float _lifetime;
            private float _elapsed = 0f;

            public void Initialize(List<Material> materials, float lifetime)
            {
                _materials = materials;
                _lifetime = lifetime;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(_elapsed / _lifetime);
                float alpha = Mathf.SmoothStep(0.7f, 0f, progress);

                if (_materials != null)
                {
                    for (int i = 0; i < _materials.Count; i++)
                    {
                        if (_materials[i] != null)
                        {
                            Color c = _materials[i].color;
                            c.a = alpha;
                            _materials[i].color = c;
                        }
                    }
                }

                if (_elapsed >= _lifetime)
                {
                    if (_materials != null)
                    {
                        for (int i = 0; i < _materials.Count; i++)
                        {
                            if (_materials[i] != null) Destroy(_materials[i]);
                        }
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}

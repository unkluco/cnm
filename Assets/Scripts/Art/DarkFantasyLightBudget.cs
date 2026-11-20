using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>Caps real-time non-shadowed point lights even when the procedural dungeon contains many rooms.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkFantasyLightBudget : MonoBehaviour
    {
        [SerializeField, Range(1, 8)] private int maximumActiveLights = 4;
        [SerializeField, Min(4f)] private float activationDistance = 18f;
        [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
        private readonly List<DarkFantasyTorchLight> candidates = new List<DarkFantasyTorchLight>();
        private Transform target;
        private float nextUpdate;

        public int MaximumActiveLights => maximumActiveLights;
        public int ActiveLightCount { get; private set; }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + updateInterval;
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }

            Vector3 focus = target != null ? target.position : (Camera.main != null ? Camera.main.transform.position : transform.position);
            Refresh(focus);
        }

        public void Refresh(Vector3 focus)
        {
            candidates.Clear();
            var lights = DarkFantasyTorchLight.ActiveLights;
            float distanceSquared = activationDistance * activationDistance;
            foreach (var torch in lights)
            {
                if (torch == null || torch.Source == null || !torch.gameObject.activeInHierarchy) continue;
                // XZ distance suits the top-down camera and avoids counting camera height.
                Vector3 diff = torch.transform.position - focus;
                diff.y = 0f;
                if (diff.sqrMagnitude <= distanceSquared) candidates.Add(torch);
                else torch.SetContributing(false);
            }

            candidates.Sort((a, b) => Score(a, focus).CompareTo(Score(b, focus)));
            ActiveLightCount = Mathf.Min(maximumActiveLights, candidates.Count);
            for (int i = 0; i < candidates.Count; i++) candidates[i].SetContributing(i < ActiveLightCount);
        }

        private static float Score(DarkFantasyTorchLight torch, Vector3 focus)
        {
            Vector3 diff = torch.transform.position - focus;
            diff.y = 0f;
            // Mild hysteresis: a currently visible torch is preferred when distances are nearly tied.
            return diff.sqrMagnitude - (torch.Source.enabled ? 3f : 0f);
        }
    }
}

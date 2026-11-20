using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>Caps moving dungeon lighting; a small sanctuary can instead keep its entire authored lighting arrangement on.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkFantasyLightBudget : MonoBehaviour
    {
        [SerializeField, Range(1, 8)] private int maximumActiveLights = 4;
        [SerializeField, Min(4f)] private float activationDistance = 18f;
        [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
        [SerializeField, Tooltip("Keep all authored lights in this scene on, independent of player position.")]
        private bool fixedSceneLighting;
        private readonly List<DarkFantasyTorchLight> candidates = new List<DarkFantasyTorchLight>();
        private Transform target;
        private float nextUpdate;

        public int MaximumActiveLights => maximumActiveLights;
        public bool FixedSceneLighting => fixedSceneLighting;
        public int ActiveLightCount { get; private set; }

        public void ConfigureFixedSceneLighting(bool value)
        {
            fixedSceneLighting = value;
            nextUpdate = 0f;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + updateInterval;
            if (fixedSceneLighting)
            {
                Refresh(transform.position);
                return;
            }
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
                if (torch.gameObject.scene != gameObject.scene) continue;
                if (fixedSceneLighting)
                {
                    candidates.Add(torch);
                    continue;
                }
                // XZ distance suits the top-down camera and avoids counting camera height.
                Vector3 diff = torch.transform.position - focus;
                diff.y = 0f;
                if (diff.sqrMagnitude <= distanceSquared) candidates.Add(torch);
                else torch.SetContributing(false);
            }

            // Lobby is a single small room. Do not turn off its sconces, shop/portal or forge
            // when the player crosses an arbitrary distance boundary. All are shadowless.
            if (fixedSceneLighting)
            {
                ActiveLightCount = candidates.Count;
                foreach (var torch in candidates) torch.SetContributing(true);
                return;
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

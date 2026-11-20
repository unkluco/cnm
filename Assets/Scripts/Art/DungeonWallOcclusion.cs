using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>Find the wall sections actually between the fixed camera and the player.</summary>
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    public sealed class DungeonWallOcclusion : MonoBehaviour
    {
        [SerializeField, Min(.02f)] private float scanInterval = .075f;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly HashSet<WallCutawayGroup> previous = new HashSet<WallCutawayGroup>();
        private readonly HashSet<WallCutawayGroup> current = new HashSet<WallCutawayGroup>();
        private Transform player;
        private float nextScan;

        public int OccludedGroupCount => previous.Count;

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + scanInterval;
            if (player == null)
            {
                var go = GameObject.FindWithTag("Player");
                player = go != null ? go.transform : null;
            }
            if (player != null) Refresh(player.position);
        }

        public void Refresh(Vector3 playerPosition)
        {
            current.Clear();
            Scan(playerPosition + Vector3.up * .70f);
            Scan(playerPosition + Vector3.up * 1.35f);
            foreach (var group in previous)
                if (group != null && !current.Contains(group)) group.SetOccluded(false);
            foreach (var group in current) if (group != null) group.SetOccluded(true);
            previous.Clear();
            foreach (var group in current) previous.Add(group);
        }

        private void Scan(Vector3 target)
        {
            Vector3 delta = target - transform.position;
            if (delta.sqrMagnitude < .01f) return;
            int count = Physics.SphereCastNonAlloc(transform.position, .18f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var anchor = hits[i].collider != null ? hits[i].collider.GetComponentInParent<WallOcclusionAnchor>() : null;
                if (anchor != null && anchor.Group != null) current.Add(anchor.Group);
            }
        }

        private void OnDisable()
        {
            foreach (var group in previous) if (group != null) group.SetOccluded(false);
            previous.Clear(); current.Clear();
        }
    }
}

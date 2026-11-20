using UnityEngine;

namespace LightSouls.Art
{
    /// <summary>Links the untouched collision wall to its upper decorative cutaway.</summary>
    [DisallowMultipleComponent]
    public sealed class WallOcclusionAnchor : MonoBehaviour
    {
        [SerializeField] private WallCutawayGroup group;
        public WallCutawayGroup Group => group;
        public void Configure(WallCutawayGroup value) => group = value;
    }
}

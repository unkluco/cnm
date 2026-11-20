using System;
using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Small local camera impulses on confirmed impacts; never changes time scale or the player physics.</summary>
    public static class CombatImpactFeedback
    {
        public static event Action<Vector3, float> Impact;
        public static void Emit(Vector3 point, float strength) => Impact?.Invoke(point, Mathf.Clamp01(strength));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Impact = null;
    }
}

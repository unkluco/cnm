using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>Cosmetic secondary layers degrade before gameplay-essential warnings are ever affected.</summary>
    public static class CombatVfxBudget
    {
        public static int ActiveTransientSystems { get; private set; }
        public static bool AllowSecondaryDetail => ActiveTransientSystems < 26;
        public static int BurstCount(int requested) => Mathf.Clamp(requested, 1, ActiveTransientSystems >= 26 ? 8 : 32);
        internal static void Register() => ActiveTransientSystems++;
        internal static void Unregister() => ActiveTransientSystems = Mathf.Max(0, ActiveTransientSystems - 1);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => ActiveTransientSystems = 0;
    }

    internal sealed class TransientVfxToken : MonoBehaviour
    {
        private void OnEnable() => CombatVfxBudget.Register();
        private void OnDisable() => CombatVfxBudget.Unregister();
    }
}

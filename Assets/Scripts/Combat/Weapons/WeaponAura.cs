using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>Hào quang bao quanh vũ khí đang cầm (theo WeaponVfxProfile.auraColor), dùng chung cho mọi loại vũ khí.</summary>
    public static class WeaponAura
    {
        public static ParticleSystem Attach(WeaponBase weapon, WeaponDataSO data, ParticleSystem existing)
        {
            Detach(ref existing);
            if (weapon == null || data == null || data.Vfx.auraColor.a <= 0f)
            {
                return null;
            }

            return VfxFactory.WeaponEnchant(weapon.transform, data.Vfx.auraColor, data.Vfx.auraRate,
                data.Vfx.element, data.Type == WeaponType.Melee);
        }

        public static void Detach(ref ParticleSystem aura)
        {
            if (aura != null)
            {
                Object.Destroy(aura.gameObject);
            }

            aura = null;
        }
    }
}

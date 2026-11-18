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

            // Hình cầu phát hạt co giãn theo tỉ lệ của mô hình vũ khí, nên hạt bám dọc theo lưỡi/nòng.
            return VfxFactory.Aura(weapon.transform, data.Vfx.auraColor, data.Vfx.auraRate, 0.6f, Vector3.zero, 0.12f, 0.6f, "VFX_WeaponAura");
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

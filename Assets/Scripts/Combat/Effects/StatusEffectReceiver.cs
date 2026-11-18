using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>
    /// Trạng thái bất lợi trên một mục tiêu (thường là quái): bỏng (mất máu theo thời gian), chậm, choáng.
    /// Tự được thêm vào mục tiêu lần đầu trúng hiệu ứng (GetOrAdd). Mỗi trạng thái có hào quang riêng.
    /// Trúng lại khi đang có trạng thái thì gia hạn và lấy mức mạnh hơn, không cộng chồng.
    /// Thêm trạng thái mới: thêm hàm ApplyX ở đây (hoặc một component riêng) rồi tạo WeaponHitEffectSO gọi vào.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusEffectReceiver : MonoBehaviour
    {
        private const float BurnTickInterval = 0.5f;

        public static readonly Color BurnColor = new Color(1f, 0.45f, 0.08f);
        public static readonly Color SlowColor = new Color(0.45f, 0.85f, 1f);
        public static readonly Color StunColor = new Color(1f, 0.95f, 0.35f);

        private IDamageable damageable;
        private ISpeedModifiable speedModifiable;
        private IStunnable stunnable;

        private float burnUntil;
        private float burnDps;
        private float nextBurnTick;
        private GameObject burnSource;
        private ParticleSystem burnAura;

        private float slowUntil;
        private float slowMultiplier = 1f;
        private ParticleSystem slowAura;

        private float stunUntil;
        private ParticleSystem stunAura;

        public bool IsBurning => Time.time < burnUntil;
        public bool IsSlowed => Time.time < slowUntil;

        public static StatusEffectReceiver GetOrAdd(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var receiver = target.GetComponent<StatusEffectReceiver>();
            return receiver != null ? receiver : target.AddComponent<StatusEffectReceiver>();
        }

        private void Awake()
        {
            damageable = GetComponent<IDamageable>();
            speedModifiable = GetComponent<ISpeedModifiable>();
            stunnable = GetComponent<IStunnable>();
        }

        public void ApplyBurn(float damagePerSecond, float duration, GameObject source)
        {
            if (IsDead() || duration <= 0f || damagePerSecond <= 0f)
            {
                return;
            }

            if (!IsBurning)
            {
                nextBurnTick = Time.time + BurnTickInterval;
            }

            burnDps = IsBurning ? Mathf.Max(burnDps, damagePerSecond) : damagePerSecond;
            burnUntil = Mathf.Max(burnUntil, Time.time + duration);
            burnSource = source;
            if (burnAura == null)
            {
                burnAura = VfxFactory.Aura(transform, BurnColor, 26f, 0.45f, Vector3.up * 1f, 0.22f, 2f, "VFX_Burn");
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (IsDead() || duration <= 0f || speedModifiable == null)
            {
                return;
            }

            slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
            speedModifiable.SetSpeedMultiplier(this, slowMultiplier);
            if (slowAura == null)
            {
                slowAura = VfxFactory.Aura(transform, SlowColor, 16f, 0.5f, Vector3.up * 0.9f, 0.16f, -0.5f, "VFX_Slow");
            }
        }

        public void ApplyStun(float duration)
        {
            if (IsDead() || duration <= 0f || stunnable == null)
            {
                return;
            }

            stunnable.Stun(duration);
            stunUntil = Mathf.Max(stunUntil, Time.time + duration);
            if (stunAura == null)
            {
                stunAura = VfxFactory.Aura(transform, StunColor, 12f, 0.3f, Vector3.up * 2.1f, 0.18f, 0f, "VFX_Stun");
            }
        }

        private void Update()
        {
            float now = Time.time;
            if (IsDead())
            {
                ClearAll();
                return;
            }

            if (burnAura != null || burnUntil > 0f)
            {
                if (now < burnUntil)
                {
                    if (now >= nextBurnTick)
                    {
                        nextBurnTick = now + BurnTickInterval;
                        damageable.TakeDamage(new DamageInfo(burnDps * BurnTickInterval, burnSource, transform.position + Vector3.up));
                    }
                }
                else
                {
                    burnUntil = 0f;
                    DestroyAura(ref burnAura);
                }
            }

            if (slowAura != null && now >= slowUntil)
            {
                if (speedModifiable != null) speedModifiable.RemoveSpeedMultiplier(this);
                DestroyAura(ref slowAura);
            }

            if (stunAura != null && now >= stunUntil)
            {
                DestroyAura(ref stunAura);
            }
        }

        private bool IsDead()
        {
            return damageable == null || damageable.IsDead;
        }

        private void ClearAll()
        {
            burnUntil = slowUntil = stunUntil = 0f;
            DestroyAura(ref burnAura);
            DestroyAura(ref slowAura);
            DestroyAura(ref stunAura);
            if (speedModifiable != null) speedModifiable.RemoveSpeedMultiplier(this);
            enabled = false;
        }

        private static void DestroyAura(ref ParticleSystem aura)
        {
            if (aura != null)
            {
                Destroy(aura.gameObject);
            }

            aura = null;
        }
    }
}

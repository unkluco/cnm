using LightSouls.Common;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Bosses;
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
        public bool IsStunned => Time.time < stunUntil;
        public bool HasNegativeEffects=>IsBurning||IsSlowed||IsStunned;
        public void Cleanse()
        {
            burnUntil=slowUntil=stunUntil=0f;burnDps=0f;slowMultiplier=1f;
            DestroyAura(ref burnAura);DestroyAura(ref slowAura);DestroyAura(ref stunAura);
            if(speedModifiable!=null)speedModifiable.RemoveSpeedMultiplier(this);
        }

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
                burnAura = VfxFactory.BurningAura(transform);
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (IsDead() || duration <= 0f || speedModifiable == null)
            {
                return;
            }

            if(GetComponent<BossEnemy>()!=null){multiplier=Mathf.Max(.8f,multiplier);duration=Mathf.Min(1.6f,duration);}
            slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
            speedModifiable.SetSpeedMultiplier(this, slowMultiplier);
            if (slowAura == null)
            {
                slowAura = VfxFactory.FrostAura(transform, SlowColor);
            }
        }

        public void ApplyStun(float duration)=>TryStun(duration);

        public bool TryStun(float duration)
        {
            if (IsDead() || duration <= 0f || stunnable == null)
            {
                return false;
            }
            if(stunnable is EnemyBase enemy)
            {
                if(!enemy.TryStun(duration,out duration))return false;
            }
            else stunnable.Stun(duration);
            stunUntil = Mathf.Max(stunUntil, Time.time + duration);
            if (stunAura == null)
            {
                stunAura = VfxFactory.StunAura(transform, StunColor);
            }
            return true;
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
                        if (CombatVfxBudget.AllowSecondaryDetail)
                            VfxFactory.Burst(transform.position + Vector3.up * .8f, BurnColor, 4, 1.7f, .13f, .27f, -.35f);
                    }
                }
                else
                {
                    burnUntil = 0f;
                    DestroyAura(ref burnAura);
                }
            }

            if (slowUntil>0f && now >= slowUntil)
            {
                if (speedModifiable != null) speedModifiable.RemoveSpeedMultiplier(this);
                DestroyAura(ref slowAura);
                slowUntil=0f;slowMultiplier=1f;
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

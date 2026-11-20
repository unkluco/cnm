using System.Collections;
using System.Collections.Generic;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.Combat.Enemies
{
    public enum SpecialEnemyKind
    {
        Charger, StoneGolem, EmberMage, FrostMage, StormArcher,
        Phantom, BloodLeech, ShieldGuard, LightPriest, BombWisp
    }

    /// <summary>Ten tactical enemy patterns. All warning areas are placed before the attack resolves.</summary>
    public sealed class SpecialEnemy : EnemyBase
    {
        [Header("Special ability")]
        [SerializeField] private SpecialEnemyKind kind;
        [SerializeField, Min(0.2f)] private float windup = 0.7f;
        [SerializeField, Min(0.5f)] private float abilityRadius = 2f;
        [SerializeField] private Color accent = Color.red;
        [SerializeField] private GameObject projectilePrefab;

        private readonly List<ArcaneTelegraph> warnings = new List<ArcaneTelegraph>();
        private ParticleSystem castAura;
        private Coroutine attack;
        private float baseReduction;
        private bool casting;
        private float nextAttack;

        public SpecialEnemyKind Kind => kind;
        public bool IsCasting => casting;
        private CombatVfxElement Element => kind switch
        {
            SpecialEnemyKind.EmberMage or SpecialEnemyKind.BombWisp => CombatVfxElement.Ember,
            SpecialEnemyKind.FrostMage => CombatVfxElement.Frost,
            SpecialEnemyKind.StormArcher => CombatVfxElement.Storm,
            SpecialEnemyKind.Phantom => CombatVfxElement.Void,
            SpecialEnemyKind.BloodLeech => CombatVfxElement.Blood,
            SpecialEnemyKind.LightPriest => CombatVfxElement.Life,
            SpecialEnemyKind.StoneGolem or SpecialEnemyKind.Charger => CombatVfxElement.Stone,
            _ => CombatVfxElement.Steel
        };

        protected override void Awake()
        {
            base.Awake();
            baseReduction = _health.DamageReduction;
            // Stagger casts when a wave spawns; prevents simultaneous unavoidable damage.
            nextAttack = Time.time + Random.Range(0.35f, 1.1f);
            if (kind == SpecialEnemyKind.ShieldGuard) SetGuard(true);
        }

        protected override void HandleBehavior(float distanceToPlayer)
        {
            if (_playerController == null || _playerController.IsDead || casting) return;
            if (IsRanged() && distanceToPlayer < 2.6f && kind != SpecialEnemyKind.Phantom)
            {
                Vector3 away = transform.position - _playerTransform.position;
                away.y = 0f;
                if (NavMesh.SamplePosition(transform.position + away.normalized * 2.5f, out var flee, 1.5f, NavMesh.AllAreas))
                    MoveTo(flee.position);
                LookAtPlayer();
                return;
            }

            if (distanceToPlayer > attackRange)
            {
                MoveTo(_playerTransform.position);
                return;
            }

            StopMoving();
            LookAtPlayer();
            if (Time.time >= nextAttack && ClearLine(transform.position, _playerTransform.position)) ExecuteAttack();
            else if (!ClearLine(transform.position, _playerTransform.position)) MoveTo(_playerTransform.position);
        }

        private bool IsRanged() => kind == SpecialEnemyKind.EmberMage || kind == SpecialEnemyKind.FrostMage ||
            kind == SpecialEnemyKind.StormArcher || kind == SpecialEnemyKind.LightPriest || kind == SpecialEnemyKind.Phantom;

        private void MoveTo(Vector3 position)
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(position);
            }
        }

        private void StopMoving()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        protected override void ExecuteAttack()
        {
            if (casting || IsDead || IsStunned || _playerTransform == null) return;
            casting = true;
            _isAttackingLimbAnim = true;
            StopMoving();
            attack = StartCoroutine(CastRoutine());
        }

        private IEnumerator CastRoutine()
        {
            Vector3 origin = transform.position;
            Vector3 target = _playerTransform.position;
            target.y = origin.y;
            Vector3 direction = target - origin;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            Vector3 destination = target;
            HealthComponent ally = null;
            Vector3[] fireMarks = null;

            switch (kind)
            {
                case SpecialEnemyKind.Charger:
                    destination = ClampToNavMesh(origin, origin + direction * Mathf.Min(7f, Vector3.Distance(origin, target) + 1.2f));
                    WarnLane(origin, destination, 0.85f);
                    break;
                case SpecialEnemyKind.EmberMage:
                    Vector3 side = Vector3.Cross(Vector3.up, direction);
                    fireMarks = new[] { target, target + side * 2.0f, target - side * 2.0f };
                    foreach (var mark in fireMarks) WarnCircle(mark, abilityRadius);
                    break;
                case SpecialEnemyKind.FrostMage:
                    WarnCircle(target, abilityRadius);
                    break;
                case SpecialEnemyKind.StormArcher:
                    for (int i = -2; i <= 2; i++)
                        WarnLane(origin, origin + Quaternion.Euler(0f, i * 13f, 0f) * direction * 10f, 0.16f);
                    break;
                case SpecialEnemyKind.Phantom:
                    destination = ClampToNavMesh(origin, target - direction * 0.8f);
                    WarnCircle(destination, abilityRadius);
                    break;
                case SpecialEnemyKind.LightPriest:
                    ally = FindInjuredAlly();
                    if (ally != null)
                    {
                        destination = ally.transform.position;
                        WarnCircle(destination, abilityRadius, true);
                    }
                    else WarnCircle(target, abilityRadius);
                    break;
                case SpecialEnemyKind.ShieldGuard:
                    SetGuard(false);
                    WarnCircle(origin, abilityRadius);
                    break;
                default:
                    WarnCircle(origin, abilityRadius);
                    break;
            }

            if (CombatVfxBudget.AllowSecondaryDetail)
                castAura = VfxFactory.Aura(transform, new Color(accent.r, accent.g, accent.b, .83f), 17f, .38f,
                    Vector3.up * 1.1f, .18f, .8f, "VFX_CastCharge");

            // IsStunned can change while the animation runs; an interrupted cast cannot hit.
            float elapsed = 0f;
            while (elapsed < windup && CanResolve())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            ClearWarnings();

            if (CanResolve())
            {
                switch (kind)
                {
                    case SpecialEnemyKind.Charger:
                        yield return ChargeTo(destination);
                        break;
                    case SpecialEnemyKind.StoneGolem:
                        Impact(origin, abilityRadius);
                        HitCircle(origin, abilityRadius, damage);
                        break;
                    case SpecialEnemyKind.EmberMage:
                        bool hit = false;
                        foreach (var mark in fireMarks)
                        {
                            Impact(mark, abilityRadius);
                            if (!hit && HitCircle(mark, abilityRadius, damage) > 0f) hit = true;
                        }
                        if (hit && _playerController != null && !_playerController.IsDead)
                            StatusEffectReceiver.GetOrAdd(_playerTransform.gameObject).ApplyBurn(3f, 2.5f, gameObject);
                        break;
                    case SpecialEnemyKind.FrostMage:
                        Impact(target, abilityRadius);
                        if (HitCircle(target, abilityRadius, damage) > 0f && !_playerController.IsDead)
                            StatusEffectReceiver.GetOrAdd(_playerTransform.gameObject).ApplySlow(0.55f, 2.5f);
                        break;
                    case SpecialEnemyKind.StormArcher:
                        VfxFactory.Muzzle(transform.position + Vector3.up * .95f + direction * .65f, direction, accent, Element, true);
                        for (int i = -2; i <= 2; i++) FireOrb(Quaternion.Euler(0f, i * 13f, 0f) * direction);
                        break;
                    case SpecialEnemyKind.Phantom:
                        VfxFactory.Burst(origin + Vector3.up * .8f, accent, 12, 2.4f, .22f, .42f, -.35f);
                        if (_agent != null && _agent.isOnNavMesh) _agent.Warp(destination);
                        Impact(transform.position, abilityRadius);
                        HitCircle(transform.position, abilityRadius, damage);
                        break;
                    case SpecialEnemyKind.BloodLeech:
                        Impact(origin, abilityRadius);
                        float stolen = HitCircle(origin, abilityRadius, damage);
                        if (stolen > 0f) _health.Heal(stolen * 1.5f);
                        break;
                    case SpecialEnemyKind.ShieldGuard:
                        Impact(origin, abilityRadius);
                        HitCircle(origin, abilityRadius, damage);
                        break;
                    case SpecialEnemyKind.LightPriest:
                        if (ally != null && !ally.IsDead && Vector3.Distance(destination, ally.transform.position) <= abilityRadius &&
                            ClearLine(origin, ally.transform.position))
                        {
                            ally.Heal(damage * 1.8f);
                            Impact(destination, abilityRadius);
                        }
                        else if (ally == null)
                        {
                            Impact(target, abilityRadius);
                            HitCircle(target, abilityRadius, damage);
                        }
                        break;
                    case SpecialEnemyKind.BombWisp:
                        Impact(origin, abilityRadius);
                        HitCircle(origin, abilityRadius, damage);
                        _health.TakeDamage(new DamageInfo(_health.MaxHealth * 20f, gameObject, transform.position));
                        break;
                }
            }

            // Guard is vulnerable during recovery too.
            yield return new WaitForSeconds(kind == SpecialEnemyKind.ShieldGuard ? 0.65f : 0.2f);
            if (kind == SpecialEnemyKind.ShieldGuard && !IsDead) SetGuard(true);
            casting = false;
            _isAttackingLimbAnim = false;
            nextAttack = Time.time + attackCooldown;
            attack = null;
        }

        private IEnumerator ChargeTo(Vector3 destination)
        {
            bool hit = false;
            VfxFactory.AttachTrail(gameObject, accent, .38f, .24f);
            VfxFactory.Burst(transform.position + Vector3.up * .18f, accent, 9, 3f, .18f, .36f, .58f);
            float start = Time.time;
            while (CanResolve() && Time.time - start < 0.55f && _agent != null && _agent.isOnNavMesh)
            {
                Vector3 delta = destination - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.05f) break;
                _agent.Move(Vector3.ClampMagnitude(delta, 19f * Time.deltaTime));
                if (!hit && HitCircle(transform.position, 1.05f, damage) > 0f) hit = true;
                yield return null;
            }
            VfxFactory.ReleaseTrail(gameObject);
            Impact(transform.position, 1.2f);
        }

        private Vector3 ClampToNavMesh(Vector3 origin, Vector3 target)
        {
            if (_agent == null || !_agent.isOnNavMesh) return origin;
            if (!NavMesh.SamplePosition(target, out var sampled, 1.5f, _agent.areaMask)) return origin;
            if (NavMesh.Raycast(origin, sampled.position, out var blocked, _agent.areaMask))
                return Vector3.MoveTowards(blocked.position, origin, 0.7f);
            return sampled.position;
        }

        private bool CanResolve() => !IsDead && !IsStunned && _playerTransform != null &&
            _playerController != null && !_playerController.IsDead;

        private bool ClearLine(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from + Vector3.up * 0.8f, to + Vector3.up * 0.8f,
                1 << 8, QueryTriggerInteraction.Ignore);
        }

        private float HitCircle(Vector3 center, float radius, float amount)
        {
            if (!CanResolve()) return 0f;
            Vector3 offset = _playerTransform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius || !ClearLine(center, _playerTransform.position)) return 0f;
            var health = _playerTransform.GetComponent<HealthComponent>();
            if (health == null || health.IsDead) return 0f;
            float before = health.CurrentHealth;
            health.TakeDamage(new DamageInfo(amount, gameObject, _playerTransform.position));
            float inflicted = Mathf.Max(0f, before - health.CurrentHealth);
            if (inflicted > 0f)
            {
                Vector3 point = _playerTransform.position + Vector3.up * .85f;
                VfxFactory.HitImpact(point, _playerTransform.position - center, accent, Element, .47f);
                CombatImpactFeedback.Emit(point, .54f);
            }
            return inflicted;
        }

        private HealthComponent FindInjuredAlly()
        {
            HealthComponent result = null;
            float bestRatio = 0.8f;
            foreach (var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            {
                if (enemy == this || enemy.IsDead || Vector3.Distance(transform.position, enemy.transform.position) > 8f) continue;
                var health = enemy.GetComponent<HealthComponent>();
                if (health == null || health.MaxHealth <= 0f || !ClearLine(transform.position, enemy.transform.position)) continue;
                float ratio = health.CurrentHealth / health.MaxHealth;
                if (ratio < bestRatio) { result = health; bestRatio = ratio; }
            }
            return result;
        }

        private void FireOrb(Vector3 direction)
        {
            Vector3 spawn = transform.position + Vector3.up * 0.95f + direction * 0.65f;
            if (projectilePrefab == null) return;
            GameObject orb = Instantiate(projectilePrefab, spawn, Quaternion.LookRotation(direction));
            orb.layer = 10;
            var projectile = orb.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(damage * 0.65f, 9f, 2f, 1 << 6, gameObject);
                projectile.SetVisualStyle(accent, Element, .15f);
            }
            else Destroy(orb);
        }

        private void WarnCircle(Vector3 center, float radius, bool blessing = false)
        {
            warnings.Add(ArcaneTelegraph.Circle("EnemyWarning_" + kind, center, radius, accent, windup, Element,
                blessing ? TelegraphStyle.Blessing : TelegraphStyle.Danger, transform));
        }

        private void WarnLane(Vector3 start, Vector3 end, float halfWidth)
        {
            warnings.Add(ArcaneTelegraph.Lane("EnemyWarning_" + kind, start, end, halfWidth, accent, windup, Element, transform));
        }

        private void Impact(Vector3 position, float radius)
        {
            if (kind == SpecialEnemyKind.BombWisp) VfxFactory.Explosion(position, radius, accent);
            else VfxFactory.SpellImpact(position, radius, accent, Element);
        }

        private void SetGuard(bool guard)
        {
            if (_health != null) _health.DamageReduction = guard ? Mathf.Max(baseReduction, 0.55f) : baseReduction;
        }

        private void ClearWarnings()
        {
            foreach (var warning in warnings) if (warning != null) warning.Remove();
            warnings.Clear();
            if (castAura != null) { castAura.gameObject.SetActive(false); Destroy(castAura.gameObject); castAura = null; }
        }

        protected override void HandleDied(IDamageable damageable)
        {
            if (attack != null) StopCoroutine(attack);
            attack = null;
            casting = false;
            ClearWarnings();
            VfxFactory.ReleaseTrail(gameObject);
            base.HandleDied(damageable);
        }

        protected override void OnDestroy()
        {
            ClearWarnings();
            base.OnDestroy();
        }

        private void OnDisable()
        {
            if (attack != null) StopCoroutine(attack);
            attack = null;
            casting = false;
            _isAttackingLimbAnim = false;
            ClearWarnings();
            if (kind == SpecialEnemyKind.ShieldGuard && !IsDead) SetGuard(true);
            if (gameObject.activeInHierarchy) VfxFactory.ReleaseTrail(gameObject);
        }
    }
}

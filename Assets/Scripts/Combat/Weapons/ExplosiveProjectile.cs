using System.Collections.Generic;
using LightSouls.Combat.Effects;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Vật bay theo đường vòng cung rồi nổ khi chạm đích (đạn phóng lựu, bom tay).
    /// Vụ nổ gây sát thương mọi IDamageable trong bán kính, giảm dần theo khoảng cách (100% ở tâm, 50% ở rìa),
    /// nhân thêm hệ số kháng nổ của mục tiêu (IHazardResistant, ví dụ Giáp Chịu Nhiệt).
    /// Tạo bằng Launch(...), không cần prefab.
    /// </summary>
    public class ExplosiveProjectile : MonoBehaviour
    {
        private static readonly Color FireColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color SmokeColor = new Color(0.35f, 0.33f, 0.32f, 0.8f);

        private Vector3 start;
        private Vector3 end;
        private float flightTime;
        private float arcHeight;
        private float elapsed;
        private float damage;
        private float radius;
        private GameObject owner;
        private bool damagesOwner;
        private WeaponDataSO weapon;
        private bool exploded;

        public static ExplosiveProjectile Launch(Vector3 from, Vector3 to, float flightTime, float arcHeight, float damage,
            float radius, GameObject owner, bool damagesOwner, Color color, WeaponDataSO weapon = null, float scale = 0.35f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExplosiveProjectile";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            VfxFactory.AttachTrail(go, new Color(1f, 0.6f, 0.2f, 0.8f), 0.22f, 0.25f);
            VfxFactory.Aura(go.transform, FireColor, 30f, 0.1f, Vector3.zero, 0.12f, 0.2f, "VFX_Fuse");

            var projectile = go.AddComponent<ExplosiveProjectile>();
            projectile.start = from;
            projectile.end = to;
            projectile.flightTime = Mathf.Max(0.1f, flightTime);
            projectile.arcHeight = arcHeight;
            projectile.damage = damage;
            projectile.radius = radius;
            projectile.owner = owner;
            projectile.damagesOwner = damagesOwner;
            projectile.weapon = weapon;
            return projectile;
        }

        private void Update()
        {
            if (exploded)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += arcHeight * 4f * t * (1f - t);
            transform.position = position;
            transform.Rotate(540f * Time.deltaTime, 0f, 0f, Space.Self);

            if (t >= 1f)
            {
                Explode();
            }
        }

        private void Explode()
        {
            exploded = true;
            Vector3 center = transform.position;

            VfxFactory.Burst(center + Vector3.up * 0.3f, FireColor, 40, 7f, 0.4f, 0.55f, 0.3f);
            VfxFactory.Burst(center + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.4f), 20, 4f, 0.3f, 0.35f, 0f);
            VfxFactory.Burst(center + Vector3.up * 0.5f, SmokeColor, 18, 1.5f, 0.7f, 1.1f, -0.2f);
            VfxFactory.Ring(center, FireColor, radius, 0.35f, 56);

            var damaged = new HashSet<IDamageable>();
            foreach (Collider hit in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                IDamageable target = WeaponHitResolver.FindDamageable(hit);
                if (target == null || target.IsDead || !damaged.Add(target))
                {
                    continue;
                }

                var component = target as Component;
                bool isOwner = component != null && owner != null && component.transform.root == owner.transform.root;
                if (isOwner && !damagesOwner)
                {
                    continue;
                }

                float distance = Vector3.Distance(center, hit.ClosestPoint(center));
                float falloff = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / Mathf.Max(0.01f, radius)));
                float amount = damage * falloff * HazardResistance.GetMultiplier(component, HazardKind.Explosion);
                if (amount <= 0f)
                {
                    continue;
                }

                if (isOwner)
                {
                    target.TakeDamage(new DamageInfo(amount, owner, center));
                }
                else
                {
                    WeaponHitResolver.Apply(weapon, amount, owner, target, hit.ClosestPoint(center), false);
                }
            }

            Destroy(gameObject);
        }
    }
}

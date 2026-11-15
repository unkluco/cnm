using System;
using System.Collections;
using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Thùng nổ chiến thuật (Explosive Barrel):
    /// Cài đặt IDamageable, có thể bị kích nổ bởi người chơi, quái vật hoặc đạn/vụ nổ lân cận.
    /// Vòng đời: Idle -> Ignited -> Exploding -> Destroyed.
    /// Gây sát thương diện rộng và hỗ trợ phản ứng dây chuyền (chain reaction) có kiểm soát, chống đệ quy lặp vô hạn.
    /// </summary>
    public class ExplosiveBarrelHazard : MonoBehaviour, IDamageable
    {
        public enum BarrelState
        {
            Idle,
            Ignited,
            Exploding,
            Destroyed
        }

        [Header("Barrel Health")]
        [SerializeField, Min(1f)] private float maxHealth = 20f;

        [Header("Explosion Properties")]
        [Tooltip("Bán kính sát thương khi phát nổ.")]
        [SerializeField, Min(1f)] private float explosionRadius = 4.0f;

        [Tooltip("Lượng sát thương tối đa tại tâm nổ (áp dụng cho cả người chơi lẫn quái).")]
        [SerializeField, Min(5f)] private float explosionDamage = 40f;

        [Tooltip("Lực đẩy văng (knockback) tác động lên đối tượng có Rigidbody.")]
        [SerializeField, Min(0f)] private float explosionForce = 12f;

        [Tooltip("Thời gian bén lửa trước khi nổ (giây).")]
        [SerializeField, Min(0f)] private float fuseTime = 0.2f;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer barrelRenderer;
        [SerializeField] private Color normalColor = new Color(0.75f, 0.25f, 0.15f);
        [SerializeField] private Color ignitedColor = new Color(1.0f, 0.85f, 0.1f);
        [SerializeField] private GameObject explosionEffectPrefab;

        private float currentHealth;
        private BarrelState currentState = BarrelState.Idle;
        private bool hasExploded = false;
        private readonly Collider[] hitColliders = new Collider[32];

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f || hasExploded;
        public BarrelState State => currentState;

        public event Action<IDamageable> Died;
        public event Action<ExplosiveBarrelHazard> OnExploded;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentState = BarrelState.Idle;
            hasExploded = false;

            if (barrelRenderer != null)
            {
                barrelRenderer.material.color = normalColor;
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || currentState != BarrelState.Idle)
            {
                return;
            }

            currentHealth -= info.Amount;
            if (currentHealth <= 0f)
            {
                Ignite();
            }
        }

        public void Ignite()
        {
            if (currentState != BarrelState.Idle)
            {
                return;
            }

            currentState = BarrelState.Ignited;

            if (barrelRenderer != null)
            {
                barrelRenderer.material.color = ignitedColor;
            }

            if (fuseTime > 0.01f)
            {
                StartCoroutine(FuseRoutine());
            }
            else
            {
                Explode();
            }
        }

        private IEnumerator FuseRoutine()
        {
            float elapsed = 0f;
            while (elapsed < fuseTime)
            {
                elapsed += Time.deltaTime;
                // Nhấp nháy màu cảnh báo
                if (barrelRenderer != null)
                {
                    float t = Mathf.PingPong(elapsed * 12f, 1f);
                    barrelRenderer.material.color = Color.Lerp(normalColor, ignitedColor, t);
                }
                yield return null;
            }

            Explode();
        }

        public void Explode()
        {
            if (hasExploded)
            {
                return;
            }

            hasExploded = true;
            currentState = BarrelState.Exploding;
            Died?.Invoke(this);

            Vector3 explosionCenter = transform.position;

            // Spawn visual effect nếu có
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, explosionCenter, Quaternion.identity);
            }

            // Quét các đối tượng trong bán kính nổ
            int hitCount = Physics.OverlapSphereNonAlloc(explosionCenter, explosionRadius, hitColliders);
            var damagedEntities = new HashSet<IDamageable>();
            var barrelsToChain = new List<ExplosiveBarrelHazard>();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitColliders[i];
                if (col == null || col.gameObject == gameObject)
                {
                    continue;
                }

                // 1. Gây sát thương IDamageable (cả Player lẫn Enemy)
                IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead && damagedEntities.Add(damageable))
                {
                    float distance = Vector3.Distance(explosionCenter, col.bounds.center);
                    float damageMultiplier = Mathf.Clamp01(1f - (distance / explosionRadius));
                    float finalDamage = Mathf.Max(5f, explosionDamage * damageMultiplier);

                    damageable.TakeDamage(new DamageInfo(finalDamage, gameObject, col.bounds.center));
                }

                // 2. Tác động lực đẩy knockback lên Rigidbody
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, 0.5f, ForceMode.Impulse);
                }

                // 3. Phản ứng dây chuyền với thùng nổ khác lân cận
                ExplosiveBarrelHazard otherBarrel = col.GetComponent<ExplosiveBarrelHazard>() ?? col.GetComponentInParent<ExplosiveBarrelHazard>();
                if (otherBarrel != null && !otherBarrel.hasExploded && otherBarrel != this)
                {
                    barrelsToChain.Add(otherBarrel);
                }
            }

            // Kích nổ dây chuyền có kiểm soát
            foreach (ExplosiveBarrelHazard chainBarrel in barrelsToChain)
            {
                if (chainBarrel != null && !chainBarrel.hasExploded)
                {
                    chainBarrel.Ignite();
                }
            }

            OnExploded?.Invoke(this);
            currentState = BarrelState.Destroyed;

            // Ẩn renderer & collider, hủy object sau một thoáng để kịp xử lý hiệu ứng
            if (barrelRenderer != null)
            {
                barrelRenderer.enabled = false;
            }

            Collider myCol = GetComponent<Collider>();
            if (myCol != null)
            {
                myCol.enabled = false;
            }

            Destroy(gameObject, 0.8f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}

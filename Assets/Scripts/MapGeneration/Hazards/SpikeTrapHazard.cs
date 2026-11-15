using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Bẫy gai: nhô lên theo chu kỳ có báo trước rõ ràng (~0.6s đổi màu cảnh báo/rung) trước khi gây sát thương.
    /// Đảm bảo người chơi luôn nhìn thấy nguy hiểm trước khi chịu sát thương.
    /// </summary>
    public class SpikeTrapHazard : HazardZoneBase
    {
        public enum TrapState
        {
            Idle,
            Telegraph,
            Active,
            Retracting
        }

        [Header("Trap Timing Settings")]
        [Tooltip("Thời gian ẩn an toàn (giây).")]
        [SerializeField, Min(0.2f)] private float idleDuration = 1.8f;

        [Tooltip("Thời gian báo trước (đổi màu cảnh báo, rung) trước khi gai trồi lên.")]
        [SerializeField, Min(0.3f)] private float telegraphDuration = 0.6f;

        [Tooltip("Thời gian gai nhô lên gây sát thương (giây).")]
        [SerializeField, Min(0.2f)] private float activeDuration = 0.8f;

        [Header("Damage Settings")]
        [Tooltip("Lượng sát thương khi gai đâm trúng.")]
        [SerializeField, Min(1f)] private float spikeDamage = 15f;

        [Header("Visual Feedback (Báo trước nhìn rõ)")]
        [SerializeField] private Transform spikesTransform;
        [SerializeField] private Renderer warningPlateRenderer;
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        [SerializeField] private Color telegraphColor = new Color(0.9f, 0.2f, 0.1f, 1f);
        [SerializeField] private float retractedHeight = -0.4f;
        [SerializeField] private float extendedHeight = 0.6f;

        private TrapState currentState = TrapState.Idle;
        private float stateTimer = 0f;
        private readonly HashSet<IDamageable> damagedEntitiesInCurrentCycle = new HashSet<IDamageable>();
        private MaterialPropertyBlock propBlock;

        public TrapState CurrentState => currentState;
        public float SpikeDamage
        {
            get => spikeDamage;
            set => spikeDamage = Mathf.Max(1f, value);
        }

        protected override void Awake()
        {
            base.Awake();
            propBlock = new MaterialPropertyBlock();
            SetVisualState(TrapState.Idle);
        }

        private void Update()
        {
            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case TrapState.Idle:
                    if (stateTimer >= idleDuration)
                    {
                        TransitionTo(TrapState.Telegraph);
                    }
                    break;

                case TrapState.Telegraph:
                    // Rung nhẹ trong giai đoạn báo trước
                    if (spikesTransform != null)
                    {
                        float shake = Mathf.Sin(Time.time * 40f) * 0.02f;
                        spikesTransform.localPosition = new Vector3(shake, retractedHeight + 0.05f, 0f);
                    }

                    if (stateTimer >= telegraphDuration)
                    {
                        TransitionTo(TrapState.Active);
                    }
                    break;

                case TrapState.Active:
                    if (spikesTransform != null)
                    {
                        spikesTransform.localPosition = new Vector3(0f, extendedHeight, 0f);
                    }

                    DamageEntitiesInTrigger();

                    if (stateTimer >= activeDuration)
                    {
                        TransitionTo(TrapState.Idle);
                    }
                    break;
            }
        }

        private void TransitionTo(TrapState nextState)
        {
            currentState = nextState;
            stateTimer = 0f;
            SetVisualState(nextState);

            if (nextState == TrapState.Active)
            {
                damagedEntitiesInCurrentCycle.Clear();
                DamageEntitiesInTrigger();
            }
        }

        private void DamageEntitiesInTrigger()
        {
            if (trackedColliders.Count == 0)
            {
                return;
            }

            foreach (Collider col in trackedColliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                IDamageable damageable = FindDamageable(col);
                if (damageable != null && !damageable.IsDead && damagedEntitiesInCurrentCycle.Add(damageable))
                {
                    Vector3 hitPoint = col.bounds.center;
                    damageable.TakeDamage(new DamageInfo(spikeDamage, gameObject, hitPoint));
                }
            }
        }

        private void SetVisualState(TrapState state)
        {
            if (spikesTransform != null)
            {
                float targetY = state == TrapState.Active ? extendedHeight : retractedHeight;
                spikesTransform.localPosition = new Vector3(0f, targetY, 0f);
            }

            if (warningPlateRenderer != null)
            {
                Color targetCol = state == TrapState.Telegraph ? telegraphColor : normalColor;
                warningPlateRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", targetCol);
                propBlock.SetColor("_Color", targetCol);
                warningPlateRenderer.SetPropertyBlock(propBlock);
            }
        }

        protected override void OnEntityEntered(Collider other)
        {
            if (currentState == TrapState.Active)
            {
                IDamageable damageable = FindDamageable(other);
                if (damageable != null && !damageable.IsDead && damagedEntitiesInCurrentCycle.Add(damageable))
                {
                    Vector3 hitPoint = other.bounds.center;
                    damageable.TakeDamage(new DamageInfo(spikeDamage, gameObject, hitPoint));
                }
            }
        }

        protected override void OnEntityExited(Collider other)
        {
            // Không cần làm gì thêm khi thoát
        }

        protected override void CleanupAllTracked()
        {
            damagedEntitiesInCurrentCycle.Clear();
            trackedColliders.Clear();
        }
    }
}

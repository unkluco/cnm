using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Bẫy gai chiến thuật: nhô lên theo chu kỳ có báo hiệu trước 0.6s (đổi màu cảnh báo đỏ/rung) rồi mới gây sát thương.
    /// Tích hợp chặt chẽ với Room Lifecycle: chỉ hoạt động trong trạng thái Combat;
    /// khi phòng chuyển sang Cleared (hoặc Unvisited), bẫy lập tức thu xuống và tắt hitbox sát thương.
    /// Hỗ trợ phaseOffset để các dải bẫy có thể kích hoạt lệch nhịp, tạo cơ hội di chuyển chiến thuật cho người chơi.
    /// </summary>
    public class SpikeTrapHazard : HazardZoneBase
    {
        public enum TrapState
        {
            Retracted,   // Ẩn an toàn dưới sàn
            Warning,     // Báo trước 0.6s: tấm cảnh báo đỏ rực, gai rung nhẹ
            Extending,   // Gai bắt đầu vọt lên
            Active,      // Gai nhô cao nhất, gây sát thương
            Retracting   // Gai thụt xuống
        }

        [Header("Trap Timing Settings (seconds)")]
        [Tooltip("Thời gian nghỉ khi gai đã thu xuống (Rest).")]
        [SerializeField, Min(0.1f)] private float restDuration = 1.2f;

        [Tooltip("Thời gian báo trước rõ ràng (đổi màu đỏ, rung) trước khi gai đâm.")]
        [SerializeField, Min(0.3f)] private float warningDuration = 0.6f;

        [Tooltip("Thời gian gai vọt lên.")]
        [SerializeField, Min(0.05f)] private float extendingDuration = 0.15f;

        [Tooltip("Thời gian duy trì trạng thái gai nhô cao gây sát thương.")]
        [SerializeField, Min(0.1f)] private float activeDuration = 0.55f;

        [Tooltip("Thời gian gai thụt xuống.")]
        [SerializeField, Min(0.05f)] private float retractingDuration = 0.25f;

        [Tooltip("Độ lệch nhịp ban đầu để các cụm bẫy khác nhau không nổ cùng lúc.")]
        [SerializeField, Min(0f)] private float phaseOffset = 0f;

        [Header("Room Lifecycle Integration")]
        [Tooltip("Tự động lắng nghe vòng đời phòng để chỉ kích hoạt trong trạng thái Combat.")]
        [SerializeField] private RoomLifecycleController roomLifecycle;

        [Tooltip("Bật nếu muốn bẫy luôn hoạt động tự do ngoài vòng đời phòng (cho các Scene test đơn lẻ).")]
        [SerializeField] private bool alwaysActive = false;

        [Header("Damage Settings")]
        [Tooltip("Lượng sát thương khi gai đâm trúng.")]
        [SerializeField, Min(1f)] private float spikeDamage = 15f;

        [Header("Visual Feedback")]
        [SerializeField] private Transform spikesTransform;
        [SerializeField] private Renderer warningPlateRenderer;
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        [SerializeField] private Color warningColor = new Color(0.95f, 0.2f, 0.1f, 1f);
        [SerializeField] private float retractedHeight = -0.4f;
        [SerializeField] private float extendedHeight = 0.6f;

        private TrapState currentState = TrapState.Retracted;
        private float stateTimer = 0f;
        private readonly HashSet<IDamageable> damagedEntitiesInCurrentCycle = new HashSet<IDamageable>();
        private MaterialPropertyBlock propBlock;
        private Collider triggerCollider;

        public TrapState CurrentState => currentState;
        public float SpikeDamage
        {
            get => spikeDamage;
            set => spikeDamage = Mathf.Max(1f, value);
        }

        public float PhaseOffset
        {
            get => phaseOffset;
            set => phaseOffset = Mathf.Max(0f, value);
        }

        public bool AlwaysActive
        {
            get => alwaysActive;
            set => alwaysActive = value;
        }

        public RoomLifecycleController RoomLifecycle
        {
            get => roomLifecycle;
            set
            {
                UnsubscribeLifecycle();
                roomLifecycle = value;
                SubscribeLifecycle();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            propBlock = new MaterialPropertyBlock();
            triggerCollider = GetComponent<Collider>();

            if (roomLifecycle == null)
            {
                roomLifecycle = GetComponentInParent<RoomLifecycleController>();
            }

            SubscribeLifecycle();

            // Áp dụng phaseOffset
            stateTimer = -phaseOffset;
            SetVisualState(TrapState.Retracted);
        }

        protected override void OnDestroy()
        {
            UnsubscribeLifecycle();
            base.OnDestroy();
        }

        protected override void OnDisable()
        {
            ForceRetractAndDisable();
            base.OnDisable();
        }

        private void SubscribeLifecycle()
        {
            if (roomLifecycle != null)
            {
                roomLifecycle.OnRoomEnteredCombat += HandleRoomEnteredCombat;
                roomLifecycle.OnRoomCleared += HandleRoomCleared;
            }
        }

        private void UnsubscribeLifecycle()
        {
            if (roomLifecycle != null)
            {
                roomLifecycle.OnRoomEnteredCombat -= HandleRoomEnteredCombat;
                roomLifecycle.OnRoomCleared -= HandleRoomCleared;
            }
        }

        private void HandleRoomEnteredCombat(RoomModule module)
        {
            // Bắt đầu chu kỳ khi phòng bước vào Combat
            stateTimer = -phaseOffset;
            TransitionTo(TrapState.Retracted);
        }

        private void HandleRoomCleared(RoomModule module)
        {
            // Ngay lập tức thu gai và tắt sát thương khi phòng đã dọn sạch
            ForceRetractAndDisable();
        }

        public void ForceRetractAndDisable()
        {
            currentState = TrapState.Retracted;
            stateTimer = 0f;
            damagedEntitiesInCurrentCycle.Clear();
            SetVisualState(TrapState.Retracted);

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }

        public bool IsHazardActiveInCurrentRoomState()
        {
            if (alwaysActive)
            {
                return true;
            }

            if (roomLifecycle == null)
            {
                return true; // Không thuộc phòng nào: cho phép chạy theo chu kỳ bình thường
            }

            return roomLifecycle.State == RoomLifecycleState.Combat;
        }

        private void Update()
        {
            if (!IsHazardActiveInCurrentRoomState())
            {
                if (currentState != TrapState.Retracted)
                {
                    ForceRetractAndDisable();
                }
                return;
            }

            // Đảm bảo collider được bật khi chu kỳ hoạt động
            if (triggerCollider != null && !triggerCollider.enabled)
            {
                triggerCollider.enabled = true;
            }

            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case TrapState.Retracted:
                    if (stateTimer >= restDuration)
                    {
                        TransitionTo(TrapState.Warning);
                    }
                    break;

                case TrapState.Warning:
                    // Rung nhẹ trong giai đoạn báo trước 0.6s
                    if (spikesTransform != null)
                    {
                        float shake = Mathf.Sin(Time.time * 45f) * 0.025f;
                        spikesTransform.localPosition = new Vector3(shake, retractedHeight + 0.05f, 0f);
                    }

                    if (stateTimer >= warningDuration)
                    {
                        TransitionTo(TrapState.Extending);
                    }
                    break;

                case TrapState.Extending:
                    float extendT = Mathf.Clamp01(stateTimer / extendingDuration);
                    if (spikesTransform != null)
                    {
                        spikesTransform.localPosition = new Vector3(0f, Mathf.Lerp(retractedHeight, extendedHeight, extendT), 0f);
                    }

                    if (stateTimer >= extendingDuration)
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
                        TransitionTo(TrapState.Retracting);
                    }
                    break;

                case TrapState.Retracting:
                    float retractT = Mathf.Clamp01(stateTimer / retractingDuration);
                    if (spikesTransform != null)
                    {
                        spikesTransform.localPosition = new Vector3(0f, Mathf.Lerp(extendedHeight, retractedHeight, retractT), 0f);
                    }

                    if (stateTimer >= retractingDuration)
                    {
                        TransitionTo(TrapState.Retracted);
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
                float targetY = state switch
                {
                    TrapState.Active => extendedHeight,
                    TrapState.Warning => retractedHeight + 0.05f,
                    _ => retractedHeight
                };
                spikesTransform.localPosition = new Vector3(0f, targetY, 0f);
            }

            if (warningPlateRenderer != null)
            {
                if (propBlock == null)
                {
                    propBlock = new MaterialPropertyBlock();
                }

                Color targetCol = state == TrapState.Warning ? warningColor : normalColor;
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
        }

        protected override void CleanupAllTracked()
        {
            damagedEntitiesInCurrentCycle.Clear();
            trackedColliders.Clear();
        }
    }
}

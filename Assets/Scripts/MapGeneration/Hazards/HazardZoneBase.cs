using System.Collections.Generic;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.MapGeneration.Hazards
{
    /// <summary>
    /// Lớp cơ sở cho các vùng địa hình nguy hiểm (Lava, Nước, Bẫy).
    /// Đảm bảo vùng trigger ở layer Hazard (11) và có Rigidbody isKinematic để nhận trigger từ quái (NavMeshAgent không có Rigidbody).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class HazardZoneBase : MonoBehaviour
    {
        public const int HazardLayer = 11;

        [Header("Hazard Configuration")]
        [Tooltip("Tự động gán layer Hazard và cấu hình Rigidbody kinematic nếu chưa có.")]
        [SerializeField] private bool autoConfigurePhysics = true;

        protected readonly HashSet<Collider> trackedColliders = new HashSet<Collider>();

        protected virtual void Awake()
        {
            if (autoConfigurePhysics)
            {
                EnsureHazardPhysicsSetup();
            }
        }

        protected virtual void Reset()
        {
            EnsureHazardPhysicsSetup();
        }

        private void EnsureHazardPhysicsSetup()
        {
            gameObject.layer = HazardLayer;

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.useGravity = false;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other == null || other.isTrigger)
            {
                return;
            }

            trackedColliders.Add(other);
            OnEntityEntered(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            trackedColliders.Remove(other);
            OnEntityExited(other);
        }

        protected virtual void OnDisable()
        {
            CleanupAllTracked();
        }

        protected virtual void OnDestroy()
        {
            CleanupAllTracked();
        }

        protected abstract void OnEntityEntered(Collider other);
        protected abstract void OnEntityExited(Collider other);
        protected abstract void CleanupAllTracked();

        protected static IDamageable FindDamageable(Collider col)
        {
            if (col == null)
            {
                return null;
            }

            return col.GetComponentInParent<IDamageable>();
        }

        protected static ISpeedModifiable FindSpeedModifiable(Collider col)
        {
            if (col == null)
            {
                return null;
            }

            return col.GetComponentInParent<ISpeedModifiable>();
        }
    }
}

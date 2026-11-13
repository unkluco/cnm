using System;
using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Vending machine kiosk placed in Room_Start allowing the player to purchase weapons.
    /// Acts as an integration contract for Khải's combat system (sword/gun selection).
    /// </summary>
    public class WeaponVendingMachine : MonoBehaviour
    {
        [Header("Vending Machine Identity")]
        [SerializeField] private string kioskId = "StartRoom_ArmoryKiosk";
        [SerializeField] private string displayName = "Weapon Vending Machine";
        [SerializeField] private Transform interactionPoint;

        public string KioskId => kioskId;
        public string DisplayName => displayName;
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        public event Action<string> OnWeaponPurchased;

        public void Configure(string id, string name, Transform interact)
        {
            kioskId = id;
            displayName = name;
            interactionPoint = interact;
        }

        /// <summary>
        /// Called by Player interaction or UI to purchase/equip a weapon.
        /// </summary>
        public bool TryPurchaseWeapon(string weaponId, out string message)
        {
            message = $"Purchased {weaponId} from {displayName}!";
            OnWeaponPurchased?.Invoke(weaponId);
            return true;
        }
    }
}

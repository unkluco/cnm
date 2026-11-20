using System;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Start-room supply kiosk. MapGeneration owns the marker and interaction volume;
    /// GameBootstrap binds the same shop used in the sanctuary without a Lobby dependency.
    /// </summary>
    public class WeaponVendingMachine : MonoBehaviour, IInteractable
    {
        [Header("Vending Machine Identity")]
        [SerializeField] private string kioskId = "StartRoom_ArmoryKiosk";
        [SerializeField] private string displayName = "Weapon Vending Machine";
        [SerializeField] private Transform interactionPoint;
        private Action<GameObject> openShop;
        private Func<GameObject, bool> canOpenShop;
        private Transform worldLabel;

        public string KioskId => kioskId;
        public string DisplayName => displayName;
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
        public string Prompt => "Cửa hàng";
        public bool IsShopReady => openShop != null;

        public void Configure(string id, string name, Transform interact)
        {
            kioskId = id;
            displayName = name;
            interactionPoint = interact;
        }

        /// <summary>Wire the real shop after dungeon generation; separate trigger keeps the cabinet solid.</summary>
        public void BindShop(Action<GameObject> showShop, Func<GameObject, bool> canShop = null)
        {
            openShop = showShop ?? throw new ArgumentNullException(nameof(showShop));
            canOpenShop = canShop;
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) layer = 12;
            // Never convert the cabinet's body collider into a trigger. Older prefabs can
            // be missing the marker entirely; make a precise marker in front of the screen.
            if (interactionPoint == null || interactionPoint == transform)
            {
                interactionPoint = new GameObject("InteractionPoint").transform;
                interactionPoint.SetParent(transform, false);
                interactionPoint.localPosition = new Vector3(0f, 0f, 1.2f);
            }
            interactionPoint.gameObject.layer = layer;
            var trigger = interactionPoint.GetComponent<SphereCollider>();
            if (trigger == null) trigger = interactionPoint.gameObject.AddComponent<SphereCollider>();
            trigger.center = Vector3.up * 0.65f;
            trigger.radius = 0.75f;
            trigger.isTrigger = true;
            if (worldLabel == null) BuildLabel();
        }

        public bool CanInteract(GameObject interactor) => IsShopReady && interactor != null &&
            interactor.CompareTag("Player") && interactor.GetComponentInParent<IInventory>() != null &&
            !BackButtonStack.HasOpenPanel && (canOpenShop == null || canOpenShop(interactor));

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor)) openShop(interactor);
        }

        private void BuildLabel()
        {
            var go = new GameObject("SupplyShopLabel", typeof(RectTransform), typeof(Canvas));
            worldLabel = go.transform;
            worldLabel.SetParent(transform, false);
            worldLabel.localPosition = new Vector3(0f, 2.72f, 0.3f);
            worldLabel.localScale = Vector3.one * 0.009f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)worldLabel).sizeDelta = new Vector2(270f, 64f);

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(worldLabel, false);
            Stretch((RectTransform)frame.transform);
            var image = frame.GetComponent<Image>();
            DarkFantasyTheme.FramePanel(image, Color.white);
            image.raycastTarget = false;

            var textGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(worldLabel, false);
            Stretch((RectTransform)textGo.transform);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = DarkFantasyTheme.Gold;
            text.text = "CỬA HÀNG";
            text.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (worldLabel != null && Camera.main != null) worldLabel.rotation = Camera.main.transform.rotation;
        }
    }
}

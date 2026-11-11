using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Interactive test controller for MapGenTest scene.
    /// Provides camera pan/orbit/zoom via New Input System, one-click map regeneration,
    /// deterministic seed replay, room count tuning, and live layout/NavMesh telemetry.
    /// </summary>
    public class MapGenTestController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private Camera targetCamera;

        [Header("HUD References (Optional - Auto-Wired in MapGenTest)")]
        [SerializeField] private Text statsText;
        [SerializeField] private DungeonMinimapUI minimapUI;
        [SerializeField] private Button regenerateButton;
        [SerializeField] private Button replaySeedButton;
        [SerializeField] private Button toggleCameraButton;
        [SerializeField] private Button togglePathButton;
        [SerializeField] private Button decreaseRoomsButton;
        [SerializeField] private Button increaseRoomsButton;
        [SerializeField] private Button killBossButton;
        [SerializeField] private Button lockClearRoomsButton;

        [Header("Camera Controls")]
        [SerializeField] private float panSpeed = 35f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private float orbitSensitivity = 0.25f;
        [SerializeField] private float minDistance = 12f;
        [SerializeField] private float maxDistance = 160f;

        private Vector3 focusPoint = Vector3.zero;
        private float cameraDistance = 68f;
        private float yawAngle = 0f;
        private float pitchAngle = 82f;
        private bool isTopDownMode = true;
        private int lastSeed;
        private int lastBossChestCount = 0;
        private string lastLifecycleStatus = "UNVISITED (Wayfinding Landmarks)";

        public void Configure(
            DungeonGenerator generator,
            Camera cam,
            Text stats,
            Button regenBtn,
            Button replayBtn,
            Button camBtn,
            Button pathBtn,
            Button decBtn,
            Button incBtn,
            Button bossBtn = null,
            Button lockClearBtn = null,
            DungeonMinimapUI minimap = null)
        {
            dungeonGenerator = generator;
            targetCamera = cam;
            statsText = stats;
            regenerateButton = regenBtn;
            replaySeedButton = replayBtn;
            toggleCameraButton = camBtn;
            togglePathButton = pathBtn;
            decreaseRoomsButton = decBtn;
            increaseRoomsButton = incBtn;
            killBossButton = bossBtn;
            lockClearRoomsButton = lockClearBtn;
            minimapUI = minimap;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnDungeonGenerated += HandleDungeonGenerated;
            }

            BindButtons();
        }

        private void OnDisable()
        {
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnDungeonGenerated -= HandleDungeonGenerated;
            }

            UnbindButtons();
        }

        private void Start()
        {
            if (dungeonGenerator != null && dungeonGenerator.IsGenerated)
            {
                FrameDungeon();
                if (minimapUI != null)
                {
                    minimapUI.RefreshMinimap(dungeonGenerator);
                }
                RefreshTelemetry();
            }
            else
            {
                ApplyCameraTransform();
                RefreshTelemetry();
            }
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            HandleCameraNavigation();
            ApplyCameraTransform();
        }

        public void RegenerateRandomMap()
        {
            if (dungeonGenerator == null)
            {
                return;
            }

            int newSeed = UnityEngine.Random.Range(1000, 999999);
            dungeonGenerator.GenerateWithSeed(newSeed);
        }

        public void ReplayLastSeed()
        {
            if (dungeonGenerator == null)
            {
                return;
            }

            if (lastSeed == 0)
            {
                RegenerateRandomMap();
                return;
            }

            dungeonGenerator.GenerateWithSeed(lastSeed);
        }

        public void ToggleCameraMode()
        {
            isTopDownMode = !isTopDownMode;
            if (isTopDownMode)
            {
                pitchAngle = 85f;
                yawAngle = 0f;
            }
            else
            {
                pitchAngle = 45f;
                yawAngle = 35f;
            }

            FrameDungeon();
            RefreshTelemetry();
        }

        public void ToggleMainPathOverlay()
        {
            if (dungeonGenerator == null)
            {
                return;
            }

            dungeonGenerator.ShowMainPathLine = !dungeonGenerator.ShowMainPathLine;
            RefreshTelemetry();
        }

        public void AdjustTargetRoomCount(int delta)
        {
            if (dungeonGenerator == null || dungeonGenerator.Config == null)
            {
                return;
            }

            dungeonGenerator.Config.TargetRoomCount += delta;
            RegenerateRandomMap();
        }

        public void FrameDungeon()
        {
            if (dungeonGenerator == null || dungeonGenerator.CurrentLayout == null ||
                dungeonGenerator.CurrentLayout.rooms.Count == 0)
            {
                focusPoint = Vector3.zero;
                cameraDistance = 60f;
                ApplyCameraTransform();
                return;
            }

            Bounds total = dungeonGenerator.CurrentLayout.rooms[0].worldBounds;
            for (int i = 1; i < dungeonGenerator.CurrentLayout.rooms.Count; i++)
            {
                total.Encapsulate(dungeonGenerator.CurrentLayout.rooms[i].worldBounds);
            }

            focusPoint = new Vector3(total.center.x, 0f, total.center.z);
            float maxSpan = Mathf.Max(total.size.x, total.size.z);
            cameraDistance = Mathf.Clamp(maxSpan * 1.05f, minDistance, maxDistance);
            ApplyCameraTransform();
        }

        private void HandleDungeonGenerated(DungeonRuntimeContext context)
        {
            lastBossChestCount = 0;
            lastLifecycleStatus = "UNVISITED (Wayfinding Landmarks)";
            if (context?.Layout != null)
            {
                lastSeed = context.Layout.seed;
            }

            FrameDungeon();
            if (minimapUI != null)
            {
                minimapUI.RefreshMinimap(dungeonGenerator);
            }
            RefreshTelemetry();
        }

        public void TriggerBossDefeat()
        {
            if (dungeonGenerator != null)
            {
                lastBossChestCount = dungeonGenerator.TriggerBossRewards();
                if (minimapUI != null)
                {
                    minimapUI.RefreshMinimap(dungeonGenerator);
                }
                RefreshTelemetry();
            }
        }

        public void CycleCombatLock()
        {
            if (dungeonGenerator != null)
            {
                lastLifecycleStatus = dungeonGenerator.CycleCombatLockSimulation();
                if (lastLifecycleStatus.Contains("CLEARED"))
                {
                    lastBossChestCount = dungeonGenerator.TriggerBossRewards();
                }
                if (minimapUI != null)
                {
                    minimapUI.RefreshMinimap(dungeonGenerator);
                }
                RefreshTelemetry();
            }
        }

        private void HandleKeyboardShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                RegenerateRandomMap();
            }
            else if (kb.cKey.wasPressedThisFrame)
            {
                ReplayLastSeed();
            }
            else if (kb.tabKey.wasPressedThisFrame)
            {
                ToggleCameraMode();
            }
            else if (kb.fKey.wasPressedThisFrame)
            {
                FrameDungeon();
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                ToggleMainPathOverlay();
            }
            else if (kb.bKey.wasPressedThisFrame)
            {
                TriggerBossDefeat();
            }
            else if (kb.lKey.wasPressedThisFrame)
            {
                CycleCombatLock();
            }
        }

        private void HandleCameraNavigation()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            Vector2 moveInput = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveInput.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveInput.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput.x -= 1f;

                if (kb.qKey.isPressed) yawAngle -= 75f * Time.unscaledDeltaTime;
                if (kb.eKey.isPressed) yawAngle += 75f * Time.unscaledDeltaTime;
            }

            if (moveInput.sqrMagnitude > 0.001f)
            {
                Quaternion flatYaw = Quaternion.Euler(0f, yawAngle, 0f);
                Vector3 forward = flatYaw * Vector3.forward;
                Vector3 right = flatYaw * Vector3.right;
                float scaledSpeed = panSpeed * (cameraDistance / 60f);
                focusPoint += (forward * moveInput.y + right * moveInput.x).normalized * (scaledSpeed * Time.unscaledDeltaTime);
            }

            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    cameraDistance = Mathf.Clamp(cameraDistance - Mathf.Sign(scrollY) * zoomSpeed, minDistance, maxDistance);
                }

                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yawAngle += delta.x * orbitSensitivity;
                    pitchAngle = Mathf.Clamp(pitchAngle - delta.y * orbitSensitivity, 20f, 89f);
                    isTopDownMode = pitchAngle > 75f;
                }
            }
        }

        private void ApplyCameraTransform()
        {
            if (targetCamera == null)
            {
                return;
            }

            Quaternion orbitRot = Quaternion.Euler(pitchAngle, yawAngle, 0f);
            Vector3 camPos = focusPoint - (orbitRot * Vector3.forward) * cameraDistance;
            targetCamera.transform.SetPositionAndRotation(camPos, orbitRot);
        }

        private void RefreshTelemetry()
        {
            if (statsText == null)
            {
                return;
            }

            if (dungeonGenerator == null || !dungeonGenerator.IsGenerated || dungeonGenerator.CurrentLayout == null)
            {
                statsText.text = string.IsNullOrEmpty(dungeonGenerator?.LastError)
                    ? "Dungeon not generated yet. Press [R] to generate."
                    : $"ERROR: {dungeonGenerator.LastError}";
                return;
            }

            DungeonLayoutData layout = dungeonGenerator.CurrentLayout;
            bool graphValid = DungeonLayoutPlanner.IsGraphValid(layout, out _);
            int majorOnMain = DungeonLayoutPlanner.CountMajorRoomsOnMainPath(layout);
            int startDeg = DungeonLayoutPlanner.GetRoomDegree(layout, layout.startRoomId);
            int bossDeg = DungeonLayoutPlanner.GetRoomDegree(layout, layout.exitRoomId);

            var sb = new StringBuilder();
            sb.AppendLine($"<b>LIGHT SOULS — PROCEDURAL DUNGEON GENERATOR</b>");
            sb.AppendLine($"Seed: <b>{layout.seed}</b>  |  Total Modules: <b>{layout.rooms.Count}</b>  |  Critical Path: <b>{majorOnMain} Rooms</b> (Start/Boss Degree: <b>{startDeg}/{bossDeg}</b>)");
            sb.AppendLine($"Layout Validation: <b>{(graphValid ? "<color=#55FF77>PASS (Verified Graph)</color>" : "<color=#FF5555>FAIL</color>")}</b>  |  NavMesh Path: <b>{(dungeonGenerator.IsNavMeshPathReachable ? "<color=#55FF77>COMPLETE</color>" : "<color=#FFAA44>CHECK</color>")}</b>");
            sb.AppendLine($"Room Combat State ([L]): <b><color=#FFCC44>{lastLifecycleStatus}</color></b>  |  Boss Chamber: <b>28×28m</b>");
            string bossChestStatus = lastBossChestCount > 0
                ? $"<color=#FFD700>{lastBossChestCount} BONUS CHESTS SPAWNED!</color>"
                : "Waiting for Boss defeat ([B])";
            sb.AppendLine($"Armory Vending Machine: <b><color=#00E5FF>READY (Start SW Corner)</color></b>  |  Boss Room Rewards: <b>{bossChestStatus}</b>");
            sb.Append($"<color=#CCCCCC>[R] Regenerate  [C] Replay Seed  [L] Toggle Door Lock  [B] Kill Boss  [Tab] Camera ({(isTopDownMode ? "Top-Down" : "3D Orbit")})  [P] Path</color>");
            statsText.text = sb.ToString();
        }

        private void BindButtons()
        {
            if (regenerateButton != null) regenerateButton.onClick.AddListener(RegenerateRandomMap);
            if (replaySeedButton != null) replaySeedButton.onClick.AddListener(ReplayLastSeed);
            if (toggleCameraButton != null) toggleCameraButton.onClick.AddListener(ToggleCameraMode);
            if (togglePathButton != null) togglePathButton.onClick.AddListener(ToggleMainPathOverlay);
            if (decreaseRoomsButton != null) decreaseRoomsButton.onClick.AddListener(HandleDecreaseRooms);
            if (increaseRoomsButton != null) increaseRoomsButton.onClick.AddListener(HandleIncreaseRooms);
            if (killBossButton != null) killBossButton.onClick.AddListener(TriggerBossDefeat);
            if (lockClearRoomsButton != null) lockClearRoomsButton.onClick.AddListener(CycleCombatLock);
        }

        private void UnbindButtons()
        {
            if (regenerateButton != null) regenerateButton.onClick.RemoveListener(RegenerateRandomMap);
            if (replaySeedButton != null) replaySeedButton.onClick.RemoveListener(ReplayLastSeed);
            if (toggleCameraButton != null) toggleCameraButton.onClick.RemoveListener(ToggleCameraMode);
            if (togglePathButton != null) togglePathButton.onClick.RemoveListener(ToggleMainPathOverlay);
            if (decreaseRoomsButton != null) decreaseRoomsButton.onClick.RemoveListener(HandleDecreaseRooms);
            if (increaseRoomsButton != null) increaseRoomsButton.onClick.RemoveListener(HandleIncreaseRooms);
            if (killBossButton != null) killBossButton.onClick.RemoveListener(TriggerBossDefeat);
            if (lockClearRoomsButton != null) lockClearRoomsButton.onClick.RemoveListener(CycleCombatLock);
        }

        private void HandleDecreaseRooms() => AdjustTargetRoomCount(-1);
        private void HandleIncreaseRooms() => AdjustTargetRoomCount(1);
    }
}

using System.Collections.Generic;
using LightSouls.Combat;
using LightSouls.Combat.UI;
using LightSouls.Common;
using LightSouls.MapGeneration;
using LightSouls.UI;
using UnityEngine;

namespace LightSouls.Game
{
    /// <summary>
    /// Điểm lắp ráp duy nhất của Scene Game: nối bộ sinh màn, nhân vật/chiến đấu và giao diện với nhau.
    /// Các hệ thống không tham chiếu lẫn nhau; mọi kết nối giữa chúng nằm ở đây (Docs/Conventions.md mục 3 và 4).
    ///
    /// Luồng một màn chơi:
    ///   1. DungeonGenerator sinh màn trong Start() và phát OnDungeonGenerated (vì vậy phải đăng ký trong Awake).
    ///   2. Sinh người chơi tại ctx.PlayerSpawnPoint, gán cho camera, HUD và bản đồ nhỏ.
    ///   3. Người chơi bước vào phòng chiến đấu: phòng khóa cửa và phát OnRoomEnteredCombat,
    ///      EnemySpawner sinh quái tại room.EnemySpawnPoints.
    ///   4. Hết quái (AllEnemiesDefeated): gọi room.LifecycleController.ClearRoom() để mở cửa.
    ///   5. Người chơi tới ctx.LevelExitPoint khi phòng cuối đã sạch quái: hiện màn Chiến thắng.
    ///      Người chơi chết: hiện màn Thất bại. Cả hai màn đều có nút về menu chính.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Sinh màn")]
        [SerializeField] private DungeonGenerator dungeonGenerator;

        [Header("Chiến đấu")]
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private CombatHUD combatHUD;

        [Tooltip("Gốc của điều khiển cảm ứng và HUD; ẩn đi khi hết game.")]
        [SerializeField] private GameObject touchControlsRoot;

        [Header("Camera")]
        [SerializeField] private GameCameraFollow cameraFollow;

        [Header("Giao diện")]
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private GameResultScreen resultScreen;
        [SerializeField] private GameMinimap minimap;

        [Header("Đích")]
        [Tooltip("Người chơi đứng trong bán kính này (mét, mặt phẳng XZ) quanh điểm đích thì thắng.")]
        [SerializeField] private float exitReachRadius = 2.5f;

        private PlayerController player;
        private Transform levelExitPoint;
        private RoomLifecycleController exitRoom;
        private RoomModule roomInCombat;
        private readonly List<RoomLifecycleController> subscribedRooms = new List<RoomLifecycleController>();
        private bool isFinished;

        private void Awake()
        {
            if (dungeonGenerator == null || playerPrefab == null || enemySpawner == null)
            {
                Debug.LogError("[GameBootstrap] Thiếu tham chiếu tới DungeonGenerator, Player prefab hoặc EnemySpawner.", this);
                enabled = false;
                return;
            }

            dungeonGenerator.OnDungeonGenerated += HandleDungeonGenerated;
            enemySpawner.AllEnemiesDefeated += HandleWaveDefeated;

            if (pauseMenu != null)
            {
                pauseMenu.PausedChanged += HandlePausedChanged;
            }
        }

        private void OnDestroy()
        {
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnDungeonGenerated -= HandleDungeonGenerated;
            }

            if (enemySpawner != null)
            {
                enemySpawner.AllEnemiesDefeated -= HandleWaveDefeated;
            }

            if (pauseMenu != null)
            {
                pauseMenu.PausedChanged -= HandlePausedChanged;
            }

            if (player != null)
            {
                player.OnDied -= HandlePlayerDied;
            }

            foreach (var room in subscribedRooms)
            {
                if (room != null)
                {
                    room.OnRoomEnteredCombat -= HandleRoomEnteredCombat;
                }
            }
        }

        private void Update()
        {
            if (isFinished || player == null || levelExitPoint == null || roomInCombat != null)
            {
                return;
            }

            // Phòng cuối cũng khóa cửa khi vào: phải dọn sạch quái rồi mới được tính là tới đích.
            if (exitRoom != null && exitRoom.RequiresCombatLock && exitRoom.State != RoomLifecycleState.Cleared)
            {
                return;
            }

            var offset = player.transform.position - levelExitPoint.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= exitReachRadius * exitReachRadius)
            {
                FinishGame(victory: true);
            }
        }

        private void HandleDungeonGenerated(DungeonRuntimeContext context)
        {
            if (context.PlayerSpawnPoint == null)
            {
                Debug.LogError("[GameBootstrap] Bản đồ không có điểm xuất phát của người chơi.", this);
                return;
            }

            levelExitPoint = context.LevelExitPoint;
            SpawnPlayer(context.PlayerSpawnPoint);

            foreach (var room in context.SpawnedRooms)
            {
                var lifecycle = room != null ? room.LifecycleController : null;
                if (lifecycle == null)
                {
                    continue;
                }

                lifecycle.OnRoomEnteredCombat += HandleRoomEnteredCombat;
                subscribedRooms.Add(lifecycle);

                if (room.RoomType == RoomType.Exit)
                {
                    exitRoom = lifecycle;
                }
            }

            if (minimap != null)
            {
                minimap.Show(context, player.transform);
            }
        }

        private void SpawnPlayer(Transform spawnPoint)
        {
            var spawnRotation = Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f);
            player = Instantiate(playerPrefab, spawnPoint.position, spawnRotation);
            player.OnDied += HandlePlayerDied;

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(player.transform, snap: true);
            }

            if (combatHUD != null)
            {
                combatHUD.SetPlayer(player);
            }
        }

        private void HandleRoomEnteredCombat(RoomModule room)
        {
            if (isFinished)
            {
                return;
            }

            // Gán trước khi sinh quái: phòng không có điểm sinh quái sẽ phát AllEnemiesDefeated ngay trong SpawnWave.
            roomInCombat = room;
            enemySpawner.SpawnWave(room.EnemySpawnPoints);
        }

        private void HandleWaveDefeated()
        {
            if (roomInCombat == null)
            {
                return;
            }

            var room = roomInCombat;
            roomInCombat = null;
            if (room.LifecycleController != null)
            {
                room.LifecycleController.ClearRoom();
            }
        }

        private void HandlePlayerDied(IDamageable _)
        {
            FinishGame(victory: false);
        }

        private void HandlePausedChanged(bool paused)
        {
            if (player != null && !isFinished && !player.IsDead)
            {
                player.SetInputActive(!paused);
            }
        }

        private void FinishGame(bool victory)
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;

            if (player != null)
            {
                player.SetInputActive(false);
            }

            if (touchControlsRoot != null)
            {
                touchControlsRoot.SetActive(false);
            }

            if (minimap != null)
            {
                minimap.Hide();
            }

            if (resultScreen == null)
            {
                return;
            }

            if (victory)
            {
                resultScreen.ShowVictory();
            }
            else
            {
                resultScreen.ShowDefeat();
            }
        }
    }
}

using System.Collections.Generic;
using LightSouls.Combat;
using LightSouls.Combat.Items;
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

        [Header("Túi đồ")]
        [Tooltip("Thanh nhanh 5 ô. Bỏ trống thì tự tìm trong Scene.")]
        [SerializeField] private HotbarUI hotbar;

        [Tooltip("Nút Tương tác cảm ứng. Bỏ trống thì tự tìm trong Scene.")]
        [SerializeField] private InteractButtonUI interactButton;

        [Tooltip("Bảng balo. Bỏ trống thì tự tìm trong Scene (kể cả khi đang ẩn).")]
        [SerializeField] private BackpackUI backpack;

        [Header("Phần thưởng")]
        [Tooltip("Số xu cộng vào RunState mỗi khi dọn sạch một phòng chiến đấu.")]
        [SerializeField, Min(0)] private int coinsPerClearedRoom = 20;

        [Header("Hòm kho báu")]
        [Tooltip("Hòm đặt tại mỗi điểm loot của phòng (trừ phòng cuối, nơi có phần thưởng boss riêng). Bỏ trống thì không đặt hòm.")]
        [SerializeField] private TreasureChest treasureChestPrefab;

        [Tooltip("Bảng mở hòm. Bỏ trống thì tự tìm trong Scene, không có thì tự tạo.")]
        [SerializeField] private TreasureChestUI treasureChestUI;

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

            SpawnTreasureChests(context);

            if (minimap != null)
            {
                minimap.Show(context, player.transform);
            }
        }

        /// <summary>
        /// Thay mỗi điểm loot (khối đánh dấu) bằng một hòm kho báu đặt trên sàn phòng.
        /// Phòng cuối (Exit) giữ điểm loot cho phần thưởng boss nên bỏ qua.
        /// </summary>
        private void SpawnTreasureChests(DungeonRuntimeContext context)
        {
            if (treasureChestPrefab == null)
            {
                return;
            }

            if (treasureChestUI == null)
            {
                treasureChestUI = FindAnyObjectByType<TreasureChestUI>(FindObjectsInactive.Include);
            }

            if (treasureChestUI == null)
            {
                treasureChestUI = new GameObject("Combat_TreasureChestUI").AddComponent<TreasureChestUI>();
            }

            foreach (var room in context.SpawnedRooms)
            {
                if (room == null || room.RoomType == RoomType.Exit || room.LootSpawnPoints == null)
                {
                    continue;
                }

                foreach (var point in room.LootSpawnPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    // Điểm loot là khối đánh dấu đặt trên sàn: hòm đặt ở đáy khối rồi ẩn khối đi.
                    // (Không dò sàn bằng tia: bộ sinh phòng thủ tục có thể còn khối cũ chờ hủy cuối khung hình.)
                    Vector3 position = point.position;
                    var marker = point.GetComponent<Renderer>();
                    if (marker != null)
                    {
                        position.y = marker.bounds.min.y;
                    }

                    point.gameObject.SetActive(false);

                    var rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
                    Instantiate(treasureChestPrefab, position, rotation, point.parent);
                }
            }
        }

        private void SpawnPlayer(Transform spawnPoint)
        {
            var spawnRotation = Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f);
            player = Instantiate(playerPrefab, spawnPoint.position, spawnRotation);
            player.OnDied += HandlePlayerDied;
            // Cuộn Dịch Chuyển đưa người chơi về đây.
            player.SetSafePoint(spawnPoint.position);

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(player.transform, snap: true);
            }

            if (combatHUD != null)
            {
                combatHUD.SetPlayer(player);
            }

            // Các giao diện túi đồ tìm người chơi trong Start(), lúc đó người chơi chưa được sinh ra
            // (phải đợi bộ sinh màn xong), nên phải gán lại ở đây.
            if (hotbar == null)
            {
                hotbar = FindAnyObjectByType<HotbarUI>(FindObjectsInactive.Include);
            }

            if (interactButton == null)
            {
                interactButton = FindAnyObjectByType<InteractButtonUI>(FindObjectsInactive.Include);
            }

            if (backpack == null)
            {
                backpack = FindAnyObjectByType<BackpackUI>(FindObjectsInactive.Include);
            }

            if (hotbar != null)
            {
                hotbar.SetPlayer(player);
            }

            if (interactButton != null)
            {
                interactButton.SetPlayer(player);
            }

            if (backpack != null)
            {
                backpack.SetPlayer(player);
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
            if (player != null)
            {
                player.InLockedCombat = true;
            }

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
            if (player != null)
            {
                player.InLockedCombat = false;
            }
            if (room.LifecycleController != null)
            {
                room.LifecycleController.ClearRoom();
            }

            RunState.AddCoins(coinsPerClearedRoom);
        }

        private void HandlePlayerDied(IDamageable _)
        {
            FinishGame(victory: false);
        }

        private void HandlePausedChanged(bool paused)
        {
            // Balo đang mở thì giữ khóa di chuyển sau khi bỏ tạm dừng; balo tự mở lại khi đóng.
            if (player != null && !isFinished && !player.IsDead && !(backpack != null && backpack.IsOpen))
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

            if (backpack != null && backpack.IsOpen)
            {
                backpack.Close();
            }

            if (treasureChestUI != null && treasureChestUI.IsOpen)
            {
                treasureChestUI.Close();
            }

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

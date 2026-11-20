using System.Collections.Generic;
using LightSouls.Combat;
using LightSouls.Combat.Items;
using LightSouls.Combat.UI;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Bosses;
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
    ///   5. Dọn phòng cuối: nhận hòm thưởng và mở cổng bí ẩn. Bước vào cổng mới biết sẽ
    ///      về sảnh mua đồ hay vào ngục khác. Người chơi chết: hiện màn Thất bại.
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

        [Header("Cổng bí ẩn sau boss")]
        [Tooltip("Bán kính (mét, XZ) bên trong cổng mà người chơi cần bước vào.")]
        [SerializeField, Min(0.5f)] private float exitReachRadius = 1.5f;
        [Tooltip("Xác suất về sảnh mua đồ. Phần còn lại sẽ đưa người chơi vào một loại ngục khác.")]
        [SerializeField, Range(0f, 1f)] private float returnToShopChance = 0.5f;

        private PlayerController player;
        private Transform levelExitPoint;
        private RoomLifecycleController exitRoom;
        private RoomModule roomInCombat;
        private BossKind? currentBossKind;
        private CraftingCatalogSO craftingCatalog;
        private readonly List<RoomLifecycleController> subscribedRooms = new List<RoomLifecycleController>();
        private bool isFinished;
        private bool mustLeaveExitArea;
        private bool exitPortalOpen;
        private GameObject exitPortalVisual;

        public bool IsExitPortalOpen => exitPortalOpen;
        public float ReturnToShopChance
        {
            get => returnToShopChance;
            set => returnToShopChance = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (dungeonGenerator == null || playerPrefab == null || enemySpawner == null)
            {
                Debug.LogError("[GameBootstrap] Thiếu tham chiếu tới DungeonGenerator, Player prefab hoặc EnemySpawner.", this);
                enabled = false;
                return;
            }

            dungeonGenerator.OnDungeonGenerated += HandleDungeonGenerated;
            dungeonGenerator.OnGenerationFailed += HandleGenerationFailed;
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
                dungeonGenerator.OnGenerationFailed -= HandleGenerationFailed;
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
            if (isFinished || player == null || player.IsDead || levelExitPoint == null || roomInCombat != null ||
                !exitPortalOpen || Time.timeScale <= 0f || BackButtonStack.HasOpenPanel)
            {
                return;
            }

            // Chỉ đi qua cổng khi phòng cuối đã sạch quái.
            if (exitRoom != null && exitRoom.RequiresCombatLock && exitRoom.State != RoomLifecycleState.Cleared)
            {
                return;
            }

            var offset = player.transform.position - levelExitPoint.position;
            offset.y = 0f;
            bool insideExitArea = offset.sqrMagnitude <= exitReachRadius * exitReachRadius;
            // Không tự kết thúc nếu người chơi đứng ngay trên đích lúc vừa dọn phòng cuối.
            // Họ phải rời vùng đích rồi quay lại, để có cơ hội nhặt phần thưởng.
            if (mustLeaveExitArea)
            {
                if (!insideExitArea) mustLeaveExitArea = false;
                return;
            }
            if (insideExitArea)
            {
                EnterMysteryPortal();
            }
        }

        private void HandleDungeonGenerated(DungeonRuntimeContext context)
        {
            if (context.PlayerSpawnPoint == null || context.LevelExitPoint == null)
            {
                HandleGenerationFailed("Bản đồ thiếu điểm xuất phát hoặc đích.");
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

            if (exitRoom == null)
            {
                HandleGenerationFailed("Bản đồ thiếu phòng cuối hợp lệ.");
                return;
            }

            PrepareExitPortal();

            SpawnTreasureChests(context);

            if (minimap != null)
            {
                minimap.Show(context, player.transform);
            }

            // Tên ngục được công bố sau khi đã bước vào cổng và tới ngục mới.
            CombatToast.Show(RunState.ConsumePortalArrivalMessage());
        }

        private void PrepareExitPortal()
        {
            exitPortalOpen = false;
            exitPortalVisual = levelExitPoint.gameObject;
            if (levelExitPoint == exitRoom.transform)
            {
                // Trường hợp prefab thiếu marker riêng: tạo một hình cổng tạm, không ẩn cả phòng.
                exitPortalVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                exitPortalVisual.name = "MysteryExitPlaceholder";
                exitPortalVisual.transform.SetParent(levelExitPoint, false);
                exitPortalVisual.transform.localPosition = Vector3.up * 0.08f;
                exitPortalVisual.transform.localScale = new Vector3(3f, 0.08f, 3f);
                var collider = exitPortalVisual.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; Destroy(collider); }
                exitPortalVisual.GetComponent<Renderer>().material.color = new Color(0.65f, 0.32f, 0.9f);
            }
            exitPortalVisual.SetActive(false);
        }

        private void OpenMysteryPortal(bool materialRewards=false)
        {
            if (exitPortalOpen || exitPortalVisual == null || player == null || player.IsDead || isFinished) return;
            exitPortalOpen = true;
            exitPortalVisual.SetActive(true);
            // Identical appearance for either outcome: no route is rolled or displayed yet.
            VfxFactory.Aura(exitPortalVisual.transform, new Color(0.65f, 0.35f, 1f, 0.8f), 22f, 0.85f,
                Vector3.up * 0.65f, 0.17f, 1.1f, "VFX_MysteryExit");
            var offset = player.transform.position - levelExitPoint.position;
            offset.y = 0f;
            mustLeaveExitArea = offset.sqrMagnitude <= exitReachRadius * exitReachRadius;
            CombatToast.Show(materialRewards?"Cổng bí ẩn đã mở. Nhặt nguyên liệu và mở hòm trước nhé.":"Cổng bí ẩn đã mở. Bước vào để biết điểm đến.");
        }

        private void EnterMysteryPortal()
        {
            if (isFinished) return;
            isFinished = true; // Prevent another route roll while SceneManager is loading.
            player.SetInputActive(false);

            DungeonEntry destination = BossPortalRouter.ChooseDestination(dungeonGenerator.Catalog,
                RunState.SelectedDungeonId, dungeonGenerator.ActiveConfig, returnToShopChance,
                Random.value, Random.value);

            if (destination == null)
            {
                RunState.SetPortalArrivalMessage("Cổng bí ẩn đưa bạn về sảnh mua đồ.");
                SceneLoader.LoadLobby();
                return;
            }

            RunState.SelectedDungeonId = destination.dungeonId;
            RunState.SetPortalArrivalMessage("Cổng bí ẩn đưa bạn đến: " + destination.displayName);
            SceneLoader.LoadGame();
        }

        private void HandleGenerationFailed(string error)
        {
            if (isFinished) return;
            isFinished = true;
            Debug.LogError($"[GameBootstrap] Không thể tạo ngục: {error}", this);
            RunState.DungeonGenerationFailed = true;
            SceneLoader.LoadLobby();
        }

        /// <summary>
        /// Thay mỗi điểm loot (khối đánh dấu) bằng một hòm kho báu đặt trên sàn phòng.
        /// Phòng cuối (Exit) giữ điểm loot để sinh hòm sau khi dọn sạch quái.
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
                    SpawnChestAt(point);
                }
            }
        }

        private void SpawnChestAt(Transform point)
        {
            if (point == null || treasureChestPrefab == null) return;

            // Điểm loot là khối đánh dấu: đặt đáy hòm trên sàn rồi ẩn khối đi.
            Vector3 position = point.position;
            var marker = point.GetComponent<Renderer>();
            if (marker != null) position.y = marker.bounds.min.y;

            point.gameObject.SetActive(false);
            var rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
            Instantiate(treasureChestPrefab, position, rotation, point.parent);
        }

        private void SpawnBossRewardChests(RoomModule room)
        {
            // ClearRoom vừa kích hoạt ngẫu nhiên 1–2 marker của BossRoomRewardController.
            // Thay đúng các marker đang bật bằng hòm thật có thể tương tác.
            if (room.RoomType != RoomType.Exit || room.BossRewardController == null) return;
            foreach (var reward in room.BossRewardController.RewardChests)
            {
                if (reward != null && reward.activeSelf) SpawnChestAt(reward.transform);
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
            currentBossKind=null;
            if (player != null)
            {
                player.InLockedCombat = true;
            }

            if(room.RoomType==RoomType.Exit)
            {
                if(!enemySpawner.SpawnBoss(room.GetCurrentWorldBounds(),room.transform.position.y,player))
                {
                    Debug.LogError("[GameBootstrap] Boss roster is missing. Falling back to a normal encounter to keep the dungeon playable.");
                    enemySpawner.SpawnWave(room.EnemySpawnPoints);
                }
                else
                {
                    currentBossKind=enemySpawner.ActiveBoss.Kind;
                    if(cameraFollow!=null)cameraFollow.SetBossFocus(enemySpawner.ActiveBoss);
                }
            }
            else enemySpawner.SpawnWave(room.EnemySpawnPoints);
        }

        private void HandleWaveDefeated()
        {
            if (roomInCombat == null)
            {
                return;
            }

            var room = roomInCombat;
            roomInCombat = null;
            var defeatedBoss=currentBossKind;currentBossKind=null;
            if(room.RoomType==RoomType.Exit && cameraFollow!=null)cameraFollow.SetBossFocus(null);
            if (player != null)
            {
                player.InLockedCombat = false;
            }
            if (room.LifecycleController != null)
            {
                room.LifecycleController.ClearRoom();
            }

            SpawnBossRewardChests(room);

            if(craftingCatalog==null)craftingCatalog=CraftingCatalogSO.Load();
            Vector3? portal=room.RoomType==RoomType.Exit&&levelExitPoint!=null?levelExitPoint.position:null;
            int materialDrops=CraftingLootDrops.SpawnInRoom(craftingCatalog,room.GetCurrentWorldBounds(),room.transform.position.y,
                player!=null?player.transform.position:room.transform.position,defeatedBoss,RunState.SelectedDungeonId,portal,exitReachRadius+1.2f);
            if (room.RoomType == RoomType.Exit) OpenMysteryPortal(materialDrops>0);
            else if(materialDrops>0)CombatToast.Show("Nguyên liệu chế tạo đã rơi ra!");

            RunState.AddCoins(coinsPerClearedRoom);
        }

        private void HandlePlayerDied(IDamageable _)
        {
            FinishGameDefeat();
        }

        private void HandlePausedChanged(bool paused)
        {
            // Balo đang mở thì giữ khóa di chuyển sau khi bỏ tạm dừng; balo tự mở lại khi đóng.
            if (player != null && !isFinished && !player.IsDead && !(backpack != null && backpack.IsOpen))
            {
                player.SetInputActive(!paused);
            }
        }

        private void FinishGameDefeat()
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

            resultScreen.ShowDefeat();
        }
    }
}

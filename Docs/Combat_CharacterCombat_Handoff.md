# Tài liệu Bàn giao Kỹ thuật: Hệ thống Nhân vật & Chiến đấu (Character & Combat)

- **Người thực hiện:** Khải (`Khai810`)
- **Nhánh gốc:** `feature/character-combat` (đã merge vào `develop`)
- **Cập nhật:** Tính tới thời điểm hiện tại (14/11/2026)
- **Scene kiểm thử:** `Assets/Scenes/CharacterCombatTest.unity`
- **Mã nguồn chính:** `Assets/Scripts/Combat/` (Assembly `LightSouls.Combat`)

---

## 1. Tổng quan Hệ thống Chiến đấu

Hệ thống cung cấp toàn bộ nền tảng cho người chơi và quái vật chiến đấu trong phòng ngục:
- **Người chơi (`Player_Character.prefab`)**: Điều khiển di chuyển, xoay theo hướng đi, tấn công cận chiến (kiếm) và tầm xa (súng), lướt né đòn (`Dash` với hiệu ứng vệt mờ Ghost Trail), nhận sát thương, chết và hồi sinh.
- **Quái vật (`EnemyBase`)**: 3 loại quái di chuyển tìm đường qua `NavMeshAgent` (agent type `Humanoid`):
  1. `Enemy_Melee_Zombie.prefab`: Quái cận chiến máu dày, tốc độ chậm.
  2. `Enemy_Melee_Goblin.prefab`: Quái cận chiến di chuyển nhanh, áp sát chém liên tục.
  3. `Enemy_Ranged_Cultist.prefab`: Quái tầm xa giữ khoảng cách, bắn đạn ma thuật.
- **Bộ sinh quái phòng (`EnemySpawner`)**: Quản lý đợt quái trong phòng chiến đấu, đồng bộ mở/khóa cửa với `GameBootstrap` và `RoomLifecycleController`.
- **Giao diện chiến đấu (`CombatHUD.prefab`)**: Thanh máu người chơi, tên nhân vật, thanh máu trên đầu quái vật, chữ số sát thương bay (`Combat_DamagePopup`).

---

## 2. Các Hàm, Thuộc Tính & Sự Kiện Quan Trọng Cho Người Khác Sử Dụng

### 2.1. `PlayerController` (`LightSouls.Combat.PlayerController`)
Đối tượng gốc đại diện cho nhân vật người chơi trong Scene. `GameBootstrap` và `PauseMenuController` gọi trực tiếp các API sau:

| Tên API | Kiểu / Cú pháp | Vai trò | Người sử dụng |
| :--- | :--- | :--- | :--- |
| `CurrentHealth` | `float` (get) | Lượng máu hiện tại của người chơi | `GameBootstrap`, `CombatHUD`, UI |
| `MaxHealth` | `float` (get) | Lượng máu tối đa của người chơi | `GameBootstrap`, `CombatHUD`, UI |
| `IsDead` | `bool` (get) | Trạng thái người chơi đã chết hay chưa | `GameBootstrap`, UI Thắng/Thua |
| `CurrentWeapon` | `WeaponDataSO` (get) | Vũ khí người chơi đang cầm trên tay | Hệ thống hiển thị / âm thanh |
| `SetInputActive(bool active)` | `void SetInputActive(bool)` | Bật/tắt toàn bộ nhận diện input (di chuyển, đánh, lướt). Gọi `false` khi Pause game, mở Balo, mở Shop... | Kiệt (`PauseMenuController`), Khải (`BackpackUI`), Đạt (`LobbyShop`) |
| `OnHealthChanged` | `event Action<float, float>` | Phát khi máu thay đổi (`currentHealth`, `maxHealth`). | `CombatHUD`, UI hiển thị máu |
| `OnDied` | `event Action<IDamageable>` | Phát **đúng một lần** khi người chơi hết máu và chết. | `GameBootstrap` để kích hoạt `GameResultScreen.ShowDefeat()` |
| `OnWeaponChanged` | `event Action<WeaponDataSO>` | Phát khi người chơi đổi vũ khí trên tay. | `CombatHUD`, Hiệu ứng vũ khí |
| `DamageReduction` | `float` (get) | Tỉ lệ giảm sát thương nhận vào từ trang bị (0.0 – 1.0). | Tính toán sát thương |

---

### 2.2. Gây Sát Thương & Nhận Sát Thương (`IDamageable` & `DamageInfo`)
Interface chuẩn nhóm đặt tại namespace `LightSouls.Common`. Tất cả thực thể có máu (`Player_Character`, quái vật, bù nhìn thử nghiệm) đều cài đặt interface này:

```csharp
namespace LightSouls.Common
{
    public struct DamageInfo
    {
        public float Amount;        // Lượng sát thương gây ra
        public GameObject Source;   // Đối tượng gây sát thương (Player, Enemy, bẫy Lava...)
        public Vector3 HitPoint;    // Toạ độ va chạm (dùng để sinh particle máu/chữ số nhảy)
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(DamageInfo info);
        event System.Action<IDamageable> Died;
    }
}
```

#### Cách gọi gây sát thương (Dành cho Đạt làm bẫy sàn, gai, lava):
```csharp
// Khi thực thể bước vào vùng trigger nguy hiểm của Đạt:
IDamageable victim = other.GetComponentInParent<IDamageable>();
if (victim != null && !victim.IsDead)
{
    victim.TakeDamage(new DamageInfo
    {
        Amount = 15f,
        Source = gameObject,
        HitPoint = other.transform.position
    });
}
```

---

### 2.3. Sinh Quái & Vòng Đời Phòng Chiến Đấu (`EnemySpawner`)
Component đặt trong Scene chiến đấu hoặc phòng ngục. Quản lý toàn bộ vòng đời chiến đấu của một phòng:

| Tên API | Kiểu / Cú pháp | Vai trò | Người sử dụng |
| :--- | :--- | :--- | :--- |
| `SpawnWave(IReadOnlyList<Transform> spawnPoints)` | `void SpawnWave(...)` | Sinh một đợt quái vật mới tại danh sách các điểm spawn được cung cấp bởi `RoomModule.EnemySpawnPoints`. | `GameBootstrap` khi người chơi vào phòng |
| `AllEnemiesDefeated` | `event Action` | Sự kiện phát **đúng một lần** khi toàn bộ quái vật trong đợt đã bị tiêu diệt sạch. | `GameBootstrap` để gọi `room.LifecycleController.ClearRoom()` mở cửa |
| `AliveEnemyCount` | `int` (get) | Số lượng quái vật hiện còn sống trong phòng. | `CombatHUD`, Kiểm tra tiến độ |
| `HasActiveWave` | `bool` (get) | `true` nếu trong phòng đang có quái vật hoạt động. | Kiểm tra trạng thái |
| `SetEnemyPrefabs(GameObject[] prefabs)` | `void SetEnemyPrefabs(...)` | Thiết lập danh sách prefab quái vật cho spawner từ code (nếu muốn thay đổi theo màn). | `GameBootstrap` |

#### Quy trình phối hợp chuẩn trong Scene `Game` (giữa Đạt, Khải và Kiệt):
1. Người chơi bước vào phòng chiến đấu $\rightarrow$ `RoomLifecycleController.OnRoomEnteredCombat(room)` phát sự kiện, cửa phòng tự động đóng và khóa.
2. `GameBootstrap` nhận sự kiện $\rightarrow$ gọi `EnemySpawner.SpawnWave(room.EnemySpawnPoints)`.
3. Người chơi đánh bại toàn bộ quái trong phòng $\rightarrow$ `EnemySpawner` phát sự kiện `AllEnemiesDefeated`.
4. `GameBootstrap` bắt sự kiện `AllEnemiesDefeated` $\rightarrow$ gọi `room.LifecycleController.ClearRoom()` $\rightarrow$ cửa phòng tự động mở ra.

---

### 2.4. `CombatHUD` (`LightSouls.Combat.UI.CombatHUD`)
Quản lý hiển thị thanh máu người chơi và tên người chơi:

| Tên API | Kiểu / Cú pháp | Vai trò | Người sử dụng |
| :--- | :--- | :--- | :--- |
| `SetPlayer(PlayerController player)` | `void SetPlayer(...)` | Gán tham chiếu người chơi để HUD tự động đăng ký sự kiện máu và vũ khí. | `GameBootstrap` khi sinh Player |
| `SetPlayerName(string name)` | `void SetPlayerName(...)` | Đặt tên hiển thị của người chơi trên thanh máu HUD. | `GameBootstrap` |

---

## 3. Quy Ước Tag, Layer & Kích Thước Vật Lý

| Đối tượng | Tag | Layer | Ghi chú |
| :--- | :--- | :--- | :--- |
| **Người chơi (Root)** | `Player` | `Player` (6) | `RoomLifecycleController` dựa vào Tag `Player` để phát hiện người chơi bước vào phòng. |
| **Quái vật (Root)** | `Enemy` | `Enemy` (7) | Dùng cho raycast quét mục tiêu và va chạm vật lý. |
| **Đòn đánh người chơi** | — | `PlayerAttack` (9) | Chỉ va chạm với `Enemy` và `Environment`. |
| **Đòn đánh của quái** | — | `EnemyAttack` (10) | Chỉ va chạm với `Player` và `Environment`. |

- **Kích thước chuẩn (Metric Standard):**
  - Chiều cao $\le 2.0\text{m}$, bán kính $\le 0.5\text{m}$.
  - Agent type: **`Humanoid`** (Khớp 100% với NavMesh bake của Đạt).
  - Vị trí mặt sàn đi lại: $Y = 0.0$.

---

## 4. Danh Sách Prefab Bàn Giao

| Tên Prefab | Đường dẫn | Vai trò |
| :--- | :--- | :--- |
| `Player_Character` | `Assets/Prefabs/Combat/Player_Character.prefab` | Nhân vật người chơi chuẩn tích hợp PlayerController, Movement, Combat, Health, NavMesh Obstacle |
| `Enemy_Melee_Zombie` | `Assets/Prefabs/Combat/Enemy_Melee_Zombie.prefab` | Quái cận chiến Zombie (Humanoid NavMeshAgent) |
| `Enemy_Melee_Goblin` | `Assets/Prefabs/Combat/Enemy_Melee_Goblin.prefab` | Quái cận chiến Goblin tốc độ cao |
| `Enemy_Ranged_Cultist` | `Assets/Prefabs/Combat/Enemy_Ranged_Cultist.prefab` | Quái bắn xa Cultist bắn đạn ma thuật |
| `Combat_TouchControls` | `Assets/Prefabs/Combat/Combat_TouchControls.prefab` | Cụm điều khiển cảm ứng (Canvas sort order 5, SafeAreaFitter) |
| `Combat_DamagePopup` | `Assets/Prefabs/Combat/Combat_DamagePopup.prefab` | Hiệu ứng số sát thương bay lên khi trúng đòn |

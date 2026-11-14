# Quy ước chung — Light Souls

> **Trạng thái:** đã được nhóm trưởng (Kiệt) duyệt. Mọi PR tính năng phải tuân theo tài liệu này. Muốn đổi quy ước thì mở PR sửa chính file này, không tự làm khác trong nhánh tính năng.

| Thành viên | GitHub | Phụ trách | Nhánh |
| :--- | :--- | :--- | :--- |
| Kiệt (nhóm trưởng) | `unkluco` | Giao diện, menu; `Common`, `ProjectSettings`; lắp ráp Scene `MainMenu`, `Game`, `Lobby`; review và merge | tạo theo từng việc |
| Khải | `Khai810` | Nhân vật, quái, vũ khí, chiến đấu; túi đồ và vật phẩm | `feature/inventory-hotbar` |
| Đạt | `Coloky7098` | Format room, sinh màn; địa hình đặc biệt; sảnh | `feature/hazard-rooms-lobby` |

Nhánh của mỗi người là nhánh của đợt việc hiện tại (xem `Tasks/` mới nhất). Nhánh của đợt cũ đã merge sẽ bị xóa; không commit tiếp lên nhánh cũ.

Mục tiêu: các phần do từng người làm riêng (sinh màn, nhân vật/chiến đấu, giao diện) ghép được vào Scene `Game` mà không phải sửa lại. Tài liệu chỉ ghi những điều phải thống nhất; chi tiết bên trong mỗi hệ thống do người phụ trách quyết định.

## 1. Môi trường

- **Unity `6000.5.7f1`** (đúng như `ProjectSettings/ProjectVersion.txt`). Không mở dự án bằng bản khác, kể cả khác bản vá: Unity sẽ ghi lại hàng loạt file trong `ProjectSettings` và gây xung đột.
- Không tự thêm hoặc nâng cấp package. Cần package mới thì ghi trong PR và để nhóm trưởng duyệt, vì `Packages/manifest.json` ảnh hưởng cả nhóm.
- Scene, prefab và asset phải lưu dạng văn bản (**Force Text** trong `Edit > Project Settings > Editor > Asset Serialization`). Trước khi mở PR, kiểm tra file `.unity`/`.prefab` không bị Git báo là nhị phân. Nếu bị, mở file trong Unity và chạy `Assets > Force Reserialize Assets` cho file đó. Nếu vẫn bị, thường là do Scene đang nhúng dữ liệu nhị phân, hay gặp nhất là NavMesh bake bằng code (`NavMeshSurface.BuildNavMesh()`): lưu `navMeshData` thành asset riêng bằng `AssetDatabase.CreateAsset`, giống cách nút Bake trong Inspector làm.
- `.gitattributes` của repo quy định file văn bản dùng xuống dòng `LF` và đánh dấu file nhị phân (ảnh, model, âm thanh). Không sửa file này trong nhánh tính năng.
- **Nhóm dùng cả Windows và macOS** (Khải dùng MacBook). Những điểm cần nhớ:
  - Phím tắt trong tài liệu ghi theo Windows; trên Mac thay `Ctrl` bằng `Cmd`. Đường dẫn menu của Unity giống nhau, trừ `Edit > Preferences` trên Windows nằm ở menu `Unity > Settings` trên Mac.
  - Đổi tên file chỉ khác chữ hoa/thường (ví dụ `player.cs` thành `Player.cs`) thì làm trong Unity rồi kiểm tra `git status`. Cả Windows và macOS đều không phân biệt hoa/thường nên Git có thể bỏ sót; nếu vậy dùng `git mv`.
  - Không commit file riêng của hệ điều hành (`.DS_Store`, `Thumbs.db`); `.gitignore` đã chặn sẵn.

### Nền tảng đích: Android, màn hình ngang

Game chạy trên điện thoại Android, điều khiển hoàn toàn bằng cảm ứng. Mọi tính năng phải dùng được mà không cần bàn phím hay chuột.

- **Màn hình ngang** (đã khóa trong Player Settings, xoay được giữa hai chiều ngang). Giao diện thiết kế theo khung 1920×1080.
- **Thử trong Editor bằng Device Simulator** (`Window > General > Device Simulator`), chọn một điện thoại Android có tai thỏ. Simulator giả lập cảm ứng và vùng an toàn. Không cần build ra máy để thử từng thay đổi.
- **Nút bấm trên màn hình** cao tối thiểu 96 px (theo khung 1920×1080), tương đương khoảng 9 mm trên điện thoại. Các nút hay bấm lúc đang chơi (tấn công, đổi vũ khí, tạm dừng) nên to hơn.
- **Vùng an toàn:** nút và chữ đặt sát cạnh màn hình phải nằm trong vùng an toàn, để không bị tai thỏ hay viền bo che. Cách làm: tạo một object con của Canvas, gắn component `LightSouls.Common.SafeAreaFitter`, rồi đặt các nút vào trong object đó.
- **Nút Back của Android** được Input System coi là phím Esc, tức action `Cancel` của map `UI`. Trong game nó mở và đóng menu tạm dừng; trong menu nó quay lại màn trước.
- **Hiệu năng:** Android dùng mức chất lượng `Mobile` (URP `Mobile_RPAsset`). Nhắm 60 FPS trên máy tầm trung; người chơi có thể hạ xuống 30 FPS trong Cài đặt để đỡ tốn pin. Không thêm hiệu ứng hậu kỳ nặng khi chưa thử trên máy thật.
- **Build ra file APK** cần module Android của Unity: trong Unity Hub chọn `Installs` → `6000.5.7f1` → `Add modules` → **Android Build Support** (cùng OpenJDK và Android SDK & NDK Tools). Chỉ người build mới cần module này; Play Mode trong Editor không cần.

## 2. Đơn vị và kích thước

| Quy ước | Giá trị |
| :--- | :--- |
| Đơn vị | 1 unit = 1 m |
| Trục | `Y` hướng lên; mặt sàn đi được nằm ở `Y = 0` |
| Ô cửa nối phòng | rộng 4 m, cao 3 m |
| NavMesh agent | Agent type `Humanoid`: bán kính 0,5 m, cao 2 m, leo bậc 0,75 m, dốc 45° |

- Người chơi và mọi quái đi bộ phải vừa agent `Humanoid` (cao ≤ 2 m, bán kính ≤ 0,5 m) và dùng agent type này. Bộ sinh màn chỉ bake NavMesh cho agent đó; quái dùng agent type khác sẽ không tìm được đường.
- Quái lớn hơn (ví dụ boss) cần agent type riêng thì phải thống nhất với người làm sinh màn trước, vì NavMesh phải bake thêm cho agent đó.

## 3. Tổ chức code

```
Assets/Scripts/
  Common/          LightSouls.Common          — interface, struct, enum dùng chung
  MapGeneration/   LightSouls.MapGeneration   — sinh màn (Đạt)
  Combat/          LightSouls.Combat          — nhân vật, quái, vũ khí, túi đồ, vật phẩm (Khải)
  Lobby/           LightSouls.Lobby           — sảnh: cửa hàng, chọn ngục (Đạt)
  UI/              LightSouls.UI              — menu, HUD (Kiệt)
  Game/            LightSouls.Game            — lắp ráp các hệ thống trong Scene Game (Kiệt)
```

- Mỗi thư mục có một assembly definition (`.asmdef`) cùng tên với namespace.
- **Quy tắc tham chiếu:**
  - `MapGeneration`, `Combat`, `UI` chỉ được tham chiếu `Common`, không tham chiếu lẫn nhau.
  - `Lobby` được tham chiếu `Common` và `MapGeneration` (để đọc danh sách ngục), không tham chiếu `Combat` hay `UI`. Cửa hàng làm việc với vật phẩm qua `ItemDefinitionSO` và `IInventory` trong `Common`.
  - Chỉ nhóm trưởng sửa `Common`. Cần thêm hay đổi gì trong đó thì nhắn nhóm trưởng; đừng tự sửa trong nhánh tính năng, vì `Common` được mọi người dùng nên rất dễ xung đột.
  - Chỉ `Game` được tham chiếu tất cả. Script `GameBootstrap` trong Scene `Game` là nơi duy nhất nối các hệ thống với nhau (xem mục 4).
  - Unity sẽ báo lỗi biên dịch nếu vi phạm, nên quy tắc tự được kiểm tra.
- Code chỉ dùng để thử nghiệm (điều khiển scene test, phím tắt debug, giao diện thử) đặt trong thư mục con `Testing/` của hệ thống và **không bao giờ** được đặt vào Scene `Game`.
- Prefab và asset cũng chia theo hệ thống: `Assets/Prefabs/<HeThong>/`, `Assets/ScriptableObjects/<HeThong>/`.
- Tên trong code (lớp, hàm, biến) bằng tiếng Anh; comment và tài liệu bằng tiếng Việt. Tên file trùng tên lớp. Prefab đặt tên `PascalCase` có tiền tố loại: `Room_`, `Enemy_`, `Weapon_`, `Item_`, `Hazard_`, `Lobby_`, `UI_`.
- **Enum được lưu trong prefab hay asset** (`RoomType`, `ItemCategory`, …): Unity lưu chúng dạng số. Chỉ thêm giá trị mới vào **cuối**, không đổi thứ tự, không xóa giá trị đang dùng, nếu không mọi prefab cũ sẽ đổi nghĩa mà không báo lỗi.
- **Tên công khai mà code người khác đang gọi thì không đổi tên hay xóa.** Danh sách nằm trong mục 4. Cần đổi thì báo nhóm trưởng trước để sửa phần lắp ráp cùng lúc.

## 4. Hợp đồng giữa các hệ thống

### 4.1. Sinh màn cung cấp gì

`DungeonGenerator` (cài đặt `IDungeonProvider`) phát sự kiện `OnDungeonGenerated(DungeonRuntimeContext ctx)` khi màn đã dựng xong và NavMesh đã bake. Các hệ thống khác chỉ dùng những trường sau:

| Trường | Dùng cho |
| :--- | :--- |
| `ctx.PlayerSpawnPoint` | đặt người chơi |
| `ctx.LevelExitPoint` | đích qua màn |
| `ctx.SpawnedRooms` | danh sách phòng; mỗi `RoomModule` có `RoomType`, `EnemySpawnPoints`, `LifecycleController` |
| `ctx.IsNavMeshBaked` | chỉ cho quái di chuyển khi `true` |

Khi `generateOnStart` bật, sự kiện này phát ngay trong `Start()` của `DungeonGenerator`. Ai cần nghe thì đăng ký trong `Awake()` hoặc `OnEnable()`; đăng ký trong `Start()` có thể bị lỡ.

Các trường còn lại (`VendingMachine`, `BossRewardController`, `TriggerBossRewards()`) nằm ngoài phạm vi prototype và **không** thuộc hợp đồng: hệ thống khác không được dựa vào chúng. Mua vũ khí và phần thưởng boss sẽ được thiết kế lại ở một đợt sau, khi hệ thống vũ khí của Khải đã có.

### 4.2. Sát thương

Đặt trong `Common`, mọi thứ có máu đều cài đặt:

```csharp
namespace LightSouls.Common
{
    public struct DamageInfo
    {
        public float Amount;
        public GameObject Source;   // ai gây sát thương
        public Vector3 HitPoint;
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(DamageInfo info);
        event System.Action<IDamageable> Died;
    }
}
```

- Hai kiểu này đã có sẵn trong `Assets/Scripts/Common/`. Không tạo bản riêng trong hệ thống của mình.
- Dùng `struct` để sau này thêm trường (loại sát thương, lực đẩy) mà không phải sửa mọi nơi gọi.
- Không đánh nhầm phe được xử lý bằng layer và ma trận va chạm (mục 5), không xử lý trong interface.

### 4.3. Vòng đời một phòng chiến đấu

Mỗi hệ thống chỉ giữ phần của mình; `GameBootstrap` nối hai bên:

1. Người chơi bước vào phòng → `RoomLifecycleController` (sinh màn) khóa cửa và phát `OnRoomEnteredCombat(room)`.
2. `GameBootstrap` gọi hệ thống chiến đấu sinh quái tại `room.EnemySpawnPoints`.
3. Hệ thống chiến đấu tự đếm quái còn sống và phát sự kiện khi đã hết quái trong đợt đó.
4. `GameBootstrap` gọi `room.LifecycleController.ClearRoom()` → cửa mở.

Sinh màn không biết gì về quái; chiến đấu không biết gì về cửa.

### 4.4. Menu và Game

`MainMenu` chỉ nạp Scene `Game` (và ngược lại khi thoát). Hai Scene không giữ tham chiếu đến đối tượng của nhau.

Luồng trong Scene `Game` (cài đặt trong `GameBootstrap`):

1. `DungeonGenerator` sinh màn, `GameBootstrap` sinh `Player_Character` tại `ctx.PlayerSpawnPoint`, gán cho `GameCameraFollow` và `CombatHUD.SetPlayer()`.
2. Mỗi phòng chiến đấu đi theo mục 4.3, quái do `EnemySpawner` trong Scene sinh ra.
3. Người chơi chết → `GameResultScreen.ShowDefeat()`. Người chơi tới trong bán kính 2.5 m quanh `ctx.LevelExitPoint` khi phòng cuối đã sạch quái → `ShowVictory()`. Cả hai màn có nút chơi lại và về menu chính.
4. `PauseMenuController.PausedChanged` bật/tắt điều khiển của người chơi (`PlayerController.SetInputActive`).

**Tên đang được phần lắp ráp gọi, không đổi tên hay xóa** (thêm hàm, thêm sự kiện thì thoải mái):

| Hệ thống | Tên |
| :--- | :--- |
| Combat | `PlayerController`: `OnDied`, `OnHealthChanged`, `OnWeaponChanged`, `SetInputActive()`, `IsDead`, `CurrentHealth`, `MaxHealth`; `EnemySpawner`: `SpawnWave()`, `AllEnemiesDefeated`, `SetEnemyPrefabs()`; `CombatHUD.SetPlayer()`; `HealthComponent`; prefab `Player_Character`, `Combat_TouchControls` |
| MapGeneration | `DungeonGenerator.OnDungeonGenerated`; `DungeonRuntimeContext`: `PlayerSpawnPoint`, `LevelExitPoint`, `SpawnedRooms`, `Layout`; `RoomModule`: `RoomType`, `RoomId`, `EnemySpawnPoints`, `LootSpawnPoints`, `LifecycleController`, `GetCurrentWorldBounds()`; `RoomLifecycleController`: `OnRoomEnteredCombat`, `ClearRoom()`, `State`, `RequiresCombatLock`; enum `RoomType` |
| UI | `PauseMenuController.PausedChanged`, `GameResultScreen` |

### 4.5. Vật phẩm và túi đồ

Các kiểu sau nằm trong `Common`, để cửa hàng, rương, phần thưởng làm việc với vật phẩm mà không cần tham chiếu `Combat`:

| Kiểu | Vai trò |
| :--- | :--- |
| `ItemDefinitionSO` | lớp gốc (abstract) của mọi vật phẩm: `ItemId`, `DisplayName`, `Description`, `Icon`, `Price`, `MaxStack`, `Category`. Vũ khí, trang bị, đồ dùng là lớp con do Khải làm |
| `ItemCategory` | `Weapon`, `Equipment`, `Consumable`, `Misc` |
| `IInventory` | túi đồ nhìn từ ngoài: `HasSpaceFor`, `TryAdd`, `TryRemove`, `GetCount`, sự kiện `Changed`. Cài đặt trên đối tượng gốc của người chơi |
| `ItemSlotRecord` | một ô túi đồ dạng lưu được (`SlotKey`, `ItemId`, `Count`) |

- `ItemId` là duy nhất và **không đổi** sau khi đã dùng, vì túi đồ được lưu qua Scene bằng mã này. Đặt dạng `loại.tên`, ví dụ `weapon.iron_sword`, `armor.leather_vest`.
- Asset vật phẩm đặt trong `Assets/ScriptableObjects/Combat/Items/` (Khải sở hữu). Hệ thống khác chỉ tham chiếu asset, không sửa.
- Muốn đưa đồ cho người chơi: lấy `IInventory` trên người chơi rồi gọi `TryAdd`. Không tự thêm vào danh sách bên trong của túi đồ.
- Thanh nhanh, balo, ô trang bị xếp thế nào là việc bên trong túi đồ; hệ thống khác không dựa vào cách xếp đó.

### 4.6. Dữ liệu một lượt chơi (`RunState`)

`LightSouls.Common.RunState` (tĩnh) giữ dữ liệu khi chuyển Scene sảnh → ngục → sảnh. Chỉ sống trong bộ nhớ, tắt game là mất.

| Dữ liệu | Ai ghi | Ai đọc |
| :--- | :--- | :--- |
| `Coins`, `AddCoins()`, `TrySpendCoins()`, `CoinsChanged` | sảnh trừ khi mua; `GameBootstrap` cộng khi dọn phòng | ai cũng đọc |
| `InventorySlots`, `SaveInventory()` | túi đồ của người chơi, mỗi khi thay đổi | túi đồ của người chơi, khi được sinh ra trong Scene mới |
| `SelectedDungeonId` | sảnh, khi người chơi chọn ngục | bộ sinh màn trong Scene `Game`; chuỗi rỗng là ngục mặc định |

`RunState.Reset()` (bắt đầu lượt mới) do phần lắp ráp gọi. Mỗi lần vào Play Mode `RunState` tự xóa sạch, nên vào thẳng Scene thử vẫn chạy được.

### 4.7. Địa hình nguy hiểm

- Vùng gây sát thương (lava, gai, bẫy) gọi `IDamageable.TakeDamage()` lên nhân vật đứng trong vùng, `Source` là chính vùng đó. Không gọi thẳng vào `HealthComponent`.
- Vùng làm chậm (nước, bùn) gọi `ISpeedModifiable.SetSpeedMultiplier(this, hệ số)` khi nhân vật vào và `RemoveSpeedMultiplier(this)` khi ra. Người chơi và mọi quái cài đặt `ISpeedModifiable` (Khải).
- Collider của vùng là trigger, đặt layer `Hazard` (mục 5). Tìm `IDamageable`/`ISpeedModifiable` bằng `GetComponentInParent` trên collider chạm vào.
- Quái dùng `NavMeshAgent` không có `Rigidbody`, nên vùng cần một `Rigidbody` `isKinematic` thì mới nhận được sự kiện trigger từ quái.
- Cho quái biết chỗ nguy hiểm bằng NavMesh area: `Water` (cost 3) và `Lava` (cost 20), đã có trong `ProjectSettings/NavMeshAreas.asset`. Đánh dấu bằng `NavMeshModifier`/`NavMeshModifierVolume` trong prefab phòng.

### 4.8. Tương tác

- Vật bấm nút Tương tác để dùng (vật phẩm rơi, rương, quầy hàng, cổng vào ngục) cài đặt `LightSouls.Common.IInteractable`: `Prompt`, `CanInteract()`, `Interact(interactor)`.
- Vật đó có một collider trigger đặt layer `Interactable`.
- Phía người chơi (tìm vật gần nhất, hiện nút cùng chữ `Prompt`, gọi `Interact`) chỉ có một bản, do Khải làm. Người khác không tự làm cách tương tác riêng.

### 4.9. Nút Back và thứ tự Canvas

- Bảng mở đè lên màn chơi (balo, cửa hàng, chọn ngục…) gọi `BackButtonStack.Push(this, Close)` khi mở và `BackButtonStack.Remove(this)` khi đóng. Nút Back (Esc) sẽ đóng bảng trên cùng trước khi mở menu tạm dừng. Bảng không tự nghe `UI/Cancel`.
- Khi bảng đang mở, gọi `PlayerController.SetInputActive(false)` (hoặc cách tương đương của túi đồ) để chạm vào bảng không làm nhân vật đánh hay đi.
- Thứ tự Canvas (sort order, Screen Space - Overlay):

  | Sort order | Canvas |
  | :--- | :--- |
  | 0 | menu chính |
  | 5 | điều khiển cảm ứng, HUD chiến đấu, thanh nhanh |
  | 6 | bản đồ nhỏ |
  | 8 | bảng mở đè lên màn chơi: balo, cửa hàng, chọn ngục |
  | 10 | menu tạm dừng |
  | 20 | màn Thắng/Thua |
  | 30 | màn Cài đặt |

- Bố cục HUD (khung 1920×1080): cần analog ở góc trái dưới, nút đánh và kỹ năng ở góc phải dưới, thanh máu ở góc trái trên, bản đồ nhỏ và nút tạm dừng ở góc phải trên. Thanh nhanh đặt ở giữa cạnh dưới, không đè lên cần analog hay nút đánh.

## 5. Tag, layer và input

| Tag | Gắn cho |
| :--- | :--- |
| `Player` | đối tượng gốc của nhân vật người chơi (`RoomLifecycleController` dựa vào tag này để nhận biết người chơi vào phòng) |
| `Enemy` | đối tượng gốc của mỗi quái |

| Layer | Số | Gắn cho |
| :--- | :--- | :--- |
| `Player` | 6 | nhân vật người chơi |
| `Enemy` | 7 | quái |
| `Environment` | 8 | sàn, tường, cửa, vật cản |
| `PlayerAttack` | 9 | đạn và vùng đánh do người chơi tạo ra |
| `EnemyAttack` | 10 | đạn và vùng đánh do quái tạo ra |
| `Hazard` | 11 | vùng trigger của địa hình nguy hiểm: lava, nước, gai, bẫy |
| `Interactable` | 12 | vùng trigger của vật tương tác được: vật phẩm rơi, rương, quầy hàng, cổng |

Ma trận va chạm đã được đặt sẵn trong `ProjectSettings/DynamicsManager.asset`:

- `PlayerAttack` chỉ va chạm với `Enemy` và `Environment`; `EnemyAttack` chỉ va chạm với `Player` và `Environment`. Nhờ vậy đòn đánh không bao giờ trúng phe mình và hai loại đạn không va vào nhau.
- `Hazard` chỉ va chạm với `Player` và `Enemy`: đạn bay qua lava không bị chặn. `Interactable` chỉ va chạm với `Player`.
- Sàn lava, mặt nước vẫn cần một collider đứng được riêng ở layer `Environment`; collider `Hazard` chỉ dùng để phát hiện ai đang đứng trong đó.
- `Player`, `Enemy`, `Environment` va chạm với nhau bình thường.
- Đòn đánh dùng collider kiểu trigger (hoặc raycast với `LayerMask` tương ứng), sau đó tìm `IDamageable` trên đối tượng trúng.

Quy định:

- Chỉ nhóm trưởng sửa danh sách tag/layer (`ProjectSettings/TagManager.asset`), ma trận va chạm và NavMesh area (`ProjectSettings/NavMeshAreas.asset`). Nói chung mọi file trong `ProjectSettings/` và `Packages/` do nhóm trưởng sửa. Cần thêm thì đề nghị trong PR hoặc nhắn nhóm.
- Điều khiển trong game dùng file chung `Assets/InputSystem_Actions.inputactions`, action map `Player` (điều khiển nhân vật) và `UI` (menu). Khải sở hữu file này; người khác cần thêm action thì nhờ Khải. Không đọc phím trực tiếp (`Keyboard.current`) ngoài code trong `Testing/`.
- Điều khiển cảm ứng dùng `OnScreenStick` và `OnScreenButton` của Input System. Hai component này giả lập một tay cầm, nên action trong map `Player` cần có binding cho tay cầm để nhận được tín hiệu từ nút trên màn hình. Binding bàn phím vẫn giữ để thử nhanh trong Editor.
- Nút tay cầm `buttonSouth` và `buttonEast` đã thuộc map `UI` (`UI/Submit`, `UI/Cancel`; `UI/Cancel` mở menu tạm dừng). Nút cảm ứng của map `Player` **không** được dùng hai nút này. Phân bổ hiện tại:

  | Action | Nút tay cầm (nút cảm ứng gửi tín hiệu này) |
  | :--- | :--- |
  | `Player/Move` | `<Gamepad>/leftStick` |
  | `Player/Attack` | `<Gamepad>/buttonWest` |
  | `Player/Dash` | `<Gamepad>/rightTrigger` |
  | `Player/Interact` | `<Gamepad>/buttonNorth` |

  `Player/SwitchWeapon` đang dùng `buttonNorth` sẽ bị bỏ khi có thanh nhanh (nhiệm vụ của Khải ngày 2026-11-14); `buttonNorth` chuyển cho `Interact`. Các nút còn trống để dùng sau: `leftShoulder`, `rightShoulder`, `leftTrigger`, `dpad`.
- Chọn ô trên thanh nhanh và mở balo là nút giao diện (uGUI `Button`), không phải action của map `Player`.
- Không thêm binding chạm toàn màn hình (`<Touchscreen>/primaryTouch/...`) hay chuột (`<Mouse>/leftButton`) cho action của người chơi: chạm hoặc bấm chuột vào joystick, nút khác cũng sẽ kích hoạt action đó. Thử trong Editor bằng bàn phím: WASD đi, Space đánh, Shift lướt, E tương tác, phím 1–5 chọn ô thanh nhanh, Tab mở balo.
- Script dùng action qua `InputActionReference` gán trên prefab. Khi thêm trường mới, kiểm tra prefab đã gán chưa: trường để trống không báo lỗi, chỉ là action không bao giờ chạy.
- Cài đặt của người chơi (âm lượng, FPS, rung) đọc qua `LightSouls.Common.GameSettings`. Ví dụ hệ thống chiến đấu chỉ rung máy khi `GameSettings.VibrationEnabled` là `true`.

## 6. Sở hữu Scene và file

- Scene chơi thật `MainMenu`, `Game` và `Lobby` (sẽ tạo khi ghép phần sảnh): chỉ nhóm trưởng (Kiệt) sửa. Thành viên bàn giao bằng prefab và script, không bằng Scene. `SceneNames` và danh sách Scene trong Build Settings cũng do nhóm trưởng sửa.
- Mọi thử nghiệm làm trong Scene thử riêng của mình. Mỗi người chỉ sửa Scene thử của mình:

  | Scene thử | Người phụ trách |
  | :--- | :--- |
  | `CharacterCombatTest`, `InventoryTest` | Khải |
  | `MapGenTest`, `HazardRoomsTest`, `LobbyTest` | Đạt |

  Asset chỉ dùng cho một Scene (NavMesh bake, Lighting) đặt trong thư mục cùng tên cạnh Scene, ví dụ `Assets/Scenes/InventoryTest/`.
- Không sửa prefab, Scene hay asset trong thư mục của hệ thống khác. Cần thay đổi thì nhờ người phụ trách.
- Được kéo prefab của người khác vào Scene thử của mình (ví dụ đặt `Player_Character` vào `LobbyTest`), vì Scene không cần tham chiếu assembly. Chỉ không được sửa prefab đó.
- Script tạo prefab hoặc Scene tự động (`CombatPrototypeBuilder`, `MapGenPrototypeBuilder`) ghi đè lên bản đang có, kể cả những sửa đổi đã làm tay khi ghép. Không chạy lại chúng lên prefab đã bàn giao. Nếu vẫn dùng thì sửa script để tạo ra đúng bản hiện tại trong `develop`, rồi so `git diff` trước khi commit.

## 7. Git và Pull Request

- Quy trình nhánh theo `README.md`. Commit theo dạng `loại(phạm vi): mô tả`, ví dụ `feat(map-gen): …`, `fix(combat): …`, `docs: …`.
- Nhánh của mỗi đợt việc được nhóm trưởng tách từ `develop` mới nhất và ghi trong file task. Làm đúng nhánh đó; nhánh cũ đã merge thì xóa bản local: `git fetch --prune`, rồi `git branch -d <tên nhánh cũ>`.
- Khi `develop` có thay đổi mới (nhóm trưởng sẽ báo), đóng Unity rồi `git fetch origin` và `git merge origin/develop` vào nhánh của mình. Luôn làm bước này ngay trước khi mở PR, để xung đột được giải quyết ở nhánh của mình chứ không phải lúc merge.
- PR chỉ chứa file thuộc phạm vi nhiệm vụ. Kiểm tra lại các file Unity tự sinh như `ProjectSettings/*.json` trước khi commit.
- Tài liệu viết bằng Markdown (`.md`) để Git xem được thay đổi; không dùng `.docx`.
- Trước khi mở PR, tự kiểm tra:
  - [ ] Mở Scene thử, vào Play Mode, console không có lỗi.
  - [ ] Không có file ngoài phạm vi; Scene/prefab không bị lưu dạng nhị phân.
  - [ ] Tài liệu bàn giao khớp với code thật (tên hàm, tham số, đường dẫn prefab).
  - [ ] Ghi rõ phần nào còn tạm thời hoặc chưa làm.

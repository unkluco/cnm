# Tài liệu Thiết kế & Bàn giao: Format Room và Sinh Màn Chơi (Map Generation)

**Phụ trách:** Đạt  
**Nhánh Git:** `feature/map-generation`  
**Dự án:** Light Souls (Unity 6 — `6000.5.x`, URP, New Input System, Unity AI Navigation)

---

## 1. Kiến trúc tổng thể & Tham khảo thiết kế

Hệ thống sinh màn chơi được thiết kế theo kiến trúc tách lớp (Data / Solver / Scene Orchestrator / Integration), kế thừa các mẫu chuẩn công nghiệp từ các repository mã nguồn mở:

1. **Tách biệt Topology Graph và Room Template Prefab** *(tham khảo `Edgar-Unity`)*:
   - Việc lập lịch vị trí phòng (`DungeonLayoutPlanner`) chạy hoàn toàn trên dữ liệu thuần C# (`RoomTemplateData` + `Bounds` + `SocketDefinition`), không `Instantiate`/`Destroy` GameObject khi thử sai hay quay lui (backtrack).
   - Chỉ sau khi tìm được bố cục 100% hợp lệ, không chồng lấn và có đường đi từ `Start` tới `Exit`, `DungeonGenerator` mới khởi tạo prefab vào Scene.
2. **Nối phòng theo Door Socket & Rotation Snapping** *(tham khảo `Unity ProceduralDungeon`)*:
   - Mỗi cửa kết nối (`DoorSocket`) mang vị trí cục bộ và hướng pháp tuyến (`North`, `East`, `South`, `West`).
   - Khi nối phòng mới vào socket chủ, hệ thống xoay phòng `0° / 90° / 180° / 270°` quanh trục `Y` để hai hướng cửa đối đầu chính xác (`dot = -1`), sau đó tịnh tiến sao cho hai socket trùng khít (`khoảng cách = 0.0m`).
3. **Giao tiếp tách rời qua ScriptableObject Event Channels & Runtime Anchors** *(tham khảo `Unity-Technologies/open-project-1`)*:
   - Các hệ thống khác (nhân vật, quái vật, GameManager của Khải) nhận dữ liệu thông qua asset `OnDungeonGenerated.asset`, `PlayerSpawnAnchor.asset`, `LevelExitAnchor.asset` hoặc interface `IDungeonProvider` mà không cần hardcode Singleton hay tham chiếu chéo Scene.
4. **Native Async & Dynamic Runtime NavMesh** *(Unity 6 `Awaitable` + `com.unity.ai.navigation`)*:
   - Hỗ trợ cả sinh đồng bộ (`Generate()`) và sinh bất đồng bộ từng frame không tốn GC (`GenerateAsync()` dùng `UnityEngine.Awaitable`).
   - Tự động gọi `NavMeshSurface.BuildNavMesh()` sau khi ghép xong phòng và kiểm chứng tính liên thông vật lý bằng `NavMesh.CalculatePath(start, exit)`.

---

## 2. Format mô tả Room (`RoomModule` & `RoomTemplateSO`)

### 2.1. Quy ước hệ tọa độ & kích thước chuẩn (Metric Standard)
Để mọi prefab ghép khớp tuyệt đối không hở vách và không z-fighting sàn:
- **Mặt sàn đi bộ (`Y = 0.0`)**: Sàn phòng có độ dày `0.5m` đặt ở tâm `Y = -0.25m`, nên mặt trên của mọi phòng đều nằm chính xác trên mặt phẳng `Y = 0.0`.
- **Tường bao (`Height = 3.5m`, `Thickness = 0.5m`)**: Tường nằm **bên trong** chu vi `width × length` của phòng.
- **Kích thước ô cửa tiêu chuẩn (`Opening = 4.0m × 3.0m`)**: Đủ rộng cho Player, Enemy và `NavMeshAgent` di chuyển qua lại dễ dàng.
- **Vị trí `DoorSocket`**: Đặt tại **mép ngoài cùng** của bức tường (ví dụ phòng `16×16` có `Socket_North` tại `(0, 0, +8)` hướng `North (+Z)`).

### 2.2. Thành phần của một Room Prefab (`RoomModule`)
Mỗi Room Prefab gắn script `RoomModule` ở Root GameObject gồm:

| Trường dữ liệu | Kiểu | Ý nghĩa |
| :--- | :--- | :--- |
| `roomType` | `RoomType` (`Start`, `Normal`, `Combat`, `Treasure`, `Corridor`, `Exit`) | Phân loại vai trò phòng trong màn chơi |
| `localBounds` | `Bounds` | Hộp bao va chạm cục bộ dùng để kiểm tra chống chồng lấn giữa các phòng |
| `doorSockets` | `List<DoorSocket>` | Danh sách các điểm nối cửa gắn trên tường phòng |
| `playerSpawnPoint` | `Transform` | Điểm xuất phát của nhân vật (bắt buộc ở `Room_Start`) |
| `exitPoint` | `Transform` | Điểm đích / cổng qua màn (bắt buộc ở `Room_Exit`) |
| `enemySpawnPoints` | `List<Transform>` | Các điểm thả quái trong phòng (cho hệ thống Combat của Khải) |
| `lootSpawnPoints` | `List<Transform>` | Các điểm đặt hòm báu / vũ khí (dùng cho `Room_Treasure` hoặc phòng thưởng) |

### 2.3. Thành phần của một Cửa nối (`DoorSocket`)
Mỗi GameObject con `Socket_*` mang component `DoorSocket`:
- `socketId` (`string`): Tên định danh duy nhất trong phạm vi room (vd: `Socket_North`).
- `localDirection` (`DoorDirection`): Hướng quay ra ngoài theo hệ tọa độ cục bộ của room (`North`, `East`, `South`, `West`).
- `openVisual` (`GameObject`): Khung cửa vòm trang trí, chỉ bật khi socket **được nối** với phòng khác.
- `closedWallVisual` (`GameObject`): Mảng tường kín có `BoxCollider`, tự động **bật** khi socket **không nối**, bảo đảm phòng luôn kín hoàn toàn và không bị lọt ra ngoài void.

### 2.4. Cấu hình dữ liệu Template (`RoomTemplateSO`)
Tạo bằng `Create > LightSouls > Map Generation > Room Template`:
- Lưu `prefab` (`RoomModule`), `selectionWeight`, `minCount`, `maxCount`, `allowRotation`.
- Tự động đồng bộ (`OnValidate` / `SyncFromPrefab`) danh sách `SocketDefinition` và `localBounds` từ prefab để thuật toán lập lịch đọc trực tiếp mà không cần sinh GameObject.

---

## 3. Dữ liệu mô tả một Màn chơi (`DungeonLayoutData`)

Khi `DungeonLayoutPlanner.TryPlan(...)` hoàn tất, toàn bộ bố cục được lưu trong `DungeonLayoutData` (có thể xuất JSON qua `layout.ToJson()`):

- **`seed`** (`int`): Hạt giống ngẫu nhiên để tái tạo chính xác cùng một màn chơi.
- **`rooms`** (`List<PlacedRoomData>`):
  - `roomId`, `templateId`, `roomType`
  - `worldPosition` (`Vector3`), `yawSteps` (`0..3`), `worldRotationEuler` (`Vector3`)
  - `worldBounds` (`Bounds` sau khi xoay)
  - `graphDepth` (khoảng cách BFS tính theo số phòng từ `StartRoom`)
  - `isOnMainPath` (`bool` — phòng có nằm trên tuyến chính `Start -> Exit` hay không)
  - `enemySpawnPositions` (`List<Vector3>`)
- **`connections`** (`List<RoomConnectionData>`):
  - `fromRoomId`, `fromSocketName`, `toRoomId`, `toSocketName`, `doorWorldPosition`
- **`startRoomId` / `exitRoomId`** (`int`)
- **`mainPathRoomIds`** (`List<int>`): Chuỗi phòng ngắn nhất từ `Start` đến `Exit` đã kiểm chứng qua BFS.
- **`playerSpawnPosition`**, **`exitPosition`**, **`enemySpawnPositions`**
- **`isReachableFromStartToExit`** (`bool`) & **`hasAnyOverlap`** (`bool`)

---

## 4. Thuật toán sinh màn: Kiến trúc 2 Pha & Các Ràng buộc Bố cục Chuẩn

`DungeonLayoutPlanner` vận hành theo kiến trúc **2 pha (Graph-First Topology $\rightarrow$ 3D Socket Instantiation)** và kiểm duyệt nghiêm ngặt:

1. **Luật 1 — Tách rời Sinh Đồ Thị Lưới 2D `(x, y)` và Dựng Hình 3D**:
   - Toàn bộ cấu trúc tầng được tính toán trên lưới 2D `(x, y)` bằng thuật toán **Self-Avoiding Random Walk** kết hợp kiểm tra **3D AABB Non-Overlap** trước khi bất kỳ GameObject nào được `Instantiate` ra Scene.
2. **Luật 2 — Tuyệt đối cấm "Hành lang cụt" & "Phòng trống bịt đầu"**:
   - Mọi cạnh nối `(Node A <-> Node B)` luôn đặt nguyên tử cặp `(Corridor + TargetRoom)`. Mọi hành lang (`Room_Corridor_Straight`, kích thước ngắn gọn `6m × 8m` chống thời gian chết 3D) bắt buộc có `Degree == 2` và `EnemySpawnPoints == 0`.
   - Mọi Room Prefab chuẩn đều có đủ 4 hướng `(North, East, South, West)`. Hướng nào có cạnh nối sẽ bật `openVisual` (`DoorFrame`); hướng nào không có cạnh nối tự động bật `closedWallVisual` (`SolidWall` đặc kín mít).
3. **Luật 3 — Trục Chính Tuyến Tính (`Start -> Combat_1 -> Combat_2 -> Combat_3 -> Boss`) + Khóa Bậc Start/Boss**:
   - Phòng **Start (Xanh)** bắt buộc chỉ mở đúng **1 cửa ra** (`Degree(Start) == 1`).
   - Phòng **Boss / Exit (Tím, 20m×20m)** nằm ở cuối trục chính, bắt buộc chỉ mở đúng **1 cửa vào** (`Degree(Boss) == 1`), không có nhánh hay hành lang thừa phía sau.
   - Nhánh phụ chỉ được phép mọc ra từ các phòng `Combat` nằm giữa trục chính.
4. **Luật 4 — Quy tắc Gán Nhãn theo Bậc của Đỉnh (Node Degree Role Rule)**:
   - **Đỉnh gốc (`Root`):** `Start` (`Degree == 1`).
   - **Đỉnh cuối trục chính:** `Boss / Exit` (`Degree == 1`, cách `Start` tối thiểu 4–5 phòng chính).
   - **Tất cả các đỉnh lá còn lại (`Leaf Nodes`, `Degree == 1`):** Bắt buộc 100% là **Phòng Kho Báu (`Room_Treasure`)**. Tuyệt đối cấm đỉnh lá là phòng trống hoặc phòng quái cụt không có thưởng.
   - **Tất cả các đỉnh trung gian (`Degree >= 2`):** Bắt buộc là phòng chiến đấu (`Normal` / `Combat`) có quái vật, tuân thủ **Doorway Safe Zone** (quái cách cửa $\ge 3.8\text{m}$) và vật cản thấp ngang thắt lưng (`1.4m`) để không che khuất tầm nhìn camera 3D.
5. **Luật 5 — Cổng Kiểm Duyệt (`DungeonLayoutPlanner.IsGraphValid`) trước khi Spawn**:
   - Kiểm tra tự động 100% điều kiện: `Degree(Start) == 1`, `Degree(Boss) == 1`, `CountMajorRoomsOnMainPath >= 4`, `Degree(Corridor) == 2`, mọi `Leaf Node` ngoài Start/Boss đều là `Treasure`, và `0` chồng lấn AABB.

---

## 5. Cách các hệ thống khác lấy dữ liệu khi tích hợp (Dành cho Khải & GameManager)

### 5.1. Điểm Tương Tác Mua Vũ Khí (`WeaponVendingMachine`) tại Điểm Bắt Đầu
Tại góc Tây Nam của phòng bắt đầu (`Room_Start`), một ki-ốt bán vũ khí tự động đã được dựng sẵn (`Assets/Prefabs/MapGeneration/Interactables/WeaponVendingMachine.prefab`):
- **Script:** `WeaponVendingMachine.cs` (nằm trên GameObject `WeaponVendingMachine` trong `Room_Start`).
- **Điểm đứng tương tác (`InteractionPoint`):** `Vector3` phía trước máy đã căn chỉnh sẵn.
- **Truy cập nhanh:**
  ```csharp
  // Từ DungeonRuntimeContext hoặc DungeonGenerator:
  WeaponVendingMachine vending = ctx.VendingMachine;
  if (vending != null)
  {
      vending.OnWeaponPurchased += (weaponId, cost) => {
          Debug.Log($"Khải: Player đã mua vũ khí {weaponId} với giá {cost}");
      };
      
      // Khải gọi hàm khi người chơi xác nhận mua trên UI:
      bool success = vending.TryPurchaseWeapon("Greatsword_Tier1", 100);
  }
  ```

### 5.2. Rương Thưởng Sau Khi Diệt Boss (`BossRoomRewardController`) tại Phòng Boss Tím
Tại phòng Boss màu tím (`Room_Exit`), hệ thống tích hợp sẵn 2 điểm xuất hiện hòm báu bí mật ẩn sẵn (`Bonus Loot Chests`):
- **Script:** `BossRoomRewardController.cs` (gắn trên `Room_Exit`).
- **Cơ chế:** Ban đầu các hòm này ẩn hoàn toàn. Khi người chơi hạ gục Boss, hệ thống Combat của Khải chỉ cần gọi:
  ```csharp
  // Cách 1: Gọi qua DungeonGenerator
  dungeonGenerator.TriggerBossRewards(); // Trả về số rương xuất hiện (1 hoặc 2)

  // Cách 2: Gọi qua DungeonRuntimeContext
  ctx.TriggerBossRewards();

  // Lắng nghe sự kiện rương xuất hiện:
  ctx.BossRewardController.OnBossRewardsSpawned += (count) => {
      Debug.Log($"Đã kích hoạt {count} rương báu phần thưởng Boss!");
  };
  ```

### 5.3. Nhận dữ liệu qua ScriptableObject Event Channel & Runtime Anchors
Chỉ cần kéo các asset trong `Assets/ScriptableObjects/MapGeneration/Channels/` vào Inspector của script bên Combat/GameManager:
- `OnDungeonGenerated.asset` (`DungeonGeneratedEventChannelSO`):
  ```csharp
  [SerializeField] private DungeonGeneratedEventChannelSO dungeonChannel;

  private void OnEnable()
  {
      dungeonChannel.OnEventRaised += HandleDungeonReady;
      if (dungeonChannel.LastContext != null)
          HandleDungeonReady(dungeonChannel.LastContext);
  }

  private void OnDisable() => dungeonChannel.OnEventRaised -= HandleDungeonReady;

  private void HandleDungeonReady(DungeonRuntimeContext ctx)
  {
      // Vị trí spawn Player: ctx.PlayerSpawnPoint.position
      // Vị trí Đích qua màn: ctx.LevelExitPoint.position
      // Danh sách điểm spawn quái: ctx.EnemySpawnPoints (không có quái trên hành lang)
      // NavMesh đã bake sẵn: ctx.IsNavMeshBaked
      // Máy bán vũ khí góc xuất phát: ctx.VendingMachine
      // Bộ điều khiển thưởng Boss: ctx.BossRewardController
  }
  ```
- `PlayerSpawnAnchor.asset` & `LevelExitAnchor.asset` (`TransformAnchorSO`): Đọc trực tiếp `anchor.Value` hoặc lắng nghe `anchor.OnAnchorProvided`.

### 5.4. Vòng Đời Phòng & Khóa Cửa Khi Giao Tranh (`RoomLifecycleController`)
Mỗi phòng chiến đấu (`Normal`, `Combat`, `Exit/Boss`) đều gắn sẵn component `RoomLifecycleController.cs` cùng vùng Trigger thụt sâu vào lòng phòng `2.2m` (chống kẹt nhân vật vào cánh cửa khi cửa đóng):
- **Trạng thái (`RoomLifecycleState`):**
  - `Unvisited`: Cửa mở sẵn, hiển thị màu khung cửa định hướng 3D (**Tím Đỏ** cho hướng Boss, **Vàng Kim** cho hướng Kho Báu, **Đồng** cho phòng thường).
  - `Combat`: Khi Player bước qua vùng Trigger (`OnTriggerEnter` với tag `"Player"`) hoặc gọi `room.LifecycleController.EnterCombat()`, toàn bộ cửa kết nối của phòng đó lập tức **hạ cổng chắn vật lý màu đỏ (`LockedGateVisual` có `BoxCollider`)** và phát sự kiện `OnRoomEnteredCombat`.
  - `Cleared`: Khi Khải xử lý diệt hết wave quái trong phòng và gọi `room.LifecycleController.ClearRoom()`, cổng chắn mở hoàn toàn, khung cửa chuyển sang màu **Xanh Ngọc (`LandmarkColorCleared`)**, và nếu là phòng Boss sẽ tự động gọi `TriggerBossRewards()`.

---

## 6. Hướng dẫn chạy thử & kiểm tra trong Scene `MapGenTest`

1. Mở Scene **`Assets/Scenes/MapGenTest.unity`** và nhấn **Play** (hoặc trong Edit Mode chuột phải vào component `DungeonGenerator` chọn **Generate New Dungeon**).
2. **Thao tác trong Play Mode**:
   - Phím **`R`** hoặc nút **Regenerate (R)**: Sinh màn chơi ngẫu nhiên mới.
   - Phím **`C`** hoặc nút **Replay Seed (C)**: Sinh lại đúng màn của seed hiện tại (kiểm tra tính tất định).
   - Phím **`L`** hoặc nút **Lock/Clear (L)**: Chuyển đổi trạng thái vòng đời phòng (`Unvisited` $\rightarrow$ `COMBAT LOCKED` đóng sập cửa chấn song đỏ $\rightarrow$ `ALL CLEARED` mở cửa xanh lá & sinh rương Boss) để kiểm tra trực quan.
   - Phím **`B`** hoặc nút **Kill Boss (B)**: Giả lập tiêu diệt boss trong phòng tím (`28×28m — 2×2 Slot`) để kích hoạt 1–2 rương báu ngẫu nhiên.
   - Nút **`- Rooms`** / **`+ Rooms`**: Thay đổi số lượng phòng mục tiêu (`3..40`).
   - Phím **`Tab`** hoặc nút **Camera**: Đổi góc nhìn **Top-Down** và **3D Orbit**.
   - Phím **`P`** hoặc nút **Path**: Bật/tắt đường chỉ dẫn tuyến chính màu vàng từ `Start` qua từng cửa nối đến `Exit`.
   - **`WASD` / Kéo chuột phải / Cuộn chuột / `F`**: Di chuyển, xoay, zoom và căn khung toàn bản đồ.
3. **Quan sát Minimap 2D (Góc trên bên phải) & Bảng Telemetry (Góc trên bên trái)**:
   - **2D Grid Minimap (`DungeonMinimapUI.cs`)**: Hiển thị trực tiếp cấu trúc lưới phòng và đường nối (`S`: Start, `C`/`E`: Combat/Elite, `$`: Kho báu, `BOSS`: Đại sảnh Boss `2×2`), tự động cập nhật trạng thái `!` (đang khóa cửa Combat) và `✓` (đã Clear).
   - **EditMode Unit Tests**: Chạy `Window > General > Test Runner > EditMode > Run All` để kiểm tra tự động 50 seed liên tiếp vượt qua `DungeonLayoutPlanner.IsGraphValid()`.

---

## 7. Giới hạn còn tồn tại (Prototype Scope)

1. **Chiều cao cố định trên một mặt phẳng (`Y = 0`)**: Tất cả các room hiện tại đặt sàn ở `Y = 0` và chỉ xoay quanh trục `Y` (`0°/90°/180°/270°`); chưa hỗ trợ cầu thang nối nhiều tầng cao độ khác nhau.
2. **Hình dạng va chạm AABB hình chữ nhật**: `DungeonLayoutPlanner` kiểm tra chống chồng lấn bằng `Bounds` hộp chữ nhật (AABB) bao quanh phòng; nếu về sau có phòng hình chữ `L` hoặc chữ `T` lớn muốn lồng khuyết vào nhau, cần nâng cấp lên danh sách nhiều sub-bounds (compound boxes) cho mỗi `RoomTemplate`.
3. **Cửa kết nối đồng kích thước (`4.0m`)**: Hiện mặc định mọi socket dùng chung chuẩn cửa rộng `4.0m`; chưa phân loại khớp nối theo tag kích thước cửa khác nhau.

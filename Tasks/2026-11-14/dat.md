# Đạt — Phòng địa hình đặc biệt và sảnh (2026-11-14)

**Nhánh: `feature/hazard-rooms-lobby`** (nhánh mới, đã được tách từ `develop` mới nhất và đẩy lên GitHub)

Việc của các đợt trước đã xong và đã merge. Nhánh cũ `feature/map-generation` đã bị xóa trên GitHub: **không commit lên nhánh cũ nữa**, mọi việc của đợt này làm trên `feature/hazard-rooms-lobby`.

Đọc lại [`Docs/Conventions.md`](../../Docs/Conventions.md) trước khi làm, nhất là các mục mới:
- 3: assembly `LightSouls.Lobby`, enum chỉ thêm vào cuối.
- 4.5: vật phẩm.
- 4.6: `RunState`.
- 4.7: địa hình nguy hiểm.
- 4.8: tương tác.
- 4.9: nút Back, Canvas.
- 5: layer `Hazard`, `Interactable`.
- 6: Scene thử.

## Việc đầu tiên: chuyển sang nhánh mới

Đóng Unity rồi chạy:

```bash
git fetch origin --prune
git checkout feature/hazard-rooms-lobby
git branch -d feature/map-generation
```

- Lệnh cuối xóa bản local của nhánh cũ. Nếu Git báo nhánh chưa merge, kiểm tra không còn commit nào chưa đẩy; nếu có, báo nhóm trưởng trước khi xóa.
- Mở lại Unity, kiểm tra console không có lỗi.
- Kiểm tra layer `Hazard` (11), `Interactable` (12) và NavMesh area `Water`, `Lava` đã có. Nhóm trưởng đã thêm sẵn, bạn không sửa `ProjectSettings`.

## Phần 1: Phòng quái có địa hình đặc biệt

Thêm các phòng chiến đấu mới có địa hình làm trận đánh khác đi. Tối thiểu bốn loại sau, thêm loại khác tùy bạn:

| Loại | Gợi ý cách làm |
| :--- | :--- |
| **Lava** | Đứng trong vùng bị trừ máu đều đặn, ví dụ 8 sát thương mỗi 0,5 s. NavMesh area `Lava` để quái né. |
| **Nước** | Đi chậm, ví dụ hệ số 0,5, qua `ISpeedModifiable`. NavMesh area `Water`. |
| **Bẫy** | Gai trồi lên theo nhịp, có báo trước khoảng 0,6 s (đổi màu, rung) rồi mới gây sát thương. Có thể thêm bẫy kích hoạt khi dẫm lên. |
| **Mê cung** | Phòng lớn có tường bên trong chia lối đi; quái sinh rải rác. |

Yêu cầu chung (mục 4.7 của quy ước):

- **Code và prefab:**
  - Code địa hình đặt trong `Assets/Scripts/MapGeneration/Hazards/`, assembly `LightSouls.MapGeneration`.
  - Prefab vùng địa hình: `Assets/Prefabs/MapGeneration/Hazards/Hazard_*.prefab`.
  - Prefab phòng mới: `Assets/Prefabs/MapGeneration/Rooms/Room_*.prefab`.
- **Gây hiệu ứng:**
  - Sát thương gọi `IDamageable.TakeDamage(new DamageInfo(lượng, gameObject, điểm trúng))`.
  - Làm chậm gọi `ISpeedModifiable.SetSpeedMultiplier(this, hệ số)` và `RemoveSpeedMultiplier(this)` khi rời vùng. Cũng gọi Remove khi vùng bị tắt hay hủy, để không ai bị chậm mãi.
  - Tìm hai interface này bằng `GetComponentInParent` trên collider. Áp dụng cho cả người chơi lẫn quái.
- **Collider:**
  - Vùng phát hiện là trigger ở layer `Hazard`, có `Rigidbody` `isKinematic`.
  - Sàn để đứng vẫn là collider riêng ở layer `Environment`.
- **Người chơi phải luôn có lối an toàn:**
  - Từ mỗi cửa tới mọi cửa khác có một đường đi không phải dẫm vào vùng nguy hiểm.
  - Điểm sinh quái và điểm sinh người chơi không nằm trong vùng nguy hiểm.
  - Ô cửa vẫn rộng 4 m, cao 3 m (mục 2).
- **Nhìn rõ:**
  - Mỗi loại phải nhận ra ngay bằng màu: lava cam phát sáng, nước xanh trong, gai xám.
  - Bẫy phải có báo trước. Người chơi không được chết vì thứ không nhìn thấy.
- **Loại phòng:**
  - Phòng mới vẫn là `RoomType.Combat` (hoặc `Normal`).
  - Không cần thêm giá trị `RoomType` mới. Nếu thật sự cần, thêm vào **cuối** enum với số tường minh (6, 7…), không đổi các giá trị đang có. `GameBootstrap` và bản đồ nhỏ đang dựa vào `Start`, `Combat`, `Treasure`, `Corridor`, `Exit`.
- **Đưa vào bộ sinh màn:**
  - Thêm phòng mới vào `DungeonGenerationConfigSO`.
  - Bộ sinh màn vẫn phải qua kiểm tra như đợt trước: không chồng lấn, có đường từ phòng đầu tới đích, NavMesh thông, kể cả qua phòng mê cung.
  - Giữ EditMode test qua và thêm test nếu được.
- **Phòng kho báu:** có ít nhất một `LootSpawnPoints`. Khi ghép, nhóm trưởng sẽ sinh vật phẩm rơi của Khải tại đó.

## Phần 2: Sảnh

Một màn chơi trước khi vào ngục. Người chơi đi lại trong sảnh, mua đồ và chọn ngục. Thiết kế cụ thể (bố cục, số quầy, cách chọn ngục) do bạn quyết định. Hợp đồng cần giữ:

- **Vị trí code:**
  - Code đặt trong `Assets/Scripts/Lobby/`, assembly mới `LightSouls.Lobby`, tham chiếu `LightSouls.Common` và `LightSouls.MapGeneration`.
  - Không tham chiếu `Combat`, `UI` hay `Game`.
  - Prefab: `Assets/Prefabs/Lobby/Lobby_*.prefab`; asset: `Assets/ScriptableObjects/Lobby/`.
- **Danh sách ngục** (trong `MapGeneration`):
  - Một asset kiểu `DungeonCatalogSO`, mỗi mục có `dungeonId`, tên hiện, mô tả, và `DungeonGenerationConfigSO` tương ứng. Ví dụ: ngục thường, ngục dung nham nhiều phòng lava, ngục mê cung.
  - `DungeonGenerator` thêm trường catalog: khi `RunState.SelectedDungeonId` khác rỗng và có trong catalog thì dùng config đó, ngược lại dùng `config` mặc định như bây giờ. Nhờ vậy Scene `Game` vẫn chạy khi vào thẳng.
  - Nhóm trưởng sẽ gán catalog vào `DungeonGenerator` trong Scene `Game` khi ghép.
- **Chọn ngục:**
  - Người chơi chọn ở sảnh (ví dụ cổng có bảng chọn), code ghi `RunState.SelectedDungeonId`.
  - Sau đó phát một sự kiện C#, ví dụ `LobbyController.DungeonChosen` (`Action<string>`).
  - **Không** tự nạp Scene: nhóm trưởng sẽ nghe sự kiện này để nạp `Game`. Trong Scene thử, bạn có thể nạp lại hoặc ghi log từ một script trong `Lobby/Testing/`.
- **Cửa hàng:**
  - Bán danh sách `ItemDefinitionSO` (ví dụ asset `ShopCatalogSO`), giá lấy từ `item.Price`.
  - Mua:
    1. Lấy `IInventory` trên người chơi (đối tượng tag `Player`).
    2. Kiểm tra `HasSpaceFor`.
    3. `RunState.TrySpendCoins(Price)`.
    4. `TryAdd`.

    Không đủ xu hay túi đầy thì báo rõ, không trừ xu.
  - Hiện số xu hiện có từ `RunState.Coins`, cập nhật theo `RunState.CoinsChanged`.
- **Tương tác:**
  - Quầy hàng và cổng chọn ngục cài đặt `IInteractable` (mục 4.8), collider trigger ở layer `Interactable`.
  - Không làm nút Tương tác phía người chơi; phần đó Khải làm.
- **Bảng giao diện** (cửa hàng, chọn ngục):
  - Canvas sort order 8.
  - Gọi `BackButtonStack.Push(this, Close)` khi mở và `Remove(this)` khi đóng, có nút đóng.
  - Nút cao tối thiểu 96 px, đặt trong `SafeAreaFitter`.
- **Phần thay thế:** `WeaponVendingMachine` và `BossRoomRewardController` (ngoài hợp đồng, mục 4.1) có thể xóa nếu cửa hàng ở sảnh thay thế chúng. Ghi trong PR.

### Thử khi phần của Khải chưa có

Khải làm túi đồ song song trên nhánh khác, nên trong lúc này chưa có vật phẩm thật hay `IInventory` trên người chơi. Đặt trong `Assets/Scripts/Lobby/Testing/`:

- Một lớp vật phẩm thử kế thừa `ItemDefinitionSO` và vài asset từ nó, để cửa hàng có hàng bán.
- Một túi đồ thử cài đặt `IInventory`, gắn lên người chơi trong Scene thử.
- Một script tương tác thử: phím `E` hoặc một nút trên màn hình gọi `Interact` của `IInteractable` gần nhất.
- Một nút hoặc phím cộng xu (`RunState.AddCoins`).

Cửa hàng chỉ dùng `ItemDefinitionSO` và `IInventory`, nên khi phần của Khải merge, chỉ cần đổi danh sách hàng sang vũ khí thật mà không phải sửa code.

## Thử nghiệm

- **Scene thử riêng:**
  - Phòng địa hình làm trong **`Assets/Scenes/HazardRoomsTest.unity`**.
  - Sảnh làm trong **`Assets/Scenes/LobbyTest.unity`**.
  - Asset riêng của Scene đặt trong thư mục cùng tên, ví dụ `Assets/Scenes/LobbyTest/`.
- **Được kéo prefab của Khải vào Scene thử của bạn**, nhưng không sửa các prefab đó. Ví dụ: `Player_Character`, `Combat_TouchControls`, và quái để thử quái né lava.
- **Không sửa:**
  - Scene `Game.unity`, `MainMenu.unity`, `MapGenTest` cũ (trừ khi để sửa lỗi), Scene của Khải.
  - Bất cứ file nào trong `Assets/Scripts/Common/`, `Assets/Scripts/Combat/`, `ProjectSettings/`.
  - Cần thêm gì vào `Common` thì nhắn nhóm trưởng.
- **Prototype builder:** không chạy lại `MapGenPrototypeBuilder` lên các prefab phòng đã có (mục 6 của quy ước). Phòng mới là prefab mới.
- **Device Simulator:** thử bằng một máy Android màn hình ngang. Kiểm tra:
  - Đi qua phòng nước bị chậm, ra khỏi thì hết chậm.
  - Đứng trên lava bị trừ máu.
  - Gai có báo trước.
  - Mua đồ trừ đúng xu.
  - Nút Back đóng bảng cửa hàng.

## Bàn giao

- **PR:** từ `feature/hazard-rooms-lobby` về `develop`. Ngay trước khi mở PR, merge `develop` mới nhất vào nhánh (mục 7 của quy ước).
- **PR sớm:** có thể mở PR sớm khi xong Phần 1, rồi làm tiếp Phần 2 trên cùng nhánh sau khi PR đó merge (nhớ merge `develop` về nhánh trước khi làm tiếp).
- **Mô tả PR ghi:**
  - Danh sách phòng mới và loại địa hình.
  - Giá trị sát thương và làm chậm.
  - `dungeonId` trong catalog.
  - Tên sự kiện chọn ngục.
  - Cách bố trí sảnh.
  - Các file đã xóa (nếu có).
- **Checklist:** tự kiểm tra theo checklist ở mục 7 của quy ước.

## Tiêu chí hoàn thành

- Có ít nhất bốn loại phòng địa hình đặc biệt (lava, nước, bẫy, mê cung) xuất hiện trong màn được sinh, mỗi loại đều có lối an toàn.
- Lava và bẫy gây sát thương cho cả người chơi và quái. Nước làm chậm cả hai, ra khỏi thì hết.
- Bộ sinh màn vẫn qua kiểm tra không chồng lấn, có đường tới đích, NavMesh thông; EditMode test qua.
- `LobbyTest`:
  - Mua được đồ bằng xu.
  - Chọn được ngục: ghi `RunState.SelectedDungeonId` và phát sự kiện.
  - Nút Back đóng bảng đang mở.
- `DungeonGenerator` dùng đúng config theo `SelectedDungeonId`; chuỗi rỗng thì dùng config mặc định.

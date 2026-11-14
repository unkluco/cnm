# Khải — Túi đồ, thanh nhanh và vật phẩm (2026-11-14)

**Nhánh: `feature/inventory-hotbar`** (nhánh mới, đã được tách từ `develop` mới nhất và đẩy lên GitHub)

Việc của các đợt trước đã xong và đã merge. Nhánh cũ `feature/character-combat` đã bị xóa trên GitHub: **không commit lên nhánh cũ nữa**, mọi việc của đợt này làm trên `feature/inventory-hotbar`.

Đọc lại [`Docs/Conventions.md`](../../Docs/Conventions.md) trước khi làm, nhất là các mục mới: 3 (quy tắc chung), 4.5 (vật phẩm), 4.6 (`RunState`), 4.8 (tương tác), 4.9 (nút Back, Canvas, bố cục HUD), 5 (input), 6 (Scene thử).

## Việc đầu tiên: chuyển sang nhánh mới

Đóng Unity rồi chạy:

```bash
git fetch origin --prune
git checkout feature/inventory-hotbar
git branch -d feature/character-combat
```

- Lệnh cuối xóa bản local của nhánh cũ. Nếu Git báo nhánh chưa merge, kiểm tra lại không còn commit nào chưa đẩy; nếu có, báo nhóm trưởng trước khi xóa.
- Mở lại Unity, kiểm tra console không có lỗi và đã có các file mới trong `Assets/Scripts/Common/`: `ItemDefinitionSO`, `IInventory`, `RunState`, `IInteractable`, `ISpeedModifiable`, `BackButtonStack`.

## Mục tiêu

Bỏ kiểu cầm vũ khí rồi bấm nút đổi. Thay bằng túi đồ giống Minecraft:
- **Thanh nhanh** 5 ô ở giữa cạnh dưới HUD. Chạm vào ô nào thì cầm món trong ô đó ngay, đổi qua lại liên tục được.
- **Ô thứ 6, ngoài cùng**, có hình dấu ba chấm `…`. Chạm vào thì mở **balo** lớn hơn.
- **Ô trang bị** gắn lên người.

Thứ tự ưu tiên: vũ khí trước, rồi trang bị, cuối cùng đồ dùng.

## Việc cần làm

### 1. Vật phẩm

- `WeaponDataSO` kế thừa `LightSouls.Common.ItemDefinitionSO` (thay cho `ScriptableObject`), `Category` trả về `ItemCategory.Weapon`.
- Điền `itemId` cho 4 vũ khí đang có, dạng `weapon.<tên>`, ví dụ `weapon.iron_sword`. Mã này không đổi về sau.
- Asset vật phẩm đặt trong `Assets/ScriptableObjects/Combat/Items/`; vũ khí cũ trong `Weapons/` có thể chuyển sang đó (chuyển trong Unity để giữ GUID).
- Làm thêm vài vũ khí mới, ưu tiên khác lối chơi (ví dụ giáo đâm xa, cung bắn chậm sát thương cao, dao đánh nhanh).
- Mỗi vật phẩm cần `Icon` để hiện trên ô. Ảnh đặt trong `Assets/Sprites/Items/`; tạm dùng hình đơn giản cũng được.
- Làm một asset danh mục, ví dụ `ItemDatabaseSO`, chứa mọi vật phẩm và tra cứu theo `ItemId`. Trong Editor cảnh báo khi có `ItemId` trống hoặc trùng.
- Sau vũ khí:
  - Trang bị: lớp con `ItemCategory.Equipment`, ví dụ giáp giảm sát thương, giày tăng tốc. Giày nên dùng chính `ISpeedModifiable` của người chơi.
  - Đồ dùng: lớp con `ItemCategory.Consumable`, ví dụ bình máu, xếp chồng được (`MaxStack` > 1).

### 2. Túi đồ

- Component túi đồ trên đối tượng gốc của `Player_Character`, cài đặt `LightSouls.Common.IInventory` (mục 4.5).
  - `TryAdd`/`TryRemove` làm đủ hoặc không làm gì.
  - Phát `Changed` sau mỗi thay đổi.
- Gồm: 5 ô thanh nhanh, balo, và các ô trang bị. Kích thước balo và loại ô trang bị do bạn quyết định; ghi lại trong PR.
- **Lưu qua Scene:**
  - Mỗi lần túi đồ đổi, ghi toàn bộ vào `RunState.SaveInventory(...)` dưới dạng `ItemSlotRecord`. `SlotKey` do bạn đặt, ví dụ `hotbar/0`, `backpack/7`, `equip/armor`.
  - Khi người chơi được sinh ra, đọc lại `RunState.InventorySlots` và tra `ItemId` qua danh mục.
  - Nếu `RunState.InventorySlots` rỗng (vào thẳng Scene thử, hoặc lượt mới), dùng bộ đồ khởi đầu gán trên prefab, ví dụ một thanh kiếm.
- Thay `weaponInventory` trong `PlayerCombat` bằng túi đồ:
  - Món đang chọn trên thanh nhanh là món đang cầm.
  - Ô chọn là vũ khí: nút Đánh dùng vũ khí đó.
  - Ô chọn là đồ dùng: nút Đánh dùng món đó (ví dụ uống bình máu), hoặc theo cách bạn thấy hợp hơn.
  - Ô trống: đánh tay không hoặc không làm gì.
- Giữ `PlayerController.OnWeaponChanged` và các tên khác trong bảng ở mục 4.4 của quy ước, vì phần lắp ráp đang gọi. `NextWeapon` và `SwitchWeapon()` có thể bỏ nếu chỉ code của bạn dùng.

### 3. Thanh nhanh và balo (giao diện)

- **Thanh nhanh:**
  - 5 ô và ô `…` ở giữa cạnh dưới, trong `SafeArea` của `Combat_TouchControls` (sort order 5), để `GameBootstrap` ẩn cả cụm khi hết game.
  - Không đè lên cần analog (góc trái dưới) hay nút đánh và lướt (góc phải dưới). Dời các nút đó nếu cần, nhưng vẫn giữ ở hai góc.
- **Ô trên thanh nhanh:**
  - Cao tối thiểu 96 px trên khung 1920×1080.
  - Có icon, số lượng (nếu > 1) và viền sáng cho ô đang chọn.
  - Chạm là chọn ngay, bằng uGUI `Button`, không qua action của map `Player`.
- **Balo:**
  - Một bảng riêng, ví dụ prefab `Combat_Backpack`, Canvas sort order 8.
  - Hiện các ô balo, ô trang bị và cả thanh nhanh.
  - Chuyển đồ giữa các ô bằng chạm: chạm một ô rồi chạm ô đích, hoặc kéo thả. Phải dùng được bằng ngón tay trên Device Simulator.
- **Khi balo mở** (mục 4.9 của quy ước):
  - Gọi `BackButtonStack.Push(this, Close)`, đóng thì `Remove(this)`.
  - Tắt điều khiển nhân vật, để chạm vào balo không làm nhân vật đánh.
  - Có nút đóng trên bảng.
  - Nút Back (Esc) đóng balo trước khi mở menu tạm dừng. `PauseMenuController` đã xử lý phần này, bạn không cần nghe `UI/Cancel`.
- `CombatHUD` không cần hiện "vũ khí tiếp theo" nữa. Sửa cho khớp với thanh nhanh.

### 4. Input (bạn sở hữu `InputSystem_Actions.inputactions`)

- Bỏ action `SwitchWeapon` cùng nút cảm ứng đổi vũ khí.
- `Interact`:
  - Dùng `<Gamepad>/buttonNorth` (bỏ interaction `Hold`) và phím `E`.
  - Có nút cảm ứng riêng ở góc phải dưới. Nút chỉ hiện khi có vật để tương tác, kèm chữ `Prompt`.
- Phím thử trong Editor:
  - `1`–`5` chọn ô thanh nhanh, `Tab` mở/đóng balo.
  - Gỡ binding `1`/`2` của `Previous`/`Next`.
- Dọn các action mẫu không dùng (`Look`, `Jump`, `Crouch`, `Sprint`, `Previous`, `Next`): `Jump` đang trùng `buttonSouth` và `Crouch` trùng `buttonEast`, là hai nút đã thuộc map `UI`.
- Cập nhật bảng nút tay cầm ở mục 5 của quy ước trong mô tả PR. Nhóm trưởng sẽ sửa file quy ước.

### 5. Tương tác và vật phẩm rơi

- Phía người chơi của `IInteractable` (mục 4.8), chỉ một bản dùng chung cho cả nhóm:
  - Tìm vật gần nhất trong khoảng 2 m trên layer `Interactable`, có `CanInteract` là true.
  - Hiện nút Tương tác với chữ `Prompt`, bấm thì gọi `Interact(gameObject của người chơi)`.
- Prefab `Item_Pickup`:
  - Vật phẩm nằm trên đất, cài đặt `IInteractable` (`Prompt` = "Nhặt").
  - Collider trigger ở layer `Interactable`.
  - Hàm công khai để sinh từ code, ví dụ `ItemPickup.Spawn(ItemDefinitionSO item, int count, Vector3 position)`.
  - Nhặt thì gọi `IInventory.TryAdd`. Không đủ chỗ thì để nguyên trên đất và báo "Túi đầy".
- Bảng tỉ lệ rơi đồ, ví dụ `LootTableSO`: danh sách vật phẩm kèm trọng số, có hàm chọn ngẫu nhiên.
- Khi ghép, `GameBootstrap` sẽ sinh `Item_Pickup` tại `LootSpawnPoints` của phòng kho báu bằng bảng này. Bạn không sửa Scene `Game`.

### 6. Tốc độ (chuẩn bị cho địa hình của Đạt)

- `PlayerMovement` và quái (`EnemyBase`) cài đặt `LightSouls.Common.ISpeedModifiable`.
  - Tốc độ thật = tốc độ gốc × tích mọi hệ số đang có.
  - Với quái, nhân vào `NavMeshAgent.speed`.
- Đạt sẽ gọi interface này từ vùng nước và bùn. Bạn chỉ cài đặt, không làm địa hình.

## Thử nghiệm

- Làm trong Scene thử mới **`Assets/Scenes/InventoryTest.unity`**. Asset riêng của Scene (NavMesh bake…) đặt trong `Assets/Scenes/InventoryTest/`.
- **Không sửa** `Game.unity`, `MainMenu.unity`, Scene của Đạt, hay bất cứ file nào trong `Assets/Scripts/Common/`, `Assets/Scripts/MapGeneration/`, `ProjectSettings/`.
  - Cần thêm gì vào `Common` thì nhắn nhóm trưởng.
  - Đặt `Item_Pickup` trong Scene thử để kiểm tra nhặt đồ.
- Giữ `CharacterCombatTest` chạy được, hoặc ghi trong PR nếu đã thay hẳn bằng `InventoryTest`.
- Không chạy lại `CombatPrototypeBuilder` lên `Player_Character` hay `Combat_TouchControls` (mục 6 của quy ước). Prefab trong `develop` đã được sửa tay khi ghép: thứ tự vũ khí, các `InputActionReference`, NavMesh lưu thành asset. Sửa prefab trực tiếp trong Unity.
- Thử chuyển Scene: thêm tạm một nút trong `Combat/Testing/` nạp lại `InventoryTest`, kiểm tra túi đồ giữ nguyên nhờ `RunState`.
- Thử bằng Device Simulator, máy Android có tai thỏ, màn hình ngang:
  - Chọn ô trên thanh nhanh bằng chạm.
  - Mở balo và chuyển đồ giữa các ô.
  - Nhặt đồ bằng nút Tương tác.

## Bàn giao

- PR từ `feature/inventory-hotbar` về `develop`. Ngay trước khi mở PR, merge `develop` mới nhất vào nhánh (mục 7 của quy ước).
- Có thể mở PR sớm (draft) khi xong phần vũ khí + thanh nhanh để nhóm trưởng xem trước, rồi làm tiếp trang bị và đồ dùng trên cùng nhánh.
- Mô tả PR ghi:
  - Kích thước balo và các ô trang bị.
  - Cách sinh `Item_Pickup` và dùng `LootTableSO` từ code.
  - Danh sách `ItemId` đã tạo.
  - Các tên công khai đã bỏ hoặc đổi, và bảng nút tay cầm mới.
- Tự kiểm tra theo checklist ở mục 7 của quy ước.

## Tiêu chí hoàn thành

- Không còn nút đổi vũ khí. Chọn vũ khí bằng thanh nhanh 5 ô, đổi liên tục được.
- Ô `…` mở balo; Back đóng balo mà không mở menu tạm dừng.
- Có ít nhất một loại trang bị gắn vào ô trang bị và có tác dụng.
- Túi đồ giữ nguyên khi nạp lại Scene.
- `Item_Pickup` nhặt được bằng nút Tương tác.
- Scene `Game` của `develop` vẫn chạy được khi merge nhánh vào (nhóm trưởng sẽ kiểm tra).

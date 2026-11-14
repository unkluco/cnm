# Tài liệu Bàn giao Kỹ thuật: Hệ thống Túi đồ, Thanh nhanh & Vật phẩm (Inventory & Hotbar)

- **Người thực hiện:** Khải (`Khai810`)
- **Nhánh Git:** `feature/inventory-hotbar`
- **Nhánh đích:** `develop`
- **Ngày hoàn thành:** 14/11/2026
- **Scene kiểm thử:** `Assets/Scenes/InventoryTest.unity`
- **Mã nguồn chính:** `Assets/Scripts/Combat/` (Thư mục con `Inventory/`, `Items/`, `UI/`)

---

## 1. Tổng quan Kiến trúc Hệ thống

Hệ thống Túi đồ & Thanh nhanh được tái cấu trúc hoàn chỉnh theo mô hình **Data-Driven & Event-Driven**, thay thế hoàn toàn cơ chế đổi vũ khí cũ bằng thanh nhanh 5 ô trực quan phong cách mobile roguelike:

1. **Vật phẩm Data-Driven (`ItemDefinitionSO`)**: Mọi vật phẩm (Vũ khí, Trang bị, Đồ tiêu hao) đều kế thừa lớp gốc `LightSouls.Common.ItemDefinitionSO`, định danh bằng chuỗi `ItemId` bất biến và quản lý tập trung qua `ItemDatabaseSO`.
2. **Túi đồ (`PlayerInventory`)**: Cài đặt interface chuẩn `LightSouls.Common.IInventory`. Quản lý 5 ô thanh nhanh (`hotbar/0..4`), 15 ô balo (`backpack/0..14`) và 2 ô trang bị (`equip/armor`, `equip/boots`).
3. **Lưu trữ qua Scene (`RunState`)**: Tự động lưu và đọc dữ liệu túi đồ qua các Scene (`sảnh → ngục → sảnh`) bằng `RunState.SaveInventory` và `RunState.InventorySlots` (`ItemSlotRecord`).
4. **Vật phẩm rơi & Tương tác (`ItemPickup`)**: Cài đặt `LightSouls.Common.IInteractable`, hỗ trợ hàm tĩnh `ItemPickup.Spawn(...)` để sinh vật phẩm rơi trong thế giới thực.
5. **Điều biến tốc độ (`ISpeedModifiable`)**: Cài đặt trên cả người chơi và quái vật, sẵn sàng kết nối với vùng làm chậm (bùn/nước) của Đạt.
6. **Giao diện Cảm ứng Di động (Mobile Touch UI)**:
   - **Thanh nhanh (`HotbarUI`)**: 5 ô và 1 nút `…` mở balo tại mép dưới chính giữa màn hình (Canvas sort order 5, `SafeAreaFitter`), ô cao $\ge 96\text{ px}$.
   - **Balo (`BackpackUI`)**: Prefab riêng `Combat_Backpack` (Canvas sort order 8). Hỗ trợ kéo thả item (phóng to $1.3\times$, bám theo con trỏ) và chạm chuyển đồ (tap-to-swap).
   - **Nút Vứt đồ hình chữ nhật**: Nút màu đỏ nổi bật ($280\times 56\text{ px}$) trong bảng chi tiết, cho phép vứt item ra sàn ngay tại vị trí chân nhân vật.
   - **Nút Tương tác đổi Icon động**: Tự động hiển thị Icon của vũ khí/vật phẩm gần nhất khi cách $\le 2\text{m}$.
   - **Bố cục chống chồng lấn**: Dời nút Lướt (Dash) sang `(-285, 135)` và nút Nhặt (Interact) sang `(-135, 305)`, giải quyết triệt để vấn đề dính chùm nút và không che khuất UI thành viên khác.

---

## 2. Toàn Bộ Các Hàm & API Cần Thiết Cho Người Khác Sử Dụng

### 2.1. Thao Tác Với Túi Đồ Qua Interface `IInventory`
Interface chuẩn dùng chung trong `LightSouls.Common`. Cửa hàng của Kiệt, phần thưởng phòng của Đạt, rương báu hoặc các hệ thống khác muốn tương tác với túi đồ người chơi **chỉ cần lấy `IInventory` trên GameObject của Player**:

```csharp
namespace LightSouls.Common
{
    public interface IInventory
    {
        // Kiểm tra túi đồ còn đủ sức chứa cho số lượng item nhất định không
        bool HasSpaceFor(ItemDefinitionSO item, int count = 1);

        // Thử thêm vật phẩm vào túi đồ (tự động xếp chồng nếu có thể, hoặc tìm ô trống đầu tiên)
        // Quy ước: làm đủ hoặc không làm gì (trả về false nếu không đủ chỗ)
        bool TryAdd(ItemDefinitionSO item, int count = 1);

        // Thử bớt vật phẩm khỏi túi đồ theo số lượng
        // Quy ước: làm đủ hoặc không làm gì (trả về false nếu trong túi không đủ số lượng)
        bool TryRemove(ItemDefinitionSO item, int count = 1);

        // Lấy tổng số lượng của vật phẩm hiện có trong toàn bộ túi đồ
        int GetCount(ItemDefinitionSO item);

        // Sự kiện phát sau mỗi lần dữ liệu túi đồ thay đổi (thêm, bớt, di chuyển, đổi trang bị)
        event System.Action Changed;
    }
}
```

---

### 2.2. Các Hàm Mở Rộng Trên `PlayerInventory` (`LightSouls.Combat.Inventory.PlayerInventory`)
Thành phần cụ thể gắn trên `Player_Character`. Cung cấp các hàm nâng cao cho gameplay chiến đấu và giao diện:

| Tên Hàm / Thuộc Tính | Cú pháp | Ý nghĩa & Vai trò | Người gọi |
| :--- | :--- | :--- | :--- |
| `SelectedHotbarIndex` | `int` (get) | Chỉ số ô hiện đang chọn trên thanh nhanh (0 đến 4). | UI Hotbar |
| `SelectedItem` | `ItemDefinitionSO` (get) | Vật phẩm trong ô thanh nhanh đang chọn (có thể là null nếu ô trống). | Combat, UI |
| `SelectedWeapon` | `WeaponDataSO` (get) | Vũ khí đang chọn trên thanh nhanh (null nếu đang chọn potion hoặc ô trống). | `PlayerCombat` |
| `EquippedArmor` | `EquipmentDataSO` (get) | Giáp hiện đang trang bị trong ô `equip/armor`. | Hệ thống phòng thủ |
| `EquippedBoots` | `EquipmentDataSO` (get) | Giày hiện đang trang bị trong ô `equip/boots`. | Hệ thống di chuyển |
| `CurrentDamageReduction`| `float` (get) | Hệ số giảm sát thương nội tại từ giáp (ví dụ 0.20 = giảm 20%). | `HealthComponent` |
| `CurrentSpeedMultiplier`| `float` (get) | Hệ số tăng tốc nội tại từ giày (ví dụ 1.30 = tăng 30%). | `PlayerMovement` |
| `SelectHotbarSlot(int index)` | `void SelectHotbarSlot(int)` | Chọn ô thanh nhanh theo chỉ số (0 đến 4). Tự động trang bị vũ khí tương ứng lên tay. | HotbarUI, Phím 1–5 |
| `GetSlot(string slotKey)` | `InventorySlot GetSlot(string)` | Lấy dữ liệu chi tiết ô đồ theo khóa (ví dụ `hotbar/0`, `backpack/4`, `equip/armor`). | BackpackUI |
| `TryRemoveFromSlot(key, count)` | `bool TryRemoveFromSlot(string, int)`| Bớt số lượng vật phẩm trực tiếp từ một ô xác định. | BackpackUI khi Vứt đồ |
| `SwapOrMoveSlots(fromKey, toKey)`| `bool SwapOrMoveSlots(string, string)`| Hoán đổi hoặc di chuyển đồ giữa 2 ô bất kỳ (Balo $\leftrightarrow$ Hotbar $\leftrightarrow$ Trang bị). | Kéo-thả, Chạm đổi |
| `OnHotbarSelectionChanged` | `event Action<int, ItemDefinitionSO>` | Phát khi người chơi đổi ô thanh nhanh đang chọn. | HUD, Combat |

---

### 2.3. Sinh Vật Phẩm Rơi Trong Thế Giới Thực: `ItemPickup.Spawn`
Hàm tĩnh công khai quan trọng bậc nhất cho việc sinh vật phẩm rơi trong Scene:

```csharp
namespace LightSouls.Combat.Items
{
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        /// <summary>
        /// Sinh một vật phẩm rơi tại toạ độ chỉ định trong thế giới thực.
        /// Tự động gán Collider Trigger Layer Interactable (12), hiệu ứng lơ lửng bồng bềnh và xoay tròn.
        /// </summary>
        /// <param name="item">Asset vật phẩm (kế thừa ItemDefinitionSO)</param>
        /// <param name="count">Số lượng vật phẩm</param>
        /// <param name="position">Toạ độ sinh (trục Y sẽ tự nâng +0.4m trên mặt sàn)</param>
        /// <returns>Component ItemPickup vừa sinh</returns>
        public static ItemPickup Spawn(ItemDefinitionSO item, int count, Vector3 position);
    }
}
```

---

### 2.4. Tra Cứu Vật Phẩm: `ItemDatabaseSO` (`LightSouls.Combat.Items.ItemDatabaseSO`)
Asset danh mục chứa toàn bộ vật phẩm trong game (`Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset`):

| Tên Hàm / Thuộc Tính | Cú pháp | Ý nghĩa & Cách dùng |
| :--- | :--- | :--- |
| `GetItem(string itemId)` | `ItemDefinitionSO GetItem(string itemId)` | Tra cứu vật phẩm theo mã định danh (ví dụ `"weapon.iron_sword"`, `"armor.leather_vest"`). Trả về null nếu không tìm thấy. |
| `GetItem<T>(string itemId)` | `T GetItem<T>(string itemId) where T : ItemDefinitionSO` | Tra cứu và ép kiểu sang lớp con cụ thể (ví dụ `db.GetItem<WeaponDataSO>("weapon.dagger")`). |
| `AllItems` | `IReadOnlyList<ItemDefinitionSO>` | Danh sách toàn bộ các vật phẩm đã đăng ký trong dự án. |

---

### 2.5. Bảng Tỉ Lệ Rơi Đồ: `LootTableSO` (`LightSouls.Combat.Items.LootTableSO`)
Asset quản lý tỉ lệ rơi đồ ngẫu nhiên (`Assets/ScriptableObjects/Combat/Items/LootTable_Default.asset`):

| Tên Hàm | Cú pháp | Ý nghĩa |
| :--- | :--- | :--- |
| `GetRandomItem()` | `ItemDefinitionSO GetRandomItem()` | Chọn ngẫu nhiên 1 vật phẩm theo trọng số (Weight). |
| `GetRandomDrop()` | `LootDropResult GetRandomDrop()` | Chọn ngẫu nhiên 1 vật phẩm kèm số lượng ngẫu nhiên trong khoảng `[MinCount, MaxCount]`. |

---

### 2.6. Điều Biến Tốc Độ: `ISpeedModifiable` (`LightSouls.Common.ISpeedModifiable`)
Cài đặt trên đối tượng người chơi (`PlayerMovement`) và quái vật (`EnemyBase`). Dành riêng cho Đạt khi thiết kế các vùng bẫy bùn, mặt nước:

```csharp
namespace LightSouls.Common
{
    public interface ISpeedModifiable
    {
        // Gán hệ số làm chậm / tăng tốc từ nguồn source (ví dụ 0.5f = giảm 50% tốc độ)
        void SetSpeedMultiplier(object source, float multiplier);

        // Gỡ bỏ hệ số tốc độ khi rời khỏi vùng ảnh hưởng của source
        void RemoveSpeedMultiplier(object source);

        // Hệ số tốc độ tổng hợp hiện tại (tích các hệ số đang có)
        float CurrentSpeedMultiplier { get; }
    }
}
```

---

### 2.7. Quản Lý Nút Back / Esc: `BackButtonStack` (`LightSouls.Common.BackButtonStack`)
Hệ thống ngăn xếp tĩnh xử lý phím Back trên Android / phím Esc trên PC:

| Tên Hàm | Cú pháp | Ý nghĩa |
| :--- | :--- | :--- |
| `Push(object owner, Action onBack)` | `BackButtonStack.Push(this, Close)` | Đăng ký callback đóng bảng khi người chơi nhấn nút Back. Bảng đăng ký sau sẽ được đóng trước. |
| `Remove(object owner)` | `BackButtonStack.Remove(this)` | Hủy đăng ký khi bảng đã đóng (bằng nút `✕` hoặc click ra ngoài). |

---

## 3. Danh Mục `ItemId` Đã Tạo

| `ItemId` | Tên Hiển Thị | Phân Loại (`Category`) | Đặc tính & Chỉ số |
| :--- | :--- | :--- | :--- |
| `weapon.iron_sword` | Kiếm Sắt | `ItemCategory.Weapon` | Cận chiến cân bằng, Sát thương: 25, Tốc đánh: 0.4s, Tầm: 2.2m |
| `weapon.greatsword` | Đại Kiếm | `ItemCategory.Weapon` | Sát thương cực cao: 55, Tốc đánh chậm: 0.85s, Tầm rộng: 2.8m |
| `weapon.spear` | Thương Sắt | `ItemCategory.Weapon` | Đòn đâm xa nhất: 3.2m, Sát thương: 30, Tốc đánh: 0.5s |
| `weapon.dagger` | Dao Găm | `ItemCategory.Weapon` | Tốc độ tấn công cực nhanh: 0.18s, Sát thương: 12, Tầm: 1.5m |
| `weapon.pistol` | Súng Lục | `ItemCategory.Weapon` | Bắn tầm xa: 14m, Sát thương: 18, Tốc bắn: 0.35s |
| `weapon.shotgun` | Súng Săn | `ItemCategory.Weapon` | Bắn chùm 5 viên đạn, Sát thương: 10/viên, Tốc bắn: 0.75s |
| `armor.leather_vest` | Áo Giáp Da | `ItemCategory.Equipment` | Ô `equip/armor`: Giảm **20%** sát thương nhận vào |
| `boots.swift_boots` | Giày Tốc Hành | `ItemCategory.Equipment` | Ô `equip/boots`: Tăng **30%** tốc độ chạy qua `ISpeedModifiable` |
| `consumable.health_potion` | Bình Máu | `ItemCategory.Consumable` | Tiêu hao: Hồi ngay **40 HP**, Xếp chồng tối đa 10 bình/ô |

---

## 4. Hướng Dẫn Code Mẫu Cho Các Thành Viên Khác

### 4.1. Dành Cho Kiệt (Sảnh & Cửa Hàng - `LobbyShop`):
Khi người chơi mua một món đồ trong Cửa hàng:
```csharp
using LightSouls.Common;

public void OnBuyItemClicked(ItemDefinitionSO itemToBuy, GameObject playerObj)
{
    IInventory inventory = playerObj.GetComponent<IInventory>();
    if (inventory == null) return;

    // 1. Kiểm tra túi đồ còn chỗ chứa không
    if (!inventory.HasSpaceFor(itemToBuy, 1))
    {
        Debug.LogWarning("Túi đồ đã đầy! Không thể mua.");
        return;
    }

    // 2. Trừ tiền qua RunState
    if (RunState.TrySpendCoins(itemToBuy.Price))
    {
        // 3. Thêm đồ vào túi người chơi
        inventory.TryAdd(itemToBuy, 1);
        Debug.Log($"Mua thành công {itemToBuy.DisplayName}!");
    }
}
```

---

### 4.2. Dành Cho Đạt (Sinh Vật Phẩm Rơi Trong Phòng Kho Báu - `RoomModule`):
Khi khởi tạo phòng kho báu (`Room_Treasure`) hoặc khi mở rương báu:
```csharp
using LightSouls.Combat.Items;

public void SpawnTreasureLoot(Transform lootPoint, LootTableSO lootTable)
{
    // Lấy 1 vật phẩm ngẫu nhiên từ bảng tỉ lệ rơi đồ
    var dropResult = lootTable.GetRandomDrop();
    if (dropResult.Item != null)
    {
        // Sinh vật phẩm rơi lơ lửng tại điểm đặt hòm báu
        ItemPickup.Spawn(dropResult.Item, dropResult.Count, lootPoint.position);
    }
}
```

---

### 4.3. Dành Cho Đạt (Vùng Bùn / Nước Làm Chậm Nhân Vật):
Gắn trên Trigger Collider của vùng bùn (Layer `Hazard`):
```csharp
using UnityEngine;
using LightSouls.Common;

public class MudHazardArea : MonoBehaviour
{
    [SerializeField] private float slowMultiplier = 0.5f; // Giảm 50% tốc độ

    private void OnTriggerEnter(Collider other)
    {
        ISpeedModifiable speedTarget = other.GetComponentInParent<ISpeedModifiable>();
        if (speedTarget != null)
        {
            speedTarget.SetSpeedMultiplier(this, slowMultiplier);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ISpeedModifiable speedTarget = other.GetComponentInParent<ISpeedModifiable>();
        if (speedTarget != null)
        {
            speedTarget.RemoveSpeedMultiplier(this);
        }
    }
}
```

---

## 5. Bảng Phân Bổ Nút Điều Khiển & Phím Tắt

| Thao tác | Phím Bàn Phím | Nút Tay Cầm (Gamepad) | Nút Cảm Ứng Màn Hình (Touch UI) | Ghi Chú |
| :--- | :--- | :--- | :--- | :--- |
| **Di chuyển (Move)** | WASD / Mũi tên | `<Gamepad>/leftStick` | Cần Analog ảo (góc trái dưới) | Vector2 |
| **Đánh / Dùng đồ (Attack)** | Space | `<Gamepad>/buttonWest` | Nút tròn Đánh `(-135, 135)` | Không dùng `buttonSouth` |
| **Lướt né đòn (Dash)** | Left Shift | `<Gamepad>/rightTrigger` | Nút Lướt `(-285, 135)` | Dời sang trái nút Đánh |
| **Tương tác / Nhặt đồ (Interact)** | E | `<Gamepad>/buttonNorth` | Nút Nhặt `(-135, 305)` | Đổi Icon động theo đồ gần |
| **Chọn ô thanh nhanh 1–5** | `1`, `2`, `3`, `4`, `5` | — | Chạm trực tiếp ô trên Hotbar | Button uGUI, kích thước 100px |
| **Bật/Tắt Balo (Backpack)** | Tab | — | Nút `…` trên Hotbar | Ô thứ 6 trên thanh nhanh |
| **Vứt vật phẩm ra sàn** | — | — | Nút chữ nhật đỏ "Vứt vật phẩm" | Trong bảng chi tiết Balo |
| **Đóng bảng trên cùng (Back)** | Escape | `<Gamepad>/buttonEast` | Nút `✕` trên bảng | Quản lý qua `BackButtonStack` |
| **Xác nhận UI (Submit)** | Enter | `<Gamepad>/buttonSouth` | Chạm UI | Thuộc action map `UI` |

---

## 6. Danh Sách Prefab & Asset Bàn Giao

| Tên Asset / Prefab | Đường Dẫn | Ghi Chú Kỹ Thuật |
| :--- | :--- | :--- |
| `Combat_Backpack.prefab` | `Assets/Prefabs/Combat/Combat_Backpack.prefab` | Canvas sort order 8, 22 ô đồ, kéo thả $1.3\times$, nút Vứt đồ |
| `Item_Pickup.prefab` | `Assets/Prefabs/Combat/Item_Pickup.prefab` | Layer 12 `Interactable`, SphereTrigger $r=0.6\text{m}$, hiệu ứng xoay |
| `Combat_TouchControls.prefab` | `Assets/Prefabs/Combat/Combat_TouchControls.prefab` | Canvas sort order 5, Hotbar 5 ô + `…`, nút Interact đổi icon |
| `ItemDatabase.asset` | `Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset` | Danh mục tổng hợp 9 vật phẩm |
| `LootTable_Default.asset` | `Assets/ScriptableObjects/Combat/Items/LootTable_Default.asset` | Bảng tỉ lệ rơi đồ mặc định |
| `InventoryTest.unity` | `Assets/Scenes/InventoryTest.unity` | Scene kiểm thử toàn diện kèm NavMesh asset riêng |
| `NavMesh-Floor_Inventory.asset`| `Assets/Scenes/InventoryTest/NavMesh-Floor_Inventory.asset` | Dữ liệu NavMesh tách riêng dạng asset |

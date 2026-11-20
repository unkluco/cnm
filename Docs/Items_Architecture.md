# Vật phẩm, độ hiếm, hiệu ứng và hòm kho báu

Tài liệu này mô tả cách hệ thống vật phẩm được chia lớp và **cách thêm nội dung mới mà không phải sửa code cũ**.

## 1. Tổng quan các lớp

| Lớp | Assembly | Vai trò |
|---|---|---|
| `ItemDefinitionSO` | Common | Gốc của mọi vật phẩm: id, tên, mô tả, icon, giá, `MaxStack`, `Rarity`. `GetStatsText()` (virtual) trả chỉ số để hiện ở bảng chi tiết. |
| `ItemRarity` / `ItemRarityStyle` | Common | 4 bậc Thường, Hiếm, Sử thi, Huyền thoại; màu, tên tiếng Việt, có phát sáng hay không. |
| `RarityFrame` | Common | `RarityFrame.Apply(slotRect, item)`: vẽ viền màu độ hiếm quanh một ô UI (Sử thi trở lên nhấp nháy). Dùng ở thanh nhanh, balo, cửa hàng, hòm. |
| `ItemContainer` / `ItemStack` | Common | Kho có số ô cố định (C# thuần): `Put`, `RemoveAt`, `TransferTo(index, IInventory)`, `SpaceFor`. Hòm kho báu dùng nó; sau này rương ở sảnh hay thùng đồ NPC dùng lại được. |
| `WeaponDataSO` | Combat | Vũ khí cận chiến / tầm xa, đạn xuyên, đạn nổ, nhiều nhát chém, `hitEffects`, `vfx`. |
| `EquipmentDataSO` | Combat | Giáp / giày: giảm sát thương, tốc độ, và danh sách `extraModifiers` (StatModifier). |
| `ConsumableDataSO` | Combat | Đồ dùng: danh sách `effects` (ConsumableEffectSO), `triggersOnDeath`. |
| `CraftMaterialSO` / `CraftingCatalogSO` | Common | Nguyên liệu có nguồn rơi, độ hiếm và ngăn riêng; danh mục công thức chế tạo, nâng cấp, giả kim và tinh luyện. |
| `ICraftingInventory` | Common | Giao dịch kiểm tra và chế tạo toàn vẹn; lò rèn ở Lobby dùng giao diện này để không phụ thuộc Combat. |
| `PlayerStats` | Combat | Gộp mọi hệ số theo nguồn (trang bị, thuốc có thời hạn...). Hệ số cuối = tích các nguồn. |

Đường đi sát thương duy nhất của vũ khí người chơi: `WeaponHitResolver.Apply` (nhân hệ số sát thương của người đánh, gây sát thương, hiệu ứng trúng đòn, hạt va chạm).

## 2. Thêm một vật phẩm mới

1. Tạo asset: chuột phải > Create > LightSouls > Combat > Weapon Data / Equipment / Consumable.
2. Điền `itemId` (duy nhất), tên, mô tả, icon, giá, `MaxStack`, **Rarity**.
3. Thêm vào `ItemDatabase.asset` (để lưu / nạp túi đồ theo id).
4. Muốn rơi trong ngục: thêm dòng vào `LootTable.asset` (có thể đặt `FavoredDungeonId` + `FavoredMultiplier` để hay rơi ở một ngục).
5. Muốn bán ở sảnh: thêm vào `ShopCatalog_Main.asset`.

Không cần sửa code giao diện: viền độ hiếm, tên màu và chỉ số tự hiện.

Bộ vật phẩm gốc được mở rộng bằng menu **LightSouls > Items > Build Item Set** (`ItemSetBuilder`). Biểu tượng low-poly được dựng bởi bộ công cụ Dark Fantasy. Hệ chế tạo và nguyên liệu mới có tài liệu riêng: [Lò rèn Ánh Tàn](Crafting.md).

## 3. Thêm hiệu ứng khi trúng đòn (vũ khí)

Kế thừa `WeaponHitEffectSO`:

```csharp
[CreateAssetMenu(menuName = "LightSouls/Combat/Hit Effects/Poison")]
public class PoisonHitEffectSO : WeaponHitEffectSO
{
    [SerializeField] private float damagePerSecond = 4f;
    public override void OnHit(WeaponHitContext ctx) { /* dùng ctx.Target, ctx.Attacker, ctx.HitPoint... */ }
    public override string Describe() => $"Độc {damagePerSecond}/giây";
}
```

Tạo asset rồi kéo vào danh sách `hitEffects` của vũ khí. Trạng thái kéo dài (đốt, chậm, choáng) nên đi qua `StatusEffectReceiver`.

## 4. Thêm hiệu ứng đồ dùng

Kế thừa `ConsumableEffectSO`: `CanApply(ctx, out reason)` (từ chối thì hiện thông báo nổi qua `CombatToast`), `Apply(ctx)`, `Describe()`. Một đồ dùng có thể ghép nhiều hiệu ứng (ví dụ hồi máu + tăng tốc).

## 5. Thêm chỉ số mới

1. Thêm giá trị vào `enum StatType` và tên hiển thị trong `StatNames`.
2. Nơi dùng chỉ số đọc `PlayerStats.Get(StatType.X)` (mặc định 1 = không đổi).
3. Trang bị / thuốc chỉ cần khai báo `StatModifier(StatType.X, hệ số)`.

Kháng bẫy môi trường đi qua `IHazardResistant` (Common), nên MapGeneration không phụ thuộc Combat.

## 6. Hiệu ứng hình ảnh

- `WeaponVfxProfile` trên mỗi vũ khí: màu nhát chém, hào quang khi cầm, màu va chạm, vệt đạn, chớp nòng, sóng chấn động mặt đất.
- `VfxFactory`: hạt dựng bằng code (Burst, Ring, Aura, Beam, TimedAura, Trail) với vật liệu cộng sáng `Resources/VFX/Mat_VFX_Additive`.
- Vật phẩm rơi trên đất có cột sáng theo màu độ hiếm.

## 7. Hòm kho báu

- `TreasureChest` giữ một `ItemContainer` 6 ô, quay đồ một lần từ `LootTableSO`, **mở lại được nhiều lần** (kể cả khi trống để cất đồ).
- `TreasureChestUI` hiện hai bên: trái là túi đồ (5 ô thanh nhanh + 15 ô balo hoặc tab nguyên liệu), phải là 6 ô của hòm.
  - Chạm ô bên hòm: lấy vào túi (túi gần đầy thì lấy một phần).
  - Chạm ô bên túi: cất cả chồng vào hòm (hòm gần đầy thì cất một phần).
  - "Lấy hết": lấy mọi ô. Đóng bảng thì nắp hòm hạ xuống.
- Muốn làm loại kho khác (rương sảnh...): tạo component giữ `ItemContainer` và tái dùng cùng kiểu giao diện.

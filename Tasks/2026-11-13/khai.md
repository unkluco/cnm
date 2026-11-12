# Khải — Bổ sung cho nhiệm vụ nhân vật và chiến đấu (2026-11-13)

**Nhánh:** `feature/character-combat`

Nhiệm vụ chính vẫn là [`Tasks/2026-11-11/khai.md`](../2026-11-11/khai.md). File này bổ sung các yêu cầu để phần của bạn ghép được với bộ sinh màn của Đạt mà không phải sửa lại. Đọc trước: [`Docs/Conventions.md`](../../Docs/Conventions.md).

## Trước khi code

- Tạo nhánh mới nhất từ `develop`: nếu nhánh của bạn đã có commit thì merge `develop` vào trước.
- Nhánh `develop` hiện đã có `Assets/Scripts/Common/` (`IDamageable`, `DamageInfo`), các tag/layer và ma trận va chạm.

## Yêu cầu bổ sung

1. **Vị trí code:**
   - Code đặt trong `Assets/Scripts/Combat/`, assembly `LightSouls.Combat`, chỉ tham chiếu `LightSouls.Common` (cùng các package Unity cần thiết).
   - Prefab đặt trong `Assets/Prefabs/Combat/`.
2. **Máu và sát thương:**
   - Người chơi và quái cài đặt `LightSouls.Common.IDamageable`.
   - Không tự định nghĩa interface sát thương khác.
   - `Died` phát đúng một lần.
3. **Tag và layer:**

   | Đối tượng | Tag | Layer |
   | :--- | :--- | :--- |
   | Người chơi (đối tượng gốc) | `Player` | `Player` |
   | Mỗi quái (đối tượng gốc) | `Enemy` | `Enemy` |
   | Đòn kiếm, đạn của người chơi | | `PlayerAttack` |
   | Đòn đánh, đạn của quái | | `EnemyAttack` |

   Bộ sinh màn nhận biết người chơi vào phòng bằng tag `Player`, nên thiếu tag này thì cửa phòng sẽ không khóa.
4. **Kích thước và di chuyển:**
   - Người chơi và quái cao tối đa 2 m, bán kính tối đa 0,5 m.
   - Quái di chuyển bằng `NavMeshAgent` với agent type `Humanoid`.
   - Trong `CharacterCombatTest`, tự đặt một `NavMeshSurface` trên sàn phẳng để bake. Khi ghép, NavMesh sẽ do bộ sinh màn bake.
5. **Điều khiển:**
   - Dùng `Assets/InputSystem_Actions.inputactions`, action map `Player`. Bạn sở hữu file này: thêm action đổi vũ khí, tấn công… tại đây.
   - Không đọc phím trực tiếp ngoài code thử nghiệm trong `Combat/Testing/`.
6. **Chuẩn bị cho đợt ghép:**
   - Cần một thành phần sinh quái gọi được từ code: nhận danh sách điểm sinh (`IReadOnlyList<Transform>`), sinh quái tại đó, và phát một sự kiện khi toàn bộ quái của lượt đó đã chết.
   - `GameBootstrap` sẽ gọi thành phần này khi người chơi vào một phòng chiến đấu, rồi mở cửa phòng khi nhận sự kiện.
   - Thử trong `CharacterCombatTest` bằng vài `Transform` đặt tay.

## Bàn giao bổ sung

- Ghi trong PR: tên và cách gọi thành phần sinh quái, sự kiện hết quái, cùng cách lấy máu hiện tại/tối đa của người chơi (để làm HUD).
- Tự kiểm tra theo checklist ở mục 7 của quy ước trước khi mở PR.

# Khải — Bổ sung cho nhiệm vụ nhân vật và chiến đấu (2026-11-13)

**Nhánh:** `feature/character-combat`

Nhiệm vụ chính vẫn là [`Tasks/2026-11-11/khai.md`](../2026-11-11/khai.md). File này bổ sung các yêu cầu để phần của bạn ghép được với bộ sinh màn của Đạt mà không phải sửa lại. Đọc trước: [`Docs/Conventions.md`](../../Docs/Conventions.md).

## Việc đầu tiên: cập nhật nhánh từ `develop`

Nhánh `feature/character-combat` đã có sẵn, **không tạo nhánh mới**. Nhánh này được tách ra trước khi `develop` có quy ước, khung `Common`, tag/layer và ma trận va chạm, nên cần lấy các thay đổi đó về trước khi viết code:

```bash
git fetch origin
git checkout feature/character-combat
git merge origin/develop
git push
```

- Nếu chưa commit gì trên nhánh, lệnh merge chỉ kéo nhánh lên ngang `develop` và không có xung đột.
- Nếu Unity đang mở dự án, đóng Unity trước khi chạy các lệnh trên, rồi mở lại.
- Sau bước này, kiểm tra đã có `Assets/Scripts/Common/` (`IDamageable`, `DamageInfo`) và `Docs/Conventions.md`.
- Trong lúc làm, mỗi khi nhóm trưởng báo `develop` có thay đổi mới thì lặp lại các lệnh trên.

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
5. **Điều khiển (game Android, màn hình ngang, chỉ dùng cảm ứng):**
   - Dùng `Assets/InputSystem_Actions.inputactions`, action map `Player`. Bạn sở hữu file này: thêm action đổi vũ khí, tấn công… tại đây.
   - Không đọc phím trực tiếp ngoài code thử nghiệm trong `Combat/Testing/`.
   - Làm điều khiển trên màn hình bằng `OnScreenStick` (cần analog di chuyển, góc trái dưới) và `OnScreenButton` (tấn công, đổi vũ khí…, góc phải dưới). Mỗi action được nút trên màn hình điều khiển phải có binding tay cầm, ví dụ `Move` có `<Gamepad>/leftStick`, `Attack` có `<Gamepad>/buttonSouth`. Giữ binding bàn phím để thử nhanh.
   - Đặt các nút trong một object có `LightSouls.Common.SafeAreaFitter` để không bị tai thỏ che. Nút cao tối thiểu 96 px trên khung 1920×1080; nút tấn công nên lớn hơn, khoảng 160–200 px.
   - Đóng gói Canvas điều khiển thành prefab `Assets/Prefabs/Combat/Combat_TouchControls.prefab`, sort order 5 (dưới menu tạm dừng của Kiệt).
   - Thử bằng Device Simulator (`Window > General > Device Simulator`), chọn một máy Android có tai thỏ, xoay ngang.
   - Chỉ rung máy (`Handheld.Vibrate()`) khi `LightSouls.Common.GameSettings.VibrationEnabled` là `true`.
   - Có hàm công khai bật/tắt điều khiển của người chơi (cả nút trên màn hình lẫn action). Khi ghép, `GameBootstrap` gọi hàm này lúc menu tạm dừng của Kiệt mở hoặc đóng, vì `LightSouls.Combat` không tham chiếu được `LightSouls.UI`.

Bạn dùng MacBook: mục 1 của quy ước có vài lưu ý cho macOS (phím `Cmd`, đổi tên file chỉ khác hoa/thường). Không cần cài module Android để làm phần này, Play Mode và Device Simulator chạy được ngay.
6. **Chuẩn bị cho đợt ghép:**
   - Cần một thành phần sinh quái gọi được từ code: nhận danh sách điểm sinh (`IReadOnlyList<Transform>`), sinh quái tại đó, và phát một sự kiện khi toàn bộ quái của lượt đó đã chết.
   - `GameBootstrap` sẽ gọi thành phần này khi người chơi vào một phòng chiến đấu, rồi mở cửa phòng khi nhận sự kiện.
   - Thử trong `CharacterCombatTest` bằng vài `Transform` đặt tay.

## Bàn giao bổ sung

- Ghi trong PR: tên và cách gọi thành phần sinh quái, sự kiện hết quái, cách lấy máu hiện tại/tối đa của người chơi (để làm HUD), và hàm bật/tắt điều khiển.
- Prefab `Combat_TouchControls` dùng được trong Device Simulator: di chuyển bằng cần analog, tấn công và đổi vũ khí bằng nút.
- Tự kiểm tra theo checklist ở mục 7 của quy ước trước khi mở PR.

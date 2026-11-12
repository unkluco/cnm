# Quy ước chung — Light Souls

> **Trạng thái:** đề xuất, chờ nhóm trưởng (Kiệt) duyệt qua PR. Sau khi merge, mọi PR tính năng phải tuân theo tài liệu này. Muốn đổi quy ước thì mở PR sửa chính file này, không tự làm khác trong nhánh tính năng.

Mục tiêu: các phần do từng người làm riêng (sinh màn, nhân vật/chiến đấu, giao diện) ghép được vào Scene `Game` mà không phải sửa lại. Tài liệu chỉ ghi những điều phải thống nhất; chi tiết bên trong mỗi hệ thống do người phụ trách quyết định.

## 1. Môi trường

- **Unity `6000.5.7f1`** (đúng như `ProjectSettings/ProjectVersion.txt`). Không mở dự án bằng bản khác, kể cả khác bản vá: Unity sẽ ghi lại hàng loạt file trong `ProjectSettings` và gây xung đột.
- Không tự thêm hoặc nâng cấp package. Cần package mới thì ghi trong PR và để nhóm trưởng duyệt, vì `Packages/manifest.json` ảnh hưởng cả nhóm.
- Scene, prefab và asset phải lưu dạng văn bản (**Force Text** trong `Edit > Project Settings > Editor > Asset Serialization`). Trước khi mở PR, kiểm tra file `.unity`/`.prefab` không bị Git báo là nhị phân. Nếu bị, mở file trong Unity và chạy `Assets > Force Reserialize Assets` cho file đó.
- `.gitattributes` của repo quy định file văn bản dùng xuống dòng `LF` và đánh dấu file nhị phân (ảnh, model, âm thanh). Không sửa file này trong nhánh tính năng.

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
  Combat/          LightSouls.Combat          — nhân vật, quái, vũ khí (Khải)
  UI/              LightSouls.UI              — menu, HUD
  Game/            LightSouls.Game            — lắp ráp các hệ thống trong Scene Game
```

- Mỗi thư mục có một assembly definition (`.asmdef`) cùng tên với namespace.
- **Quy tắc tham chiếu:**
  - `MapGeneration`, `Combat`, `UI` chỉ được tham chiếu `Common`, không tham chiếu lẫn nhau.
  - Chỉ `Game` được tham chiếu tất cả. Script `GameBootstrap` trong Scene `Game` là nơi duy nhất nối các hệ thống với nhau (xem mục 4).
  - Unity sẽ báo lỗi biên dịch nếu vi phạm, nên quy tắc tự được kiểm tra.
- Code chỉ dùng để thử nghiệm (điều khiển scene test, phím tắt debug, giao diện thử) đặt trong thư mục con `Testing/` của hệ thống và **không bao giờ** được đặt vào Scene `Game`.
- Prefab và asset cũng chia theo hệ thống: `Assets/Prefabs/<HeThong>/`, `Assets/ScriptableObjects/<HeThong>/`.
- Tên trong code (lớp, hàm, biến) bằng tiếng Anh; comment và tài liệu bằng tiếng Việt. Tên file trùng tên lớp. Prefab đặt tên `PascalCase` có tiền tố loại: `Room_`, `Enemy_`, `Weapon_`, `UI_`.

## 4. Hợp đồng giữa các hệ thống

### 4.1. Sinh màn cung cấp gì

`DungeonGenerator` (cài đặt `IDungeonProvider`) phát sự kiện `OnDungeonGenerated(DungeonRuntimeContext ctx)` khi màn đã dựng xong và NavMesh đã bake. Các hệ thống khác chỉ dùng những trường sau:

| Trường | Dùng cho |
| :--- | :--- |
| `ctx.PlayerSpawnPoint` | đặt người chơi |
| `ctx.LevelExitPoint` | đích qua màn |
| `ctx.SpawnedRooms` | danh sách phòng; mỗi `RoomModule` có `RoomType`, `EnemySpawnPoints`, `LifecycleController` |
| `ctx.IsNavMeshBaked` | chỉ cho quái di chuyển khi `true` |

Các trường còn lại (máy bán vũ khí, rương boss) chưa thuộc phạm vi prototype và chưa được tính là hợp đồng.

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

## 5. Tag, layer và input

| Tag | Gắn cho |
| :--- | :--- |
| `Player` | đối tượng gốc của nhân vật người chơi (`RoomLifecycleController` dựa vào tag này để nhận biết người chơi vào phòng) |
| `Enemy` | đối tượng gốc của mỗi quái |

| Layer | Gắn cho |
| :--- | :--- |
| `Player` | nhân vật người chơi |
| `Enemy` | quái |
| `Environment` | sàn, tường, cửa |
| `Projectile` | đạn và vùng đánh |

- Chỉ nhóm trưởng sửa danh sách tag/layer (`ProjectSettings/TagManager.asset`) và ma trận va chạm. Cần thêm thì đề nghị trong PR hoặc nhắn nhóm.
- Điều khiển trong game dùng file chung `Assets/InputSystem_Actions.inputactions`, action map `Player`. Người làm nhân vật (Khải) sở hữu file này; người khác cần thêm action thì nhờ Khải. Không đọc phím trực tiếp (`Keyboard.current`) ngoài code trong `Testing/`.

## 6. Sở hữu Scene và file

- Scene `Game`: chỉ nhóm trưởng sửa. Thành viên bàn giao bằng prefab và script, không bằng Scene.
- Scene thử (`MapGenTest`, `CharacterCombatTest`, …): chỉ người phụ trách sửa.
- Không sửa prefab, Scene hay asset trong thư mục của hệ thống khác. Cần thay đổi thì nhờ người phụ trách.

## 7. Git và Pull Request

- Quy trình nhánh theo `README.md`. Commit theo dạng `loại(phạm vi): mô tả`, ví dụ `feat(map-gen): …`, `fix(combat): …`, `docs: …`.
- PR chỉ chứa file thuộc phạm vi nhiệm vụ. Kiểm tra lại các file Unity tự sinh như `ProjectSettings/*.json` trước khi commit.
- Tài liệu viết bằng Markdown (`.md`) để Git xem được thay đổi; không dùng `.docx`.
- Trước khi mở PR, tự kiểm tra:
  - [ ] Mở Scene thử, vào Play Mode, console không có lỗi.
  - [ ] Không có file ngoài phạm vi; Scene/prefab không bị lưu dạng nhị phân.
  - [ ] Tài liệu bàn giao khớp với code thật (tên hàm, tham số, đường dẫn prefab).
  - [ ] Ghi rõ phần nào còn tạm thời hoặc chưa làm.

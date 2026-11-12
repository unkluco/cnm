# Đạt — Hoàn thiện bàn giao sinh màn (2026-11-13)

**Nhánh:** `feature/map-generation`

Bộ sinh màn đã được chạy thử và hoạt động tốt:
- 4/4 EditMode test qua.
- 30/30 seed sinh với prefab thật đều không chồng lấn, có đường đi từ phòng bắt đầu đến đích và NavMesh thông.

Việc còn lại là sửa phần bàn giao cho khớp với code và với quy ước chung ([`Docs/Conventions.md`](../../Docs/Conventions.md)), rồi mở PR.

## Việc cần làm

1. **Cập nhật nhánh:**
   - Merge `develop` vào `feature/map-generation` để lấy quy ước, `.gitattributes`, `Assets/Scripts/Common/` và tag/layer mới.
   - Nếu xung đột ở `ProjectSettings/SceneTemplateSettings.json`, giữ bản của `develop`.
2. **Lưu Scene dạng văn bản:**
   - `Assets/Scenes/MapGenTest.unity` hiện đang ở dạng nhị phân, nên Git không xem được thay đổi và không merge được.
   - Mở Scene trong Unity, chọn file và chạy `Assets > Force Reserialize Assets`, rồi kiểm tra file bắt đầu bằng `%YAML`.
   - Nếu file vẫn nhị phân, kiểm tra script `MapGenPrototypeBuilder` có lưu Scene theo cách khác không.
3. **Sửa tài liệu `Docs/MapGeneration_RoomFormat_And_Handoff.md` cho khớp code:**
   - Mục 5.1 mô tả prefab `Interactables/WeaponVendingMachine.prefab` và hàm `TryPurchaseWeapon(weaponId, cost)`. Thực tế không có prefab đó, và hàm là `TryPurchaseWeapon(string, out string)`.
   - Máy bán vũ khí và rương boss đã được quyết định là **ngoài hợp đồng prototype** (quy ước mục 4.1). Ghi rõ điều này trong tài liệu, hoặc bỏ hai mục đó.
   - Mục 5.4 nói `RoomLifecycleController` chỉ gắn cho phòng chiến đấu, nhưng code gắn cho mọi phòng. Sửa tài liệu hoặc code cho khớp.
4. **Layer:** sàn, tường, cửa và cổng khóa trong prefab room đặt layer `Environment`.
5. **Dọn commit:**
   - Chuyển `Docs/BaoCao_ThietKe_Va_NghiemThu_SinhMap_LightSouls.docx` sang Markdown, hoặc bỏ nếu đã trùng nội dung với tài liệu bàn giao.
   - Trong `DungeonLayoutPlannerTests`, bỏ ba dòng `foreach` thêm template bị lặp.
6. **Mở PR** từ `feature/map-generation` về `develop`, mô tả cách chạy `MapGenTest` và cách kiểm tra các phòng đã nối đúng. Tự kiểm tra theo checklist ở mục 7 của quy ước trước khi mở.

## Tiêu chí hoàn thành

- PR mở về `develop`, không có xung đột.
- `MapGenTest.unity` là file văn bản; EditMode test vẫn qua.
- Mọi tên hàm, tham số và đường dẫn trong tài liệu bàn giao đều có thật trong code.

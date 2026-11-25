# Rune Casting — hệ chiến đấu cổ ngữ

Nhánh `feature/rune-casting` tích hợp model ViT-Tiny đã fine-tune LoRA rank 4 + Triplet Loss vào gameplay Light Souls. Unity nhận dạng trực tiếp trên thiết bị qua Unity Inference Engine 2.6.1 (Sentis), không yêu cầu chạy server Rune Lab, Python hay kết nối mạng trong lúc chơi.

## Điều khiển

- Chọn vũ khí trên hotbar. Góc dưới phải hiện 4 mẫu cổ ngữ riêng của vũ khí và mẫu **Lướt Né** (nét chéo `/`) dùng chung cho mọi vũ khí.
- Dùng chuột hoặc một ngón tay để vẽ **một nét liên tục** trong ô cổ ngữ. Khi nhấc tay, model đọc hình và thi triển chiêu tương ứng nếu hợp lệ.
- Nút LƯỚT riêng đã bỏ; vẽ một nét chéo theo mẫu (`/` hoặc `\`, từ đầu nào cũng được) để lướt theo hướng joystick hoặc hướng đang nhìn khi đứng yên. Lướt có thời gian hồi riêng (cơ bản 1,85s, có tính trang bị) và dùng được kể cả khi đòn đánh đang hồi. Đổi vũ khí không làm mới hồi lướt.
- Ô vẽ tăng từ 310×310 lên **420×420** đơn vị UI tham chiếu (+84% diện tích). Joystick, tương tác, balo và thao tác sử dụng vật phẩm tiêu hao vẫn hoạt động. Khi chọn bình máu/vật phẩm tiêu hao, chỉ mẫu Lướt Né còn hiện trong ô vẽ, nút **DÙNG** vẫn xuất hiện bên cạnh.
- Nét quá ngắn, không đủ giống, mơ hồ giữa hai ký hiệu, không thuộc bộ chiêu hiện tại hoặc chiêu đang hồi đều không kích hoạt hành động.
- Nét đang vẽ/kết quả đang chờ bị hủy nếu đổi vũ khí, khóa input, chuyển scene hoặc nhân vật không còn có thể chiến đấu.
- Chế độ **THOÁNG** (`0.80` cosine, chênh `0.05`) là mặc định, giống cấu hình Rune Lab đã thử. Có thể chọn **CHẶT** (`0.90`, `0.05`) nếu muốn giảm nhận nhầm. Game lưu lựa chọn cho những lần sau; bản nâng cấp này đặt lại lựa chọn thử nghiệm một lần sang mặc định mới. Điểm tương đồng không phải xác suất đúng.

## Vũ khí

Toàn bộ 45 vũ khí trong ItemDatabase có bộ 4 chiêu riêng: tổng cộng 180 hành động đã khai báo tường minh trong `Assets/Scripts/Combat/Runes/RuneSkillCatalog.cs`, cộng một hành động Lướt Né thống nhất. Mỗi bộ có một cổ ngữ đánh cơ bản, ba cổ ngữ đặc biệt và một cổ ngữ lướt: **225 ô chiêu**, 181 hành động phân biệt. Dành riêng nét chéo cho lướt giúp người chơi phản xạ mà không phải nhớ vũ khí đang cầm. Bộ 12 ký hiệu thống nhất giúp tránh bắt người chơi ghi nhớ 180 hình khác nhau; cùng một hình (trừ nét lướt) có thể gọi chiêu khác khi đổi vũ khí.

Ví dụ:

| Vũ khí | 4 chiêu (ký hiệu → tên) |
| --- | --- |
| Kiếm sắt | Ngang → Thiết Trảm; V → Phá Giáp; trăng khuyết → Nguyệt Trảm; tròn → Kiếm Phong Hộ Thân |
| Kiếm lửa | Ngang → Huyền Hỏa Trảm; S → Hỏa Kích; tròn → Vòng Thiêu Đốt; sóng → Sóng Lửa |
| Frost Scepter | Dọc → Hàn Ấn Tiễn; tròn → Hàn Ấn Vực; sóng → Hàn Sương Tuyến; tam giác → Băng Giáp |
| Guardian Maul | Ngang → Hộ Thành Chùy; vuông → Bức Thành Hộ Linh; tròn → Hộ Thành Chấn; tam giác → Thủ Vệ Ấn |
| Dream Scepter | Dọc → Mộng Tiễn; xoáy → Mộng Lệnh; tròn → Mộng Vực; tam giác → Tâm Ấn |
| Mirage Sabre | Ngang → Ảo Trảm; xoáy → Bước Ảnh; trăng khuyết → Nguyệt Ảnh; tam giác → Ảo Tâm |

Các dạng chiêu: chém/bắn, quét hình quạt, đòn đường thẳng, bùng nổ tại mục tiêu, vùng quanh người chơi, lướt theo hướng di chuyển, dịch chuyển, vách đá/băng, lá chắn và tập trung. Hiệu ứng trúng đòn có đẩy/hút, đóng băng, làm chậm, choáng, thiêu đốt, hút máu, sét lan, phá giáp, thôi miên quái nhỏ yếu máu. Không tự kích hoạt toàn bộ passive cũ mỗi lần vẽ; hiệu ứng được xác định bởi chính chiêu.

Các giới hạn cân bằng được chia sẻ giữa các vũ khí: hồi đòn toàn cục, hồi riêng từng cổ ngữ, hồi theo nhóm khống chế/địa hình/thôi miên/hút máu; chống đổi vũ khí để spam chiêu. Một chiêu vùng trúng tối đa 6 mục tiêu; hồi máu tối đa 8 HP mỗi lần dùng kỹ năng hút máu, quái boss không bị đẩy/hút/thôi miên và kháng khống chế. Các vách có collider/NavMeshObstacle, dịch chuyển kiểm tra địa hình và cửa khóa. Lá chắn/tăng sức mạnh/vách/dịch chuyển chỉ dùng trong phòng đang giao tranh để không ảnh hưởng cửa hay NPC tại sảnh.

## Cấu trúc nhận dạng

1. `RuneGesturePad` ghi tọa độ chuẩn hóa theo ô vẽ (x sang phải, y hướng xuống); chỉ theo dõi đúng một pointer.
2. `RuneRasterizer` kiểm tra nét ngắn và chuẩn hóa khung bao vào ảnh 224×224, giữ hướng và tỉ lệ, dùng nét đen nét dày cố định/nền trắng, Lanczos ở độ phân giải 3x và ImageNet normalization.
3. `RuneClassifier` dùng chính ONNX đã merge LoRA (~22.18 MB), lấy CLS vector 192 chiều. So cosine với bank 120 vector (12 loại × 10 mẫu), lấy điểm cao nhất mỗi loại rồi đối chiếu top-1/top-2 toàn thư viện.
4. Chỉ sau khi top-1 được nhận và thuộc bộ chiêu đang trang bị mới gọi `PlayerCombat.TryCastRune`. Không ép nét thuộc ký hiệu khác trở thành chiêu khả dụng gần nhất.
5. `RuneSkillExecutor` chạy thật hành động, damage, VFX, an toàn vật lý, hồi chiêu. Một request mỗi lần, không gửi nét vẽ ra ngoài, không lưu lại nét gameplay.

## Reproduce / validation

Model nguồn: [ViT-Tiny/16-224 DINOv2 distilled](https://huggingface.co/hasanfaesal/vit-tiny-patch16-224-dinov2-distill), revision `82c08b3370e0f10c3726352c0b75ae39da034d69`, Apache-2.0. Đây là checkpoint cộng đồng; không phải model Tiny chính thức của Meta. License/NOTICE và hashes có trong `Assets/Resources/RuneCasting`.

Môi trường Python train/export cần thêm `onnx` và `onnxruntime` ngoài dependencies của Rune Lab:

```powershell
python Tools/RuneLab/export_unity.py Tools/RuneLab/.local/training/lora-triplet-pilot-01 --assets Assets/Resources/RuneCasting
python Tools/RuneLab/prepare_unity_validation.py Tools/RuneLab/.local/training/lora-triplet-pilot-01 Tools/RuneLab/.local/unity-holdout.json
$env:LIGHTSOULS_RUNE_HOLDOUT = (Resolve-Path Tools/RuneLab/.local/unity-holdout.json).Path
```

Script xuất model kiểm tra cosine output ONNX với PyTorch và bank mẫu chuẩn. Chỉ xuất model đã gộp, bank vector, biểu tượng canonical và metadata; các stroke gốc, split, adapter/checkpoint huấn luyện và fixture kiểm tra luôn nằm trong `.local` bị Git ignore. Test `LocalUnseenStrokeHoldoutUsesExactUnityPreprocessingAndModel` chỉ chạy khi có fixture cục bộ, không yêu cầu người khác cung cấp dữ liệu đã vẽ.

Các test Unity EditMode/PlayMode kiểm tra catalog, preprocessing, ONNX, pointer down/drag/up, khóa kết quả khi đổi vũ khí, hồi lướt độc lập, cooldown, hiệu ứng thực, pose và GPU compute. Desktop chọn Burst CPU mặc định; các thiết bị mobile có compute shader ưu tiên GPU, lỗi backend thì dùng CPU. Độ trễ/độ chính xác trên Android và tập người vẽ độc lập cần đo thêm trước khi phát hành chính thức. Không dùng kết quả holdout để huấn luyện lại hoặc điều chỉnh ngưỡng trong cùng đợt thử nghiệm.

### Kết quả xác minh model

- Mô hình ONNX float32 có 22.180.842 byte. Cosine thấp nhất giữa output PyTorch/ONNX Runtime trên 72 mẫu chuẩn và biến thể là `0.99999976`.
- Unity Inference Engine nhận đúng cả 12/12 mẫu chuẩn tại mức CHẶT. Trên 65 nét cục bộ giữ riêng từ trước khi train (60 hợp lệ, 5 lạ), Unity nhận đúng top-1 60/60; CHẶT chấp nhận đúng 49/60 và 0/5 nét lạ; THOÁNG chấp nhận đúng 57/60 và 1/5 nét lạ. Không dùng dữ liệu test đó để thay đổi trọng số hoặc ngưỡng.
- Hai backend CPU/GPU đều chạy trực tiếp cùng ONNX, không thay bằng nhận dạng hình học. Đo tốc độ PlayMode desktop sau khi khởi tạo cho kết quả xấp xỉ 15–17 ms inference (CPU Burst) và khoảng 18–35 ms ở các lượt GPU ổn định; có thể có lượt dài hơn do compile shader và khung hình. Đó không phải latency đầu-cuối hay benchmark điện thoại.
- Unity `6000.5.7f1` CLI: **62/62 EditMode** (bao gồm local holdout) và **31/31 PlayMode**. Kiểm tra lần lượt 225/225 lượt thi triển theo các bộ chiêu, khôi phục pose vũ khí, khóa hồi chiêu, lướt thực tế khi đang hồi đòn và lướt khi chọn bình máu. Holdout CPU Burst lần xác nhận cuối: median 14,6 ms và p95 15,6 ms inference trên PC kiểm thử.

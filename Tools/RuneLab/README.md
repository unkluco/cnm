# Rune Lab — Giai đoạn 1

Phòng thử nghiệm nhận dạng cổ ngữ của Light Souls, tách biệt khỏi Unity và gameplay. Đây **không phải** phiên bản thay nút đánh của game.

## Chạy

Cần Python 3.11 trở lên, trên máy có PyTorch tương thích. Từ thư mục `Tools/RuneLab`:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python run_lab.py --open-browser
```

Mở <http://127.0.0.1:8765>. Lần đầu chạy sẽ tải khoảng 22,1 MB trọng số vào `.local/model/` và tính trước 60 vector mẫu. Sau đó nhận dạng hoạt động offline; không gửi nét vẽ đến dịch vụ bên ngoài. Nhấn Ctrl+C trong cửa sổ khởi chạy để dừng. Mặc định chỉ truy cập được từ máy đang chạy; nếu cần thử cảm ứng trên mạng Wi-Fi tin cậy, chạy `python run_lab.py --lan` và mở địa chỉ IP LAN của máy ở cổng 8765. Chế độ LAN cho phép các thiết bị cùng mạng đọc/lưu mẫu; chỉ bật khi thử nghiệm và tắt sau khi xong.

## Model và cách nhận dạng

- Model: [ViT-Tiny/16-224 chưng cất từ DINOv2](https://huggingface.co/hasanfaesal/vit-tiny-patch16-224-dinov2-distill) (Apache-2.0), bản cộng đồng từ tác giả model, không phải DINOv2 Tiny chính thức của Meta.
- Checkpoint revision `82c08b3370e0f10c3726352c0b75ae39da034d69`, kiểm tra SHA-256 `0bcaedfa2fefe1bd04625e2a361ba42c82509fcbf2796063f8f2f42d7e23b1b6`; chỉ đọc safetensors, không chạy code từ model hub.
- Backbone 5.524.416 tham số thực tế; đầu vào RGB 224×224; lấy CLS 192 chiều, chuẩn hóa L2. Mean/std ImageNet lấy theo cấu hình checkpoint. Weight frozen (eval / inference mode).
- Nhận một nét liên tục, lấy tọa độ khi nhấc tay. Nét quá ngắn bị từ chối trước inference. Đưa bounding box vào giữa ảnh, scale đồng nhất, giữ tỉ lệ và hướng; nét đen có độ dày cố định trên nền trắng. Đảo thứ tự vẽ không chủ ý tạo chiêu khác vì model nhìn hình tĩnh.
- 12 cổ ngữ, mỗi loại có 1 hình chuẩn và 4 biến thể tổng hợp nhỏ, **không phải dữ liệu huấn luyện**. Tính trước các embeddings tham chiếu. Một lần inference cho mỗi nét, lấy cosine cao nhất trong 5 mẫu của mỗi loại.
- Chỉ chấp nhận nếu điểm top 1 đạt similarity tối thiểu và chênh đủ với **loại ký hiệu** đứng thứ 2. Giá trị khởi điểm `0,92` và `0,02` lấy từ thử nghiệm tổng hợp, có thể kéo hai thanh để khám phá trade-off. Chưa coi các ngưỡng là ngưỡng sản phẩm; điểm cosine không phải xác suất đúng.

## Thu thập dữ liệu thật cho giai đoạn 2

1. Chọn một ký hiệu ở thư viện. Dùng chuột hoặc cảm ứng vẽ ký hiệu đó bằng một nét, nhấc tay.
2. Xem kết quả. Có thể chỉnh ngưỡng mà không phải vẽ lại. Nhập mã người vẽ (VD `Kiet-01`, không cần tên/email).
3. Bấm **Lưu mẫu kiểm thử**, kể cả khi model đoán sai hoặc từ chối. Với nét ngẫu nhiên/không hợp lệ, chọn nhãn tương ứng trước khi lưu. Nhãn kỳ vọng chỉ để đánh giá, không được dùng trong inference.
4. Tải JSONL để đánh giá sau. Dữ liệu gồm điểm tọa độ, nhãn kỳ vọng, mọi điểm cosine, dự đoán, lý do từ chối, ngưỡng, latency và phiên bản model. Mặc định lưu cục bộ trong `.local/data/samples.jsonl`, không đưa vào Git.

Mỗi thành viên thử 10–15 lần mỗi ký hiệu theo kích cỡ/tốc độ khác nhau, thêm nét dang dở và nét không thuộc thư viện. Giữ người kiểm thử độc lập với người dùng để chọn ngưỡng; mở rộng thêm người vẽ sau. Không chọn ngưỡng từ toàn bộ dữ liệu rồi báo cáo lại trên chính dữ liệu đó.

## Kiểm thử kỹ thuật

```powershell
python -m unittest discover -s tests -v
python benchmark.py --per-rune 20
```

Benchmark tổng hợp dùng hai bộ seed riêng biệt cho đề xuất ngưỡng và kiểm tra. Nó báo top-1, tỷ lệ nét hợp lệ được chấp nhận đúng, kích hoạt sai, nét lạ bị nhận nhầm, phân tích nhầm ký hiệu và độ trễ CPU trên máy chạy. Báo cáo `.local/reports/synthetic-benchmark.json`. **Không phải** kết quả người chơi thực hoặc thiết bị mobile.

Kết quả pilot: 350 nét hiệu chỉnh + 350 nét mô phỏng kiểm tra độc lập (240 hợp lệ, 110 không hợp lệ). Top-1 đúng 239/240 (99,6%); tại ngưỡng 0,92 / chênh 0,02, chấp nhận đúng 226/240 (94,2%), kích hoạt sai trên nét hợp lệ 0/240, nhưng 3/110 nét không hợp lệ vẫn được nhận (2,7%). Vì vậy chưa thể công nhận đạt mục tiêu sản phẩm. P95 inference desktop CPU 4 luồng khoảng 12 ms; tốc độ trên mobile chưa được đo.

Tùy chọn kiểm thử Chromium (dùng dữ liệu test tạm, không lưu vào dataset của người chơi):

```powershell
python -m pip install playwright
python tests/browser_smoke.py --browser-path "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

Mục tiêu nghiên cứu sau khi có dữ liệu thật: ≥95% chấp nhận đúng nét hợp lệ; <2% kích hoạt nhầm và <2% nét lạ bị nhận nhầm; đo p95 latency trên thiết bị mục tiêu. Chưa xuất ONNX, chưa thêm Sentis, chưa thay đổi scene, chiêu thức hoặc cooldown. Chỉ tiến sang Unity khi phù hợp với các tiêu chí đã thống nhất.

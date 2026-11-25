# Thử nghiệm LoRA + Triplet Loss

Mục tiêu: chuyên biệt hóa embedding ViT-Tiny cho cổ ngữ Light Souls mà không mở rộng model lúc inference. Mọi tham số backbone được đóng băng; chỉ LoRA trên attention projection được tối ưu. Không có classifier head hoặc cross-entropy.

## Quy trình

1. Chụp snapshot dữ liệu `.local/data/samples.jsonl` và tính SHA-256 để cố định bộ dữ liệu.
2. Chia theo nhãn thật **trước augmentation**. Với 20 mẫu mỗi loại: giữ 5 mẫu test cuối (60 hợp lệ, 5 không hợp lệ); 15 mẫu còn lại vào 3 fold development (10 train + 5 validation / loại ở mỗi fold). Kiểm tra không trùng ID hoặc ảnh pixel-identical qua split.
3. Mỗi cấu hình dùng chung split, ngân hàng 5 mẫu tổng hợp + 5 mẫu người vẽ lấy **chỉ từ tập train**, lịch lấy mẫu theo seed và augmentation nhẹ. Riêng diagonal coi `/` và `\` là cùng ký hiệu.
4. LoRA lắp vào `attn.qkv` và tùy cấu hình `attn.proj` ở các block cuối. Khởi tạo B bằng 0. Khoảng cách cosine: `d(a,b)=1-cos(a,b)`; loss: `max(0,d(a,p)-d(a,n)+margin)`. Nét không hợp lệ là negative-only, không gom chúng thành một lớp positive.
5. Chọn checkpoint bằng validation mỗi fold. Gộp kết quả validation của 3 fold để chọn ngưỡng và cấu hình (ưu tiên không thi triển nhầm, sau đó số nét đúng). Chốt cấu hình/epoch/ngưỡng **trước khi** mở tập test cuối. Train lại cấu hình được chọn trên 15 mẫu development / loại, kiểm tra bằng tập test đã giữ lại.
6. So với frozen synthetic-only và frozen + mẫu người vẽ (baseline mạnh). Không đánh đồng top-1 với tỷ lệ thực sự được chấp nhận.

## Cấu hình mặc định

| Tên | Rank | Alpha | Block cuối | Projection | Triplet margin | LR | Mining |
|---|---:|---:|---:|---|---:|---:|---|
| r2-last2-qkv | 2 | 4 | 2 | qkv | 0,15 | 7e-4 | batch-hard |
| r4-last4-hard | 4 | 8 | 4 | qkv + proj | 0,20 | 5e-4 | batch-hard |
| r8-last4-hard | 8 | 16 | 4 | qkv + proj | 0,25 | 3e-4 | batch-hard |
| r4-last4-semihard | 4 | 8 | 4 | qkv + proj | 0,20 | 5e-4 | semi-hard |

AdamW, warmup và cosine decay, tối đa 20 epoch × 6 batch cho mỗi fold. Batch gồm 3 mẫu/loại hợp lệ từ các nguồn khác nhau và tối đa 6 nét lạ. BF16 mixed precision nếu GPU hỗ trợ; đánh giá embedding bằng FP32.

## Chạy

Trong thư mục RuneLab với môi trường Python riêng đã cài `requirements.txt` (PyTorch CUDA nếu cần):

```powershell
python train_lora.py --device cuda --output .local/training/rune-experiment
# Nếu máy tắt giữa chừng, dùng lại đúng đường dẫn và tham số:
python train_lora.py --device cuda --output .local/training/rune-experiment --resume
```

Có thể chạy `--device cpu` với model nhỏ nhưng chậm hơn. Xem [hướng dẫn cài PyTorch](https://pytorch.org/get-started/locally/) để chọn CUDA tương thích. `--smoke` chạy một bước ở development, không đánh giá holdout. Không cần cloud GPU cho đợt dữ liệu nhỏ này.

Kết quả nằm trong thư mục output:

- `REPORT.md`, `report.json`: các bảng so sánh và kết quả test cuối.
- `fold-*/...`: lịch sử loss, validation, best epoch, adapter ở từng fold.
- `candidate-adapter.safetensors`: trọng số LoRA.
- `candidate-merged.safetensors`: backbone gộp LoRA, giữ nguyên 192 chiều và số tham số. Được kiểm tra tương đương embedding.
- `candidate-template-embeddings.npz`, `candidate-metadata.json`: ngân hàng vector cho chính model đã gộp và ngưỡng đạt từ development.

Kiểm tra lại candidate đã gộp trên CPU mà **không** thay đổi ngưỡng:

```powershell
python verify_candidate.py .local/training/rune-experiment
```

Lệnh này kiểm tra tính tương đương các vector CPU/GPU, độ trễ một ảnh, tập test giữ riêng và một bộ nét mô phỏng độc lập; ghi `verification.json`, `REPORT-FULL.md` cùng hình minh họa trường hợp lỗi. Không dùng các kết quả test này để hiệu chỉnh lại trong cùng đợt thử nghiệm.

Không đưa dataset, adapter/candidate hoặc các vector cá nhân lên Git tự động. Các artifact là thử nghiệm; không thay checkpoint/model nhận dạng đang phục vụ Rune Lab hay game. Cần thêm người vẽ độc lập và nhiều unknowns trước khi triển khai mobile.

## Thử model đã train trong Rune Lab

Sau khi có candidate được xác minh:

```powershell
python run_lab.py --candidate .local/training/rune-experiment --port 8765 --open-browser
```

Rune Lab sẽ hiển thị rõ phiên bản LoRA, nạp **trọng số đã gộp** và 120 embedding tham chiếu của đúng phiên bản đó, kiểm tra tính tương đương của các vector mẫu chuẩn khi khởi động. Có hai mức: ngưỡng hiệu chỉnh từ development và mức chặt hơn 0,90 / 0,05 để tiếp tục thử nghiệm. Người dùng vẫn có thể thay đổi hai thanh ngưỡng. Không coi ngưỡng này là ngưỡng sản phẩm.

Mẫu thu mới ghi riêng ở `.local/trials/lora-<hash-model>/samples.jsonl`, kèm hash đúng của trọng số và ngân hàng tham chiếu. Không ghi vào `.local/data/samples.jsonl` ban đầu. Chạy lại `python run_lab.py` (không có `--candidate`) để trở về phiên bản frozen.

Tham khảo: [LoRA](https://arxiv.org/abs/2106.09685), [In Defense of the Triplet Loss](https://arxiv.org/abs/1703.07737).

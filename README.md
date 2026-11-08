# Học Liệu AI

Nạp giáo trình / tài liệu học tập (Word, PDF, PDF scan, ảnh) và hỏi đáp **bám sát tài liệu**, phân tích câu hỏi, liên hệ thực tế theo bộ phận/lĩnh vực. Chạy **offline** trên Windows và Android.

## Build bằng GitHub Actions

1. Tạo repo mới trên GitHub (VD `HOCLIEUAI`), upload **toàn bộ** thư mục này (giữ nguyên cấu trúc, gồm cả `.github`).
2. (Nên làm) Vào **Settings → Secrets and variables → Actions**, thêm 4 secret ký APK:
   `KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_ALIAS`, `KEY_PASSWORD` (dùng lại bộ khoá đang dùng cho app khác được).
   Không có secret thì APK vẫn build, nhưng mỗi bản mới phải gỡ bản cũ mới cài được.
3. Tab **Actions** → chạy **Build PC (HocLieuAI.exe)** và **Build Android (HocLieuAI.apk)** (nút *Run workflow*).
4. Kết quả ở mục **Releases**:
   - `HocLieuAI-PC.zip` — bản PC đầy đủ (đã kèm mô hình AI 1.5B, llama.cpp, thư viện offline).
   - `HocLieuAI.apk` + `qwen2.5-0.5b…gguf`, `qwen2.5-1.5b…gguf` — cho điện thoại.

Tải các file đó ở máy có Internet rồi chép vào máy trong mạng LAN.

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `web/index.html` | Toàn bộ giao diện + bộ máy: đọc file, OCR, chia đoạn, tìm kiếm BM25 tiếng Việt, soạn câu lệnh, 3 kiểu chạy AI |
| `pc/HocLieuAI.cs` | Chương trình khởi động Windows: tự chọn mô hình theo RAM, chạy `llama-server`, mở app, hiện địa chỉ LAN |
| `android/` | App Android (WebView): chạy AI trên máy bằng wllama hoặc gọi AI của PC qua LAN |
| `scripts/fetch-libs.sh` | Tải thư viện offline lúc build (mammoth, pdf.js, tesseract.js + tiếng Việt, wllama) |
| `.github/workflows/` | 2 quy trình build exe và apk |

Thư viện và mô hình được tải lúc build — không cần để file nhị phân/ảnh trong repo.

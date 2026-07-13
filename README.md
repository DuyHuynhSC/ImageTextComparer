# AI Image Text Comparer

Ứng dụng desktop viết bằng C# WPF dùng để trích xuất văn bản từ hình ảnh bằng mô hình AI Vision (ví dụ: Qwen-VL, GPT-4o...) và tiến hành so sánh đối chiếu sự khác biệt giữa hai văn bản trực quan theo thời gian thực.

## Các tính năng chính

1. **Chọn hoặc khoanh vùng ảnh bằng chuột**:
   - Nhập ảnh gốc và ảnh đối chiếu từ trình duyệt tệp.
   - Cho phép sử dụng chuột kéo thả trực tiếp trên ảnh để khoanh vùng (crop) phần chữ cần so sánh hoặc so sánh toàn bộ ảnh.
2. **Trích xuất văn bản qua AI Vision**:
   - Tích hợp chuẩn API OpenAI Chat Completions tương thích với các server hosted Vision Model (như Qwen2.5-VL hoặc Ollama, vLLM).
   - Mã hóa ảnh tự động sang Base64 để gửi yêu cầu trích xuất đồng thời (parallel requests) tăng hiệu năng.
3. **So sánh văn bản trực quan (Split-Diff)**:
   - Thuật toán LCS (Longest Common Subsequence) so sánh từ-theo-từ (word-by-word) chính xác.
   - Tô màu văn bản thay đổi: Màu đỏ (các từ bị xóa/khác ở ảnh 1) và màu xanh lá (các từ được thêm/thay thế ở ảnh 2).
4. **Giao diện hiện đại**:
   - Thiết kế Dark Mode cao cấp, tối giản, thanh cấu hình linh hoạt tự động lưu cấu hình cục bộ vào tệp `config.json`.

---

## Kiến trúc mã nguồn

Dự án gồm các thành phần cốt lõi:
- **`MainWindow.xaml` / `MainWindow.xaml.cs`**: Giao diện và xử lý sự kiện tương tác chuột để vẽ vùng chọn, tính toán ánh xạ tọa độ pixel thực tế của ảnh gốc.
- **`DiffEngine.cs`**: Engine phân tích sự khác biệt (diff) giữa 2 chuỗi ký tự bằng thuật toán LCS.
- **`VisionApiService.cs`**: Dịch vụ giao tiếp HTTP gọi mô hình AI trích xuất văn bản từ hình ảnh.
- **`RichTextBoxHelper.cs`**: Hỗ trợ hiển thị và định dạng văn bản màu sắc vào khung kết quả RichTextBox.

---

## Hướng dẫn cài đặt & Chạy ứng dụng

### Yêu cầu hệ thống
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) hoặc [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Các bước thực hiện

1. Clone hoặc tải mã nguồn về thư mục máy tính của bạn.
2. Mở cửa sổ dòng lệnh (PowerShell hoặc Command Prompt) tại thư mục chứa dự án:
   ```powershell
   cd "D:\Dev\Source Code\ImageTextComparer"
   ```
3. Chạy lệnh xây dựng và khởi chạy dự án:
   ```powershell
   dotnet run
   ```

---

## Cấu hình AI Host

Tại bảng điều khiển bên trái ứng dụng, bạn cần cấu hình các thông số phù hợp với server AI nội bộ:
- **API Endpoint URL**: URL của endpoint chat completions hỗ trợ vision (ví dụ: `http://localhost:11434/v1/chat/completions` của Ollama hoặc cổng vLLM doanh nghiệp).
- **API Key**: Mã khóa API nếu server yêu cầu bảo mật.
- **Model Name**: Tên model vision đang chạy trên host (ví dụ: `qwen2.5-vl`).
- **AI Prompt**: Lời nhắc yêu cầu AI thực hiện trích xuất văn bản (đã có sẵn lời nhắc mặc định tiếng Việt tối ưu).

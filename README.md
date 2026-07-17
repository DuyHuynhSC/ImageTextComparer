# AI Image Text Comparer

Ứng dụng desktop viết bằng C# WPF dùng để trích xuất văn bản từ hình ảnh bằng mô hình AI Vision (ví dụ: Qwen-VL, GPT-4o...) và tiến hành so sánh đối chiếu sự khác biệt giữa hai văn bản trực quan theo thời gian thực.

---

## Các tính năng nổi bật & Cải tiến mới

1. **Chụp ảnh màn hình dạng Snipping Tool (Đa màn hình & DPI-Aware)**:
   - Ngoài việc chọn tệp ảnh sẵn có, bạn có thể bấm **Chụp ảnh...** để quét vùng màn hình trực tiếp (tương tự Snipping Tool của Windows).
   - Hỗ trợ đầy đủ hệ thống **nhiều màn hình (Multi-monitor)** và tự động đồng bộ hóa tỉ lệ DPI của từng màn hình, đảm bảo ảnh chụp không bị mờ nhòe hay lệch toạ độ.
   - Nhấn **ESC** để hủy chụp bất kỳ lúc nào.

2. **Quản lý phiên làm việc & Lịch sử phiên (Session & History Log)**:
   - **Tự động lưu/khôi phục (Auto-Save/Restore)**: Tự động lưu trạng thái làm việc gần nhất (hình ảnh, vùng khoanh chọn, chữ trích xuất, diff kết quả) để mở lại ngay khi khởi động ứng dụng.
   - **Nhật ký Lịch sử (History Log)**: Hiển thị danh sách các phiên so sánh gần đây được lưu theo Ngày/Giờ ở thanh cấu hình bên trái. Chỉ cần click vào để mở lại ngay lập tức. Tự động giới hạn lưu tối đa 20 phiên để tối ưu bộ nhớ.
   - **Nhập/Xuất phiên thủ công**: Cho phép lưu phiên đang làm việc thành tệp `.json` độc lập (mã hóa ảnh Base64 tự đóng gói) và mở lại trên bất kỳ máy tính nào.

3. **Giao diện Sidebar Thu gọn (Dockable Sidebar)**:
   - Thanh cấu hình bên trái có thể thu gọn (gập lại về chiều rộng bằng 0) hoặc mở rộng linh hoạt bằng nút bấm `◀` / `▶` giúp tối ưu diện tích so sánh hình ảnh.

4. **Chế độ Sáng/Tối (Light & Dark Theme Switcher)**:
   - Hỗ trợ đổi giao diện sáng/tối tức thời tại runtime. Ứng dụng khởi động mặc định ở giao diện **Sáng (Light theme)** dịu mắt.

5. **So sánh ký tự tiếng Nhật chuẩn xác cao (Katakana Precision Diffing)**:
   - Thuật toán so sánh được nâng cấp để tách nhỏ và đối chiếu từng ký tự CJK (đặc biệt phân biệt rõ nét các chữ Katakana dễ nhầm lẫn như `バス` - basu và `パス` - pasu).

6. **Tự động Phóng to ảnh nhỏ (Nearest Neighbor Upscaling)**:
   - Khi khoanh vùng chữ nhỏ (chiều cao dưới 300px), ứng dụng tự động upscale ảnh bằng bộ lọc Nearest Neighbor chất lượng cao trước khi gửi lên AI nhằm tối ưu hóa độ sắc nét của điểm ảnh giúp AI nhận dạng chính xác hơn.

7. **Bỏ qua xác thực SSL tự ký (Bypass SSL)**:
   - Cho phép tùy chọn bỏ qua kiểm tra chứng chỉ SSL (`Bỏ qua xác thực SSL (tự ký)`) đối với các server API chạy cục bộ hoặc trong mạng nội bộ doanh nghiệp.

8. **Mở thư mục chẩn đoán nhanh (Diagnostics Folder)**:
   - Nút **Mở thư mục chẩn đoán** giúp bạn mở nhanh thư mục chứa tệp cấu hình, tệp debug ảnh cắt (`crop_debug_1.png`, `crop_debug_2.png`), tệp session và lịch sử làm việc.

---

## Kiến trúc mã nguồn

Dự án gồm các thành phần cốt lõi:
- [MainWindow.xaml](file:///D:/Dev/Source%20Code/ImageTextComparer/MainWindow.xaml) / [MainWindow.xaml.cs](file:///D:/Dev/Source%20Code/ImageTextComparer/MainWindow.xaml.cs): Giao diện chính, xử lý tương tác kéo thả chuột, tính toán tỉ lệ tọa độ, chụp ảnh đa màn hình, lưu/khôi phục session và nhật ký lịch sử.
- [DiffEngine.cs](file:///D:/Dev/Source%20Code/ImageTextComparer/DiffEngine.cs): Engine so sánh chuỗi ký tự dựa trên thuật toán LCS (Longest Common Subsequence), hỗ trợ phân tách CJK.
- [VisionApiService.cs](file:///D:/Dev/Source%20Code/ImageTextComparer/VisionApiService.cs): Gửi dữ liệu ảnh Base64 bất đồng bộ lên Vision API Gateway của Fujinet hoặc các host tương thích OpenAI.
- [RichTextBoxHelper.cs](file:///D:/Dev/Source%20Code/ImageTextComparer/RichTextBoxHelper.cs): Định dạng highlight màu đỏ (xóa) và xanh lá (thêm mới) bằng các thẻ `Span` bọc trong `Run` để hiển thị trực quan lỗi khác biệt.

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
3. Khởi chạy ứng dụng:
   ```powershell
   dotnet run
   ```

---

## Cấu hình AI Host

Tại bảng điều khiển bên trái ứng dụng, cấu hình các thông số phù hợp với server AI:
- **API Endpoint URL**: URL của endpoint chat completions hỗ trợ vision (mặc định trỏ đến Gateway Fujinet: `https://models-gateway.fujinet.net/v1/chat/completions`).
- **API Key**: Mã khóa API nếu server yêu cầu bảo mật.
- **Model Name**: Tên model vision đang chạy trên host (mặc định: `programmer`).
- **AI Prompt**: Lời nhắc yêu cầu AI thực hiện trích xuất chữ thô cách nhau bằng khoảng trắng dạng trung lập `"A B C D E"`, ngăn ngừa lỗi tự sửa chính tả theo ngữ cảnh của mô hình.

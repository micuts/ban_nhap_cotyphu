# Trò chơi: Cờ Tỷ Phú
## 🛠 Hướng dẫn cài đặt cấu hình (Configuration)

Dự án sử dụng Supabase làm Database. Để chạy được ứng dụng, bạn cần thực hiện các bước sau:

1. **Tạo file cấu hình cá nhân**:
   - Copy file `.env.example` và đổi tên thành `.env` (hoặc cập nhật trực tiếp trong `App.config` của bạn).
   
2. **Điền thông tin kết nối**:
   - Mở file cấu hình và thay thế các giá trị bằng thông tin từ dự án Supabase của bạn:
     - `Host`: Địa chỉ server Supabase.
     - `Password`: Mật khẩu Database đã tạo.

3. **Lưu ý quan trọng**:
   - Không được đẩy file chứa mật khẩu thật lên GitHub.
   - Đảm bảo máy tính đã cài đặt `Npgsql` (thông qua NuGet Package Manager) để kết nối PostgreSQL.
4.**Còn lỗi chưa tạo phòng vì timeout**

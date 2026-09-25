# SmartLabKit – Backend

API quản lý vòng đời linh kiện phòng lab IoT (Capstone).
.NET 8 · kiến trúc N-layer (API → BLL → DAL) · EF Core database-first · PostgreSQL (Neon) · JWT HS256.

## Yêu cầu

- .NET SDK 8 trở lên (project target `net8.0`, cần runtime .NET 8)
- Truy cập database Neon của dự án

## Cài đặt lần đầu

```powershell
dotnet tool restore        # cài dotnet-ef 8.0.11 (ghim trong dotnet-tools.json)

# Secret: chỉ lưu bằng user-secrets, KHÔNG ghi vào appsettings
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require" -p SmartLab.API
dotnet user-secrets set "Jwt:SecretKey" "<chuỗi ngẫu nhiên, tối thiểu 32 ký tự>" -p SmartLab.API
dotnet user-secrets set "SeedAdmin:Email" "admin@smartlab.local" -p SmartLab.API
dotnet user-secrets set "SeedAdmin:Password" "<mật khẩu mạnh>" -p SmartLab.API
# Tùy chọn: SeedAdmin:Username (mặc định "admin"), SeedAdmin:FullName
```

> Tạo `Jwt:SecretKey` ngẫu nhiên (PowerShell): `$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)` (macOS/Linux: `openssl rand -base64 48`)
> Mỗi máy dev dùng secret riêng cũng được; trên server phải giữ cố định, đổi secret thì mọi token cũ mất hiệu lực.

> Chuỗi Neon dạng `postgresql://user:pass@host/db?sslmode=require` cần đổi sang dạng `Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`.

## Chạy

```powershell
dotnet run --project SmartLab.API
```

- Swagger: http://localhost:5297/swagger (bấm **Authorize**, dán `accessToken` lấy từ `/api/auth/login`)
- Health: http://localhost:5297/health

Khi khởi động lần đầu:
- Nếu chưa có user Admin, app tự tạo từ `SeedAdmin:*`.
- Nếu thiếu `Jwt:SecretKey` (hoặc ngắn hơn 32 ký tự), app báo lỗi và không khởi động.

## API hiện có

| Method | Route | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Đăng ký sinh viên (users + student_profiles + role Student) |
| POST | `/api/auth/login` | Anonymous | Đăng nhập bằng email hoặc username → JWT |
| GET | `/api/auth/me` | Đã đăng nhập | Thông tin user + roles + permissions |
| GET | `/health` | Anonymous | Kiểm tra kết nối DB |

Mọi response có dạng:
```json
{ "success": true, "message": "...", "data": { }, "errors": null }
```

## Thay đổi database

Database-first, **không dùng migration**:
1. Viết SQL và chạy trên Neon.
2. `./scripts/scaffold.ps1` (Windows) hoặc `./scripts/scaffold.sh` (macOS/Linux).
3. `dotnet build`.

## Docker

```bash
docker build -t smartlab-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
  -e Jwt__SecretKey=... \
  -e SeedAdmin__Email=admin@smartlab.local -e SeedAdmin__Password=... \
  smartlab-api
```

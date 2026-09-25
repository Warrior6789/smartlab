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

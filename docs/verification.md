# Báo cáo kiểm chứng Tuần 1 — 16/11/2026

Đã chạy thực tế trên Windows, .NET SDK 10.0.201, ASP.NET Core 10.0.5, PostgreSQL 18.6; EF Core/CLI 10.0.4 và Npgsql provider 10.0.3.

| Tình huống | HTTP kỳ vọng | HTTP thực tế | Kết quả |
|---|---|---|---|
| GET /menu, ít nhất 3 món seed | 200 | 200 | Đạt |
| POST hợp lệ, hai món 1×2 và 2×1 | 201 + Location, Pending | 201 + /orders/a00b9404-0e38-4523-9790-40a3d2171a15, Pending | Đạt |
| GET đúng Location, Id và số lượng khớp | 200 | 200, body bằng POST | Đạt |
| GET /orders, đơn mới trước | 200 | 200, đơn vừa tạo đầu danh sách | Đạt |
| GET /orders trên database mới chưa có đơn | 200 [] | 200 [] | Đạt |
| Guid hợp lệ nhưng không tồn tại | 404 | 404 | Đạt |
| Danh sách món rỗng | 400 | 400, empty_order | Đạt |
| Khách không tồn tại | 400 | 400, customer_not_found | Đạt |
| Món không tồn tại | 400 | 400, menu_item_not_found | Đạt |
| Quantity bằng 0 | 400 | 400, invalid_quantity | Đạt |
| MenuItemId lặp | 400 | 400, duplicate_menu_item | Đạt |
| Items null | 400 | 400, empty_order | Đạt |
| Một phần tử items null | 400 | 400, invalid_quantity | Đạt |
| JSON sai cú pháp, framework từ chối | 400 | 400, body rỗng, không Content-Type | Đạt |
| Content-Type text/plain | 415 | 415, body rỗng, không Content-Type | Đạt |
| Id không parse thành Guid | 400 | 400, body rỗng, không Content-Type | Đạt |
| Dừng/chạy lại API, GET chính Location đã lưu | 200, dữ liệu giữ nguyên | 200, body khớp nguyên văn | Đạt |

Validation không tạo đơn: script chụp tổng số đơn trước và sau cả lượt, xác nhận chỉ tăng 1 (đơn hợp lệ). Tất cả validation trong handler trả JSON có code và message. Trường hợp framework từ chối trước handler dùng phản hồi rỗng như đã quan sát, không áp DTO lỗi ứng dụng.

## Lệnh đã chạy

```powershell
dotnet --info
git status --short
# Không có metadata Git; lệnh git báo not a git repository.
dotnet new sln -n KitchenFlow
dotnet new web -n Order.Api -o src/Order.Api --framework net10.0
dotnet sln add src/Order.Api
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.4 --configfile NuGet.Config
dotnet add src/Order.Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
dotnet add src/Order.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.4
& D:/PostgreSQL/18/bin/initdb.exe -D D:/KitchenFlow/.local/pgdata -U kitchenflow --auth=trust --encoding=UTF8 --locale=C
& D:/PostgreSQL/18/bin/pg_ctl.exe -D D:/KitchenFlow/.local/pgdata -l D:/KitchenFlow/.local/postgres.log -o '-p 55432 -h 127.0.0.1' start
& D:/PostgreSQL/18/bin/createdb.exe -h 127.0.0.1 -p 55432 -U kitchenflow kitchenflow
$env:ConnectionStrings__Orders = 'Host=127.0.0.1;Port=55432;Database=kitchenflow;Username=kitchenflow'
dotnet build
dotnet ef migrations add InitialCreate --project src/Order.Api
dotnet ef database update --project src/Order.Api
dotnet run --no-build --project src/Order.Api --urls http://localhost:5080
# Cửa sổ khác:
pwsh -File scripts/Test-OrderApi.ps1
# Ctrl+C tiến trình API rồi chạy lại cùng lệnh, cùng database:
$location = (Get-Content docs/created-order.json -Raw | ConvertFrom-Json).location
pwsh -File scripts/Test-OrderApi.ps1 -SavedLocation $location
dotnet build --no-restore
```

Đã tạo thêm database `kitchenflow_empty`, áp dụng cùng migration và chạy API trên port 5081 để assert GET /orders là `[]`. Đã dừng API phụ sau khi kiểm tra. Cụm PostgreSQL và dữ liệu demo không bị xóa. API chính được giữ ở port 5080.

## Minh chứng đã lưu

- `build-results.txt`: Build succeeded, 0 warnings, 0 errors.
- `http-test-results.txt`: toàn bộ lượt HTTP cuối cùng thành công.
- `create-response.txt`: status line HTTP/1.1 201 Created, header Location, body Pending.
- `created-order.json`: Location và body dùng để so sánh sau restart.
- `restart-test-results.txt`: HTTP 200, identical body.
- `empty-list-test-results.txt`: HTTP 200, body [].
- `customer-time-test-results.txt`: khách 1, thời điểm UTC.
- `seed-after-restart.txt`: 2 khách, 3 món sau nhiều lần seed.

Migration đã xuất hiện trong bảng __EFMigrationsHistory: `20261116104029_InitialCreate`, ProductVersion `10.0.4`. Lần migrate database mới có log truy vấn __EFMigrationsHistory chưa tồn tại; EF sau đó tạo bảng lịch sử và áp dụng migration thành công (exit 0).

## Lỗi môi trường đã xử lý và giới hạn

- NuGet config ở profile bị sandbox chặn: chạy restore/install với quyền được duyệt, thêm NuGet.Config nguồn public trong repo.
- Docker daemon chưa chạy: dùng binary PostgreSQL cài sẵn, không cần Docker cho kiểm chứng.
- PostgreSQL instance sẵn có cần mật khẩu chưa cung cấp: không thay đổi instance đó; dùng cluster riêng cổng 55432.
- pg_ctl bị sandbox hạn chế token: khởi động cụm riêng với quyền được duyệt.
- Windows Event Log không ghi được khi logger xử lý lỗi binding: cấu hình console logging trong repo và giữ binding rejection dưới dạng HTTP status, chạy lại đầy đủ các ca.
- CreatedAt ban đầu lệch 100 ns giữa POST và GET: reload scalar từ PostgreSQL sau SaveChanges; kiểm chứng body bằng nhau cả trước và sau restart.
- Không kiểm thử Docker compose vì Docker daemon chưa hoạt động. Không có hạng mục nghiệm thu HTTP nào còn chưa chạy. Chưa thực hiện kiểm thử tải hoặc triển khai production; không nằm trong Tuần 1.

Để chụp ảnh bàn giao, mở `create-response.txt` để thấy 201, Location và Pending; chạy lại GET đúng URI trên rồi chụp 200. Để minh chứng trực tiếp đầy đủ, tạo một đơn mới bằng OrderApi.http, chụp 201/Location, dừng/chạy lại API và chụp 200 từ đúng Location đó.

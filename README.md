# KitchenFlow — Tuần 1, Đề tài 04

Order API cho môn Phát triển ứng dụng phân tán. Phạm vi: menu, tạo đơn, tra cứu đơn và lưu PostgreSQL. Không triển khai RabbitMQ/Kitchen Worker (Tuần 2), hủy đơn/phát sự kiện (Tuần 3), SignalR/UI (Tuần 4).

## Môi trường

- .NET SDK 10.0.201 (global.json), ASP.NET Core Minimal API net10.0.
- EF Core/CLI 10.0.4, Npgsql EF provider 10.0.3.
- PostgreSQL 18; Docker Desktop là lựa chọn thay thế cho PostgreSQL cài trực tiếp.
- PowerShell 7 để chạy script kiểm thử (`pwsh`). Các lệnh dưới chạy từ thư mục gốc repository.

Thư mục ban đầu trống, không có AGENTS.md, solution hoặc metadata Git. Không tự tạo commit hay thay đổi instance PostgreSQL sẵn có.

## Kết nối PostgreSQL trên PowerShell

Dùng PostgreSQL có sẵn: tạo database `kitchenflow` và tài khoản có quyền tạo bảng, rồi đặt biến môi trường trong **mỗi cửa sổ PowerShell** chạy EF/API:

```powershell
$secret = Read-Host 'PostgreSQL password' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $secret).Password
$env:ConnectionStrings__Orders = "Host=localhost;Port=5432;Database=kitchenflow;Username=kitchenflow;Password=$password"
Remove-Variable password, secret
```

Đây là giá trị mẫu; thay host/port/database/user theo máy. Mật khẩu thật chỉ ở biến môi trường, không đưa vào Git. API đọc `ConnectionStrings:Orders` qua hệ thống configuration ASP.NET Core; thiếu cấu hình sẽ báo lỗi rõ ràng.

Hoặc dùng Docker (cần Docker Desktop đang chạy và port 5432 còn trống):

```powershell
$secret = Read-Host 'Password cho database demo' -AsSecureString
$env:POSTGRES_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
docker compose up -d
$env:ConnectionStrings__Orders = "Host=localhost;Port=5432;Database=kitchenflow;Username=kitchenflow;Password=$env:POSTGRES_PASSWORD"
Remove-Variable secret
```

Volume `postgres_data` giữ dữ liệu khi container khởi động lại. Không dùng `docker compose down -v` nếu cần giữ đơn.

## Build, migration và chạy

```powershell
dotnet tool restore
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet ef database update --project src/Order.Api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-build --project src/Order.Api --no-launch-profile --urls http://localhost:5080
```

Migration `InitialCreate` có khóa ngoại, khóa ghép OrderItem `(OrderId, MenuItemId)` và check `Quantity >= 1`. Không dùng EnsureCreated. Migration được áp dụng bằng CLI, API không tự thay schema. Seed chỉ chạy trong Development, dùng INSERT ON CONFLICT theo ID nên chạy lại không thêm trùng.

ID demo: khách `1 = Nguyen An`, `2 = Tran Binh`; món `1 = Pho bo`, `2 = Com ga`, `3 = Bun cha`.

## Mã nguồn

- `src/Order.Api/Program.cs`: cấu hình JSON, database, seed Development, đăng ký endpoint. Console logging tránh phụ thuộc quyền ghi Windows Event Log. `ThrowOnBadRequest=false` giữ phản hồi HTTP mặc định gọn khi binding thất bại.
- `Models/Entities.cs`: bốn entity và enum Pending/Preparing/Ready/Cancelled.
- `Data/OrderDbContext.cs`: quan hệ, ràng buộc, seed.
- `Contracts/OrderDtos.cs`: DTO tạo đơn, chi tiết và lỗi `{code,message}`.
- `Endpoints/OrderEndpoints.cs`: bốn endpoint và validation. Một SaveChangesAsync ghi toàn bộ graph trong transaction EF; reload scalar sau ghi để CreatedAt trong phản hồi khớp độ chính xác PostgreSQL.
- `Migrations/`: migration và model snapshot.
- `OrderApi.http`: request demo; `scripts/Test-OrderApi.ps1`: kiểm thử HTTP có assert.

ID đơn là Guid do server sinh, CreatedAt là UTC, trạng thái JSON là chuỗi. Tuần 1 chỉ tạo Pending. GET chi tiết trả customer `{id,name}` và items `{menuItemId,name,quantity}`. GET danh sách dùng cùng DTO, sắp CreatedAt giảm dần và Id giảm dần khi bằng thời điểm. Mỗi đơn qua API phải có ít nhất một dòng; DB có FK, quantity check và khóa ghép chống lặp món. Chưa có endpoint sửa/xóa khách/món.

## Demo và chụp minh chứng

1. Mở `OrderApi.http`, GET /menu để xem seed.
2. POST /orders với khách 1 và hai món (1: số lượng 2, 2: số lượng 1).
3. Chụp màn hình response gồm **201 Created**, header **Location: /orders/{id}** và JSON có **Pending**. Lưu đúng Location, không tạo đơn mới để thay thế khi kiểm tra restart.
4. GET chính Location, đối chiếu Id, customer, CreatedAt và số lượng 2/1; GET /orders để thấy đơn ở đầu.
5. Chạy các request lỗi trong tệp .http; mỗi lỗi validation có 400 và DTO code/message. JSON sai cú pháp: 400; Guid không parse được: 400; Content-Type text/plain: 415. Với cấu hình hiện tại cả ba lỗi binding trả body rỗng, không có Content-Type, không có DTO lỗi ứng dụng.
6. Ctrl+C dừng API. Chạy lại cùng lệnh và cùng database. GET lại chính Location đã lưu; chụp response 200 và cùng dữ liệu.

Tự động hóa bước 1–5 trong một cửa sổ PowerShell khác:

```powershell
pwsh -File scripts/Test-OrderApi.ps1
```

Script thêm một đơn hợp lệ mỗi lần chạy, kiểm tra validation không tạo thêm đơn, lưu Location và toàn bộ body ở `docs/created-order.json`, response 201 gồm header ở `docs/create-response.txt`. Sau khi **thật sự dừng/chạy lại API**, dùng:

```powershell
$location = (Get-Content docs/created-order.json -Raw | ConvertFrom-Json).location
pwsh -File scripts/Test-OrderApi.ps1 -SavedLocation $location
curl.exe -i "http://localhost:5080$location"
```

Tham số SavedLocation chỉ kiểm tra lại, không tạo đơn; assert response body khớp nguyên văn body POST đã lưu. Chỉ chạy chế độ tạo lại nếu muốn thay minh chứng cũ.

Để chụp 201 bằng curl trên PowerShell, tránh vấn đề quote JSON bằng tệp:

```powershell
'{"customerId":1,"items":[{"menuItemId":1,"quantity":2},{"menuItemId":2,"quantity":1}]}' | Set-Content -Encoding utf8 .local/demo-order.json
curl.exe -i http://localhost:5080/orders -H 'Content-Type: application/json' --data-binary '@.local/demo-order.json'
```

## Kết quả thực tế ngày 16/11/2026

Xem `docs/verification.md`, `docs/http-test-results.txt`, `docs/restart-test-results.txt`, `docs/create-response.txt` và `docs/seed-after-restart.txt`.

Máy có SDK 10.0.201, PostgreSQL 18.6 tại `D:/PostgreSQL/18/bin`. Docker daemon không chạy. NuGet.Config người dùng bị sandbox từ chối đọc; restore/install đã chạy với quyền được duyệt. PostgreSQL cổng 5432 có sẵn nhưng không có credential, nên đã tạo **cụm demo riêng** `.local/pgdata` trên `127.0.0.1:55432`, database `kitchenflow`, user `kitchenflow`, auth trust chỉ cho demo cục bộ. Không dùng trust cho triển khai thực tế.

Để chạy lại chính dữ liệu minh chứng trên máy này:

```powershell
# Nếu cụm riêng đã dừng (không chạy start khi đang hoạt động):
& D:/PostgreSQL/18/bin/pg_ctl.exe -D D:/KitchenFlow/.local/pgdata -l D:/KitchenFlow/.local/postgres.log -o '-p 55432 -h 127.0.0.1' start
$env:ConnectionStrings__Orders = 'Host=127.0.0.1;Port=55432;Database=kitchenflow;Username=kitchenflow'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-build --project src/Order.Api --no-launch-profile --urls http://localhost:5080
```

`.local` bị Git ignore; dữ liệu PostgreSQL không phải artifact bàn giao. Trên máy khác, tạo database, áp dụng migration, seed và chạy kiểm thử để có Location mới. Các đơn tạo trong những lần chẩn đoán trước vẫn được giữ trong database demo.

Tham khảo: [EF CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet), [Npgsql provider 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3).

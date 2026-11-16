# KitchenFlow

Hệ thống đặt món và xử lý tại bếp, được phát triển trong môn **Phát triển ứng dụng phân tán**.

KitchenFlow cung cấp API để khách hàng xem thực đơn, tạo đơn và tra cứu đơn hàng. Đơn hàng được lưu trong PostgreSQL và có thể truy cập lại sau khi khởi động lại ứng dụng.

## Chức năng

- Xem danh sách món ăn.
- Tạo đơn hàng gồm một hoặc nhiều món.
- Tra cứu chi tiết đơn theo mã và xem danh sách đơn mới nhất.
- Kiểm tra khách hàng, món ăn, số lượng và món bị trùng trước khi lưu.
- Cung cấp dữ liệu mẫu cho môi trường phát triển.

Phiên bản hiện tại triển khai Order API của Tuần 1. Đơn mới luôn có trạng thái `Pending`. Các trạng thái `Preparing`, `Ready` và `Cancelled` đã được khai báo; luồng xử lý tương ứng sẽ được bổ sung ở các giai đoạn sau.

## Công nghệ

- **.NET 10** và **ASP.NET Core Minimal API**.
- **Entity Framework Core 10** với **Npgsql**.
- **PostgreSQL 18**.
- **Docker Compose** để chạy PostgreSQL cục bộ (tùy chọn).
- **PowerShell 7** để chạy bộ kiểm thử HTTP.

## Cấu trúc dự án

```text
KitchenFlow/
├── src/Order.Api/
│   ├── Contracts/          # DTO cho request, response và lỗi
│   ├── Data/               # DbContext, quan hệ và seed dữ liệu
│   ├── Endpoints/          # Endpoint menu và đơn hàng
│   ├── Migrations/         # EF Core migrations
│   ├── Models/             # Entity và trạng thái đơn hàng
│   └── Program.cs          # Cấu hình và khởi động API
├── scripts/
│   └── Test-OrderApi.ps1    # Kiểm thử HTTP và kiểm tra dữ liệu sau restart
├── docs/                   # Tài liệu kiểm chứng và kết quả kiểm thử
├── compose.yaml            # PostgreSQL cho môi trường phát triển
├── OrderApi.http            # Các request mẫu
├── dotnet-tools.json        # Công cụ dotnet-ef cục bộ
├── global.json              # Phiên bản .NET SDK
└── KitchenFlow.slnx
```

## Yêu cầu môi trường

- .NET SDK **10.0.201** hoặc bản vá tương thích theo `global.json`.
- PostgreSQL 18 cài trực tiếp, hoặc Docker Desktop hỗ trợ Docker Compose.
- PowerShell 7 (`pwsh`) nếu sử dụng script kiểm thử.

Các lệnh dưới đây sử dụng PowerShell và chạy từ thư mục gốc dự án.

## Cài đặt và chạy

### 1. Lấy mã nguồn

```powershell
git clone https://github.com/vzmanhdev/KitchenFlow.git
cd KitchenFlow
dotnet tool restore
dotnet restore --configfile NuGet.Config
```

### 2. Chuẩn bị PostgreSQL

**Sử dụng Docker Compose**

Khởi động Docker Desktop, sau đó đặt mật khẩu database và chạy dịch vụ:

```powershell
$secret = Read-Host 'PostgreSQL password' -AsSecureString
$env:POSTGRES_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
Remove-Variable secret
docker compose up -d postgres
docker compose exec postgres pg_isready -U kitchenflow -d kitchenflow
```

Chờ đến khi PostgreSQL báo `accepting connections` trước khi áp dụng migration. Compose tạo database `kitchenflow`, user `kitchenflow` và mở cổng `5432` trên loopback. Cổng này cần còn trống.

```powershell
$env:ConnectionStrings__Orders = "Host=localhost;Port=5432;Database=kitchenflow;Username=kitchenflow;Password=$env:POSTGRES_PASSWORD"
```

**Sử dụng PostgreSQL có sẵn**

Tạo database và tài khoản có quyền tạo bảng, rồi thiết lập chuỗi kết nối theo môi trường của bạn:

```powershell
$secret = Read-Host 'PostgreSQL password' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $secret).Password
$env:ConnectionStrings__Orders = "Host=localhost;Port=5432;Database=kitchenflow;Username=kitchenflow;Password=$password"
Remove-Variable password, secret
```

API đọc cấu hình `ConnectionStrings:Orders`; biến môi trường tương ứng là `ConnectionStrings__Orders`. Thay host, port, database và username nếu cần. Không lưu mật khẩu thật trong mã nguồn. Biến môi trường chỉ có hiệu lực trong cửa sổ PowerShell hiện tại và các tiến trình được khởi chạy từ đó.

### 3. Build và áp dụng migration

```powershell
dotnet build --no-restore
dotnet ef database update --project src/Order.Api
```

Migration tạo schema và các ràng buộc dữ liệu. API không tự áp dụng migration khi khởi động.

### 4. Khởi động API

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-build --project src/Order.Api --no-launch-profile --urls http://localhost:5080
```

API lắng nghe tại `http://localhost:5080`. Dùng các đường dẫn `/menu` và `/orders` để gọi API; phiên bản hiện tại chưa có trang chủ hoặc giao diện web.

Trong môi trường `Development`, ứng dụng tự thêm dữ liệu mẫu nếu chưa tồn tại. Khởi động lại không tạo bản ghi seed trùng:

- Khách hàng: `1` — Nguyen An; `2` — Tran Binh.
- Món ăn: `1` — Pho bo; `2` — Com ga; `3` — Bun cha.

## Sử dụng API

Mở [OrderApi.http](OrderApi.http) bằng công cụ hỗ trợ HTTP request, hoặc sử dụng Postman/curl. Với Windows PowerShell, dùng `curl.exe`.

### Xem thực đơn

```http
GET /menu
```

Trả `200 OK` và danh sách món ăn gồm `id`, `name`.

### Tạo đơn hàng

```http
POST /orders
Content-Type: application/json

{
  "customerId": 1,
  "items": [
    { "menuItemId": 1, "quantity": 2 },
    { "menuItemId": 2, "quantity": 1 }
  ]
}
```

Trả `201 Created`, header `Location: /orders/{id}` và chi tiết đơn hàng. `id` là GUID do máy chủ sinh; `createdAt` do máy chủ đặt theo UTC; `status` là chuỗi `Pending`.

Phản hồi chi tiết có cấu trúc:

```json
{
  "id": "a00b9404-0e38-4523-9790-40a3d2171a15",
  "customer": { "id": 1, "name": "Nguyen An" },
  "createdAt": "2026-11-16T10:42:45.175543Z",
  "status": "Pending",
  "items": [
    { "menuItemId": 1, "name": "Pho bo", "quantity": 2 },
    { "menuItemId": 2, "name": "Com ga", "quantity": 1 }
  ]
}
```

ID và thời điểm trong ví dụ chỉ minh họa. Sử dụng `Location` nhận được từ request của bạn để tra cứu.

### Tra cứu đơn hàng

```http
GET /orders/{id}
```

Trả `200 OK` với chi tiết đơn, hoặc `404 Not Found` nếu GUID hợp lệ nhưng đơn không tồn tại.

### Xem danh sách đơn hàng

```http
GET /orders
```

Trả `200 OK` với danh sách chi tiết đơn, sắp xếp theo thời gian tạo giảm dần. Nếu chưa có đơn, kết quả là `[]`.

### Phản hồi lỗi

Lỗi kiểm tra dữ liệu trả `400 Bad Request` với `code` và `message`, ví dụ:

```json
{
  "code": "empty_order",
  "message": "At least one item is required."
}
```

Các mã lỗi gồm `empty_order`, `invalid_quantity`, `duplicate_menu_item`, `customer_not_found` và `menu_item_not_found`. Mỗi đơn cần ít nhất một món, số lượng từ 1 trở lên và mỗi mã món chỉ xuất hiện một lần.

Request bị ASP.NET Core từ chối trước khi vào handler có định dạng khác: JSON sai cú pháp hoặc GUID không hợp lệ trả `400`; Content-Type không được hỗ trợ trả `415`. Các phản hồi này có body rỗng theo cấu hình hiện tại.

## Kiểm thử

Khi API đang chạy tại cổng `5080`, mở một cửa sổ PowerShell khác tại thư mục dự án:

```powershell
pwsh -File scripts/Test-OrderApi.ps1
```

Script kiểm tra menu, tạo đơn, tra cứu, danh sách đơn và các trường hợp dữ liệu không hợp lệ. Mỗi lượt chạy tạo một đơn hợp lệ và ghi đè các tệp `docs/created-order.json`, `docs/create-response.txt` bằng kết quả mới nhất.

Để kiểm tra tính bền vững của dữ liệu, dừng API bằng `Ctrl+C`, chạy lại với cùng chuỗi kết nối, sau đó gọi:

```powershell
$location = (Get-Content docs/created-order.json -Raw | ConvertFrom-Json).location
pwsh -File scripts/Test-OrderApi.ps1 -SavedLocation $location
```

Chế độ `-SavedLocation` tra cứu đúng URI đã lưu và đối chiếu toàn bộ body với phản hồi tạo đơn, không tạo thêm đơn mới. Chi tiết kết quả kiểm chứng được lưu riêng tại [docs/verification.md](docs/verification.md).

## Dữ liệu và dịch vụ cục bộ

Đơn hàng và các dòng món được lưu cùng nhau trong một transaction của EF Core. Khóa ngoại liên kết đơn với khách hàng và món ăn; khóa ghép của dòng món ngăn trùng món trong cùng đơn.

Khi sử dụng Docker, dữ liệu được giữ trong volume `postgres_data`. Để dừng PostgreSQL mà vẫn giữ dữ liệu:

```powershell
docker compose down
```

Lệnh `docker compose down -v` sẽ xóa volume và dữ liệu database.

## Lộ trình phát triển

- **Tuần 1:** Order API, PostgreSQL và kiểm thử HTTP — đã triển khai.
- **Tuần 2:** RabbitMQ và Kitchen Worker.
- **Tuần 3:** Hủy đơn và phát sự kiện.
- **Tuần 4:** SignalR và giao diện người dùng.

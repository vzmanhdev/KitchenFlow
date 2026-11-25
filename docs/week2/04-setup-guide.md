# 04 — Chạy và demo

Chạy PowerShell 7 từ thư mục gốc. Cần .NET 10 SDK và Docker Desktop với Linux Engine hoạt động. Dùng dự án Compose hiện tại hoặc chọn tên riêng với `-p` để có volume riêng.

```powershell
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N')
$env:RABBITMQ_PASSWORD = [guid]::NewGuid().ToString('N')
$env:RABBITMQ_USER = 'kitchenflow'
$env:POSTGRES_PORT = '5432'
$env:RABBITMQ_MANAGEMENT_PORT = '15672'
$env:ORDER_API_PORT = '5080'
$env:KITCHEN_COOKING_SECONDS = '8'

docker compose up -d --wait postgres rabbitmq
dotnet restore --configfile NuGet.Config
dotnet tool restore
$env:ConnectionStrings__Orders = "Host=127.0.0.1;Port=$env:POSTGRES_PORT;Database=kitchenflow;Username=kitchenflow;Password=$env:POSTGRES_PASSWORD"
dotnet ef database update --project src/Order.Api
docker compose up -d --build order-api
```

Giữ lại thông tin xác thực đã dùng nếu volume đã tồn tại. Các cổng host có thể cấu hình qua `POSTGRES_PORT`, `RABBITMQ_PORT`, `RABBITMQ_MANAGEMENT_PORT` và `ORDER_API_PORT`.

```powershell
$base = "http://localhost:$env:ORDER_API_PORT"
Invoke-RestMethod "$base/menu"
$body = '{"customerId":1,"items":[{"menuItemId":1,"quantity":2}]}'
$order = Invoke-RestMethod "$base/orders" -Method Post -ContentType application/json -Body $body
Invoke-RestMethod "$base/orders/$($order.id)"
```

Mở Management UI tại `http://localhost:15672` hoặc cổng đã đặt và đăng nhập bằng `RABBITMQ_USER` và `RABBITMQ_PASSWORD`. Trong **Queues and Streams**, mở `kitchen.orders` để xem message chờ. API khai báo queue khi publish lần đầu. `kitchen.orders.dead` giữ các message bị loại hoặc hết giới hạn giao lại.

```powershell
docker compose up -d --build kitchen-worker
1..12 | ForEach-Object {
    (Invoke-RestMethod "$base/orders/$($order.id)").status
    Start-Sleep -Seconds 1
}
docker compose logs --tail 80 order-api kitchen-worker
docker compose stop kitchen-worker
```

Khi Worker dừng, tạo ba đơn và kiểm tra `Ready` trong Management UI là số message đang chờ. Khởi động Worker bằng `docker compose start kitchen-worker` để theo dõi các đơn lần lượt chuyển trạng thái.

Để chạy API và Worker dưới dạng hai tiến trình .NET trên host, sau khi khởi động hạ tầng và migration, đặt các biến sau trong hai cửa sổ PowerShell.

```powershell
$env:RabbitMQ__Host = '127.0.0.1'
$env:RabbitMQ__Port = '5672' # dùng cổng host đã chọn
$env:RabbitMQ__User = $env:RABBITMQ_USER
$env:RabbitMQ__Password = $env:RABBITMQ_PASSWORD
$env:Kitchen__CookingSeconds = '8'
```

Đặt cùng `ConnectionStrings__Orders` trong cả hai cửa sổ. Cửa sổ API chạy `dotnet run --project src/Order.Api`. Cửa sổ Worker chạy `dotnet run --project src/KitchenFlow.KitchenWorker`.

Chạy `pwsh -NoProfile -File scripts/Test-Week2.ps1` để tạo môi trường kiểm thử riêng. Script dùng các cổng 55432, 55672, 55673 và 55080, đồng thời giữ lại dự án Compose sau lần chạy để xem log.

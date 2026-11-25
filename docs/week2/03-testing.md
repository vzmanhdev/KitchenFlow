# 03 — Kết quả kiểm thử Tuần 2

Chạy ngày 25/11/2026 trên Windows với .NET SDK 10.0.201, PostgreSQL 18 và RabbitMQ 4.2.9. API và Worker chạy thành hai tiến trình. Bộ kiểm tra dùng HTTP, SQL qua `psql`, RabbitMQ Management HTTP API và log ứng dụng. Luồng sử dụng các bước kiểm thử trong `scripts/Test-Week2.ps1`, với lệnh điều khiển Compose thay bằng lệnh điều khiển tiến trình Windows cho lần chạy này. Thời gian mô phỏng là năm giây.

| Ca | Kết quả | Quan sát |
| --- | --- | --- |
| TC01 | PASS | API kết nối AMQP, Worker đăng ký `kitchen.orders`. |
| TC02 | PASS | HTTP 201, Location đúng, body và truy vấn SQL đều là `Pending` khi Worker dừng. |
| TC03 | PASS | Log xác nhận của RabbitMQ khớp OrderId. |
| TC04 | PASS | GET chi tiết trả `Preparing` trong lúc mô phỏng. |
| TC05 | PASS | Cùng đơn chuyển `Ready`, SQL xác nhận trạng thái cuối. |
| TC06 | PASS | Ba đơn đều hoàn tất. Log của từng đơn ghi `Ready` trước khi đơn kế tiếp ghi `Preparing`. |
| TC07 | PASS | Ba POST trả 201 khi Worker dừng. Management API ghi `messages_ready = 3`, API đọc cả ba ở `Pending`. |
| TC08 | PASS | Sau khi khởi động Worker, cả ba đến `Ready`. |
| TC09 | PASS | Phát lại cùng OrderId, Worker ghi `Skipping order … in status Ready` và trạng thái giữ nguyên. |
| TC10 | PASS | Payload `{` được ghi log và đi vào DLQ. Đơn hợp lệ tiếp theo đến `Ready`. |
| TC11 | PASS | Khởi động lại API, GET chi tiết vẫn trả đơn `Ready` đã lưu. |
| TC12 | PASS | Dừng broker, POST trả 201, SQL giữ `Pending`, API ghi lỗi mà không ghi confirm. Khởi động broker, Worker đăng ký lại và đơn mới đến `Ready`. |
| TC13 | PASS | Hồi quy cả bốn endpoint, gồm 201/200, 404, 400, 415 và số đơn sau request không hợp lệ. |

## Minh chứng

```http
HTTP/1.1 201 Created
Location: /orders/1aea636d-5165-4e4f-ba6c-71c0e170487a

{"id":"1aea636d-5165-4e4f-ba6c-71c0e170487a","customer":{"id":1,"name":"Nguyen An"},"createdAt":"2026-11-25T06:39:38.454023Z","status":"Pending","items":[{"menuItemId":1,"name":"Pho bo","quantity":2}]}
```

Các dòng trạng thái sau khi broker đã khôi phục, lấy từ PostgreSQL.

```text
1aea636d-5165-4e4f-ba6c-71c0e170487a|Ready
e8b7ea82-f652-489b-a270-0b21d53a025f|Pending
7e0a1fe6-3b4c-4852-8c5a-845871a86cb5|Ready
```

Log khi broker ngắt rồi khôi phục.

```text
Order e8b7ea82-f652-489b-a270-0b21d53a025f is saved but delivery of message 3c9da6bf-7cbd-4dc1-8cd8-70ee3f7e6990 could not be verified. Check the existing order before retrying.
Kitchen worker connected to RabbitMQ and subscribed to kitchen.orders
RabbitMQ confirmed message 9b5749bf-75a4-4769-9db1-4a490442c0b8 for order 7e0a1fe6-3b4c-4852-8c5a-845871a86cb5
Order 7e0a1fe6-3b4c-4852-8c5a-845871a86cb5 is Preparing
Order 7e0a1fe6-3b4c-4852-8c5a-845871a86cb5 is Ready
```

## Kiểm tra công cụ và giới hạn

- Build solution có 0 lỗi, 0 cảnh báo. Kiểm thử serialization PASS. Hai tiến trình publish được ở cấu hình Release. Script PowerShell qua kiểm tra cú pháp.
- `docker compose --profile apps config --quiet` PASS. Runtime Compose chưa được xác minh do Docker Desktop lỗi ở socket `dockerInference` và không tạo Linux Engine pipe.
- Kiểm tra bổ sung PASS khi dừng Worker giữa `Preparing` rồi khởi động lại. Worker ghi log tiếp tục mô phỏng và đơn `d747ea79-ef4d-453a-9374-324110b4a8da` đến `Ready`.
- Kiểm tra bổ sung PASS khi dừng PostgreSQL trong lúc đơn chờ trong queue. Worker ghi lỗi tạm thời và đưa message trở lại queue sau thời gian chờ. Truy vấn SQL thấy đơn `913d0c4e-b081-40ba-8dd5-15e4a8640849` ở `Ready` sau khi PostgreSQL khởi động lại. Một GET đầu tiên trong lần thử trước gặp kết nối pooled cũ và trả HTTP 500.
- Hàng đợi được kiểm tra qua Management HTTP API. Lần chạy này không thu ảnh giao diện.

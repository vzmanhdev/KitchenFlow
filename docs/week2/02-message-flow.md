# 02 — Luồng message

1. `POST /orders` kiểm tra dữ liệu, ghi đơn `Pending` và dòng món trong transaction EF Core.
2. Sau khi commit, API gửi JSON gồm `messageId`, `orderId`, `createdAt` theo UTC và `correlationId`. API bật mandatory routing, publisher confirms và chờ broker trong một lần thử có deadline. Các lần POST tiếp theo mở kết nối mới nên có thể tiếp tục sau khi broker phục hồi.
3. API trả `201 Created`, `Location: /orders/{id}` và chi tiết đơn vừa lưu. Khi không xác nhận được publish, API ghi log với cả hai ID. Phản hồi vẫn thể hiện đơn đã lưu. Client dùng Location để tra cứu, vì gửi lại POST sẽ tạo đơn mới.
4. Worker nhận message với manual ACK. Câu lệnh cập nhật có điều kiện chỉ đổi `Pending` thành `Preparing`. Trạng thái khả dụng ngay qua API.
5. Worker chờ bằng `Task.Delay` với cancellation token, rồi cập nhật có điều kiện từ `Preparing` sang `Ready` và ACK sau khi lưu.
6. Worker ACK các đơn đã `Ready` hoặc `Cancelled` sau khi xác minh trạng thái. Khi nhận đơn `Preparing`, Worker tiếp tục mô phỏng từ điểm đã lưu. Mô phỏng chỉ gồm thời gian chờ và không có tác dụng phụ bên ngoài. Khi Worker dừng giữa chừng, việc đóng kênh trả delivery chưa ACK về queue.
7. JSON lỗi hoặc đơn không tồn tại đi vào dead-letter queue. Lỗi PostgreSQL tạm thời chờ ba giây trước khi requeue. Quorum queue giới hạn số lần giao lại ở mức năm. Worker kết nối lại sau ba giây khi subscription bị mất.

## Giới hạn trước Tuần 3

Giữa commit PostgreSQL và xác nhận RabbitMQ có một khoảng gián đoạn có thể khiến đơn nằm ở `Pending`. Khi timeout xảy ra, broker cũng có thể đã nhận message nhưng API chưa nhận được confirm. Kiểm tra đơn và log trước khi cân nhắc phát lại message cho cùng `OrderId`.

Các đơn Tuần 1 vẫn tra cứu được. Đơn `Pending` đã tồn tại từ trước cần một message riêng nếu muốn đưa vào luồng bếp.

Tuần 3 sẽ lưu sự kiện outbox cùng transaction tạo đơn và dùng một tiến trình phát lại sự kiện đã lưu. Message ID, publisher confirms và các cập nhật trạng thái có điều kiện là những phần có thể tái sử dụng.

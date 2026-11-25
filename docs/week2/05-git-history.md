# 05 — Lịch sử Git Tuần 2

Nhánh làm việc và nhánh GitHub là `feature/week2-rabbitmq-kitchen`.

| Giai đoạn | Hash | Commit | Kiểm tra |
| --- | --- | --- | --- |
| 01 | `ee39174` | `feat(infra): configure RabbitMQ messaging infrastructure` | Build và Compose config PASS |
| 02 | `bd3dc87` | `feat(messaging): add order created message contracts` | Build và kiểm thử JSON PASS |
| 03 | `6116e86` | `feat(orders): publish new orders to RabbitMQ` | Build và kiểm thử JSON PASS |
| 04 | `0d621c5` | `feat(kitchen): implement asynchronous kitchen order worker` | Build, JSON và Compose config PASS |
| 05 | `f99c932` | `test(integration): verify RabbitMQ and kitchen order processing` | Build, JSON, PowerShell và TC01–TC13 trên Windows PASS. Xem [03 — Testing](03-testing.md) |
| 06 | Tra bằng lệnh bên dưới | `docs(week2): document asynchronous kitchen workflow` | Build, JSON và kiểm tra tài liệu |

Giai đoạn 01–05 đã push thành công. Commit 06 chứa chính tệp này nên hash được tra từ ref sau khi commit và push.

```powershell
git log -6 --format='%h %s' feature/week2-rabbitmq-kitchen
git rev-parse feature/week2-rabbitmq-kitchen
git ls-remote --heads origin feature/week2-rabbitmq-kitchen
```

Hai lệnh cuối cho cùng hash khi nhánh local và GitHub đã đồng bộ.

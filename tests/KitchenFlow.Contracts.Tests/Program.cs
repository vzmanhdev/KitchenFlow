using System.Text.Json;
using KitchenFlow.Contracts;

var message = new OrderCreatedMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "test");
var bytes = JsonSerializer.SerializeToUtf8Bytes(message, OrderMessaging.Json);
using var document = JsonDocument.Parse(bytes);
var json = document.RootElement;
if (json.GetProperty("messageId").GetGuid() != message.MessageId
    || json.GetProperty("orderId").GetGuid() != message.OrderId
    || json.GetProperty("createdAt").GetDateTime() != message.CreatedAt
    || json.GetProperty("correlationId").GetString() != message.CorrelationId
    || JsonSerializer.Deserialize<OrderCreatedMessage>(bytes, OrderMessaging.Json) != message)
    throw new InvalidOperationException("OrderCreatedMessage wire contract changed.");

Console.WriteLine("PASS: OrderCreatedMessage JSON contract");

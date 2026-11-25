using System.Text.Json;

namespace KitchenFlow.Contracts;

public sealed record OrderCreatedMessage(Guid MessageId, Guid OrderId, DateTime CreatedAt, string? CorrelationId = null);

public static class OrderMessaging
{
    public const string Queue = "kitchen.orders";
    public const string DeadLetterQueue = "kitchen.orders.dead";
    public static JsonSerializerOptions Json { get; } = JsonSerializerOptions.Web;
}

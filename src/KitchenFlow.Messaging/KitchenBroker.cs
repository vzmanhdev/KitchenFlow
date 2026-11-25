using KitchenFlow.Contracts;
using RabbitMQ.Client;

namespace KitchenFlow.Messaging;

public static class KitchenBroker
{
    public static ConnectionFactory CreateFactory(string? host, int port, string? user, string? password) => new()
    {
        HostName = host ?? "localhost",
        Port = port,
        UserName = user ?? "kitchenflow",
        Password = password ?? "",
        AutomaticRecoveryEnabled = false,
        RequestedHeartbeat = TimeSpan.FromSeconds(10)
    };

    public static async Task DeclareQueuesAsync(IChannel channel, CancellationToken token)
    {
        await channel.QueueDeclareAsync(OrderMessaging.DeadLetterQueue, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: token);
        await channel.QueueDeclareAsync(OrderMessaging.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-single-active-consumer"] = true,
                ["x-delivery-limit"] = 5,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = OrderMessaging.DeadLetterQueue
            }, cancellationToken: token);
    }
}

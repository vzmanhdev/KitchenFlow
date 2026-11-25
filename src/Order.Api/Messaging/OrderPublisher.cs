using System.Text.Json;
using KitchenFlow.Contracts;
using KitchenFlow.Messaging;
using RabbitMQ.Client;

namespace Order.Api.Messaging;

public sealed class OrderPublisher(IConfiguration config, ILogger<OrderPublisher> logger)
{
    private readonly ConnectionFactory factory = KitchenBroker.CreateFactory(config["RabbitMQ:Host"],
        config.GetValue("RabbitMQ:Port", 5672), config["RabbitMQ:User"], config["RabbitMQ:Password"]);

    public async Task PublishAsync(OrderCreatedMessage message)
    {
        // The order is already committed. Allow this bounded attempt even if the HTTP client disconnects.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = deadline.Token;
        try
        {
            await using var connection = await factory.CreateConnectionAsync("kitchenflow-order-api", token);
            logger.LogInformation("Order API connected to RabbitMQ");
            await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
            await KitchenBroker.DeclareQueuesAsync(channel, token);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.MessageId.ToString(),
                CorrelationId = message.CorrelationId
            };
            await channel.BasicPublishAsync("", OrderMessaging.Queue, mandatory: true, properties,
                JsonSerializer.SerializeToUtf8Bytes(message, OrderMessaging.Json), token);
            logger.LogInformation("RabbitMQ confirmed message {MessageId} for order {OrderId}",
                message.MessageId, message.OrderId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Order {OrderId} is saved but delivery of message {MessageId} could not be verified. Check the existing order before retrying.",
                message.OrderId, message.MessageId);
        }
    }
}

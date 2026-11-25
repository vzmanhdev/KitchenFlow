using System.Text.Json;
using KitchenFlow.Contracts;
using KitchenFlow.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Order.Api.Data;
using Order.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KitchenFlow.KitchenWorker;

public sealed class KitchenConsumer(IConfiguration config, IServiceScopeFactory scopes,
    ILogger<KitchenConsumer> logger) : BackgroundService
{
    private readonly ConnectionFactory factory = KitchenBroker.CreateFactory(config["RabbitMQ:Host"],
        config.GetValue("RabbitMQ:Port", 5672), config["RabbitMQ:User"], config["RabbitMQ:Password"]);
    private readonly TimeSpan cookingTime = TimeSpan.FromSeconds(
        Math.Clamp(config.GetValue("Kitchen:CookingSeconds", 3), 0, 600));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                    logger.LogWarning("Kitchen subscription ended. Reconnecting to RabbitMQ.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Kitchen subscription failed. Reconnecting to RabbitMQ.");
            }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await using var connection = await factory.CreateConnectionAsync("kitchenflow-worker", stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        using var deliveryLifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            ended.TrySetResult();
            return Task.CompletedTask;
        };
        await KitchenBroker.DeclareQueuesAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 1, global: false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.UnregisteredAsync += (_, _) =>
        {
            ended.TrySetResult();
            return Task.CompletedTask;
        };
        var deliveryToken = deliveryLifetime.Token;
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await ProcessDeliveryAsync(channel, delivery, deliveryToken);
            }
            catch (OperationCanceledException) when (deliveryToken.IsCancellationRequested)
            {
                // Closing the channel returns the unacknowledged delivery to RabbitMQ.
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Delivery {DeliveryTag} could not be settled", delivery.DeliveryTag);
                ended.TrySetResult();
            }
        };
        await channel.BasicConsumeAsync(OrderMessaging.Queue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Kitchen worker connected to RabbitMQ and subscribed to {Queue}", OrderMessaging.Queue);
        try
        {
            await ended.Task.WaitAsync(stoppingToken);
        }
        finally
        {
            await deliveryLifetime.CancelAsync();
        }
    }

    private async Task ProcessDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken token)
    {
        try
        {
            var message = JsonSerializer.Deserialize<OrderCreatedMessage>(delivery.Body.Span, OrderMessaging.Json);
            if (message is null || message.OrderId == Guid.Empty || message.MessageId == Guid.Empty
                || message.CreatedAt == default)
                throw new JsonException("OrderId, MessageId and CreatedAt are required.");

            if (!await PrepareAsync(message.OrderId, token))
            {
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, token);
                return;
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid kitchen message {MessageId}", delivery.BasicProperties.MessageId);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, token);
            return;
        }
        catch (Exception exception) when (exception is NpgsqlException { IsTransient: true } or TimeoutException
            || exception.InnerException is NpgsqlException { IsTransient: true })
        {
            logger.LogWarning(exception, "Transient database failure for message {MessageId}; retrying after delay",
                delivery.BasicProperties.MessageId);
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: true, token);
            return;
        }
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, token);
    }

    private async Task<bool> PrepareAsync(Guid orderId, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = db.Orders.Where(x => x.Id == orderId);
        var claimed = await order.Where(x => x.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, OrderStatus.Preparing), token);
        if (claimed == 1)
        {
            logger.LogInformation("Order {OrderId} is Preparing", orderId);
        }
        else
        {
            var status = await order.Select(x => (OrderStatus?)x.Status).SingleOrDefaultAsync(token);
            if (status is null)
            {
                logger.LogWarning("Order {OrderId} was not found", orderId);
                return false;
            }
            if (status is OrderStatus.Ready or OrderStatus.Cancelled)
            {
                logger.LogInformation("Skipping order {OrderId} in status {Status}", orderId, status);
                return true;
            }
            if (status != OrderStatus.Preparing)
                throw new InvalidOperationException($"Unexpected order status {status} for {orderId}.");
            // A prior attempt persisted Preparing. The simulation has no external side effects to repeat.
            logger.LogInformation("Resuming simulation for order {OrderId} in Preparing", orderId);
        }

        await Task.Delay(cookingTime, token);
        var updated = await order.Where(x => x.Status == OrderStatus.Preparing)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, OrderStatus.Ready), token);
        if (updated == 1)
        {
            logger.LogInformation("Order {OrderId} is Ready", orderId);
            return true;
        }
        var current = await order.Select(x => (OrderStatus?)x.Status).SingleOrDefaultAsync(token);
        logger.LogWarning("Order {OrderId} changed during preparation to {Status}", orderId, current);
        return current is OrderStatus.Ready or OrderStatus.Cancelled;
    }
}

using Order.Api.Models;
namespace Order.Api.Contracts;

public record CreateOrderRequest(int CustomerId, List<CreateOrderItemRequest?>? Items);
public record CreateOrderItemRequest(int MenuItemId, int Quantity);
public record ErrorResponse(string Code, string Message);
public record CustomerResponse(int Id, string Name);
public record MenuItemResponse(int Id, string Name);
public record OrderItemResponse(int MenuItemId, string Name, int Quantity);
public record OrderResponse(Guid Id, CustomerResponse Customer, DateTime CreatedAt,
    OrderStatus Status, List<OrderItemResponse> Items);

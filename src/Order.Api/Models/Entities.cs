namespace Order.Api.Models;

public enum OrderStatus { Pending, Preparing, Ready, Cancelled }
public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<Order> Orders { get; set; } = [];
}
public class MenuItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
public class Order
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
public class OrderItem
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public int Quantity { get; set; }
}

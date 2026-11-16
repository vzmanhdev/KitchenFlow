using Microsoft.EntityFrameworkCore;
using Order.Api.Contracts;
using Order.Api.Data;
using Order.Api.Models;
namespace Order.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/menu", async (OrderDbContext db) => Results.Ok(await db.MenuItems.AsNoTracking()
            .OrderBy(x => x.Id).Select(x => new MenuItemResponse(x.Id, x.Name)).ToListAsync()));
        app.MapPost("/orders", CreateOrder);
        app.MapGet("/orders/{id}", async (Guid id, OrderDbContext db) =>
        {
            var order = await Details(db).SingleOrDefaultAsync(x => x.Id == id);
            return order is null ? Results.NotFound(new ErrorResponse("order_not_found", "Order does not exist."))
                : Results.Ok(ToResponse(order));
        });
        app.MapGet("/orders", async (OrderDbContext db) => Results.Ok((await Details(db)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync()).Select(ToResponse)));
    }
    private static async Task<IResult> CreateOrder(CreateOrderRequest request, OrderDbContext db)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Bad("empty_order", "At least one item is required.");
        if (request.Items.Any(x => x is null || x.Quantity < 1))
            return Bad("invalid_quantity", "Every item must have quantity at least 1.");
        var ids = request.Items.Select(x => x!.MenuItemId).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            return Bad("duplicate_menu_item", "Each menu item must appear only once.");
        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null) return Bad("customer_not_found", "Customer does not exist.");
        var menu = await db.MenuItems.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        if (menu.Count != ids.Length) return Bad("menu_item_not_found", "One or more menu items do not exist.");
        var order = new Models.Order
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
            CreatedAt = DateTime.UtcNow, Status = OrderStatus.Pending,
            Items = request.Items.Select(x => new OrderItem
            { MenuItemId = x!.MenuItemId, MenuItem = menu[x.MenuItemId], Quantity = x.Quantity }).ToList()
        };
        db.Orders.Add(order);
        // EF Core wraps this single save of the entire graph in a transaction.
        await db.SaveChangesAsync();
        await db.Entry(order).ReloadAsync();
        return Results.Created($"/orders/{order.Id}", ToResponse(order));
    }
    private static IResult Bad(string code, string message) => Results.BadRequest(new ErrorResponse(code, message));
    private static IQueryable<Models.Order> Details(OrderDbContext db) => db.Orders.AsNoTracking()
        .Include(x => x.Customer).Include(x => x.Items).ThenInclude(x => x.MenuItem);
    private static OrderResponse ToResponse(Models.Order x) => new(x.Id,
        new(x.CustomerId, x.Customer.Name), x.CreatedAt, x.Status,
        x.Items.OrderBy(i => i.MenuItemId).Select(i => new OrderItemResponse(i.MenuItemId, i.MenuItem.Name, i.Quantity)).ToList());
}


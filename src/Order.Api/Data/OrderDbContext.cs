using Microsoft.EntityFrameworkCore;
using Order.Api.Models;
namespace Order.Api.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Models.Order> Orders => Set<Models.Order>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Customer>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<MenuItem>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<Models.Order>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        model.Entity<Models.Order>().HasOne(x => x.Customer).WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderItem>().HasKey(x => new { x.OrderId, x.MenuItemId });
        model.Entity<OrderItem>().HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
        model.Entity<OrderItem>().HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<OrderItem>().ToTable(t => t.HasCheckConstraint("CK_OrderItem_Quantity", "\"Quantity\" >= 1"));
    }
    public async Task SeedDevelopmentAsync()
    {
        // Fixed IDs and ON CONFLICT make repeated or concurrent development starts safe.
        await Database.ExecuteSqlRawAsync("""
            INSERT INTO "Customers" ("Id", "Name") VALUES (1, 'Nguyen An'), (2, 'Tran Binh') ON CONFLICT ("Id") DO NOTHING;
            INSERT INTO "MenuItems" ("Id", "Name") VALUES (1, 'Pho bo'), (2, 'Com ga'), (3, 'Bun cha') ON CONFLICT ("Id") DO NOTHING;
            """);
    }
}

using Microsoft.EntityFrameworkCore;
using MicroShop.Services.Ordering.Domain;

namespace MicroShop.Services.Ordering.Infrastructure;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Total).HasColumnType("numeric(18,2)");
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId);
            e.HasIndex(x => x.UserId);
        });
        b.Entity<OrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
        });
    }
}

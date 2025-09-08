using Microsoft.EntityFrameworkCore;
using MicroShop.Services.Catalog.Domain;


namespace MicroShop.Services.Catalog.Infrastructure;


public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Price).HasColumnType("numeric(18,2)");
        });
    }
}
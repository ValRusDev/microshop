using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MicroShop.Services.Identity.Infrastructure;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var cs = "Host=localhost;Port=5434;Database=identitydb;Username=identity;Password=identity";
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs).Options;
        return new ApplicationDbContext(opts);
    }
}

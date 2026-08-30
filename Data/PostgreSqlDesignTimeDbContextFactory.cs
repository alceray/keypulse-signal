#if DEBUG
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KeyPulse.Data;

public sealed class PostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlApplicationDbContext>
{
    public PostgreSqlApplicationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder
            .UseLazyLoadingProxies()
            .UseNpgsql("Host=localhost;Database=keypulse_design;Username=keypulse;Password=design-only");
        return new PostgreSqlApplicationDbContext(builder.Options);
    }
}
#endif

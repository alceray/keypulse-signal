#if DEBUG
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KeyPulse.Data;

// Pin the base context to SQLite even when the user's active database is PostgreSQL.
public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=keypulse_design.db").Options);
}
#endif

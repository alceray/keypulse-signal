using System.IO;
using KeyPulse.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KeyPulse.Tests.Infrastructure;

/// <summary>
/// A throwaway, file-backed SQLite database for exercising services that depend on
/// <see cref="IDbContextFactory{ApplicationDbContext}"/>.
///
/// A real temp file (not <c>:memory:</c>) is used so every context the factory hands out shares the
/// same data — matching how the app's services repeatedly create/dispose contexts. The schema is
/// built from the EF model (default) or via real migrations, so the actual value converters and
/// indexes from <see cref="ApplicationDbContext.OnModelCreating"/> are exercised by the tests.
///
/// Dispose deletes the temp files; create one per test (it is cheap and keeps tests isolated).
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _dbPath;

    public IDbContextFactory<ApplicationDbContext> Factory { get; }

    public SqliteTestDatabase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"keypulse-test-{Guid.NewGuid():N}.db");
        Factory = new FileBackedFactory($"Data Source={_dbPath}");

        // Build the schema from the model. This also lets DataService construct: its ctor calls
        // Database.Migrate(), which (a) applies zero migrations here — they're attributed to the base
        // ApplicationDbContext, not this subclass — so it doesn't conflict with the existing tables,
        // and (b) is downgraded from the spurious pending-changes error by the warning suppression below.
        using var ctx = Factory.CreateDbContext();
        ctx.Database.EnsureCreated();
    }

    /// <summary>Opens a fresh context against the test database (caller disposes).</summary>
    public ApplicationDbContext CreateContext() => Factory.CreateDbContext();

    /// <summary>
    /// Creates the <c>AppMeta</c> key/value table, which the app builds lazily at runtime rather than
    /// through an EF migration — so neither EnsureCreated nor Migrate emits it.
    /// </summary>
    public void EnsureAppMetaTable()
    {
        using var ctx = CreateContext();
        ctx.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS AppMeta (MetaKey TEXT PRIMARY KEY NOT NULL, MetaValue TEXT NOT NULL);"
        );
    }

    public void Dispose()
    {
        // Return pooled connections so the file handle is released before we delete it.
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            TryDelete(_dbPath + suffix);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort — the OS reclaims temp files regardless.
        }
    }

    private sealed class FileBackedFactory(string connectionString) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new TestApplicationDbContext(connectionString);
    }

    /// <summary>
    /// Overrides only the connection target. The base <see cref="ApplicationDbContext.OnConfiguring"/>
    /// points at the real <c>%AppData%</c> database, so we intentionally do not chain to it — but the
    /// inherited <c>OnModelCreating</c> (converters, indexes) still applies. MigrationsAssembly is set
    /// to the app assembly so <c>Migrate()</c> finds the migrations through this subclass.
    /// </summary>
    private sealed class TestApplicationDbContext(string connectionString) : ApplicationDbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseLazyLoadingProxies()
                .UseSqlite(
                    connectionString,
                    o => o.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)
                )
                // The model snapshot is attributed to ApplicationDbContext, not this subclass, so EF
                // reports a false "pending model changes" during Migrate(). The migrations are correct
                // (production applies the same set), so ignore the spurious guard.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }
    }
}

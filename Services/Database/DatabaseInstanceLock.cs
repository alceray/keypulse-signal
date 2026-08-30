using KeyPulse.Configuration;
using KeyPulse.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;

namespace KeyPulse.Services;

/// <summary>Prevents two KeyPulse processes from writing to the same PostgreSQL database.</summary>
public sealed class DatabaseInstanceLock : IDisposable
{
    private readonly object _gate = new();
    private NpgsqlConnection? _connection;

    public void Acquire(ApplicationDbContext context)
    {
        if (!context.Database.IsNpgsql())
            return;

        Acquire(context.Database.GetConnectionString()!);
    }

    /// <summary>Takes the lock from a connection string, before any database-backed service exists.</summary>
    public void Acquire(string connectionString)
    {
        lock (_gate)
        {
            if (_connection != null)
                return;

            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@lock_key);";
            command.Parameters.AddWithValue("lock_key", AppConstants.App.PostgreSqlAdvisoryLockKey);
            if (command.ExecuteScalar() is not true)
            {
                connection.Dispose();
                throw new InvalidOperationException(
                    "This PostgreSQL database is already in use by another KeyPulse process"
                );
            }

            _connection = connection;
            Log.Debug("Exclusive PostgreSQL database lock acquired");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_connection == null)
                return;

            try
            {
                // Closing the connection returns it to the pool, where its reset is deferred and the lock
                // would survive. Releasing it here frees the database for the next writer straight away.
                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(@lock_key);";
                command.Parameters.AddWithValue("lock_key", AppConstants.App.PostgreSqlAdvisoryLockKey);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to release the exclusive database lock");
            }
            finally
            {
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}

using Campfire.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Campfire.Api.Tests.Integration;

/// <summary>
/// Real Postgres, not EF InMemory: this is what makes these Integration tests,
/// not Unit tests, per the course's Collofello framing. Points at the same
/// docker-compose Postgres container as local dev (port 5433) but a dedicated
/// "campfire_test" database, reset on first use each test run so integration
/// tests never depend on or disturb whatever is in the dev database.
///
/// CI has no Postgres service, so every test here calls EnsureReadyAsync first
/// and skips (Assert.Ignore) rather than fails when the database is unreachable.
/// </summary>
public static class IntegrationTestDatabase
{
    private const string MaintenanceConnectionString =
        "Host=localhost;Port=5433;Username=campfire;Password=campfire;Database=campfire";

    private const string TestConnectionString =
        "Host=localhost;Port=5433;Username=campfire;Password=campfire;Database=campfire_test";

    private static bool? _isReady;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public static async Task<bool> EnsureReadyAsync()
    {
        if (_isReady is not null)
        {
            return _isReady.Value;
        }

        await InitLock.WaitAsync();
        try
        {
            if (_isReady is not null)
            {
                return _isReady.Value;
            }

            await using var maintenanceConnection = new NpgsqlConnection(MaintenanceConnectionString);
            await maintenanceConnection.OpenAsync();

            var exists = await new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = 'campfire_test'", maintenanceConnection)
                .ExecuteScalarAsync();

            if (exists is null)
            {
                await new NpgsqlCommand("CREATE DATABASE campfire_test", maintenanceConnection)
                    .ExecuteNonQueryAsync();
            }

            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            _isReady = true;
            return true;
        }
        catch
        {
            _isReady = false;
            return false;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public static CampfireDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        return new CampfireDbContext(options);
    }
}

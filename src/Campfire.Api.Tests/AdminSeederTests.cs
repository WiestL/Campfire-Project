using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Campfire.Api.Tests;

public class AdminSeederTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    [Fact]
    public async Task EnsureAdminSeededAsync_CreatesAdmin_WhenNoneExists()
    {
        await using var db = CreateDb();
        var options = Microsoft.Extensions.Options.Options.Create(new AdminSeedOptions
        {
            Email = "admin@campfire.local",
            Password = "seed-password-123",
            DisplayName = "Admin",
        });

        await AdminSeeder.EnsureAdminSeededAsync(db, options);

        var admin = Assert.Single(db.Users);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal("admin@campfire.local", admin.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("seed-password-123", admin.PasswordHash));
    }

    [Fact]
    public async Task EnsureAdminSeededAsync_DoesNothing_WhenAdminAlreadyExists()
    {
        await using var db = CreateDb();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing-admin@campfire.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing-password"),
            DisplayName = "Existing Admin",
            Role = UserRole.Admin,
        });
        await db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new AdminSeedOptions
        {
            Email = "admin@campfire.local",
            Password = "seed-password-123",
            DisplayName = "Admin",
        });

        await AdminSeeder.EnsureAdminSeededAsync(db, options);

        Assert.Single(db.Users);
    }

    [Fact]
    public async Task EnsureAdminSeededAsync_DoesNothing_WhenNoCredentialsConfigured()
    {
        await using var db = CreateDb();
        var options = Microsoft.Extensions.Options.Options.Create(new AdminSeedOptions());

        await AdminSeeder.EnsureAdminSeededAsync(db, options);

        Assert.Empty(db.Users);
    }
}

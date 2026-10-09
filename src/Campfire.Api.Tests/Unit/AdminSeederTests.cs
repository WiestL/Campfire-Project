using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("An AdminSeeder")]
[TestFixture]
public class AdminSeederTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    [Test]
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

        Assert.That(db.Users, Has.Exactly(1).Items);
        var admin = db.Users.Single();
        Assert.That(admin.Role, Is.EqualTo(UserRole.Admin));
        Assert.That(admin.Email, Is.EqualTo("admin@campfire.local"));
        Assert.That(BCrypt.Net.BCrypt.Verify("seed-password-123", admin.PasswordHash), Is.True);
    }

    [Test]
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

        Assert.That(db.Users, Has.Exactly(1).Items);
    }

    [Test]
    public async Task EnsureAdminSeededAsync_DoesNothing_WhenNoCredentialsConfigured()
    {
        await using var db = CreateDb();
        var options = Microsoft.Extensions.Options.Options.Create(new AdminSeedOptions());

        await AdminSeeder.EnsureAdminSeededAsync(db, options);

        Assert.That(db.Users, Is.Empty);
    }
}

using Campfire.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests.Integration;

/// <summary>
/// Exercises constraints and cascade behavior that live in the real Postgres
/// schema (unique indexes, FK delete rules, decimal precision), none of which
/// EF Core's InMemory provider enforces by default. The Unit test suite covers
/// controller decision logic against fakes; these tests cover the database
/// itself, per the plan's "API to Postgres" integration point.
/// </summary>
[Category("Integration Tests")]
[Category("The Campfire database")]
[TestFixture]
public class DatabaseConstraintIntegrationTests
{
    [SetUp]
    public async Task SetUp()
    {
        if (!await IntegrationTestDatabase.EnsureReadyAsync())
        {
            Assert.Ignore("Postgres is not reachable on localhost:5433; start it with `docker compose up -d` to run integration tests.");
        }
    }

    private static async Task<User> InsertUserAsync(Campfire.Api.Data.CampfireDbContext db, string email, UserRole role = UserRole.Member)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            DisplayName = "Test User",
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task WritingThroughOneContext_IsReadableFromAFreshContext()
    {
        var email = $"roundtrip-{Guid.NewGuid()}@example.com";

        await using (var writeDb = IntegrationTestDatabase.CreateContext())
        {
            await InsertUserAsync(writeDb, email);
        }

        // a new DbContext over a new connection, simulating a separate request
        await using var readDb = IntegrationTestDatabase.CreateContext();
        var found = await readDb.Users.SingleOrDefaultAsync(u => u.Email == email);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.DisplayName, Is.EqualTo("Test User"));
    }

    [Test]
    public async Task DuplicateEmail_ViolatesTheRealUniqueIndex()
    {
        var email = $"dupe-{Guid.NewGuid()}@example.com";

        await using var db1 = IntegrationTestDatabase.CreateContext();
        await InsertUserAsync(db1, email);

        // a second, independent context racing to insert the same email: the
        // application's AnyAsync check can't catch this, only the DB index can
        await using var db2 = IntegrationTestDatabase.CreateContext();
        db2.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            DisplayName = "Racing Duplicate",
            Role = UserRole.Member,
            CreatedAt = DateTime.UtcNow,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await db2.SaveChangesAsync());
    }

    [Test]
    public async Task DeletingATier_SetsRequiredTierIdNullOnItsContent_RatherThanDeletingTheContent()
    {
        await using var db = IntegrationTestDatabase.CreateContext();
        var creator = await InsertUserAsync(db, $"creator-{Guid.NewGuid()}@example.com", UserRole.Creator);

        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Gold",
            Description = "desc",
            Price = 9.99m,
            Level = 1,
        };
        db.MembershipTiers.Add(tier);

        var content = new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            RequiredTierId = tier.Id,
            Type = ContentType.Text,
            Title = "Gated post",
            Body = "secret",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
        };
        db.Content.Add(content);
        await db.SaveChangesAsync();

        db.MembershipTiers.Remove(tier);
        await db.SaveChangesAsync();

        await using var verifyDb = IntegrationTestDatabase.CreateContext();
        var survivingContent = await verifyDb.Content.FindAsync(content.Id);

        Assert.That(survivingContent, Is.Not.Null, "content must survive its tier being deleted");
        Assert.That(survivingContent!.RequiredTierId, Is.Null);
    }

    [Test]
    public async Task DeletingATierWithAnActiveSubscription_IsRestrictedByTheDatabase()
    {
        await using var db = IntegrationTestDatabase.CreateContext();
        var creator = await InsertUserAsync(db, $"creator-{Guid.NewGuid()}@example.com", UserRole.Creator);
        var member = await InsertUserAsync(db, $"member-{Guid.NewGuid()}@example.com");

        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Gold",
            Description = "desc",
            Price = 9.99m,
            Level = 1,
        };
        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();

        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // a fresh context that has never loaded the Subscription: EF Core
        // evaluates Restrict client-side when a dependent is already tracked
        // in the same context, which would short-circuit before this delete
        // ever reached Postgres. A separate context (a separate request, in
        // practice) forces the real FK constraint to be the one that rejects it.
        await using var deleteDb = IntegrationTestDatabase.CreateContext();
        var tierStub = new MembershipTier { Id = tier.Id };
        deleteDb.MembershipTiers.Attach(tierStub);
        deleteDb.MembershipTiers.Remove(tierStub);

        // Subscription.Tier is configured DeleteBehavior.Restrict: the real FK
        // constraint should reject this, protecting billing history from an
        // accidental cascade.
        Assert.ThrowsAsync<DbUpdateException>(async () => await deleteDb.SaveChangesAsync());
    }

    [Test]
    public async Task DeletingACreator_CascadesToTheirTiersAndContent()
    {
        await using var db = IntegrationTestDatabase.CreateContext();
        var creator = await InsertUserAsync(db, $"creator-{Guid.NewGuid()}@example.com", UserRole.Creator);

        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Gold",
            Description = "desc",
            Price = 9.99m,
            Level = 1,
        };
        db.MembershipTiers.Add(tier);

        var content = new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Type = ContentType.Text,
            Title = "Post",
            Body = "body",
            CreatedAt = DateTime.UtcNow,
        };
        db.Content.Add(content);
        await db.SaveChangesAsync();

        db.Users.Remove(creator);
        await db.SaveChangesAsync();

        await using var verifyDb = IntegrationTestDatabase.CreateContext();
        Assert.That(await verifyDb.MembershipTiers.FindAsync(tier.Id), Is.Null);
        Assert.That(await verifyDb.Content.FindAsync(content.Id), Is.Null);
    }

    [Test]
    public async Task TierPrice_RoundTripsWithTwoDecimalPrecision()
    {
        await using var db = IntegrationTestDatabase.CreateContext();
        var creator = await InsertUserAsync(db, $"creator-{Guid.NewGuid()}@example.com", UserRole.Creator);

        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Gold",
            Description = "desc",
            Price = 12.345m, // more precision than the column allows (10,2)
            Level = 0,
        };
        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();

        await using var verifyDb = IntegrationTestDatabase.CreateContext();
        var stored = await verifyDb.MembershipTiers.FindAsync(tier.Id);

        Assert.That(stored!.Price, Is.EqualTo(12.35m).Or.EqualTo(12.34m),
            "the real column rounds to 2 decimal places; InMemory would have silently kept all three");
    }
}

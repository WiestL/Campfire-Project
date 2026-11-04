using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("An AnalyticsController")]
[TestFixture]
public class AnalyticsControllerTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static async Task<User> AddCreatorAsync(CampfireDbContext db, string displayName = "Test Creator")
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = displayName,
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        return creator;
    }

    private static async Task<User> AddMemberAsync(CampfireDbContext db)
    {
        var member = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = "Test Member",
            Role = UserRole.Member,
        };
        db.Users.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    private static async Task<MembershipTier> AddTierAsync(CampfireDbContext db, Guid creatorId, decimal price, int level = 0, string name = "Tier")
    {
        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = name,
            Description = "desc",
            Price = price,
            Level = level,
        };
        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();
        return tier;
    }

    private static async Task AddSubscriptionAsync(CampfireDbContext db, Guid memberId, Guid tierId, SubscriptionStatus status)
    {
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            TierId = tierId,
            Status = status,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddContentAsync(CampfireDbContext db, Guid creatorId, bool published)
    {
        db.Content.Add(new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Type = ContentType.Text,
            Title = "Post",
            Body = "body",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = published ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync();
    }

    private static AnalyticsController CreateController(CampfireDbContext db, Guid actingUserId)
    {
        var controller = new AnalyticsController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())], "test")),
            },
        };
        return controller;
    }

    [Test]
    public async Task GetMine_WithNoTiers_ReturnsAllZeros()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.TotalActiveSubscribers, Is.EqualTo(0));
        Assert.That(response.MonthlyRecurringRevenue, Is.EqualTo(0));
        Assert.That(response.SubscribersByTier, Is.Empty);
    }

    [Test]
    public async Task GetMine_WithTiersButNoSubscribers_ReturnsZeroCountsPerTier()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        await AddTierAsync(db, creator.Id, 9.99m, name: "Gold");

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.SubscribersByTier, Has.Exactly(1).Items);
        Assert.That(response.SubscribersByTier[0].ActiveSubscribers, Is.EqualTo(0));
        Assert.That(response.MonthlyRecurringRevenue, Is.EqualTo(0));
    }

    [Test]
    public async Task GetMine_OnlyCountsActiveSubscriptions_NotCanceledOrIncomplete()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, 10m);
        var activeMember = await AddMemberAsync(db);
        var canceledMember = await AddMemberAsync(db);
        var incompleteMember = await AddMemberAsync(db);

        await AddSubscriptionAsync(db, activeMember.Id, tier.Id, SubscriptionStatus.Active);
        await AddSubscriptionAsync(db, canceledMember.Id, tier.Id, SubscriptionStatus.Canceled);
        await AddSubscriptionAsync(db, incompleteMember.Id, tier.Id, SubscriptionStatus.Incomplete);

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.TotalActiveSubscribers, Is.EqualTo(1));
        Assert.That(response.MonthlyRecurringRevenue, Is.EqualTo(10m));
    }

    [Test]
    public async Task GetMine_WithMultipleTiers_ComputesRevenueAsSumAcrossTiers()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var silver = await AddTierAsync(db, creator.Id, 5m, level: 0, name: "Silver");
        var gold = await AddTierAsync(db, creator.Id, 15m, level: 1, name: "Gold");

        var m1 = await AddMemberAsync(db);
        var m2 = await AddMemberAsync(db);
        var m3 = await AddMemberAsync(db);
        await AddSubscriptionAsync(db, m1.Id, silver.Id, SubscriptionStatus.Active);
        await AddSubscriptionAsync(db, m2.Id, silver.Id, SubscriptionStatus.Active);
        await AddSubscriptionAsync(db, m3.Id, gold.Id, SubscriptionStatus.Active);

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.TotalActiveSubscribers, Is.EqualTo(3));
        Assert.That(response.MonthlyRecurringRevenue, Is.EqualTo(5m * 2 + 15m * 1));
        Assert.That(response.SubscribersByTier.Single(t => t.TierName == "Silver").ActiveSubscribers, Is.EqualTo(2));
        Assert.That(response.SubscribersByTier.Single(t => t.TierName == "Gold").ActiveSubscribers, Is.EqualTo(1));
    }

    [Test]
    public async Task GetMine_NeverIncludesAnotherCreatorsTiersOrSubscribers()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db, "Me");
        var otherCreator = await AddCreatorAsync(db, "Someone Else");
        var otherTier = await AddTierAsync(db, otherCreator.Id, 100m, name: "Other Tier");
        var member = await AddMemberAsync(db);
        await AddSubscriptionAsync(db, member.Id, otherTier.Id, SubscriptionStatus.Active);

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.TotalActiveSubscribers, Is.EqualTo(0));
        Assert.That(response.MonthlyRecurringRevenue, Is.EqualTo(0));
        Assert.That(response.SubscribersByTier, Is.Empty);
    }

    [Test]
    public async Task GetMine_PublishedPostCount_OnlyCountsThisCreatorsPublishedContent()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db, "Me");
        var otherCreator = await AddCreatorAsync(db, "Someone Else");

        await AddContentAsync(db, creator.Id, published: true);
        await AddContentAsync(db, creator.Id, published: true);
        await AddContentAsync(db, creator.Id, published: false); // draft, should not count
        await AddContentAsync(db, otherCreator.Id, published: true); // someone else's, should not count

        var result = await CreateController(db, creator.Id).GetMine();

        var response = ((OkObjectResult)result.Result!).Value as Campfire.Api.Dtos.CreatorAnalyticsResponse;
        Assert.That(response!.PublishedPostCount, Is.EqualTo(2));
    }
}

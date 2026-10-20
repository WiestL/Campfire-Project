using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A SubscriptionsController")]
[TestFixture]
public class SubscriptionsControllerTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static async Task<User> AddMemberAsync(CampfireDbContext db, string? stripeCustomerId = null)
    {
        var member = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = "Test Member",
            Role = UserRole.Member,
            StripeCustomerId = stripeCustomerId,
        };
        db.Users.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    private static async Task<MembershipTier> AddTierAsync(CampfireDbContext db)
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = "Test Creator",
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);
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
        return tier;
    }

    private static SubscriptionsController CreateController(
        CampfireDbContext db, Guid actingUserId, Mock<IStripeSubscriptionService> stripeMock)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new StripeOptions
        {
            PublishableKey = "pk_test_fake",
        });

        var controller = new SubscriptionsController(db, stripeMock.Object, options, NullLogger<SubscriptionsController>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())], "test"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        return controller;
    }

    [Test]
    public async Task GetMine_NoSubscription_ReturnsNull()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db);
        var controller = CreateController(db, member.Id, new Mock<IStripeSubscriptionService>());

        var result = await controller.GetMine();

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(((OkObjectResult)result.Result!).Value, Is.Null);
    }

    [Test]
    public async Task GetMine_WithActiveSubscription_ReturnsIt()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db);
        var tier = await AddTierAsync(db);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_123",
            StripeCustomerId = "cus_123",
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, member.Id, new Mock<IStripeSubscriptionService>());

        var result = await controller.GetMine();

        var response = (SubscriptionResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.TierName, Is.EqualTo("Gold"));
        Assert.That(response.Status, Is.EqualTo(SubscriptionStatus.Active));
    }

    [Test]
    public async Task CreateSetupIntent_NewCustomer_CreatesStripeCustomerAndSavesIt()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateCustomerAsync(member.Email, member.DisplayName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_new");
        stripeMock.Setup(s => s.CreateSetupIntentAsync("cus_new", It.IsAny<CancellationToken>()))
            .ReturnsAsync("seti_secret_123");
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.CreateSetupIntent();

        var response = (SetupIntentResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.ClientSecret, Is.EqualTo("seti_secret_123"));
        Assert.That((await db.Users.FindAsync(member.Id))!.StripeCustomerId, Is.EqualTo("cus_new"));
    }

    [Test]
    public async Task CreateSetupIntent_ExistingCustomer_ReusesStoredCustomerId()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_existing");
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateSetupIntentAsync("cus_existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync("seti_secret_456");
        var controller = CreateController(db, member.Id, stripeMock);

        await controller.CreateSetupIntent();

        stripeMock.Verify(s => s.CreateCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Create_UnknownTier_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var controller = CreateController(db, member.Id, new Mock<IStripeSubscriptionService>());

        var result = await controller.Create(new CreateSubscriptionRequest(Guid.NewGuid(), "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task Create_MemberAlreadyHasActiveSubscription_ReturnsConflict()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var tier = await AddTierAsync(db);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_existing",
            StripeCustomerId = "cus_1",
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, member.Id, new Mock<IStripeSubscriptionService>());

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
    }

    [Test]
    public async Task Create_WithUnknownActingUser_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var tier = await AddTierAsync(db);
        var controller = CreateController(db, Guid.NewGuid(), new Mock<IStripeSubscriptionService>());

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<UnauthorizedResult>());
    }

    [Test]
    public async Task Create_StripeReturnsActive_SavesActiveSubscriptionLocally()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var tier = await AddTierAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateSubscriptionAsync("cus_1", "pm_123", tier.Name, tier.Price, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSubscriptionResult("sub_abc", "active", DateTime.UtcNow.AddMonths(1), null));
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (SubscriptionResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Status, Is.EqualTo(SubscriptionStatus.Active));
        Assert.That(response.ClientSecretForAction, Is.Null);
        Assert.That(db.Subscriptions, Has.Exactly(1).Items);
    }

    [Test]
    public async Task Create_StripeRequiresAction_SavesIncompleteAndReturnsClientSecret()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var tier = await AddTierAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateSubscriptionAsync("cus_1", "pm_123", tier.Name, tier.Price, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSubscriptionResult("sub_abc", "incomplete", null, "pi_secret_xyz"));
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        var response = (SubscriptionResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Status, Is.EqualTo(SubscriptionStatus.Incomplete));
        Assert.That(response.ClientSecretForAction, Is.EqualTo("pi_secret_xyz"));
    }

    [Test]
    public async Task Create_NewCustomer_CreatesStripeCustomerFirst()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db);
        var tier = await AddTierAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateCustomerAsync(member.Email, member.DisplayName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_brand_new");
        stripeMock.Setup(s => s.CreateSubscriptionAsync("cus_brand_new", "pm_123", tier.Name, tier.Price, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSubscriptionResult("sub_abc", "active", DateTime.UtcNow.AddMonths(1), null));
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That((await db.Users.FindAsync(member.Id))!.StripeCustomerId, Is.EqualTo("cus_brand_new"));
    }

    [Test]
    public async Task CreateSetupIntent_StripeThrows_ReturnsCleanBadGatewayNotRawException()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("Your API key is invalid, as it is an empty string."));
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.CreateSetupIntent();

        Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result.Result!;
        Assert.That(objectResult.StatusCode, Is.EqualTo(502));
        Assert.That(objectResult.Value, Is.Not.Null);
        Assert.That(objectResult.Value!.ToString(), Does.Not.Contain("StripeException"));
        Assert.That(objectResult.Value!.ToString(), Does.Not.Contain("at Campfire.Api"));
    }

    [Test]
    public async Task Create_StripeThrows_ReturnsCleanBadGatewayNotRawException()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var tier = await AddTierAsync(db);
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock.Setup(s => s.CreateSubscriptionAsync("cus_1", "pm_123", tier.Name, tier.Price, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("Your card was declined."));
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.Create(new CreateSubscriptionRequest(tier.Id, "pm_123"));

        Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result.Result!;
        Assert.That(objectResult.StatusCode, Is.EqualTo(502));
        Assert.That(db.Subscriptions, Is.Empty);
    }
}

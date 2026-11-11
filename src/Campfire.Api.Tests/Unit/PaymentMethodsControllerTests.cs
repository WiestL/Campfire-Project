using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A PaymentMethodsController")]
[TestFixture]
public class PaymentMethodsControllerTests
{
    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static async Task<User> AddMemberAsync(CampfireDbContext db, string? stripeCustomerId)
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

    private static PaymentMethodsController CreateController(CampfireDbContext db, Guid actingUserId, Mock<IStripeSubscriptionService> stripeMock)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new StripeOptions { PublishableKey = "pk_test_fake" });
        var controller = new PaymentMethodsController(db, stripeMock.Object, options, NullLogger<PaymentMethodsController>.Instance);
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
    public async Task RemoveDefault_WithAnActiveSubscription_IsBlockedWithConflict()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var creator = new User { Id = Guid.NewGuid(), Email = "c@example.com", PasswordHash = "x", DisplayName = "Creator", Role = UserRole.Creator };
        var tier = new MembershipTier { Id = Guid.NewGuid(), CreatorId = creator.Id, Name = "Gold", Description = "d", Price = 5, Level = 0 };
        db.Users.Add(creator);
        db.MembershipTiers.Add(tier);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var stripeMock = new Mock<IStripeSubscriptionService>();
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.RemoveDefault();

        Assert.That(result, Is.InstanceOf<ConflictObjectResult>());
        stripeMock.Verify(s => s.RemoveDefaultPaymentMethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RemoveDefault_WithNoActiveSubscription_RemovesIt()
    {
        await using var db = CreateDb();
        var member = await AddMemberAsync(db, stripeCustomerId: "cus_1");
        var stripeMock = new Mock<IStripeSubscriptionService>();
        var controller = CreateController(db, member.Id, stripeMock);

        var result = await controller.RemoveDefault();

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        stripeMock.Verify(s => s.RemoveDefaultPaymentMethodAsync("cus_1", It.IsAny<CancellationToken>()), Times.Once);
    }
}

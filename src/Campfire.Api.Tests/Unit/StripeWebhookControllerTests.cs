using System.Security.Cryptography;
using System.Text;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A StripeWebhookController")]
[TestFixture]
public class StripeWebhookControllerTests
{
    private const string WebhookSecret = "whsec_test_secret";

    [TestCase("active", SubscriptionStatus.Active)]
    [TestCase("trialing", SubscriptionStatus.Active)]
    [TestCase("past_due", SubscriptionStatus.PastDue)]
    [TestCase("incomplete", SubscriptionStatus.Incomplete)]
    [TestCase("incomplete_expired", SubscriptionStatus.Incomplete)]
    [TestCase("canceled", SubscriptionStatus.Canceled)]
    [TestCase("unpaid", SubscriptionStatus.Canceled)]
    [TestCase("some_future_stripe_status", SubscriptionStatus.Incomplete)]
    public void MapStatus_MapsEveryKnownStripeStatus(string stripeStatus, SubscriptionStatus expected)
    {
        var result = StripeWebhookController.MapStatus(stripeStatus);

        Assert.That(result, Is.EqualTo(expected));
    }

    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static StripeWebhookController CreateController(CampfireDbContext db, string payload, string? signatureHeader)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new StripeOptions { WebhookSecret = WebhookSecret });
        var controller = new StripeWebhookController(db, options, NullLogger<StripeWebhookController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        if (signatureHeader is not null)
        {
            httpContext.Request.Headers["Stripe-Signature"] = signatureHeader;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static string SignPayload(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        var hash = new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var hex = Convert.ToHexStringLower(hash);
        return $"t={timestamp},v1={hex}";
    }

    private static string SubscriptionEventJson(string subscriptionId, string status) => $$"""
        {
          "id": "evt_test",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "status": "{{status}}",
              "items": { "object": "list", "data": [] }
            }
          }
        }
        """;

    [Test]
    public async Task Webhook_MissingSignature_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var payload = SubscriptionEventJson("sub_1", "active");
        var controller = CreateController(db, payload, signatureHeader: null);

        var result = await controller.Webhook();

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task Webhook_TamperedSignature_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var payload = SubscriptionEventJson("sub_1", "active");
        var validHeader = SignPayload(payload, WebhookSecret);
        var controller = CreateController(db, payload, validHeader.Replace("v1=", "v1=tampered"));

        var result = await controller.Webhook();

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task Webhook_WrongSecret_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var payload = SubscriptionEventJson("sub_1", "active");
        var headerSignedWithWrongSecret = SignPayload(payload, "whsec_different_secret");
        var controller = CreateController(db, payload, headerSignedWithWrongSecret);

        var result = await controller.Webhook();

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task Webhook_ValidSignature_UnknownSubscription_StillReturnsOk()
    {
        await using var db = CreateDb();
        var payload = SubscriptionEventJson("sub_not_in_db", "active");
        var controller = CreateController(db, payload, SignPayload(payload, WebhookSecret));

        var result = await controller.Webhook();

        Assert.That(result, Is.InstanceOf<OkResult>());
    }

    [Test]
    public async Task Webhook_ValidSignature_KnownSubscription_UpdatesLocalStatus()
    {
        await using var db = CreateDb();
        var creator = new User { Id = Guid.NewGuid(), Email = "c@example.com", PasswordHash = "h", DisplayName = "C", Role = UserRole.Creator };
        var member = new User { Id = Guid.NewGuid(), Email = "m@example.com", PasswordHash = "h", DisplayName = "M", Role = UserRole.Member };
        var tier = new MembershipTier { Id = Guid.NewGuid(), CreatorId = creator.Id, Name = "Gold", Description = "d", Price = 5m, Level = 0 };
        db.Users.AddRange(creator, member);
        db.MembershipTiers.Add(tier);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Incomplete,
            StripeSubscriptionId = "sub_known",
            StripeCustomerId = "cus_1",
        });
        await db.SaveChangesAsync();

        var payload = SubscriptionEventJson("sub_known", "active");
        var controller = CreateController(db, payload, SignPayload(payload, WebhookSecret));

        var result = await controller.Webhook();

        Assert.That(result, Is.InstanceOf<OkResult>());
        var updated = db.Subscriptions.Single(s => s.StripeSubscriptionId == "sub_known");
        Assert.That(updated.Status, Is.EqualTo(SubscriptionStatus.Active));
    }

    [Test]
    public async Task Webhook_SubscriptionCanceled_SetsCanceledAt()
    {
        await using var db = CreateDb();
        var creator = new User { Id = Guid.NewGuid(), Email = "c2@example.com", PasswordHash = "h", DisplayName = "C", Role = UserRole.Creator };
        var member = new User { Id = Guid.NewGuid(), Email = "m2@example.com", PasswordHash = "h", DisplayName = "M", Role = UserRole.Member };
        var tier = new MembershipTier { Id = Guid.NewGuid(), CreatorId = creator.Id, Name = "Gold", Description = "d", Price = 5m, Level = 0 };
        db.Users.AddRange(creator, member);
        db.MembershipTiers.Add(tier);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_cancel_me",
            StripeCustomerId = "cus_2",
        });
        await db.SaveChangesAsync();

        var payload = SubscriptionEventJson("sub_cancel_me", "canceled");
        var controller = CreateController(db, payload, SignPayload(payload, WebhookSecret));

        await controller.Webhook();

        var updated = db.Subscriptions.Single(s => s.StripeSubscriptionId == "sub_cancel_me");
        Assert.That(updated.Status, Is.EqualTo(SubscriptionStatus.Canceled));
        Assert.That(updated.CanceledAt, Is.Not.Null);
    }
}

using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StripeException = Stripe.StripeException;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize(Roles = "Member")]
public class SubscriptionsController(
    CampfireDbContext db,
    IStripeSubscriptionService stripe,
    IOptions<StripeOptions> stripeOptions,
    ILogger<SubscriptionsController> logger) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static SubscriptionResponse ToResponse(Subscription s, string tierName, string? clientSecretForAction = null) =>
        new(s.Id, s.TierId, tierName, s.Status, s.StartedAt, s.CurrentPeriodEnd, clientSecretForAction);

    private static SubscriptionStatus MapStripeStatus(string stripeStatus) => stripeStatus switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "incomplete" or "incomplete_expired" => SubscriptionStatus.Incomplete,
        "canceled" or "unpaid" => SubscriptionStatus.Canceled,
        _ => SubscriptionStatus.Incomplete,
    };

    /// <summary>
    /// Stripe SDK calls fail with a raw StripeException (misconfigured API key,
    /// network failure, Stripe outage) that must never reach the client as an
    /// unhandled 500 with ASP.NET Core's developer exception page, which returns
    /// a plain-text/HTML body containing the full exception and stack trace, not
    /// JSON, and was surfaced verbatim in the UI before this was added.
    /// </summary>
    private ObjectResult StripeUnavailable(StripeException ex)
    {
        logger.LogError(ex, "Stripe call failed in SubscriptionsController.");
        return StatusCode(StatusCodes.Status502BadGateway, "Payment processing is temporarily unavailable. Please try again shortly.");
    }

    [HttpGet("me")]
    public async Task<ActionResult<SubscriptionResponse?>> GetMine()
    {
        var subscription = await db.Subscriptions
            .Include(s => s.Tier)
            .Where(s => s.MemberId == CurrentUserId && s.Status != SubscriptionStatus.Canceled && s.Status != SubscriptionStatus.Expired)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        return Ok(subscription is null ? null : ToResponse(subscription, subscription.Tier.Name));
    }

    [HttpPost("setup-intent")]
    public async Task<ActionResult<SetupIntentResponse>> CreateSetupIntent()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            if (user.StripeCustomerId is null)
            {
                user.StripeCustomerId = await stripe.CreateCustomerAsync(user.Email, user.DisplayName);
                await db.SaveChangesAsync();
            }

            var clientSecret = await stripe.CreateSetupIntentAsync(user.StripeCustomerId);

            return Ok(new SetupIntentResponse(clientSecret, stripeOptions.Value.PublishableKey));
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionResponse>> Create(CreateSubscriptionRequest request)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return Unauthorized();
        }

        var tier = await db.MembershipTiers.FindAsync(request.TierId);
        if (tier is null)
        {
            return NotFound("Tier not found.");
        }

        var hasActive = await db.Subscriptions.AnyAsync(s =>
            s.MemberId == CurrentUserId && s.Status == SubscriptionStatus.Active);
        if (hasActive)
        {
            return Conflict("You already have an active subscription.");
        }

        try
        {
            if (user.StripeCustomerId is null)
            {
                user.StripeCustomerId = await stripe.CreateCustomerAsync(user.Email, user.DisplayName);
                await db.SaveChangesAsync();
            }

            var result = await stripe.CreateSubscriptionAsync(
                user.StripeCustomerId, request.PaymentMethodId, tier.Name, tier.Price);

            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                MemberId = user.Id,
                TierId = tier.Id,
                Status = MapStripeStatus(result.RawStatus),
                StripeSubscriptionId = result.StripeSubscriptionId,
                StripeCustomerId = user.StripeCustomerId,
                StartedAt = DateTime.UtcNow,
                CurrentPeriodEnd = result.CurrentPeriodEnd,
            };

            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            return Ok(ToResponse(subscription, tier.Name, result.ClientSecretForAction));
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }
}

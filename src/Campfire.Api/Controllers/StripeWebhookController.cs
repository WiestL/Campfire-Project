using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using StripeSubscription = Stripe.Subscription;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/stripe")]
[AllowAnonymous]
public class StripeWebhookController(
    CampfireDbContext db,
    IOptions<StripeOptions> options,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    public static SubscriptionStatus MapStatus(string stripeStatus) => stripeStatus switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "incomplete" or "incomplete_expired" => SubscriptionStatus.Incomplete,
        "canceled" or "unpaid" => SubscriptionStatus.Canceled,
        _ => SubscriptionStatus.Incomplete,
    };

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json, Request.Headers["Stripe-Signature"], options.Value.WebhookSecret);
        }
        catch (Exception ex) when (ex is StripeException or NullReferenceException)
        {
            // ConstructEvent can throw a raw NullReferenceException (not just
            // StripeException) for a malformed or incomplete event payload,
            // e.g. one missing api_version; found by test, not by docs.
            logger.LogWarning(ex, "Rejected Stripe webhook with an invalid signature or payload.");
            return BadRequest();
        }

        if (stripeEvent.Data.Object is StripeSubscription stripeSubscription)
        {
            await SyncSubscriptionAsync(stripeSubscription);
        }

        return Ok();
    }

    private async Task SyncSubscriptionAsync(StripeSubscription stripeSubscription)
    {
        var local = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (local is null)
        {
            return;
        }

        local.Status = MapStatus(stripeSubscription.Status);

        var currentPeriodEnd = stripeSubscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (currentPeriodEnd is not null)
        {
            local.CurrentPeriodEnd = currentPeriodEnd;
        }

        if (local.Status == SubscriptionStatus.Canceled && local.CanceledAt is null)
        {
            local.CanceledAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}

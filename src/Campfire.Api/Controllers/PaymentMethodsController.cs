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
[Route("api/payment-methods")]
[Authorize(Roles = "Member")]
public class PaymentMethodsController(
    CampfireDbContext db,
    IStripeSubscriptionService stripe,
    IOptions<StripeOptions> stripeOptions,
    ILogger<PaymentMethodsController> logger) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private ObjectResult StripeUnavailable(StripeException ex)
    {
        logger.LogError(ex, "Stripe call failed in PaymentMethodsController.");
        return StatusCode(StatusCodes.Status502BadGateway, "Payment processing is temporarily unavailable. Please try again shortly.");
    }

    [HttpGet("me")]
    public async Task<ActionResult<PaymentMethodResponse?>> GetMine()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user?.StripeCustomerId is null)
        {
            return Ok(null);
        }

        try
        {
            var summary = await stripe.GetDefaultPaymentMethodAsync(user.StripeCustomerId);
            return Ok(summary is null ? null : new PaymentMethodResponse(summary.Id, summary.Brand, summary.Last4, summary.ExpMonth, summary.ExpYear));
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }

    [HttpPost("me/setup-intent")]
    public async Task<ActionResult<SetupIntentResponse>> CreateSetupIntent()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var customerId = await StripeCustomerProvisioner.EnsureCustomerAsync(db, stripe, user);
            var clientSecret = await stripe.CreateSetupIntentAsync(customerId);
            return Ok(new SetupIntentResponse(clientSecret, stripeOptions.Value.PublishableKey));
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }

    [HttpPost("me")]
    public async Task<IActionResult> SetDefault(SetDefaultPaymentMethodRequest request)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var customerId = await StripeCustomerProvisioner.EnsureCustomerAsync(db, stripe, user);
            await stripe.SetDefaultPaymentMethodAsync(customerId, request.PaymentMethodId);
            return NoContent();
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }

    [HttpDelete("me")]
    public async Task<IActionResult> RemoveDefault()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user?.StripeCustomerId is null)
        {
            return NoContent();
        }

        var hasActiveSubscription = await db.Subscriptions
            .AnyAsync(s => s.MemberId == CurrentUserId && s.Status == SubscriptionStatus.Active);
        if (hasActiveSubscription)
        {
            return Conflict("You have an active subscription that needs a payment method on file. Cancel it first, or add a replacement card before removing this one.");
        }

        try
        {
            await stripe.RemoveDefaultPaymentMethodAsync(user.StripeCustomerId);
            return NoContent();
        }
        catch (StripeException ex)
        {
            return StripeUnavailable(ex);
        }
    }
}

namespace Campfire.Api.Payments;

public record StripeSubscriptionResult(
    string StripeSubscriptionId,
    string RawStatus,
    DateTime? CurrentPeriodEnd,
    string? ClientSecretForAction
);

/// <summary>
/// Wraps the Stripe SDK calls needed for the subscription checkout flow. Kept
/// behind an interface so controller decision logic (which branch to take for
/// each possible Stripe response) can be unit tested without a live Stripe
/// account, the same pattern used for IMediaStorage in Week 5.
/// </summary>
public interface IStripeSubscriptionService
{
    Task<string> CreateCustomerAsync(string email, string displayName, CancellationToken ct = default);

    Task<string> CreateSetupIntentAsync(string customerId, CancellationToken ct = default);

    Task<StripeSubscriptionResult> CreateSubscriptionAsync(
        string customerId, string paymentMethodId, string tierName, decimal monthlyPriceUsd, CancellationToken ct = default);

    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
}

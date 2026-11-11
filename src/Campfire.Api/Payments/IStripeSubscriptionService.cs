namespace Campfire.Api.Payments;

public record StripeSubscriptionResult(
    string StripeSubscriptionId,
    string RawStatus,
    DateTime? CurrentPeriodEnd,
    string? ClientSecretForAction
);

public record PaymentMethodSummary(
    string Id,
    string Brand,
    string Last4,
    long ExpMonth,
    long ExpYear
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

    /// <summary>Changes an existing subscription's price in place (Stripe prorates automatically) rather than cancel-and-recreate.</summary>
    Task<StripeSubscriptionResult> ChangeSubscriptionTierAsync(
        string stripeSubscriptionId, string newTierName, decimal newMonthlyPriceUsd, CancellationToken ct = default);

    Task<PaymentMethodSummary?> GetDefaultPaymentMethodAsync(string customerId, CancellationToken ct = default);

    /// <summary>Attaches the given payment method to the customer, sets it as the default, and detaches whatever was previously the default.</summary>
    Task SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken ct = default);

    /// <summary>Detaches the customer's current default payment method, if any.</summary>
    Task RemoveDefaultPaymentMethodAsync(string customerId, CancellationToken ct = default);
}

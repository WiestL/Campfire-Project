using Stripe;

namespace Campfire.Api.Payments;

/// <summary>
/// Real Stripe.net implementation. All property names below were verified by
/// reflecting the installed Stripe.net 52.4.2 assembly rather than assumed from
/// older Stripe API docs: Subscription.CurrentPeriodEnd and Invoice.PaymentIntent
/// no longer exist in this API version. Period end now lives per subscription
/// item (SubscriptionItem.CurrentPeriodEnd), and the PaymentIntent needed for
/// 3D Secure confirmation is reached via Invoice.Payments[].Payment.PaymentIntent.
/// Verified end-to-end against a real test-mode Stripe account.
/// </summary>
public class StripeSubscriptionService : IStripeSubscriptionService
{
    private const string MembershipProductName = "Campfire Membership";
    private static string? _cachedProductId;
    private static readonly SemaphoreSlim ProductLock = new(1, 1);

    public async Task<string> CreateCustomerAsync(string email, string displayName, CancellationToken ct = default)
    {
        var service = new CustomerService();
        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = displayName,
        }, cancellationToken: ct);

        return customer.Id;
    }

    public async Task<string> CreateSetupIntentAsync(string customerId, CancellationToken ct = default)
    {
        var service = new SetupIntentService();
        var setupIntent = await service.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = ["card"],
        }, cancellationToken: ct);

        return setupIntent.ClientSecret;
    }

    public async Task<StripeSubscriptionResult> CreateSubscriptionAsync(
        string customerId, string paymentMethodId, string tierName, decimal monthlyPriceUsd, CancellationToken ct = default)
    {
        var paymentMethodService = new PaymentMethodService();
        await paymentMethodService.AttachAsync(paymentMethodId, new PaymentMethodAttachOptions
        {
            Customer = customerId,
        }, cancellationToken: ct);

        var customerService = new CustomerService();
        await customerService.UpdateAsync(customerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = paymentMethodId,
            },
        }, cancellationToken: ct);

        var productId = await EnsureMembershipProductAsync(ct);

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.CreateAsync(new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items =
            [
                new SubscriptionItemOptions
                {
                    PriceData = new SubscriptionItemPriceDataOptions
                    {
                        Currency = "usd",
                        Product = productId,
                        UnitAmount = (long)Math.Round(monthlyPriceUsd * 100m, MidpointRounding.AwayFromZero),
                        Recurring = new SubscriptionItemPriceDataRecurringOptions
                        {
                            Interval = "month",
                        },
                    },
                },
            ],
            Metadata = new Dictionary<string, string> { ["tierName"] = tierName },
            PaymentBehavior = "default_incomplete",
        }, cancellationToken: ct);

        var currentPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;

        // Stripe caps Expand at 4 levels; latest_invoice.payments.data.payment.payment_intent
        // is 5, so the invoice has to be fetched separately to reach the PaymentIntent.
        // Found by running this against a real Stripe account, not from docs.
        PaymentIntent? paymentIntent = null;
        if (subscription.LatestInvoiceId is not null)
        {
            var invoiceService = new InvoiceService();
            var invoice = await invoiceService.GetAsync(subscription.LatestInvoiceId, new InvoiceGetOptions
            {
                Expand = ["payments.data.payment.payment_intent"],
            }, cancellationToken: ct);

            paymentIntent = invoice.Payments?.Data?.FirstOrDefault()?.Payment?.PaymentIntent;
        }

        // payment_behavior=default_incomplete leaves the invoice's PaymentIntent in
        // "requires_confirmation", not just "requires_action": the client always has
        // to call confirmCardPayment, not only for 3D Secure. Confirmed against a real
        // Stripe account; "requires_action"-only here would have silently never
        // collected payment for the common case.
        var clientSecretForAction = paymentIntent?.Status is "requires_action" or "requires_confirmation"
            ? paymentIntent.ClientSecret
            : null;

        return new StripeSubscriptionResult(
            subscription.Id,
            subscription.Status,
            currentPeriodEnd,
            clientSecretForAction);
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var service = new SubscriptionService();
        await service.CancelAsync(stripeSubscriptionId, cancellationToken: ct);
    }

    /// <summary>
    /// Every membership tier shares one Stripe Product (price varies per
    /// subscription via price_data); Stripe requires an existing Product id for
    /// subscription item price_data, unlike one-off PaymentIntents which allow
    /// fully inline product_data.
    /// </summary>
    private static async Task<string> EnsureMembershipProductAsync(CancellationToken ct)
    {
        if (_cachedProductId is not null)
        {
            return _cachedProductId;
        }

        await ProductLock.WaitAsync(ct);
        try
        {
            if (_cachedProductId is not null)
            {
                return _cachedProductId;
            }

            var productService = new ProductService();
            var existing = await productService.ListAsync(new ProductListOptions { Limit = 100 }, cancellationToken: ct);
            var match = existing.Data.FirstOrDefault(p => p.Name == MembershipProductName && p.Active == true);
            if (match is not null)
            {
                _cachedProductId = match.Id;
                return _cachedProductId;
            }

            var created = await productService.CreateAsync(new ProductCreateOptions
            {
                Name = MembershipProductName,
            }, cancellationToken: ct);

            _cachedProductId = created.Id;
            return _cachedProductId;
        }
        finally
        {
            ProductLock.Release();
        }
    }
}

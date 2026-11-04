using Campfire.Api.Payments;
using Microsoft.Extensions.Options;
using Stripe;

namespace Campfire.Api.Tests.Integration;

/// <summary>
/// Exercises the real Stripe.net SDK against a real test-mode Stripe account,
/// per the plan's "API to Stripe" integration point. Unit tests cover
/// SubscriptionsController's branching against a mocked IStripeSubscriptionService;
/// these cover StripeSubscriptionService's actual calls against Stripe's API,
/// which is where the expand-depth and API-version bugs documented elsewhere
/// in this project were originally found.
/// </summary>
[Category("Integration Tests")]
[Category("The Stripe integration")]
[TestFixture]
public class StripeIntegrationTests
{
    private IStripeSubscriptionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        if (!ExternalServiceConfig.IsStripeConfigured)
        {
            Assert.Ignore("Stripe:SecretKey is not a real sk_test_ key; skipping live Stripe integration tests.");
        }

        StripeConfiguration.ApiKey = ExternalServiceConfig.Stripe.SecretKey;
        _service = new StripeSubscriptionService();
    }

    [Test]
    public async Task CreateCustomerAsync_CreatesARealStripeCustomer()
    {
        var email = $"integration-{Guid.NewGuid()}@example.com";

        var customerId = await _service.CreateCustomerAsync(email, "Integration Test");

        Assert.That(customerId, Does.StartWith("cus_"));

        var customerService = new CustomerService();
        var customer = await customerService.GetAsync(customerId);
        Assert.That(customer.Email, Is.EqualTo(email));
    }

    [Test]
    public async Task CreateSetupIntentAsync_ReturnsARealClientSecret()
    {
        var customerId = await _service.CreateCustomerAsync($"setup-{Guid.NewGuid()}@example.com", "Setup Test");

        var clientSecret = await _service.CreateSetupIntentAsync(customerId);

        Assert.That(clientSecret, Does.StartWith("seti_"));
        Assert.That(clientSecret, Does.Contain("_secret_"));
    }

    [Test]
    public async Task CreateSubscriptionAsync_WithAConfirmedTestCard_CreatesARealSubscription()
    {
        var customerId = await _service.CreateCustomerAsync($"sub-{Guid.NewGuid()}@example.com", "Subscription Test");

        // pm_card_visa is Stripe's documented always-succeeds test payment method;
        // it still has to be attached and confirmed against the real API, same as
        // a browser would via Stripe Elements.
        var paymentMethodService = new PaymentMethodService();
        var paymentMethod = await paymentMethodService.AttachAsync("pm_card_visa", new PaymentMethodAttachOptions
        {
            Customer = customerId,
        });

        var result = await _service.CreateSubscriptionAsync(customerId, paymentMethod.Id, "Integration Tier", 4.99m);

        Assert.That(result.StripeSubscriptionId, Does.StartWith("sub_"));
        Assert.That(result.RawStatus, Is.EqualTo("incomplete"),
            "a freshly created subscription is incomplete until the invoice's PaymentIntent is confirmed client-side");

        var subscriptionService = new SubscriptionService();
        await subscriptionService.CancelAsync(result.StripeSubscriptionId);
    }
}

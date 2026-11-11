using System.Collections.Concurrent;
using Campfire.Api.Data;
using Campfire.Api.Models;

namespace Campfire.Api.Payments;

/// <summary>
/// Shared by every controller that needs "this user's Stripe customer id,
/// creating one if needed" (subscriptions, payment methods). Guards against
/// concurrent requests for the same user (e.g. React StrictMode's dev
/// double-invoke, a double-click, a retry) each seeing StripeCustomerId as
/// null and creating a separate Stripe customer, which previously surfaced
/// several requests later as a cryptic "payment method already attached to a
/// customer" error. Kept here, not duplicated per-controller, so that fix
/// can't silently regress in a second copy.
/// </summary>
public static class StripeCustomerProvisioner
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CustomerLocks = new();

    public static async Task<string> EnsureCustomerAsync(CampfireDbContext db, IStripeSubscriptionService stripe, User user)
    {
        if (user.StripeCustomerId is not null)
        {
            return user.StripeCustomerId;
        }

        var userLock = CustomerLocks.GetOrAdd(user.Id, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            // re-check after acquiring the lock: another request for this same
            // user may have just finished creating the customer while we waited
            await db.Entry(user).ReloadAsync();
            if (user.StripeCustomerId is not null)
            {
                return user.StripeCustomerId;
            }

            user.StripeCustomerId = await stripe.CreateCustomerAsync(user.Email, user.DisplayName);
            await db.SaveChangesAsync();
            return user.StripeCustomerId;
        }
        finally
        {
            userLock.Release();
        }
    }
}

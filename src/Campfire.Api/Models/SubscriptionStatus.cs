namespace Campfire.Api.Models;

public enum SubscriptionStatus
{
    Active,
    PastDue,
    Canceled,
    Expired,
    /// <summary>Stripe subscription created but awaiting payment confirmation (e.g. 3D Secure).</summary>
    Incomplete
}

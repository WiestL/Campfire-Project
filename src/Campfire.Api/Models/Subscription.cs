namespace Campfire.Api.Models;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid TierId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }

    public User Member { get; set; } = null!;
    public MembershipTier Tier { get; set; } = null!;
}

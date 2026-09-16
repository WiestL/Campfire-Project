namespace Campfire.Api.Models;

public class MembershipTier
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Level { get; set; }

    public User Creator { get; set; } = null!;
    public ICollection<Content> Content { get; set; } = new List<Content>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

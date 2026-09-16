namespace Campfire.Api.Models;

public class Content
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? RequiredTierId { get; set; }
    public ContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public User Creator { get; set; } = null!;
    public MembershipTier? RequiredTier { get; set; }
}

namespace Campfire.Api.Models;

public class Like
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Content Content { get; set; } = null!;
    public User User { get; set; } = null!;
}

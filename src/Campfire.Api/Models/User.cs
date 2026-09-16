namespace Campfire.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Content> Content { get; set; } = new List<Content>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

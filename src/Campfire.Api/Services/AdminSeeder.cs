using Campfire.Api.Data;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Campfire.Api.Services;

public static class AdminSeeder
{
    public static async Task EnsureAdminSeededAsync(CampfireDbContext db, IOptions<AdminSeedOptions> options)
    {
        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrWhiteSpace(seed.Password))
        {
            return;
        }

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            return;
        }

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = seed.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password),
            DisplayName = seed.DisplayName,
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}

using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(CampfireDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static object ToProfile(Models.User user) =>
        new { user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAt };

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        return user is null ? NotFound() : Ok(ToProfile(user));
    }

    // Email changes take effect immediately with no verification step, since
    // there is no outbound email sending in this project; a real deployment
    // would gate this behind a confirmation link first.
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return NotFound();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized("Current password is incorrect.");
        }

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        if (emailNormalized != user.Email && await db.Users.AnyAsync(u => u.Email == emailNormalized))
        {
            return Conflict("An account with this email already exists.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = emailNormalized;
        await db.SaveChangesAsync();

        return Ok(ToProfile(user));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return NotFound();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized("Current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();

        return NoContent();
    }
}

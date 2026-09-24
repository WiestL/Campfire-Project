using System.Security.Claims;
using Campfire.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(CampfireDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAt })
            .SingleOrDefaultAsync();

        return user is null ? NotFound() : Ok(user);
    }
}

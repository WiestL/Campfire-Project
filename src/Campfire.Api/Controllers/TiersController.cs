using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TiersController(CampfireDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static TierResponse ToResponse(MembershipTier tier) =>
        new(tier.Id, tier.CreatorId, tier.Creator.DisplayName, tier.Name, tier.Description, tier.Price, tier.Level);

    [HttpGet]
    public async Task<ActionResult<List<TierResponse>>> GetAll()
    {
        var tiers = await db.MembershipTiers
            .Include(t => t.Creator)
            .OrderBy(t => t.Level)
            .ToListAsync();

        return Ok(tiers.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TierResponse>> GetById(Guid id)
    {
        var tier = await db.MembershipTiers.Include(t => t.Creator).FirstOrDefaultAsync(t => t.Id == id);
        return tier is null ? NotFound() : Ok(ToResponse(tier));
    }

    [HttpPost]
    [Authorize(Roles = "Creator")]
    public async Task<ActionResult<TierResponse>> Create(CreateTierRequest request)
    {
        var creator = await db.Users.FindAsync(CurrentUserId);
        if (creator is null)
        {
            return Unauthorized();
        }

        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Level = request.Level,
        };

        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();

        return Ok(new TierResponse(tier.Id, tier.CreatorId, creator.DisplayName, tier.Name, tier.Description, tier.Price, tier.Level));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Creator")]
    public async Task<ActionResult<TierResponse>> Update(Guid id, UpdateTierRequest request)
    {
        var tier = await db.MembershipTiers.Include(t => t.Creator).FirstOrDefaultAsync(t => t.Id == id);
        if (tier is null)
        {
            return NotFound();
        }

        if (tier.CreatorId != CurrentUserId)
        {
            return Forbid();
        }

        tier.Name = request.Name.Trim();
        tier.Description = request.Description;
        tier.Price = request.Price;
        tier.Level = request.Level;

        await db.SaveChangesAsync();

        return Ok(ToResponse(tier));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Creator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tier = await db.MembershipTiers.FirstOrDefaultAsync(t => t.Id == id);
        if (tier is null)
        {
            return NotFound();
        }

        if (tier.CreatorId != CurrentUserId)
        {
            return Forbid();
        }

        var hasSubscriptions = await db.Subscriptions.AnyAsync(s => s.TierId == id);
        if (hasSubscriptions)
        {
            return Conflict("This tier has active subscriptions and cannot be deleted.");
        }

        db.MembershipTiers.Remove(tier);
        await db.SaveChangesAsync();

        return NoContent();
    }
}

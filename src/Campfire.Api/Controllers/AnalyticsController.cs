using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Creator")]
public class AnalyticsController(CampfireDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    // Only what's directly derivable from existing data (subscriptions, tiers,
    // published content) rather than anything needing new tracking
    // infrastructure (views, likes, comments don't exist yet).
    [HttpGet("me")]
    public async Task<ActionResult<CreatorAnalyticsResponse>> GetMine()
    {
        var tiers = await db.MembershipTiers
            .Where(t => t.CreatorId == CurrentUserId)
            .ToListAsync();

        var tierIds = tiers.Select(t => t.Id).ToList();

        var activeSubscriptions = await db.Subscriptions
            .Where(s => tierIds.Contains(s.TierId) && s.Status == SubscriptionStatus.Active)
            .ToListAsync();

        var subscribersByTier = tiers
            .Select(t => new TierSubscriberCount(
                t.Id,
                t.Name,
                activeSubscriptions.Count(s => s.TierId == t.Id)))
            .ToList();

        var monthlyRecurringRevenue = tiers.Sum(t =>
            t.Price * activeSubscriptions.Count(s => s.TierId == t.Id));

        var publishedPostCount = await db.Content
            .CountAsync(c => c.CreatorId == CurrentUserId && c.PublishedAt != null);

        return Ok(new CreatorAnalyticsResponse(
            activeSubscriptions.Count,
            monthlyRecurringRevenue,
            publishedPostCount,
            subscribersByTier));
    }
}

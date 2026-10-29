using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Services;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentController(CampfireDbContext db, IMediaStorage mediaStorage) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private ContentResponse ToResponse(Content content, string creatorDisplayName, bool hasAccess) =>
        new(
            content.Id, content.CreatorId, creatorDisplayName, content.Type, content.Title,
            hasAccess ? content.Body : null,
            hasAccess && content.MediaUrl is not null ? mediaStorage.GetSignedUrl(content.MediaUrl) : null,
            content.RequiredTierId, content.RequiredTier?.Name, !hasAccess,
            content.CreatedAt, content.PublishedAt);

    [HttpGet]
    public async Task<ActionResult<List<ContentResponse>>> GetFeed()
    {
        var content = await db.Content
            .Include(c => c.Creator)
            .Include(c => c.RequiredTier)
            .Where(c => c.PublishedAt != null)
            .OrderByDescending(c => c.PublishedAt)
            .ToListAsync();

        var viewerId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        // Active subscriptions grant access to every tier at or below the level
        // subscribed to, scoped per creator; a member can have at most one active
        // subscription (enforced in SubscriptionsController.Create), but this stays
        // correct even if that ever changes.
        var accessByCreator = isAdmin
            ? new Dictionary<Guid, int>()
            : await db.Subscriptions
                .Where(s => s.MemberId == viewerId && s.Status == SubscriptionStatus.Active)
                .Include(s => s.Tier)
                .GroupBy(s => s.Tier.CreatorId)
                .Select(g => new { CreatorId = g.Key, MaxLevel = g.Max(s => s.Tier.Level) })
                .ToDictionaryAsync(x => x.CreatorId, x => x.MaxLevel);

        bool HasAccess(Content c)
        {
            if (c.RequiredTierId is null) return true;
            if (isAdmin) return true;
            if (c.CreatorId == viewerId) return true;
            return accessByCreator.TryGetValue(c.CreatorId, out var level) && level >= c.RequiredTier!.Level;
        }

        return Ok(content.Select(c => ToResponse(c, c.Creator.DisplayName, HasAccess(c))).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Creator")]
    public async Task<ActionResult<ContentResponse>> Create(CreateContentRequest request)
    {
        if ((request.Type == ContentType.Video || request.Type == ContentType.Audio) && string.IsNullOrWhiteSpace(request.MediaPath))
        {
            return BadRequest("Video and audio content require a MediaPath (upload the file first via /api/content/upload).");
        }

        var creator = await db.Users.FindAsync(CurrentUserId);
        if (creator is null)
        {
            return Unauthorized();
        }

        MembershipTier? requiredTier = null;
        if (request.RequiredTierId is not null)
        {
            requiredTier = await db.MembershipTiers.FindAsync(request.RequiredTierId);
            if (requiredTier is null || requiredTier.CreatorId != creator.Id)
            {
                return BadRequest("RequiredTierId must be one of your own membership tiers.");
            }
        }

        var content = new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Type = request.Type,
            Title = request.Title.Trim(),
            Body = request.Body,
            MediaUrl = request.MediaPath,
            RequiredTierId = request.RequiredTierId,
            RequiredTier = requiredTier,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = request.Publish ? DateTime.UtcNow : null,
        };

        db.Content.Add(content);
        await db.SaveChangesAsync();

        return Ok(ToResponse(content, creator.DisplayName, hasAccess: true));
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Creator")]
    [RequestSizeLimit(500L * 1024 * 1024)]
    // RequestSizeLimit only raises Kestrel's raw body cap; ASP.NET Core's multipart
    // form reader enforces its own separate, lower default (128 MB) and rejects the
    // request before the action even runs. Found via a real 144 MB upload failing
    // immediately with "Multipart body length limit 134217728 exceeded."
    [RequestFormLimits(MultipartBodyLengthLimit = 500L * 1024 * 1024)]
    public async Task<ActionResult<UploadResponse>> Upload(IFormFile file)
    {
        if (file.Length == 0)
        {
            return BadRequest("No file was uploaded.");
        }

        if (!MediaUploadValidator.TryValidate(file.FileName, file.Length, out var error))
        {
            return BadRequest(error);
        }

        await using var stream = file.OpenReadStream();
        var storagePath = await mediaStorage.UploadAsync(stream, file.FileName);

        return Ok(new UploadResponse(storagePath, file.FileName, file.Length));
    }
}

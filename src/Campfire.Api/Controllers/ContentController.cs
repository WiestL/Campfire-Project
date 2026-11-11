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

    private ContentResponse ToResponse(
        Content content, string creatorDisplayName, bool hasAccess,
        int likeCount = 0, bool likedByCurrentUser = false, int commentCount = 0) =>
        new(
            content.Id, content.CreatorId, creatorDisplayName, content.Type, content.Title,
            hasAccess ? content.Body : null,
            hasAccess && content.MediaUrl is not null ? mediaStorage.GetSignedUrl(content.MediaUrl) : null,
            content.RequiredTierId, content.RequiredTier?.Name, !hasAccess,
            content.CreatedAt, content.PublishedAt,
            likeCount, likedByCurrentUser, commentCount);

    /// <summary>
    /// Single-item version of the access check in GetFeed, used by the Like and
    /// Comment endpoints so a member can't interact with content they can't
    /// actually see. Kept separate from GetFeed's batched dictionary version,
    /// which exists purely so the feed doesn't run one subscription query per item.
    /// </summary>
    private async Task<bool> HasAccessAsync(Content content)
    {
        if (content.RequiredTierId is null) return true;
        if (User.IsInRole("Admin")) return true;
        if (content.CreatorId == CurrentUserId) return true;

        var requiredLevel = content.RequiredTier?.Level
            ?? (await db.MembershipTiers.FindAsync(content.RequiredTierId))!.Level;

        var maxLevel = await db.Subscriptions
            .Where(s => s.MemberId == CurrentUserId && s.Status == SubscriptionStatus.Active)
            .Join(db.MembershipTiers, s => s.TierId, t => t.Id, (s, t) => new { t.CreatorId, t.Level })
            .Where(x => x.CreatorId == content.CreatorId)
            .Select(x => (int?)x.Level)
            .MaxAsync();

        return maxLevel is not null && maxLevel >= requiredLevel;
    }

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
        var contentIds = content.Select(c => c.Id).ToList();

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

        var likeCounts = await db.Likes
            .Where(l => contentIds.Contains(l.ContentId))
            .GroupBy(l => l.ContentId)
            .Select(g => new { ContentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContentId, x => x.Count);

        var likedByViewer = (await db.Likes
            .Where(l => contentIds.Contains(l.ContentId) && l.UserId == viewerId)
            .Select(l => l.ContentId)
            .ToListAsync())
            .ToHashSet();

        var commentCounts = await db.Comments
            .Where(c => contentIds.Contains(c.ContentId) && !c.IsHidden)
            .GroupBy(c => c.ContentId)
            .Select(g => new { ContentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContentId, x => x.Count);

        bool HasAccess(Content c)
        {
            if (c.RequiredTierId is null) return true;
            if (isAdmin) return true;
            if (c.CreatorId == viewerId) return true;
            return accessByCreator.TryGetValue(c.CreatorId, out var level) && level >= c.RequiredTier!.Level;
        }

        return Ok(content.Select(c => ToResponse(
            c, c.Creator.DisplayName, HasAccess(c),
            likeCounts.GetValueOrDefault(c.Id),
            likedByViewer.Contains(c.Id),
            commentCounts.GetValueOrDefault(c.Id))).ToList());
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

    [HttpPost("{id:guid}/like")]
    public async Task<ActionResult<LikeStatusResponse>> ToggleLike(Guid id)
    {
        var content = await db.Content.Include(c => c.RequiredTier).FirstOrDefaultAsync(c => c.Id == id);
        if (content is null || content.PublishedAt is null)
        {
            return NotFound();
        }

        if (!await HasAccessAsync(content))
        {
            return Forbid();
        }

        var existing = await db.Likes.FirstOrDefaultAsync(l => l.ContentId == id && l.UserId == CurrentUserId);
        if (existing is not null)
        {
            db.Likes.Remove(existing);
        }
        else
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), ContentId = id, UserId = CurrentUserId, CreatedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();

        var count = await db.Likes.CountAsync(l => l.ContentId == id);
        return Ok(new LikeStatusResponse(count, existing is null));
    }

    private CommentResponse ToCommentResponse(Comment comment, Guid viewerId, bool viewerIsModerator)
    {
        var canSeeHiddenBody = viewerIsModerator || comment.UserId == viewerId;
        return new CommentResponse(
            comment.Id, comment.ContentId, comment.UserId, comment.User.DisplayName,
            comment.IsHidden && !canSeeHiddenBody ? null : comment.Body,
            comment.ParentCommentId, comment.IsHidden, comment.CreatedAt,
            comment.Replies.OrderBy(r => r.CreatedAt).Select(r => ToCommentResponse(r, viewerId, viewerIsModerator)).ToList());
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<List<CommentResponse>>> GetComments(Guid id)
    {
        var content = await db.Content.Include(c => c.RequiredTier).FirstOrDefaultAsync(c => c.Id == id);
        if (content is null)
        {
            return NotFound();
        }

        if (!await HasAccessAsync(content))
        {
            return Forbid();
        }

        var topLevel = await db.Comments
            .Include(c => c.User)
            .Where(c => c.ContentId == id && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var replies = await db.Comments
            .Include(c => c.User)
            .Where(c => c.ContentId == id && c.ParentCommentId != null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        foreach (var parent in topLevel)
        {
            parent.Replies = replies.Where(r => r.ParentCommentId == parent.Id).ToList();
        }

        var viewerIsModerator = User.IsInRole("Admin") || content.CreatorId == CurrentUserId;
        return Ok(topLevel.Select(c => ToCommentResponse(c, CurrentUserId, viewerIsModerator)).ToList());
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> CreateComment(Guid id, CreateCommentRequest request)
    {
        var content = await db.Content.Include(c => c.RequiredTier).FirstOrDefaultAsync(c => c.Id == id);
        if (content is null || content.PublishedAt is null)
        {
            return NotFound();
        }

        if (!await HasAccessAsync(content))
        {
            return Forbid();
        }

        if (request.ParentCommentId is not null)
        {
            var parent = await db.Comments.FirstOrDefaultAsync(c => c.Id == request.ParentCommentId && c.ContentId == id);
            if (parent is null)
            {
                return BadRequest("Parent comment not found.");
            }

            if (parent.ParentCommentId is not null)
            {
                return BadRequest("Replies can only be one level deep.");
            }
        }

        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return Unauthorized();
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ContentId = id,
            UserId = user.Id,
            ParentCommentId = request.ParentCommentId,
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        return Ok(new CommentResponse(
            comment.Id, comment.ContentId, comment.UserId, user.DisplayName,
            comment.Body, comment.ParentCommentId, comment.IsHidden, comment.CreatedAt, []));
    }
}

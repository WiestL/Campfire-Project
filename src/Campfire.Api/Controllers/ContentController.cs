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

    private ContentResponse ToResponse(Content content, string creatorDisplayName) =>
        new(
            content.Id, content.CreatorId, creatorDisplayName, content.Type, content.Title, content.Body,
            content.MediaUrl is null ? null : mediaStorage.GetSignedUrl(content.MediaUrl),
            content.RequiredTierId, content.CreatedAt, content.PublishedAt);

    [HttpGet]
    public async Task<ActionResult<List<ContentResponse>>> GetFeed()
    {
        var content = await db.Content
            .Include(c => c.Creator)
            .Where(c => c.PublishedAt != null)
            .OrderByDescending(c => c.PublishedAt)
            .ToListAsync();

        return Ok(content.Select(c => ToResponse(c, c.Creator.DisplayName)).ToList());
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

        var content = new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Type = request.Type,
            Title = request.Title.Trim(),
            Body = request.Body,
            MediaUrl = request.MediaPath,
            RequiredTierId = request.RequiredTierId,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = request.Publish ? DateTime.UtcNow : null,
        };

        db.Content.Add(content);
        await db.SaveChangesAsync();

        return Ok(ToResponse(content, creator.DisplayName));
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Creator")]
    [RequestSizeLimit(500L * 1024 * 1024)]
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

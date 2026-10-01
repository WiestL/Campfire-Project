using System.Security.Claims;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentController(CampfireDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<List<ContentResponse>>> GetFeed()
    {
        var content = await db.Content
            .Include(c => c.Creator)
            .Where(c => c.PublishedAt != null)
            .OrderByDescending(c => c.PublishedAt)
            .Select(c => new ContentResponse(
                c.Id, c.CreatorId, c.Creator.DisplayName, c.Type, c.Title, c.Body,
                c.MediaUrl, c.RequiredTierId, c.CreatedAt, c.PublishedAt))
            .ToListAsync();

        return Ok(content);
    }

    [HttpPost]
    [Authorize(Roles = "Creator")]
    public async Task<ActionResult<ContentResponse>> Create(CreateContentRequest request)
    {
        if ((request.Type == ContentType.Video || request.Type == ContentType.Audio) && string.IsNullOrWhiteSpace(request.MediaUrl))
        {
            return BadRequest("Video and audio content require a MediaUrl (upload the file first via /api/content/upload).");
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
            MediaUrl = request.MediaUrl,
            RequiredTierId = request.RequiredTierId,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = request.Publish ? DateTime.UtcNow : null,
        };

        db.Content.Add(content);
        await db.SaveChangesAsync();

        return Ok(new ContentResponse(
            content.Id, content.CreatorId, creator.DisplayName, content.Type, content.Title,
            content.Body, content.MediaUrl, content.RequiredTierId, content.CreatedAt, content.PublishedAt));
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

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid()}{extension}";
        var uploadsDir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var filePath = Path.Combine(uploadsDir, storedName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new UploadResponse($"/uploads/{storedName}", file.FileName, file.Length));
    }
}

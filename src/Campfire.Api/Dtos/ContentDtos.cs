using System.ComponentModel.DataAnnotations;
using Campfire.Api.Models;

namespace Campfire.Api.Dtos;

public record CreateContentRequest(
    [Required] ContentType Type,
    [Required, MaxLength(200)] string Title,
    [Required] string Body,
    string? MediaPath,
    Guid? RequiredTierId,
    bool Publish
);

public record ContentResponse(
    Guid Id,
    Guid CreatorId,
    string CreatorDisplayName,
    ContentType Type,
    string Title,
    string Body,
    string? MediaUrl,
    Guid? RequiredTierId,
    DateTime CreatedAt,
    DateTime? PublishedAt
);

public record UploadResponse(string StoragePath, string FileName, long SizeBytes);

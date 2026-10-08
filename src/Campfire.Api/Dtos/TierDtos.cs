using System.ComponentModel.DataAnnotations;

namespace Campfire.Api.Dtos;

public record CreateTierRequest(
    [Required, MaxLength(100)] string Name,
    [Required] string Description,
    [Range(0, 100000)] decimal Price,
    [Range(0, 100)] int Level
);

public record UpdateTierRequest(
    [Required, MaxLength(100)] string Name,
    [Required] string Description,
    [Range(0, 100000)] decimal Price,
    [Range(0, 100)] int Level
);

public record TierResponse(
    Guid Id,
    Guid CreatorId,
    string CreatorDisplayName,
    string Name,
    string Description,
    decimal Price,
    int Level
);

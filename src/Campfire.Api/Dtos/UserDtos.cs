using System.ComponentModel.DataAnnotations;

namespace Campfire.Api.Dtos;

public record UpdateProfileRequest(
    [Required, MaxLength(100)] string DisplayName,
    [Required, EmailAddress] string Email,
    [Required] string CurrentPassword
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

using Campfire.Api.Models;

namespace Campfire.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

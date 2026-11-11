namespace Campfire.Api.Dtos;

public record LikeStatusResponse(
    int LikeCount,
    bool LikedByCurrentUser
);

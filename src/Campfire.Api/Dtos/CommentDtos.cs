using System.ComponentModel.DataAnnotations;

namespace Campfire.Api.Dtos;

public record CreateCommentRequest(
    [Required, MaxLength(2000)] string Body,
    Guid? ParentCommentId
);

public record CommentResponse(
    Guid Id,
    Guid ContentId,
    Guid UserId,
    string AuthorDisplayName,
    string? Body,
    Guid? ParentCommentId,
    bool IsHidden,
    DateTime CreatedAt,
    List<CommentResponse> Replies
);

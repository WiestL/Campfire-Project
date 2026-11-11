using System.Security.Claims;
using Campfire.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Controllers;

[ApiController]
[Route("api/comments")]
[Authorize]
public class CommentsController(CampfireDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    // REQ-004: moderation is the content's creator's call, not the commenter's;
    // hide is reversible (toggle) so a creator can undo a mistaken hide.
    [HttpPost("{id:guid}/hide")]
    [Authorize(Roles = "Creator,Admin")]
    public async Task<IActionResult> ToggleHide(Guid id)
    {
        var comment = await db.Comments.Include(c => c.Content).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        if (comment.Content.CreatorId != CurrentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        comment.IsHidden = !comment.IsHidden;
        await db.SaveChangesAsync();

        return Ok(new { comment.Id, comment.IsHidden });
    }

    // A comment's own author can always delete their own comment; the content's
    // creator (or an Admin) can delete anyone's comment on their own content.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var comment = await db.Comments.Include(c => c.Content).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null)
        {
            return NotFound();
        }

        var isOwnComment = comment.UserId == CurrentUserId;
        var isContentCreator = comment.Content.CreatorId == CurrentUserId;
        if (!isOwnComment && !isContentCreator && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();

        return NoContent();
    }
}

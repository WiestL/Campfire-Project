using System.Security.Claims;
using Campfire.Api.Controllers;
using Campfire.Api.Data;
using Campfire.Api.Dtos;
using Campfire.Api.Models;
using Campfire.Api.Options;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Campfire.Api.Tests.Unit;

/// <summary>
/// Focused on the areas most likely to house errors for this feature (access
/// control on gated content, the toggle semantics of like/hide, the
/// one-level-reply boundary, and hidden-comment visibility rules) rather than
/// exhaustive per-decision-point coverage, per project direction.
/// </summary>
[Category("Unit Tests")]
[Category("Likes and Comments")]
[TestFixture]
public class LikesAndCommentsControllerTests
{
    private string _mediaRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), "campfire-likes-comments-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_mediaRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    private static CampfireDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CampfireDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CampfireDbContext(options);
    }

    private static async Task<User> AddUserAsync(CampfireDbContext db, UserRole role, string displayName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            DisplayName = displayName,
            Role = role,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Content> AddPublishedContentAsync(CampfireDbContext db, Guid creatorId, Guid? requiredTierId = null)
    {
        var content = new Content
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            RequiredTierId = requiredTierId,
            Type = ContentType.Text,
            Title = "Post",
            Body = "body",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
        };
        db.Content.Add(content);
        await db.SaveChangesAsync();
        return content;
    }

    private static async Task<MembershipTier> AddTierAsync(CampfireDbContext db, Guid creatorId, int level)
    {
        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Gated Tier",
            Description = "desc",
            Price = 5,
            Level = level,
        };
        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();
        return tier;
    }

    private ContentController CreateContentController(CampfireDbContext db, Guid actingUserId, UserRole role)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_mediaRoot);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString()),
                    new Claim(ClaimTypes.Role, role.ToString()),
                ], "test")),
        };

        var cdnOptions = Microsoft.Extensions.Options.Options.Create(new CdnOptions
        {
            Provider = "Local",
            SigningKey = "unit-test-cdn-signing-key",
            UrlExpirySeconds = 3600,
        });

        var controller = new ContentController(db, new LocalMediaStorage(cdnOptions, env.Object));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static CommentsController CreateCommentsController(CampfireDbContext db, Guid actingUserId, UserRole role)
    {
        var controller = new CommentsController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString()),
                        new Claim(ClaimTypes.Role, role.ToString()),
                    ], "test")),
            },
        };
        return controller;
    }

    [Test]
    public async Task ToggleLike_OnGatedContentWithoutAccess_IsForbidden()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var tier = await AddTierAsync(db, creator.Id, level: 1);
        var content = await AddPublishedContentAsync(db, creator.Id, tier.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member"); // no subscription

        var controller = CreateContentController(db, member.Id, UserRole.Member);
        var result = await controller.ToggleLike(content.Id);

        Assert.That(result.Result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task ToggleLike_CalledTwiceBySameUser_LikesThenUnlikes()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");
        var controller = CreateContentController(db, member.Id, UserRole.Member);

        var first = (LikeStatusResponse)((OkObjectResult)(await controller.ToggleLike(content.Id)).Result!).Value!;
        Assert.That(first, Is.EqualTo(new LikeStatusResponse(1, true)));

        var second = (LikeStatusResponse)((OkObjectResult)(await controller.ToggleLike(content.Id)).Result!).Value!;
        Assert.That(second, Is.EqualTo(new LikeStatusResponse(0, false)));
    }

    [Test]
    public async Task CreateComment_OnGatedContentWithoutAccess_IsForbidden()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var tier = await AddTierAsync(db, creator.Id, level: 1);
        var content = await AddPublishedContentAsync(db, creator.Id, tier.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");

        var controller = CreateContentController(db, member.Id, UserRole.Member);
        var result = await controller.CreateComment(content.Id, new CreateCommentRequest("hello", null));

        Assert.That(result.Result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task CreateComment_ReplyToATopLevelComment_Succeeds()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");
        var controller = CreateContentController(db, member.Id, UserRole.Member);

        var topLevel = (CommentResponse)((OkObjectResult)(await controller.CreateComment(
            content.Id, new CreateCommentRequest("top level", null))).Result!).Value!;

        var reply = await controller.CreateComment(content.Id, new CreateCommentRequest("a reply", topLevel.Id));

        Assert.That(reply.Result, Is.InstanceOf<OkObjectResult>());
    }

    [Test]
    public async Task CreateComment_ReplyToAReply_IsRejected()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");
        var controller = CreateContentController(db, member.Id, UserRole.Member);

        var topLevel = (CommentResponse)((OkObjectResult)(await controller.CreateComment(
            content.Id, new CreateCommentRequest("top level", null))).Result!).Value!;
        var reply = (CommentResponse)((OkObjectResult)(await controller.CreateComment(
            content.Id, new CreateCommentRequest("a reply", topLevel.Id))).Result!).Value!;

        var replyToReply = await controller.CreateComment(content.Id, new CreateCommentRequest("too deep", reply.Id));

        Assert.That(replyToReply.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetComments_HiddenComment_HidesBodyFromOrdinaryViewers_ButShowsItToTheContentCreator()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var author = await AddUserAsync(db, UserRole.Member, "Author");
        var otherViewer = await AddUserAsync(db, UserRole.Member, "Other Viewer");

        var authorController = CreateContentController(db, author.Id, UserRole.Member);
        var created = (CommentResponse)((OkObjectResult)(await authorController.CreateComment(
            content.Id, new CreateCommentRequest("secret-ish text", null))).Result!).Value!;

        var commentsController = CreateCommentsController(db, creator.Id, UserRole.Creator);
        await commentsController.ToggleHide(created.Id);

        var otherViewerController = CreateContentController(db, otherViewer.Id, UserRole.Member);
        var asOtherViewer = (List<CommentResponse>)((OkObjectResult)(await otherViewerController.GetComments(content.Id)).Result!).Value!;
        Assert.That(asOtherViewer[0].Body, Is.Null);
        Assert.That(asOtherViewer[0].IsHidden, Is.True);

        var creatorController = CreateContentController(db, creator.Id, UserRole.Creator);
        var asCreator = (List<CommentResponse>)((OkObjectResult)(await creatorController.GetComments(content.Id)).Result!).Value!;
        Assert.That(asCreator[0].Body, Is.EqualTo("secret-ish text"));
    }

    [Test]
    public async Task ToggleHide_ByUnrelatedCreator_IsForbidden()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");
        var otherCreator = await AddUserAsync(db, UserRole.Creator, "Someone Else");

        var memberController = CreateContentController(db, member.Id, UserRole.Member);
        var created = (CommentResponse)((OkObjectResult)(await memberController.CreateComment(
            content.Id, new CreateCommentRequest("text", null))).Result!).Value!;

        var controller = CreateCommentsController(db, otherCreator.Id, UserRole.Creator);
        var result = await controller.ToggleHide(created.Id);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task Delete_ByTheCommentsOwnAuthor_Succeeds()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var member = await AddUserAsync(db, UserRole.Member, "Member");

        var memberController = CreateContentController(db, member.Id, UserRole.Member);
        var created = (CommentResponse)((OkObjectResult)(await memberController.CreateComment(
            content.Id, new CreateCommentRequest("text", null))).Result!).Value!;

        var commentsController = CreateCommentsController(db, member.Id, UserRole.Member);
        var result = await commentsController.Delete(created.Id);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        Assert.That(db.Comments, Is.Empty);
    }

    [Test]
    public async Task Delete_ByAnUnrelatedMember_IsForbidden()
    {
        await using var db = CreateDb();
        var creator = await AddUserAsync(db, UserRole.Creator, "Creator");
        var content = await AddPublishedContentAsync(db, creator.Id);
        var author = await AddUserAsync(db, UserRole.Member, "Author");
        var bystander = await AddUserAsync(db, UserRole.Member, "Bystander");

        var authorController = CreateContentController(db, author.Id, UserRole.Member);
        var created = (CommentResponse)((OkObjectResult)(await authorController.CreateComment(
            content.Id, new CreateCommentRequest("text", null))).Result!).Value!;

        var commentsController = CreateCommentsController(db, bystander.Id, UserRole.Member);
        var result = await commentsController.Delete(created.Id);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
        Assert.That(db.Comments, Has.Exactly(1).Items);
    }
}

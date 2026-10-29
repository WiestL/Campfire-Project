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
using Microsoft.Extensions.Options;
using Moq;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A ContentController")]
[TestFixture]
public class ContentControllerTests
{
    private const string SigningKey = "unit-test-cdn-signing-key";
    private string _mediaRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), "campfire-content-tests-" + Guid.NewGuid());
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

    private static async Task<User> AddCreatorAsync(CampfireDbContext db, string email = "creator@example.com", string displayName = "Test Creator")
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            DisplayName = displayName,
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        return creator;
    }

    private static async Task<User> AddMemberAsync(CampfireDbContext db, string email = "member@example.com")
    {
        var member = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            DisplayName = "Test Member",
            Role = UserRole.Member,
        };
        db.Users.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    private static async Task<MembershipTier> AddTierAsync(CampfireDbContext db, Guid creatorId, int level, string name = "Tier")
    {
        var tier = new MembershipTier
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = name,
            Description = "desc",
            Price = 5,
            Level = level,
        };
        db.MembershipTiers.Add(tier);
        await db.SaveChangesAsync();
        return tier;
    }

    private static async Task AddActiveSubscriptionAsync(CampfireDbContext db, Guid memberId, Guid tierId)
    {
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            TierId = tierId,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private ContentController CreateController(CampfireDbContext db, Guid actingUserId, UserRole role = UserRole.Creator)
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
            SigningKey = SigningKey,
            UrlExpirySeconds = 3600,
        });

        var mediaStorage = new LocalMediaStorage(cdnOptions, env.Object);
        var controller = new ContentController(db, mediaStorage);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    [Test]
    public async Task Create_PublishedTextPost_AppearsInFeed()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var createResult = await controller.Create(new CreateContentRequest(
            ContentType.Text, "Hello world", "My first post", null, null, Publish: true));

        Assert.That(createResult.Result, Is.InstanceOf<OkObjectResult>());

        var feedResult = await controller.GetFeed();
        var feed = ((OkObjectResult)feedResult.Result!).Value as List<ContentResponse>;

        Assert.That(feed, Has.Exactly(1).Items);
        Assert.That(feed![0].Title, Is.EqualTo("Hello world"));
        Assert.That(feed[0].CreatorDisplayName, Is.EqualTo("Test Creator"));
        Assert.That(feed[0].MediaUrl, Is.Null);
    }

    [Test]
    public async Task Create_Draft_DoesNotAppearInFeed()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        await controller.Create(new CreateContentRequest(
            ContentType.Text, "Draft post", "Not ready yet", null, null, Publish: false));

        var feedResult = await controller.GetFeed();
        var feed = ((OkObjectResult)feedResult.Result!).Value as List<ContentResponse>;

        Assert.That(feed, Is.Empty);
    }

    [Test]
    public async Task Create_VideoWithoutMediaPath_IsRejected()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Video, "My vlog", "Body text", null, null, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(db.Content, Is.Empty);
    }

    [Test]
    public async Task Create_AudioWithoutMediaPath_IsRejected()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Audio, "My podcast", "Body text", null, null, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(db.Content, Is.Empty);
    }

    [Test]
    public async Task Create_VideoWithMediaPath_ReturnsSignedUrl()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Video, "My vlog", "Body text", "abc.mp4", null, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(db.Content, Has.Exactly(1).Items);

        var response = (ContentResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.MediaUrl, Does.Contain("/media/abc.mp4"));
        Assert.That(response.MediaUrl, Does.Contain("token="));
        Assert.That(response.MediaUrl, Does.Contain("expires="));

        // the stored path on the entity stays a bare storage path, not a signed URL
        var stored = db.Content.Single();
        Assert.That(stored.MediaUrl, Is.EqualTo("abc.mp4"));
    }

    [Test]
    public async Task Create_WithUnknownActingUser_ReturnsUnauthorized()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Guid.NewGuid());

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Text, "Hello", "Body", null, null, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<UnauthorizedResult>());
        Assert.That(db.Content, Is.Empty);
    }

    private static FormFile CreateFormFile(string fileName, int sizeBytes)
    {
        var stream = new MemoryStream(new byte[sizeBytes]);
        return new FormFile(stream, 0, sizeBytes, "file", fileName);
    }

    [Test]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var file = CreateFormFile("clip.mp4", 0);

        var result = await controller.Upload(file);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Upload_UnsupportedFileType_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var file = CreateFormFile("malware.exe", 1024);

        var result = await controller.Upload(file);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Upload_ValidFile_SavesAndReturnsStoragePath()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var controller = CreateController(db, creator.Id);
        var file = CreateFormFile("clip.mp4", 1024);

        var result = await controller.Upload(file);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (UploadResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.StoragePath, Does.EndWith(".mp4"));
        Assert.That(response.FileName, Is.EqualTo("clip.mp4"));
        Assert.That(response.SizeBytes, Is.EqualTo(1024));

        var savedPath = Path.Combine(_mediaRoot, "media", response.StoragePath);
        Assert.That(File.Exists(savedPath), Is.True);
    }

    [Test]
    public async Task Create_RequiredTierBelongingToAnotherCreator_IsRejected()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var otherCreator = await AddCreatorAsync(db, "other@example.com", "Other Creator");
        var otherTier = await AddTierAsync(db, otherCreator.Id, level: 0);
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Body", null, otherTier.Id, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(db.Content, Is.Empty);
    }

    [Test]
    public async Task Create_RequiredTierBelongingToSelf_Succeeds()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 1, name: "Gold");
        var controller = CreateController(db, creator.Id);

        var result = await controller.Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Body", null, tier.Id, Publish: true));

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var response = (ContentResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.RequiredTierName, Is.EqualTo("Gold"));
        Assert.That(response.IsLocked, Is.False);
    }

    [Test]
    public async Task GetFeed_PublicContent_IsVisibleToEveryone()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var member = await AddMemberAsync(db);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Public post", "Body", null, null, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.False);
        Assert.That(feed[0].Body, Is.EqualTo("Body"));
    }

    [Test]
    public async Task GetFeed_GatedContent_OwningCreatorAlwaysSeesIt()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 1);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, creator.Id, UserRole.Creator);

        Assert.That(feed![0].IsLocked, Is.False);
        Assert.That(feed[0].Body, Is.EqualTo("Secret body"));
    }

    [Test]
    public async Task GetFeed_GatedContent_AdminAlwaysSeesIt()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 1);
        var admin = await AddMemberAsync(db, "admin@example.com");
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, admin.Id, UserRole.Admin);

        Assert.That(feed![0].IsLocked, Is.False);
        Assert.That(feed[0].Body, Is.EqualTo("Secret body"));
    }

    [Test]
    public async Task GetFeed_GatedContent_MemberWithNoSubscription_IsLocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 1, name: "Gold");
        var member = await AddMemberAsync(db);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.True);
        Assert.That(feed[0].Body, Is.Null);
        Assert.That(feed[0].MediaUrl, Is.Null);
        Assert.That(feed[0].RequiredTierName, Is.EqualTo("Gold"));
    }

    [Test]
    public async Task GetFeed_GatedContent_MemberWithMatchingTierSubscription_IsUnlocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 1);
        var member = await AddMemberAsync(db);
        await AddActiveSubscriptionAsync(db, member.Id, tier.Id);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.False);
        Assert.That(feed[0].Body, Is.EqualTo("Secret body"));
    }

    [Test]
    public async Task GetFeed_GatedContent_MemberWithHigherTierSubscription_IsUnlocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var lowTier = await AddTierAsync(db, creator.Id, level: 0, name: "Silver");
        var highTier = await AddTierAsync(db, creator.Id, level: 2, name: "Platinum");
        var member = await AddMemberAsync(db);
        await AddActiveSubscriptionAsync(db, member.Id, highTier.Id);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, lowTier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.False);
    }

    [Test]
    public async Task GetFeed_GatedContent_MemberWithLowerTierSubscription_IsLocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var lowTier = await AddTierAsync(db, creator.Id, level: 0, name: "Silver");
        var highTier = await AddTierAsync(db, creator.Id, level: 2, name: "Platinum");
        var member = await AddMemberAsync(db);
        await AddActiveSubscriptionAsync(db, member.Id, lowTier.Id);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, highTier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.True);
    }

    [Test]
    public async Task GetFeed_GatedContent_SubscriptionToDifferentCreator_IsLocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var otherCreator = await AddCreatorAsync(db, "other@example.com", "Other Creator");
        var tier = await AddTierAsync(db, creator.Id, level: 0);
        var otherTier = await AddTierAsync(db, otherCreator.Id, level: 5);
        var member = await AddMemberAsync(db);
        await AddActiveSubscriptionAsync(db, member.Id, otherTier.Id);
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.True);
    }

    [Test]
    public async Task GetFeed_GatedContent_CanceledSubscription_IsLocked()
    {
        await using var db = CreateDb();
        var creator = await AddCreatorAsync(db);
        var tier = await AddTierAsync(db, creator.Id, level: 0);
        var member = await AddMemberAsync(db);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            TierId = tier.Id,
            Status = SubscriptionStatus.Canceled,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await CreateController(db, creator.Id).Create(new CreateContentRequest(
            ContentType.Text, "Gated post", "Secret body", null, tier.Id, Publish: true));

        var feed = await GetFeedAsync(db, member.Id, UserRole.Member);

        Assert.That(feed![0].IsLocked, Is.True);
    }

    private async Task<List<ContentResponse>?> GetFeedAsync(CampfireDbContext db, Guid viewerId, UserRole role)
    {
        var controller = CreateController(db, viewerId, role);
        var result = await controller.GetFeed();
        return ((OkObjectResult)result.Result!).Value as List<ContentResponse>;
    }
}

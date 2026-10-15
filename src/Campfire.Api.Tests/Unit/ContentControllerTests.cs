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

    private static async Task<User> AddCreatorAsync(CampfireDbContext db)
    {
        var creator = new User
        {
            Id = Guid.NewGuid(),
            Email = "creator@example.com",
            PasswordHash = "hash",
            DisplayName = "Test Creator",
            Role = UserRole.Creator,
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        return creator;
    }

    private ContentController CreateController(CampfireDbContext db, Guid actingUserId)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_mediaRoot);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())], "test")),
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
}

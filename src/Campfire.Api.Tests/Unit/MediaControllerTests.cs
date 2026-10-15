using Campfire.Api.Controllers;
using Campfire.Api.Options;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A MediaController")]
[TestFixture]
public class MediaControllerTests
{
    private const string SigningKey = "test-signing-key";
    private string _root = null!;
    private MediaController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "campfire-mediacontroller-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "media"));

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_root);

        var options = Microsoft.Extensions.Options.Options.Create(new CdnOptions { SigningKey = SigningKey });

        _sut = new MediaController(env.Object, options);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void Get_WithValidSignatureAndExistingFile_ReturnsThePhysicalFile()
    {
        File.WriteAllBytes(Path.Combine(_root, "media", "clip.mp4"), [1, 2, 3]);
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/clip.mp4", 3600);

        var result = _sut.Get("clip.mp4", token, expires);

        Assert.That(result, Is.InstanceOf<PhysicalFileResult>());
        var fileResult = (PhysicalFileResult)result;
        Assert.That(fileResult.ContentType, Is.EqualTo("video/mp4"));
        Assert.That(fileResult.EnableRangeProcessing, Is.True);
    }

    [Test]
    public void Get_WithExpiredSignature_ReturnsUnauthorized()
    {
        File.WriteAllBytes(Path.Combine(_root, "media", "clip.mp4"), [1, 2, 3]);
        var (token, _) = BunnyUrlSigner.Sign(SigningKey, "/clip.mp4", -3600);
        var expiredTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

        var result = _sut.Get("clip.mp4", token, expiredTimestamp);

        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
    }

    [Test]
    public void Get_WithTamperedToken_ReturnsUnauthorized()
    {
        File.WriteAllBytes(Path.Combine(_root, "media", "clip.mp4"), [1, 2, 3]);
        var (_, expires) = BunnyUrlSigner.Sign(SigningKey, "/clip.mp4", 3600);

        var result = _sut.Get("clip.mp4", "not-the-real-token", expires);

        Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
    }

    [Test]
    public void Get_WithValidSignatureButMissingFile_ReturnsNotFound()
    {
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/missing.mp4", 3600);

        var result = _sut.Get("missing.mp4", token, expires);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }
}

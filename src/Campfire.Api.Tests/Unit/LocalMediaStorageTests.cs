using Campfire.Api.Options;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A LocalMediaStorage")]
[TestFixture]
public class LocalMediaStorageTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "campfire-media-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalMediaStorage CreateSut(string signingKey = "test-key", int expirySeconds = 3600)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_root);

        var options = Microsoft.Extensions.Options.Options.Create(new CdnOptions
        {
            Provider = "Local",
            SigningKey = signingKey,
            UrlExpirySeconds = expirySeconds,
        });

        return new LocalMediaStorage(options, env.Object);
    }

    [Test]
    public async Task UploadAsync_WritesFileUnderWebRootMedia_AndPreservesExtension()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream([1, 2, 3, 4]);

        var storagePath = await sut.UploadAsync(content, "clip.mp4");

        Assert.That(storagePath, Does.EndWith(".mp4"));
        var savedPath = Path.Combine(_root, "media", storagePath);
        Assert.That(File.Exists(savedPath), Is.True);
        Assert.That(File.ReadAllBytes(savedPath), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public async Task UploadAsync_GivesEachUploadAUniqueStoragePath()
    {
        var sut = CreateSut();
        await using var contentA = new MemoryStream([1]);
        await using var contentB = new MemoryStream([2]);

        var pathA = await sut.UploadAsync(contentA, "a.mp3");
        var pathB = await sut.UploadAsync(contentB, "b.mp3");

        Assert.That(pathA, Is.Not.EqualTo(pathB));
    }

    [Test]
    public void GetSignedUrl_IsRelativeAndIncludesPathTokenAndExpiry()
    {
        var sut = CreateSut();

        var url = sut.GetSignedUrl("abc.mp4");

        Assert.That(url, Does.StartWith("/media/abc.mp4?"));
        Assert.That(url, Does.Contain("token="));
        Assert.That(url, Does.Contain("expires="));
    }

    [Test]
    public void GetSignedUrl_ProducesATokenThatPassesBunnyUrlSignerValidation()
    {
        var sut = CreateSut(signingKey: "shared-key");

        var url = sut.GetSignedUrl("abc.mp4");
        var (token, expires) = ParseTokenAndExpiry(url);

        var isValid = BunnyUrlSigner.Validate("shared-key", "/abc.mp4", token, expires);

        Assert.That(isValid, Is.True);
    }

    private static (string Token, long Expires) ParseTokenAndExpiry(string url)
    {
        var query = url[(url.IndexOf('?') + 1)..];
        var pairs = query.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        return (pairs["token"], long.Parse(pairs["expires"]));
    }
}

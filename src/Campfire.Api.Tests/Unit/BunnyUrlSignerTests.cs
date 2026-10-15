using Campfire.Api.Storage;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A BunnyUrlSigner")]
[TestFixture]
public class BunnyUrlSignerTests
{
    private const string SigningKey = "test-signing-key";

    [Test]
    public void Validate_AcceptsATokenItJustSigned()
    {
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", 3600);

        var result = BunnyUrlSigner.Validate(SigningKey, "/video.mp4", token, expires);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Validate_RejectsAnExpiredToken()
    {
        var expiredTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var (token, _) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", -7200);

        var result = BunnyUrlSigner.Validate(SigningKey, "/video.mp4", token, expiredTimestamp);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_RejectsATamperedPath()
    {
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", 3600);

        var result = BunnyUrlSigner.Validate(SigningKey, "/different-video.mp4", token, expires);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_RejectsATamperedExpiry()
    {
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", 3600);

        var result = BunnyUrlSigner.Validate(SigningKey, "/video.mp4", token, expires + 100);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_RejectsAWrongSigningKey()
    {
        var (token, expires) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", 3600);

        var result = BunnyUrlSigner.Validate("a-different-key", "/video.mp4", token, expires);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_RejectsANullToken()
    {
        var result = BunnyUrlSigner.Validate(SigningKey, "/video.mp4", null, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());

        Assert.That(result, Is.False);
    }

    [Test]
    public void Sign_ProducesExpiryApproximatelyNSecondsFromNow()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var (_, expires) = BunnyUrlSigner.Sign(SigningKey, "/video.mp4", 3600);

        Assert.That(expires, Is.InRange(before + 3600, before + 3605));
    }
}

using Campfire.Api.Services;

namespace Campfire.Api.Tests.Unit;

[Category("Unit Tests")]
[Category("A MediaUploadValidator")]
[TestFixture]
public class MediaUploadValidatorTests
{
    [TestCase("clip.mp4", 10L * 1024 * 1024)]
    [TestCase("clip.mov", 10L * 1024 * 1024)]
    [TestCase("clip.webm", 10L * 1024 * 1024)]
    [TestCase("episode.mp3", 5L * 1024 * 1024)]
    [TestCase("episode.wav", 5L * 1024 * 1024)]
    [TestCase("episode.m4a", 5L * 1024 * 1024)]
    public void TryValidate_AcceptsAllowedTypesWithinSizeLimit(string fileName, long sizeBytes)
    {
        var result = MediaUploadValidator.TryValidate(fileName, sizeBytes, out var error);

        Assert.That(result, Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidate_IsCaseInsensitiveToExtension()
    {
        var result = MediaUploadValidator.TryValidate("Clip.MP4", 10L * 1024 * 1024, out var error);

        Assert.That(result, Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidate_AcceptsFileExactlyAtVideoSizeLimit()
    {
        var result = MediaUploadValidator.TryValidate("clip.mp4", 500L * 1024 * 1024, out var error);

        Assert.That(result, Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidate_AcceptsFileExactlyAtAudioSizeLimit()
    {
        var result = MediaUploadValidator.TryValidate("episode.mp3", 100L * 1024 * 1024, out var error);

        Assert.That(result, Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidate_RejectsNegativeSize()
    {
        var result = MediaUploadValidator.TryValidate("clip.mp4", -1, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("empty"));
    }

    [Test]
    public void TryValidate_RejectsUnsupportedExtension()
    {
        var result = MediaUploadValidator.TryValidate("malware.exe", 1024, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("Unsupported file type"));
    }

    [Test]
    public void TryValidate_RejectsEmptyFile()
    {
        var result = MediaUploadValidator.TryValidate("clip.mp4", 0, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("empty"));
    }

    [Test]
    public void TryValidate_RejectsVideoOverSizeLimit()
    {
        var result = MediaUploadValidator.TryValidate("clip.mp4", 600L * 1024 * 1024, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("500 MB limit"));
    }

    [Test]
    public void TryValidate_RejectsAudioOverSizeLimit()
    {
        var result = MediaUploadValidator.TryValidate("episode.mp3", 150L * 1024 * 1024, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("100 MB limit"));
    }
}

using System.Text;
using Campfire.Api.Storage;

namespace Campfire.Api.Tests.Integration;

/// <summary>
/// Exercises the real Bunny.net Storage and Pull Zone API, not a mock
/// HttpClient, per the plan's "API to CDN" integration point. Confirms
/// BunnyUrlSigner's hand-implemented token-auth algorithm still matches what
/// Bunny's production servers actually accept, upload through delivery.
/// </summary>
[Category("Integration Tests")]
[Category("The Bunny CDN integration")]
[TestFixture]
public class BunnyCdnIntegrationTests
{
    private HttpClient _httpClient = null!;
    private BunnyMediaStorage _storage = null!;

    [SetUp]
    public void SetUp()
    {
        if (!ExternalServiceConfig.IsBunnyConfigured)
        {
            Assert.Ignore("Cdn:Provider is not set to \"Bunny\" with real credentials; skipping live CDN integration tests.");
        }

        _httpClient = new HttpClient();
        _storage = new BunnyMediaStorage(Microsoft.Extensions.Options.Options.Create(ExternalServiceConfig.Cdn), _httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task UploadThenFetch_RoundTripsTheExactBytesThroughRealBunnyStorage()
    {
        var original = Encoding.UTF8.GetBytes("campfire integration test " + Guid.NewGuid());
        await using var uploadStream = new MemoryStream(original);

        var storagePath = await _storage.UploadAsync(uploadStream, "integration-test.txt");
        var signedUrl = _storage.GetSignedUrl(storagePath);

        using var response = await _httpClient.GetAsync(signedUrl);
        response.EnsureSuccessStatusCode();
        var downloaded = await response.Content.ReadAsByteArrayAsync();

        Assert.That(downloaded, Is.EqualTo(original));
    }

    [Test]
    public async Task SignedUrl_WithTamperedToken_IsRejectedByRealBunnyServers()
    {
        await using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes("tamper test"));
        var storagePath = await _storage.UploadAsync(uploadStream, "tamper-test.txt");
        var signedUrl = _storage.GetSignedUrl(storagePath);

        var tamperedUrl = signedUrl.Replace("token=", "token=tampered-");

        using var response = await _httpClient.GetAsync(tamperedUrl);

        Assert.That((int)response.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task SignedUrl_WithNoToken_IsRejectedByRealBunnyServers()
    {
        await using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes("no token test"));
        var storagePath = await _storage.UploadAsync(uploadStream, "no-token-test.txt");

        var urlWithoutToken = $"https://{ExternalServiceConfig.Cdn.PullZoneHostname}/{storagePath}";
        using var response = await _httpClient.GetAsync(urlWithoutToken);

        Assert.That((int)response.StatusCode, Is.EqualTo(403));
    }
}

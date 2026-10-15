using Campfire.Api.Options;
using Microsoft.Extensions.Options;

namespace Campfire.Api.Storage;

/// <summary>
/// Real Bunny.net implementation: uploads to Bunny Storage via their REST API
/// and signs playback URLs for the Pull Zone using Bunny's token-auth scheme.
/// Activated by setting Cdn:Provider to "Bunny" with real credentials.
/// </summary>
public class BunnyMediaStorage(IOptions<CdnOptions> options, HttpClient httpClient) : IMediaStorage
{
    private readonly CdnOptions _options = options.Value;

    public async Task<string> UploadAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var storagePath = $"{Guid.NewGuid()}{extension}";
        var url = $"https://{_options.StorageHostname}/{_options.StorageZoneName}/{storagePath}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("AccessKey", _options.StorageApiKey);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return storagePath;
    }

    public string GetSignedUrl(string storagePath)
    {
        var urlPath = "/" + storagePath;
        var (token, expires) = BunnyUrlSigner.Sign(_options.SigningKey, urlPath, _options.UrlExpirySeconds);
        return $"https://{_options.PullZoneHostname}{urlPath}?token={token}&expires={expires}";
    }
}

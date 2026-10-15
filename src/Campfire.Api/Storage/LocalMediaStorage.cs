using Campfire.Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Campfire.Api.Storage;

/// <summary>
/// Local dev/test stand-in for BunnyMediaStorage. Saves files under wwwroot/media
/// and signs URLs using the exact same scheme Bunny uses, served back through
/// MediaController (which validates the signature/expiry) instead of raw static
/// file hosting, so signed-URL behavior is real and testable without a Bunny
/// account. Active whenever Cdn:Provider is not "Bunny".
///
/// URLs are returned relative (/media/...), not absolute, so they flow through
/// the frontend dev server's existing proxy to the API instead of requiring the
/// browser to trust the API's self-signed local HTTPS certificate directly.
/// The real BunnyMediaStorage always returns absolute Pull Zone URLs.
/// </summary>
public class LocalMediaStorage(IOptions<CdnOptions> options, IWebHostEnvironment env) : IMediaStorage
{
    private readonly CdnOptions _options = options.Value;

    public async Task<string> UploadAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var storagePath = $"{Guid.NewGuid()}{extension}";
        var mediaDir = Path.Combine(env.WebRootPath, "media");
        Directory.CreateDirectory(mediaDir);

        var filePath = Path.Combine(mediaDir, storagePath);
        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, ct);

        return storagePath;
    }

    public string GetSignedUrl(string storagePath)
    {
        var urlPath = "/" + storagePath;
        var (token, expires) = BunnyUrlSigner.Sign(_options.SigningKey, urlPath, _options.UrlExpirySeconds);

        return $"/media/{storagePath}?token={token}&expires={expires}";
    }
}

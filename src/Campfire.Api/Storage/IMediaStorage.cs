namespace Campfire.Api.Storage;

public interface IMediaStorage
{
    /// <summary>Uploads the stream and returns a stable storage path (not a URL) to persist on the Content record.</summary>
    Task<string> UploadAsync(Stream content, string fileName, CancellationToken ct = default);

    /// <summary>Builds a signed, time-limited URL for the given storage path, valid for CdnOptions.UrlExpirySeconds.</summary>
    string GetSignedUrl(string storagePath);
}

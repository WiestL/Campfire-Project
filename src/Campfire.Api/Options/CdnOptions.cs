namespace Campfire.Api.Options;

public class CdnOptions
{
    public const string SectionName = "Cdn";

    /// <summary>"Local" (dev fallback, served by MediaController) or "Bunny".</summary>
    public string Provider { get; set; } = "Local";

    public string StorageZoneName { get; set; } = string.Empty;
    public string StorageApiKey { get; set; } = string.Empty;
    public string StorageHostname { get; set; } = "storage.bunnycdn.com";
    public string PullZoneHostname { get; set; } = string.Empty;

    /// <summary>Shared secret used to sign (and, for Local, validate) time-limited media URLs.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int UrlExpirySeconds { get; set; } = 3600;
}

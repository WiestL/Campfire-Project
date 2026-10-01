namespace Campfire.Api.Services;

public static class MediaUploadValidator
{
    private static readonly Dictionary<string, long> AllowedVideoExtensions = new()
    {
        [".mp4"] = 500L * 1024 * 1024,
        [".mov"] = 500L * 1024 * 1024,
        [".webm"] = 500L * 1024 * 1024,
    };

    private static readonly Dictionary<string, long> AllowedAudioExtensions = new()
    {
        [".mp3"] = 100L * 1024 * 1024,
        [".wav"] = 100L * 1024 * 1024,
        [".m4a"] = 100L * 1024 * 1024,
    };

    public static bool TryValidate(string fileName, long sizeBytes, out string error)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var allowed = AllowedVideoExtensions.TryGetValue(extension, out var videoLimit)
            ? videoLimit
            : AllowedAudioExtensions.TryGetValue(extension, out var audioLimit) ? audioLimit : (long?)null;

        if (allowed is null)
        {
            error = $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", AllowedVideoExtensions.Keys.Concat(AllowedAudioExtensions.Keys))}.";
            return false;
        }

        if (sizeBytes <= 0)
        {
            error = "The uploaded file is empty.";
            return false;
        }

        if (sizeBytes > allowed.Value)
        {
            error = $"File exceeds the {allowed.Value / (1024 * 1024)} MB limit for '{extension}' files.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

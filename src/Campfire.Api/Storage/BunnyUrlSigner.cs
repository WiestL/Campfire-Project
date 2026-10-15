using System.Security.Cryptography;
using System.Text;

namespace Campfire.Api.Storage;

/// <summary>
/// Implements Bunny.net's documented token-authentication scheme:
/// token = base64url(SHA256(signingKey + urlPath + expiresUnixSeconds)).
/// Used for both the real Bunny provider and the local dev stand-in, so the
/// signing/validation logic is identical regardless of which one is active.
/// </summary>
public static class BunnyUrlSigner
{
    public static (string Token, long Expires) Sign(string signingKey, string urlPath, int expirySeconds)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expirySeconds;
        return (ComputeToken(signingKey, urlPath, expires), expires);
    }

    public static bool Validate(string signingKey, string urlPath, string? token, long expires)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        var expected = ComputeToken(signingKey, urlPath, expires);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(token);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string ComputeToken(string signingKey, string urlPath, long expires)
    {
        var hashable = signingKey + urlPath + expires;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(hashable));

        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}

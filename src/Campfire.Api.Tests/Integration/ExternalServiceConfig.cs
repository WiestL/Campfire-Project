using Campfire.Api.Options;
using Microsoft.Extensions.Configuration;

namespace Campfire.Api.Tests.Integration;

/// <summary>
/// Reads the same user-secrets as Campfire.Api (identified by its UserSecretsId,
/// since this test project has no secrets.json of its own) so the Bunny/Stripe
/// integration tests exercise the real sandbox accounts already configured for
/// local dev, instead of mocks. Tests using this skip gracefully when a
/// machine (including CI) has no secrets configured.
/// </summary>
public static class ExternalServiceConfig
{
    private const string ApiUserSecretsId = "2970722a-62a4-455d-9432-1250b769fd80";

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(ApiUserSecretsId)
        .Build();

    public static CdnOptions Cdn { get; } = Configuration.GetSection(CdnOptions.SectionName).Get<CdnOptions>() ?? new CdnOptions();

    public static StripeOptions Stripe { get; } = Configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>() ?? new StripeOptions();

    public static bool IsBunnyConfigured =>
        Cdn.Provider == "Bunny" && !string.IsNullOrWhiteSpace(Cdn.StorageApiKey) && !string.IsNullOrWhiteSpace(Cdn.StorageZoneName);

    public static bool IsStripeConfigured =>
        !string.IsNullOrWhiteSpace(Stripe.SecretKey) && Stripe.SecretKey.StartsWith("sk_test_");
}

using System.ComponentModel.DataAnnotations;
using Campfire.Api.Models;

namespace Campfire.Api.Dtos;

public record CreateSubscriptionRequest(
    [Required] Guid TierId,
    [Required] string PaymentMethodId
);

public record SetupIntentResponse(string ClientSecret, string PublishableKey);

public record SubscriptionResponse(
    Guid Id,
    Guid TierId,
    string TierName,
    SubscriptionStatus Status,
    DateTime StartedAt,
    DateTime? CurrentPeriodEnd,
    string? ClientSecretForAction
);

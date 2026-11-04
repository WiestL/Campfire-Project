namespace Campfire.Api.Dtos;

public record TierSubscriberCount(
    Guid TierId,
    string TierName,
    int ActiveSubscribers
);

public record CreatorAnalyticsResponse(
    int TotalActiveSubscribers,
    decimal MonthlyRecurringRevenue,
    int PublishedPostCount,
    List<TierSubscriberCount> SubscribersByTier
);

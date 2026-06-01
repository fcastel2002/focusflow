namespace FocusAnchor.Data.Google;

public sealed record GoogleCalendarEvent(
    string Id,
    string Summary,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ETag,
    DateTimeOffset UpdatedAt,
    string? FocusAnchorPlanId);

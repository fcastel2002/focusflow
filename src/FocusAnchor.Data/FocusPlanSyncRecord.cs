using FocusAnchor.Core;

namespace FocusAnchor.Data;

public sealed record FocusPlanSyncRecord(
    FocusPlan Plan,
    string? GoogleEventId,
    string? GoogleETag,
    DateTimeOffset LocalUpdatedAt,
    DateTimeOffset? GoogleUpdatedAt,
    DateTimeOffset? LastSyncedAt,
    bool IsDeleted);

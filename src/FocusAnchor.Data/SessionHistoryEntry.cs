namespace FocusAnchor.Data;

public sealed record SessionHistoryEntry(
    long Id,
    string IntentDescription,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan PlannedDuration,
    TimeSpan FocusedDuration,
    int DistractionCount,
    string? Reflection);

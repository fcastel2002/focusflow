namespace FocusAnchor.Core;

public sealed record DailyAttentionSummary(
    DateOnly Date,
    int PlannedBlockCount,
    int ReviewedSessionCount,
    TimeSpan FocusedDuration,
    int DistractionCount);

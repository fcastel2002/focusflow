namespace FocusAnchor.Data;

public sealed record AttentionSummary(
    int SessionCount,
    TimeSpan TotalFocusedDuration,
    int TotalDistractionCount);

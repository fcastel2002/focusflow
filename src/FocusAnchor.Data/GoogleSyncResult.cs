using FocusAnchor.Data.Google;

namespace FocusAnchor.Data;

public sealed record GoogleSyncResult(
    int ExportedCount,
    int ImportedCount,
    IReadOnlyList<GoogleCalendarEvent> ExternalEvents,
    IReadOnlyList<GoogleSyncConflict> Conflicts);

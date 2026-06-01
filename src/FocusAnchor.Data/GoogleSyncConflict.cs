using FocusAnchor.Core;
using FocusAnchor.Data.Google;

namespace FocusAnchor.Data;

public sealed record GoogleSyncConflict(
    FocusPlan LocalPlan,
    GoogleCalendarEvent GoogleEvent);

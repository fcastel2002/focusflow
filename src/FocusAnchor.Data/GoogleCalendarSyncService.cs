using FocusAnchor.Data.Google;

namespace FocusAnchor.Data;

public sealed class GoogleCalendarSyncService
{
    private readonly GoogleCalendarApiClient _apiClient;
    private readonly ICalendarRepository _repository;

    public GoogleCalendarSyncService(ICalendarRepository repository, GoogleCalendarApiClient apiClient)
    {
        _repository = repository;
        _apiClient = apiClient;
    }

    public async Task<GoogleSyncResult> SyncAsync(
        string accessToken,
        long calendarId,
        CancellationToken cancellationToken = default)
    {
        var link = _repository.GetGoogleCalendarLink(calendarId)
            ?? throw new InvalidOperationException("Vinculá un calendario Google antes de sincronizar.");
        var syncedAt = DateTimeOffset.UtcNow;
        var remoteEvents = await _apiClient.GetEventsAsync(accessToken, link.GoogleCalendarId, cancellationToken);
        var remoteFocusEvents = remoteEvents
            .Where(remote => remote.FocusAnchorPlanId is not null)
            .ToArray();
        var externalEvents = remoteEvents
            .Where(remote => remote.FocusAnchorPlanId is null)
            .ToArray();
        var localRecords = _repository.GetPlansForSync(calendarId);
        var conflicts = new List<GoogleSyncConflict>();
        var exported = 0;
        var imported = 0;

        foreach (var local in localRecords)
        {
            var remote = remoteFocusEvents.FirstOrDefault(candidate =>
                candidate.Id == local.GoogleEventId
                || candidate.FocusAnchorPlanId == local.Plan.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (local.IsDeleted)
            {
                if (local.GoogleEventId is not null)
                {
                    await _apiClient.DeleteEventAsync(accessToken, link.GoogleCalendarId, local.GoogleEventId, cancellationToken);
                }

                _repository.DeletePlanPermanently(local.Plan.Id);
                exported++;
                continue;
            }

            if (local.GoogleEventId is null)
            {
                if (remote is not null)
                {
                    conflicts.Add(new GoogleSyncConflict(local.Plan, remote));
                    continue;
                }

                var inserted = await _apiClient.InsertPlanEventAsync(accessToken, link.GoogleCalendarId, local.Plan, cancellationToken);
                _repository.MarkPlanSynced(local.Plan.Id, inserted, syncedAt);
                exported++;
                continue;
            }

            var localChanged = local.LastSyncedAt is null || local.LocalUpdatedAt > local.LastSyncedAt;

            if (remote is null)
            {
                if (localChanged)
                {
                    var inserted = await _apiClient.InsertPlanEventAsync(accessToken, link.GoogleCalendarId, local.Plan, cancellationToken);
                    _repository.MarkPlanSynced(local.Plan.Id, inserted, syncedAt);
                    exported++;
                }
                else
                {
                    _repository.DeletePlanPermanently(local.Plan.Id);
                }

                continue;
            }

            var googleChanged = local.LastSyncedAt is not null && remote.UpdatedAt > local.LastSyncedAt;

            if (localChanged && googleChanged)
            {
                conflicts.Add(new GoogleSyncConflict(local.Plan, remote));
            }
            else if (localChanged)
            {
                var patched = await _apiClient.PatchPlanEventAsync(
                    accessToken,
                    link.GoogleCalendarId,
                    local.GoogleEventId,
                    local.Plan,
                    cancellationToken);
                _repository.MarkPlanSynced(local.Plan.Id, patched, syncedAt);
                exported++;
            }
            else if (googleChanged)
            {
                _repository.ImportGooglePlan(calendarId, remote);
                imported++;
            }
        }

        var knownEventIds = localRecords
            .Select(local => local.GoogleEventId)
            .Where(eventId => eventId is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var remote in remoteFocusEvents.Where(remote => !knownEventIds.Contains(remote.Id)))
        {
            _repository.ImportGooglePlan(calendarId, remote);
            imported++;
        }

        return new GoogleSyncResult(exported, imported, externalEvents, conflicts);
    }

    public async Task ResolveConflictAsync(
        string accessToken,
        long calendarId,
        GoogleSyncConflict conflict,
        GoogleSyncConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        var link = _repository.GetGoogleCalendarLink(calendarId)
            ?? throw new InvalidOperationException("Vinculá un calendario Google antes de sincronizar.");

        if (resolution is GoogleSyncConflictResolution.UseLocal)
        {
            var patched = await _apiClient.PatchPlanEventAsync(
                accessToken,
                link.GoogleCalendarId,
                conflict.GoogleEvent.Id,
                conflict.LocalPlan,
                cancellationToken);
            _repository.MarkPlanSynced(conflict.LocalPlan.Id, patched, DateTimeOffset.UtcNow);
        }
        else
        {
            _repository.ImportGooglePlan(calendarId, conflict.GoogleEvent);
        }
    }
}

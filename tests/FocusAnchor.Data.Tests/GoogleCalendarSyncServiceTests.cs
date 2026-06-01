using System.Net;
using System.Text;
using FocusAnchor.Core;
using FocusAnchor.Data.Google;

namespace FocusAnchor.Data.Tests;

[TestClass]
public sealed class GoogleCalendarSyncServiceTests
{
    [TestMethod]
    public async Task SyncAsync_ExportsLocalPlanAndKeepsExternalEventReadOnly()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        var plan = repository.SavePlan(new FocusPlan(
            0,
            calendar.Id,
            "Draft proposal",
            DateTimeOffset.Parse("2026-06-02T09:00:00-03:00"),
            TimeSpan.FromMinutes(25)));
        string? insertedBody = null;
        var service = CreateService(repository, async request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(EventsJson(ExternalEventJson()));
            }

            insertedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(FocusEventJson("created", plan, DateTimeOffset.UtcNow));
        });

        var result = await service.SyncAsync("access", calendar.Id);

        Assert.AreEqual(1, result.ExportedCount);
        Assert.AreEqual("Meeting", result.ExternalEvents.Single().Summary);
        StringAssert.Contains(insertedBody, "focusAnchorPlanId");
        Assert.AreEqual("created", repository.GetPlansForSync(calendar.Id).Single().GoogleEventId);
    }

    [TestMethod]
    public async Task SyncAsync_PatchesChangedLocalPlan()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        var plan = repository.SavePlan(NewPlan(calendar.Id, "Draft proposal"));
        var lastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        repository.MarkPlanSynced(plan.Id, RemoteEvent("remote", plan, lastSyncedAt.AddMinutes(-1)), lastSyncedAt);
        var changed = repository.SavePlan(new FocusPlan(plan.Id, calendar.Id, "Revised proposal", plan.StartsAt, plan.Duration));
        var patched = false;
        var service = CreateService(repository, request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(EventsJson(FocusEventJson("remote", plan, lastSyncedAt.AddMinutes(-1)))));
            }

            patched = request.Method.Method == "PATCH";
            return Task.FromResult(JsonResponse(FocusEventJson("remote", changed, DateTimeOffset.UtcNow)));
        });

        await service.SyncAsync("access", calendar.Id);

        Assert.IsTrue(patched);
    }

    [TestMethod]
    public async Task SyncAsync_DeletesRemoteEventAfterLocalDeletion()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        var plan = repository.SavePlan(NewPlan(calendar.Id, "Draft proposal"));
        var syncedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        repository.MarkPlanSynced(plan.Id, RemoteEvent("remote", plan, syncedAt), syncedAt);
        repository.DeletePlan(plan.Id);
        var deleted = false;
        var service = CreateService(repository, request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(EventsJson(FocusEventJson("remote", plan, syncedAt))));
            }

            deleted = request.Method == HttpMethod.Delete;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        await service.SyncAsync("access", calendar.Id);

        Assert.IsTrue(deleted);
        Assert.IsEmpty(repository.GetPlansForSync(calendar.Id));
    }

    [TestMethod]
    public async Task SyncAsync_ReportsConflictWithoutOverwritingEitherVersion()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        var plan = repository.SavePlan(NewPlan(calendar.Id, "Draft proposal"));
        var lastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        repository.MarkPlanSynced(plan.Id, RemoteEvent("remote", plan, lastSyncedAt), lastSyncedAt);
        repository.SavePlan(new FocusPlan(plan.Id, calendar.Id, "Local revision", plan.StartsAt, plan.Duration));
        var googleRevision = new FocusPlan(plan.Id, calendar.Id, "Google revision", plan.StartsAt, plan.Duration);
        var service = CreateService(repository, request =>
            Task.FromResult(JsonResponse(EventsJson(FocusEventJson("remote", googleRevision, DateTimeOffset.UtcNow)))));

        var result = await service.SyncAsync("access", calendar.Id);

        Assert.HasCount(1, result.Conflicts);
        Assert.AreEqual("Local revision", repository.GetPlans(DateOnly.FromDateTime(plan.StartsAt.LocalDateTime)).Single().IntentDescription);
    }

    [TestMethod]
    public async Task SyncAsync_LeavesLocalPlanPendingWhenNetworkFails()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        repository.SavePlan(NewPlan(calendar.Id, "Offline plan"));
        var service = CreateService(repository, _ => throw new HttpRequestException("offline"));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => service.SyncAsync("access", calendar.Id));

        Assert.IsNull(repository.GetPlansForSync(calendar.Id).Single().GoogleEventId);
    }

    [TestMethod]
    public async Task SyncAsync_ImportsRemoteFocusAnchorEventAsEditablePlan()
    {
        using var database = new TemporaryDatabase();
        var repository = CreateLinkedRepository(database.Path, out var calendar);
        var remotePlan = NewPlan(calendar.Id, "Imported plan");
        var service = CreateService(repository, _ =>
            Task.FromResult(JsonResponse(EventsJson(FocusEventJson("remote", remotePlan, DateTimeOffset.UtcNow)))));

        var result = await service.SyncAsync("access", calendar.Id);

        Assert.AreEqual(1, result.ImportedCount);
        Assert.AreEqual("Imported plan", repository.GetPlans(DateOnly.FromDateTime(remotePlan.StartsAt.LocalDateTime)).Single().IntentDescription);
    }

    private static SqliteSessionHistoryRepository CreateLinkedRepository(string path, out FocusCalendar calendar)
    {
        var repository = new SqliteSessionHistoryRepository(path);
        calendar = repository.GetCalendars().Single();
        repository.SaveGoogleCalendarLink(new GoogleCalendarLink(calendar.Id, "remote-calendar", "Remote"));
        return repository;
    }

    private static FocusPlan NewPlan(long calendarId, string intent)
    {
        return new FocusPlan(
            0,
            calendarId,
            intent,
            DateTimeOffset.Parse("2026-06-02T09:00:00-03:00"),
            TimeSpan.FromMinutes(25));
    }

    private static GoogleCalendarSyncService CreateService(
        ICalendarRepository repository,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        return new GoogleCalendarSyncService(
            repository,
            new GoogleCalendarApiClient(new HttpClient(new StubHandler(handler))));
    }

    private static GoogleCalendarEvent RemoteEvent(string id, FocusPlan plan, DateTimeOffset updatedAt)
    {
        return new GoogleCalendarEvent(
            id,
            plan.IntentDescription,
            plan.StartsAt,
            plan.StartsAt.Add(plan.Duration),
            "\"etag\"",
            updatedAt,
            plan.Id.ToString());
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string EventsJson(params string[] events)
    {
        return """{"items":[""" + string.Join(",", events) + "]}";
    }

    private static string ExternalEventJson()
    {
        return """
            {
              "id":"meeting",
              "summary":"Meeting",
              "status":"confirmed",
              "etag":"\"external\"",
              "updated":"2026-06-01T12:00:00Z",
              "start":{"dateTime":"2026-06-02T10:00:00-03:00"},
              "end":{"dateTime":"2026-06-02T10:30:00-03:00"}
            }
            """;
    }

    private static string FocusEventJson(string id, FocusPlan plan, DateTimeOffset updatedAt)
    {
        return $$$"""
            {
              "id":"{{{id}}}",
              "summary":"{{{plan.IntentDescription}}}",
              "status":"confirmed",
              "etag":"\"etag\"",
              "updated":"{{{updatedAt:O}}}",
              "start":{"dateTime":"{{{plan.StartsAt:O}}}"},
              "end":{"dateTime":"{{{plan.StartsAt.Add(plan.Duration):O}}}"},
              "extendedProperties":{"private":{"focusAnchorPlanId":"{{{plan.Id}}}"}}
            }
            """;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string _directory =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FocusAnchor.SyncTests", Guid.NewGuid().ToString("N"));

        public TemporaryDatabase()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "focus-anchor.db");
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

}

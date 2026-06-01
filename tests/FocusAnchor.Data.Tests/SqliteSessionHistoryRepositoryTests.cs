using FocusAnchor.Core;
using Microsoft.Data.Sqlite;

namespace FocusAnchor.Data.Tests;

[TestClass]
public sealed class SqliteSessionHistoryRepositoryTests
{
    [TestMethod]
    public void Constructor_CreatesDatabaseFile()
    {
        using var database = new TemporaryDatabase();

        _ = new SqliteSessionHistoryRepository(database.Path);

        Assert.IsTrue(File.Exists(database.Path));
    }

    [TestMethod]
    public void Save_AndGetRecent_PreserveReviewedSession()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var startedAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var session = new FocusSession(new FocusIntent("Write release notes"), TimeSpan.FromMinutes(25));
        session.Start(startedAt);
        session.AddDistraction(new DistractionEntry("Check email", startedAt.AddMinutes(4)));
        session.Pause(startedAt.AddMinutes(10));
        session.Resume(startedAt.AddMinutes(15));
        session.End(startedAt.AddMinutes(25));
        var review = session.CreateReview("Stayed with the document.", startedAt.AddMinutes(26));

        repository.Save(review);

        var stored = repository.GetRecent().Single();
        Assert.AreEqual("Write release notes", stored.IntentDescription);
        Assert.AreEqual(startedAt, stored.StartedAt);
        Assert.AreEqual(startedAt.AddMinutes(25), stored.EndedAt);
        Assert.AreEqual(TimeSpan.FromMinutes(25), stored.PlannedDuration);
        Assert.AreEqual(TimeSpan.FromMinutes(20), stored.FocusedDuration);
        Assert.AreEqual(1, stored.DistractionCount);
        Assert.AreEqual("Stayed with the document.", stored.Reflection);
    }

    [TestMethod]
    public void GetRecent_ReturnsNewestSessionsFirst()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        SaveReview(repository, "Earlier focus", new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
        SaveReview(repository, "Later focus", new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero));

        var stored = repository.GetRecent();

        Assert.AreEqual("Later focus", stored[0].IntentDescription);
        Assert.AreEqual("Earlier focus", stored[1].IntentDescription);
    }

    [TestMethod]
    public void GetSummary_AggregatesAttentionWithoutScoringIt()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        SaveReview(repository, "First focus", new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), distractionCount: 1);
        SaveReview(repository, "Second focus", new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), distractionCount: 2);

        var summary = repository.GetSummary();

        Assert.AreEqual(2, summary.SessionCount);
        Assert.AreEqual(TimeSpan.FromMinutes(50), summary.TotalFocusedDuration);
        Assert.AreEqual(3, summary.TotalDistractionCount);
    }

    [TestMethod]
    public void SetPreference_UpsertsValue()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);

        repository.SetPreference("theme", "light");
        repository.SetPreference("theme", "dark");

        Assert.AreEqual("dark", repository.GetPreference("theme"));
    }

    [TestMethod]
    public void Constructor_SeedsPersonalCalendar()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);

        var calendar = repository.GetCalendars().Single();

        Assert.AreEqual("Personal", calendar.Name);
    }

    [TestMethod]
    public void Constructor_MigratesExistingSessionDatabase()
    {
        using var database = new TemporaryDatabase();
        using (var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE focus_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    intent_description TEXT NOT NULL,
                    started_at TEXT NOT NULL,
                    ended_at TEXT NOT NULL,
                    planned_duration_seconds INTEGER NOT NULL,
                    focused_duration_seconds INTEGER NOT NULL,
                    distraction_count INTEGER NOT NULL,
                    reflection TEXT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        var repository = new SqliteSessionHistoryRepository(database.Path);

        Assert.AreEqual("Personal", repository.GetCalendars().Single().Name);
        using var migratedConnection = new SqliteConnection($"Data Source={database.Path};Pooling=False");
        migratedConnection.Open();
        using var versionCommand = migratedConnection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        Assert.AreEqual(4L, versionCommand.ExecuteScalar());
    }

    [TestMethod]
    public void SavePlan_AndGetPlans_PreservePlan()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var calendar = repository.GetCalendars().Single();
        var startsAt = new DateTimeOffset(2026, 6, 2, 9, 30, 0, TimeSpan.Zero);

        var stored = repository.SavePlan(new FocusPlan(0, calendar.Id, "Draft proposal", startsAt, TimeSpan.FromMinutes(45)));

        Assert.AreEqual(stored, repository.GetPlans(new DateOnly(2026, 6, 2)).Single());
    }

    [TestMethod]
    public void SaveCalendar_AndDeleteCalendar_CascadePlans()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var calendar = repository.SaveCalendar(new FocusCalendar(0, "Studio", "#123ABC"));
        repository.SavePlan(new FocusPlan(
            0,
            calendar.Id,
            "Outline article",
            new DateTimeOffset(2026, 6, 2, 9, 30, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(25)));

        var updated = repository.SaveCalendar(new FocusCalendar(calendar.Id, "Writing", "#654321"));
        repository.DeleteCalendar(updated.Id);

        Assert.IsFalse(repository.GetCalendars().Any(item => item.Id == updated.Id));
        Assert.IsEmpty(repository.GetPlans(new DateOnly(2026, 6, 2)));
    }

    [TestMethod]
    public void SetDailyGoal_ReplacesExistingGoal()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var calendar = repository.GetCalendars().Single();
        var date = new DateOnly(2026, 6, 2);

        repository.SetDailyGoal(new DailyGoal(calendar.Id, date, "Write calmly"));
        repository.SetDailyGoal(new DailyGoal(calendar.Id, date, "Protect attention"));

        Assert.AreEqual("Protect attention", repository.GetDailyGoal(calendar.Id, date)?.Description);
    }

    [TestMethod]
    public void GetDailySummary_DescribesPlansAndReviewedSessions()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var calendar = repository.GetCalendars().Single();
        var startedAt = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
        var plan = repository.SavePlan(new FocusPlan(0, calendar.Id, "Draft proposal", startedAt, TimeSpan.FromMinutes(25)));
        var session = new FocusSession(new FocusIntent(plan.IntentDescription), plan.Duration, plan.Id);
        session.Start(startedAt);
        session.AddDistraction(new DistractionEntry("Check inbox", startedAt.AddMinutes(2)));
        session.End(startedAt.AddMinutes(25));
        repository.Save(session.CreateReview(null, startedAt.AddMinutes(26)));

        var summary = repository.GetDailySummary(new DateOnly(2026, 6, 2));

        Assert.AreEqual(1, summary.PlannedBlockCount);
        Assert.AreEqual(1, summary.ReviewedSessionCount);
        Assert.AreEqual(TimeSpan.FromMinutes(25), summary.FocusedDuration);
        Assert.AreEqual(1, summary.DistractionCount);
    }

    [TestMethod]
    public void SaveGoogleCalendarLink_AndDeleteGoogleCalendarLink_PreserveLocalCalendar()
    {
        using var database = new TemporaryDatabase();
        var repository = new SqliteSessionHistoryRepository(database.Path);
        var calendar = repository.GetCalendars().Single();

        repository.SaveGoogleCalendarLink(new GoogleCalendarLink(calendar.Id, "remote-id", "Remote calendar"));

        Assert.AreEqual("remote-id", repository.GetGoogleCalendarLink(calendar.Id)?.GoogleCalendarId);

        repository.DeleteGoogleCalendarLink(calendar.Id);

        Assert.IsNull(repository.GetGoogleCalendarLink(calendar.Id));
        Assert.AreEqual(calendar, repository.GetCalendars().Single());
    }

    private static void SaveReview(
        SqliteSessionHistoryRepository repository,
        string intent,
        DateTimeOffset startedAt,
        int distractionCount = 0)
    {
        var session = new FocusSession(new FocusIntent(intent), TimeSpan.FromMinutes(25));
        session.Start(startedAt);

        for (var index = 0; index < distractionCount; index++)
        {
            session.AddDistraction(new DistractionEntry($"Distraction {index}", startedAt.AddMinutes(index + 1)));
        }

        session.End(startedAt.AddMinutes(25));
        repository.Save(session.CreateReview(null, startedAt.AddMinutes(26)));
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string _directoryPath =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FocusAnchor.Tests", Guid.NewGuid().ToString("N"));

        public TemporaryDatabase()
        {
            Directory.CreateDirectory(_directoryPath);
            Path = System.IO.Path.Combine(_directoryPath, "focus-anchor.db");
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }
}

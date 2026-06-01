using FocusAnchor.Core;

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

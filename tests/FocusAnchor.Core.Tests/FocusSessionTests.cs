using FocusAnchor.Core;

namespace FocusAnchor.Core.Tests;

[TestClass]
public sealed class FocusSessionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_CreatesReadySession()
    {
        var intent = new FocusIntent("Write the report");

        var session = new FocusSession(intent, TimeSpan.FromMinutes(25));

        Assert.AreSame(intent, session.Intent);
        Assert.AreEqual(FocusSessionStatus.Ready, session.Status);
        Assert.IsNull(session.StartedAt);
    }

    [TestMethod]
    public void Constructor_RejectsNonPositiveDuration()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FocusSession(new FocusIntent("Write the report"), TimeSpan.Zero));
    }

    [TestMethod]
    public void Start_ActivatesReadySession()
    {
        var session = CreateSession();

        session.Start(StartedAt);

        Assert.AreEqual(FocusSessionStatus.Active, session.Status);
        Assert.AreEqual(StartedAt, session.StartedAt);
    }

    [TestMethod]
    public void Pause_AndResume_ChangeSessionStatus()
    {
        var session = CreateActiveSession();

        session.Pause(StartedAt.AddMinutes(5));
        Assert.AreEqual(FocusSessionStatus.Paused, session.Status);

        session.Resume(StartedAt.AddMinutes(8));
        Assert.AreEqual(FocusSessionStatus.Active, session.Status);
    }

    [TestMethod]
    public void Pause_RejectsReadySession()
    {
        var session = CreateSession();

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Pause(StartedAt));
    }

    [TestMethod]
    public void Resume_RejectsActiveSession()
    {
        var session = CreateActiveSession();

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Resume(StartedAt.AddMinutes(1)));
    }

    [TestMethod]
    public void End_CompletesActiveSession()
    {
        var session = CreateActiveSession();
        var endedAt = StartedAt.AddMinutes(10);

        session.End(endedAt);

        Assert.AreEqual(FocusSessionStatus.Completed, session.Status);
        Assert.AreEqual(endedAt, session.EndedAt);
    }

    [TestMethod]
    public void End_CompletesPausedSession()
    {
        var session = CreateActiveSession();
        session.Pause(StartedAt.AddMinutes(5));

        session.End(StartedAt.AddMinutes(8));

        Assert.AreEqual(FocusSessionStatus.Completed, session.Status);
    }

    [TestMethod]
    public void AddDistraction_AssociatesEntryWithActiveSession()
    {
        var session = CreateActiveSession();
        var distraction = new DistractionEntry("Check email", StartedAt.AddMinutes(3));

        session.AddDistraction(distraction);

        CollectionAssert.AreEqual(new[] { distraction }, session.Distractions.ToArray());
    }

    [TestMethod]
    public void AddDistraction_RejectsEntryWhilePaused()
    {
        var session = CreateActiveSession();
        session.Pause(StartedAt.AddMinutes(3));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.AddDistraction(new DistractionEntry("Check email", StartedAt.AddMinutes(4))));
    }

    [TestMethod]
    public void AddDistraction_RejectsEntryBeforeSessionIsStarted()
    {
        var session = CreateSession();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.AddDistraction(new DistractionEntry("Check email", StartedAt)));
    }

    [TestMethod]
    public void AddDistraction_RejectsEntryAfterSessionIsCompleted()
    {
        var session = CreateActiveSession();
        session.End(StartedAt.AddMinutes(3));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.AddDistraction(new DistractionEntry("Check email", StartedAt.AddMinutes(4))));
    }

    [TestMethod]
    public void AddDistraction_RejectsEntryBeforeSessionStarts()
    {
        var session = CreateActiveSession();
        var distraction = new DistractionEntry("Check email", StartedAt.AddSeconds(-1));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.AddDistraction(distraction));
    }

    [TestMethod]
    public void GetRemainingTime_ExcludesPausedTime()
    {
        var session = CreateActiveSession();
        session.Pause(StartedAt.AddMinutes(5));

        Assert.AreEqual(TimeSpan.FromMinutes(20), session.GetRemainingTime(StartedAt.AddMinutes(10)));

        session.Resume(StartedAt.AddMinutes(10));

        Assert.AreEqual(TimeSpan.FromMinutes(15), session.GetRemainingTime(StartedAt.AddMinutes(15)));
    }

    [TestMethod]
    public void CreateReview_CreatesReviewForCompletedSession()
    {
        var session = CreateActiveSession();
        session.End(StartedAt.AddMinutes(25));

        var review = session.CreateReview("  Stayed focused  ", StartedAt.AddMinutes(26));

        Assert.AreSame(review, session.Review);
        Assert.AreEqual("Stayed focused", review.Reflection);
    }

    [TestMethod]
    public void CreateReview_RejectsActiveSession()
    {
        var session = CreateActiveSession();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.CreateReview(null, StartedAt.AddMinutes(1)));
    }

    [TestMethod]
    public void CreateReview_RejectsSecondReview()
    {
        var session = CreateActiveSession();
        session.End(StartedAt.AddMinutes(25));
        session.CreateReview(null, StartedAt.AddMinutes(26));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.CreateReview(null, StartedAt.AddMinutes(27)));
    }

    private static FocusSession CreateActiveSession()
    {
        var session = CreateSession();
        session.Start(StartedAt);
        return session;
    }

    private static FocusSession CreateSession()
    {
        return new FocusSession(new FocusIntent("Write the report"), TimeSpan.FromMinutes(25));
    }
}

using FocusAnchor.Core;

namespace FocusAnchor.Core.Tests;

[TestClass]
public sealed class AttentionReviewTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_RejectsActiveSession()
    {
        var session = CreateSession();
        session.Start(StartedAt);

        Assert.ThrowsExactly<ArgumentException>(() => new AttentionReview(session, null, StartedAt.AddMinutes(25)));
    }

    [TestMethod]
    public void Constructor_AllowsEmptyReflection()
    {
        var session = CreateEndedSession();

        var review = new AttentionReview(session, " ", StartedAt.AddMinutes(25));

        Assert.IsNull(review.Reflection);
    }

    [TestMethod]
    public void Constructor_RejectsReviewBeforeSessionEnds()
    {
        var session = CreateEndedSession();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new AttentionReview(session, null, StartedAt.AddMinutes(24)));
    }

    private static FocusSession CreateEndedSession()
    {
        var session = CreateSession();
        session.Start(StartedAt);
        session.End(StartedAt.AddMinutes(25));
        return session;
    }

    private static FocusSession CreateSession()
    {
        return new FocusSession(new FocusIntent("Write the report"), TimeSpan.FromMinutes(25));
    }
}

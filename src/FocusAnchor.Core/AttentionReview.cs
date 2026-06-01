namespace FocusAnchor.Core;

public sealed class AttentionReview
{
    public AttentionReview(FocusSession session, string? reflection, DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status is not FocusSessionStatus.Completed)
        {
            throw new ArgumentException("Only an ended session can be reviewed.", nameof(session));
        }

        if (reviewedAt < session.EndedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(reviewedAt), "A review cannot happen before the session ends.");
        }

        Session = session;
        Reflection = string.IsNullOrWhiteSpace(reflection)
            ? null
            : reflection.Trim();
        ReviewedAt = reviewedAt;
    }

    public FocusSession Session { get; }

    public string? Reflection { get; }

    public DateTimeOffset ReviewedAt { get; }
}

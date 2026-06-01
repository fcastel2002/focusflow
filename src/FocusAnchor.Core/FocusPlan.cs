namespace FocusAnchor.Core;

public sealed record FocusPlan
{
    public FocusPlan(long id, long calendarId, string intentDescription, DateTimeOffset startsAt, TimeSpan duration)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (calendarId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(calendarId));
        }

        if (string.IsNullOrWhiteSpace(intentDescription))
        {
            throw new ArgumentException("A focus plan intention is required.", nameof(intentDescription));
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Id = id;
        CalendarId = calendarId;
        IntentDescription = intentDescription.Trim();
        StartsAt = startsAt;
        Duration = duration;
    }

    public long Id { get; }

    public long CalendarId { get; }

    public string IntentDescription { get; }

    public DateTimeOffset StartsAt { get; }

    public TimeSpan Duration { get; }
}

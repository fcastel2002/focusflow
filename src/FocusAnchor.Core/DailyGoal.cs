namespace FocusAnchor.Core;

public sealed record DailyGoal
{
    public DailyGoal(long calendarId, DateOnly date, string? description)
    {
        if (calendarId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(calendarId));
        }

        CalendarId = calendarId;
        Date = date;
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    public long CalendarId { get; }

    public DateOnly Date { get; }

    public string? Description { get; }
}

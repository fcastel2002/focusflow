namespace FocusAnchor.Core;

public sealed class DistractionEntry
{
    public DistractionEntry(string description, DateTimeOffset capturedAt)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A distraction description is required.", nameof(description));
        }

        Description = description.Trim();
        CapturedAt = capturedAt;
    }

    public string Description { get; }

    public DateTimeOffset CapturedAt { get; }
}

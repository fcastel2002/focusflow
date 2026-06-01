namespace FocusAnchor.Core;

public sealed class FocusIntent
{
    public FocusIntent(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A focus intention is required.", nameof(description));
        }

        Description = description.Trim();
    }

    public string Description { get; }
}

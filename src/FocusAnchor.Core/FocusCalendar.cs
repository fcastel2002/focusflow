namespace FocusAnchor.Core;

public sealed record FocusCalendar
{
    public FocusCalendar(long id, string name, string colorHex)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A calendar name is required.", nameof(name));
        }

        if (!IsHexColor(colorHex))
        {
            throw new ArgumentException("A calendar color must use #RRGGBB format.", nameof(colorHex));
        }

        Id = id;
        Name = name.Trim();
        ColorHex = colorHex.ToUpperInvariant();
    }

    public long Id { get; }

    public string Name { get; }

    public string ColorHex { get; }

    private static bool IsHexColor(string? colorHex)
    {
        return colorHex is { Length: 7 }
            && colorHex[0] == '#'
            && colorHex[1..].All(Uri.IsHexDigit);
    }
}

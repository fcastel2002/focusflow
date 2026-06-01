namespace FocusAnchor.Data.Google;

public sealed record GoogleOAuthToken(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt);

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FocusAnchor.Data.Google;

public sealed class GoogleOAuthClient
{
    public const string CalendarListReadonlyScope = "https://www.googleapis.com/auth/calendar.calendarlist.readonly";
    public const string CalendarEventsScope = "https://www.googleapis.com/auth/calendar.events";

    private readonly HttpClient _httpClient;
    private readonly string _clientId;

    public GoogleOAuthClient(HttpClient httpClient, string clientId)
    {
        _httpClient = httpClient;
        _clientId = string.IsNullOrWhiteSpace(clientId)
            ? throw new ArgumentException("A Google OAuth client ID is required.", nameof(clientId))
            : clientId;
    }

    public GoogleAuthorizationRequest CreateAuthorizationRequest(string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        var state = CreateUrlSafeRandomValue(32);
        var verifier = CreateUrlSafeRandomValue(64);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = $"{CalendarListReadonlyScope} {CalendarEventsScope}",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };

        return new GoogleAuthorizationRequest(
            new Uri($"https://accounts.google.com/o/oauth2/v2/auth?{BuildQueryString(query)}"),
            state,
            verifier,
            redirectUri);
    }

    public static string ValidateAuthorizationResponse(
        string expectedState,
        string? actualState,
        string? code,
        string? error)
    {
        if (!string.Equals(expectedState, actualState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Google OAuth state did not match.");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"Google OAuth failed: {error}.");
        }

        return string.IsNullOrWhiteSpace(code)
            ? throw new InvalidOperationException("Google OAuth did not return an authorization code.")
            : code;
    }

    public Task<GoogleOAuthToken> ExchangeAuthorizationCodeAsync(
        string code,
        string verifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        return RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            },
            cancellationToken);
    }

    public Task<GoogleOAuthToken> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        return RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            },
            cancellationToken);
    }

    private async Task<GoogleOAuthToken> RequestTokenAsync(
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(values),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        return new GoogleOAuthToken(
            root.GetProperty("access_token").GetString()!,
            root.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() : null,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string> values)
    {
        return string.Join("&", values.Select(value =>
            $"{Uri.EscapeDataString(value.Key)}={Uri.EscapeDataString(value.Value)}"));
    }

    private static string CreateUrlSafeRandomValue(int byteLength)
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

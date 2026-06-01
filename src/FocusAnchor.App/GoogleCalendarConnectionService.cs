using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using FocusAnchor.Data.Google;

namespace FocusAnchor.App;

public sealed class GoogleCalendarConnectionService
{
    private readonly GoogleCalendarApiClient _calendarApiClient;
    private readonly string? _clientId;
    private readonly IGoogleCredentialStore _credentialStore;
    private readonly GoogleOAuthClient? _oauthClient;
    private GoogleOAuthToken? _token;

    public GoogleCalendarConnectionService(
        HttpClient httpClient,
        IGoogleCredentialStore credentialStore,
        string? clientId)
    {
        _credentialStore = credentialStore;
        _clientId = clientId;
        _calendarApiClient = new GoogleCalendarApiClient(httpClient);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            _oauthClient = new GoogleOAuthClient(httpClient, clientId);
        }
    }

    public event EventHandler? StateChanged;

    public bool IsConfigured => _oauthClient is not null;

    public bool IsConnected => _token is not null || _credentialStore.ReadRefreshToken() is not null;

    public IReadOnlyList<GoogleRemoteCalendar> RemoteCalendars { get; private set; } = [];

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_oauthClient is null)
        {
            throw new InvalidOperationException("Configurá FOCUSANCHOR_GOOGLE_CLIENT_ID antes de vincular Google Calendar.");
        }

        var port = GetAvailablePort();
        var redirectUri = $"http://127.0.0.1:{port}/";
        var authorizationRequest = _oauthClient.CreateAuthorizationRequest(redirectUri);
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        Process.Start(new ProcessStartInfo(authorizationRequest.AuthorizationUri.ToString())
        {
            UseShellExecute = true
        });

        var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
        var query = context.Request.QueryString;
        var code = GoogleOAuthClient.ValidateAuthorizationResponse(
            authorizationRequest.State,
            query["state"],
            query["code"],
            query["error"]);
        await RespondToBrowserAsync(context.Response, cancellationToken);

        _token = await _oauthClient.ExchangeAuthorizationCodeAsync(
            code,
            authorizationRequest.CodeVerifier,
            authorizationRequest.RedirectUri,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_token.RefreshToken))
        {
            _credentialStore.SaveRefreshToken(_token.RefreshToken);
        }

        await LoadRemoteCalendarsAsync(cancellationToken);
    }

    public async Task LoadRemoteCalendarsAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        RemoteCalendars = await _calendarApiClient.GetWritableCalendarsAsync(accessToken, cancellationToken);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return GetAccessTokenCoreAsync(cancellationToken);
    }

    public void Disconnect()
    {
        _token = null;
        RemoteCalendars = [];
        _credentialStore.DeleteRefreshToken();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string> GetAccessTokenCoreAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && _token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _token.AccessToken;
        }

        if (_oauthClient is null)
        {
            throw new InvalidOperationException("Google Calendar no está configurado.");
        }

        var refreshToken = _credentialStore.ReadRefreshToken()
            ?? throw new InvalidOperationException("Vinculá Google Calendar para continuar.");
        _token = await _oauthClient.RefreshAccessTokenAsync(refreshToken, cancellationToken);
        return _token.AccessToken;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task RespondToBrowserAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        const string html = "<html><body><h2>FocusAnchor vinculado.</h2><p>Podés cerrar esta ventana.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }
}

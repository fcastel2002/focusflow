using System.Net.Http.Headers;
using System.Text.Json;

namespace FocusAnchor.Data.Google;

public sealed class GoogleCalendarApiClient
{
    private readonly HttpClient _httpClient;

    public GoogleCalendarApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<GoogleRemoteCalendar>> GetWritableCalendarsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/calendar/v3/users/me/calendarList");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Where(item => item.GetProperty("accessRole").GetString() is "writer" or "owner")
            .Select(item => new GoogleRemoteCalendar(
                item.GetProperty("id").GetString()!,
                item.GetProperty("summary").GetString()!))
            .ToArray();
    }
}

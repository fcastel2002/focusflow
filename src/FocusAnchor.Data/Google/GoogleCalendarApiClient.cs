using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusAnchor.Core;

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

    public async Task<IReadOnlyList<GoogleCalendarEvent>> GetEventsAsync(
        string accessToken,
        string calendarId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?singleEvents=true",
            accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Where(item => item.TryGetProperty("status", out var status) && status.GetString() != "cancelled")
            .Select(ParseEvent)
            .ToArray();
    }

    public Task<GoogleCalendarEvent> InsertPlanEventAsync(
        string accessToken,
        string calendarId,
        FocusPlan plan,
        CancellationToken cancellationToken = default)
    {
        return WritePlanEventAsync(HttpMethod.Post, accessToken, calendarId, null, plan, cancellationToken);
    }

    public Task<GoogleCalendarEvent> PatchPlanEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        FocusPlan plan,
        CancellationToken cancellationToken = default)
    {
        return WritePlanEventAsync(new HttpMethod("PATCH"), accessToken, calendarId, eventId, plan, cancellationToken);
    }

    public async Task DeleteEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Delete,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}",
            accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GoogleCalendarEvent> WritePlanEventAsync(
        HttpMethod method,
        string accessToken,
        string calendarId,
        string? eventId,
        FocusPlan plan,
        CancellationToken cancellationToken)
    {
        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events";

        if (eventId is not null)
        {
            url += $"/{Uri.EscapeDataString(eventId)}";
        }

        using var request = CreateRequest(method, url, accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                summary = plan.IntentDescription,
                start = new { dateTime = plan.StartsAt.ToString("O") },
                end = new { dateTime = plan.StartsAt.Add(plan.Duration).ToString("O") },
                extendedProperties = new
                {
                    @private = new Dictionary<string, string>
                    {
                        ["focusAnchorPlanId"] = plan.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }
                }
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseEvent(json.RootElement);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static GoogleCalendarEvent ParseEvent(JsonElement item)
    {
        var start = ParseEventDate(item.GetProperty("start"));
        var end = ParseEventDate(item.GetProperty("end"));
        string? focusAnchorPlanId = null;

        if (item.TryGetProperty("extendedProperties", out var extendedProperties)
            && extendedProperties.TryGetProperty("private", out var privateProperties)
            && privateProperties.TryGetProperty("focusAnchorPlanId", out var planId))
        {
            focusAnchorPlanId = planId.GetString();
        }

        return new GoogleCalendarEvent(
            item.GetProperty("id").GetString()!,
            item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "(Sin título)" : "(Sin título)",
            start,
            end,
            item.TryGetProperty("etag", out var etag) ? etag.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("updated", out var updated)
                ? DateTimeOffset.Parse(updated.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                : DateTimeOffset.MinValue,
            focusAnchorPlanId);
    }

    private static DateTimeOffset ParseEventDate(JsonElement value)
    {
        if (value.TryGetProperty("dateTime", out var dateTime))
        {
            return DateTimeOffset.Parse(dateTime.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        }

        var date = DateOnly.Parse(value.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
    }
}

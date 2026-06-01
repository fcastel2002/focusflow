using System.Net;
using System.Text;
using FocusAnchor.Data.Google;

namespace FocusAnchor.Data.Tests;

[TestClass]
public sealed class GoogleOAuthClientTests
{
    [TestMethod]
    public void CreateAuthorizationRequest_UsesPkceAndRequiredScopes()
    {
        var client = new GoogleOAuthClient(new HttpClient(new StubHandler()), "client-id");

        var request = client.CreateAuthorizationRequest("http://127.0.0.1:1234/");
        var query = Uri.UnescapeDataString(request.AuthorizationUri.Query);

        StringAssert.Contains(query, "code_challenge=");
        StringAssert.Contains(query, "code_challenge_method=S256");
        StringAssert.Contains(query, GoogleOAuthClient.CalendarListReadonlyScope);
        StringAssert.Contains(query, GoogleOAuthClient.CalendarEventsScope);
        Assert.IsNotEmpty(request.State);
        Assert.IsNotEmpty(request.CodeVerifier);
    }

    [TestMethod]
    public void ValidateAuthorizationResponse_RejectsMismatchedState()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => GoogleOAuthClient.ValidateAuthorizationResponse("expected", "different", "code", null));
    }

    [TestMethod]
    public async Task RefreshAccessTokenAsync_UsesRefreshGrant()
    {
        string? requestBody = null;
        var client = new GoogleOAuthClient(
            new HttpClient(new StubHandler(async request =>
            {
                requestBody = await request.Content!.ReadAsStringAsync();
                return JsonResponse("""{"access_token":"new-access","expires_in":3600}""");
            })),
            "client-id");

        var token = await client.RefreshAccessTokenAsync("refresh-value");

        Assert.AreEqual("new-access", token.AccessToken);
        StringAssert.Contains(requestBody, "grant_type=refresh_token");
        StringAssert.Contains(requestBody, "refresh_token=refresh-value");
    }

    [TestMethod]
    public async Task GetWritableCalendarsAsync_FiltersReadOnlyCalendars()
    {
        var client = new GoogleCalendarApiClient(new HttpClient(new StubHandler(_ =>
            Task.FromResult(JsonResponse(
                """
                {"items":[
                    {"id":"reader","summary":"Read only","accessRole":"reader"},
                    {"id":"writer","summary":"Editable","accessRole":"writer"},
                    {"id":"owner","summary":"Owned","accessRole":"owner"}
                ]}
                """)))));

        var calendars = await client.GetWritableCalendarsAsync("access");

        CollectionAssert.AreEquivalent(
            new[] { "writer", "owner" },
            calendars.Select(calendar => calendar.Id).ToArray());
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handle;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>>? handle = null)
        {
            _handle = handle ?? (_ => Task.FromResult(JsonResponse("{}")));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handle(request);
        }
    }
}

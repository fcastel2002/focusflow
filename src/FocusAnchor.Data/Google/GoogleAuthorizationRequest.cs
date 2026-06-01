namespace FocusAnchor.Data.Google;

public sealed record GoogleAuthorizationRequest(
    Uri AuthorizationUri,
    string State,
    string CodeVerifier,
    string RedirectUri);

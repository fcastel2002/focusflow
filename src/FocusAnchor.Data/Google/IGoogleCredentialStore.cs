namespace FocusAnchor.Data.Google;

public interface IGoogleCredentialStore
{
    string? ReadRefreshToken();

    void SaveRefreshToken(string refreshToken);

    void DeleteRefreshToken();
}

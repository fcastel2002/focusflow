using FocusAnchor.Core;

namespace FocusAnchor.Data;

public interface ISessionHistoryRepository
{
    void Save(AttentionReview review);

    IReadOnlyList<SessionHistoryEntry> GetRecent(int limit = 50);

    AttentionSummary GetSummary();

    string? GetPreference(string key);

    void SetPreference(string key, string value);
}

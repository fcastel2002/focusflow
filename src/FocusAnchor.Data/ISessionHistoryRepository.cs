using FocusAnchor.Core;

namespace FocusAnchor.Data;

public interface ISessionHistoryRepository
{
    void Save(AttentionReview review);

    IReadOnlyList<SessionHistoryEntry> GetRecent(int limit = 50);

    AttentionSummary GetSummary();
}

using System.Globalization;
using FocusAnchor.Core;
using Microsoft.Data.Sqlite;

namespace FocusAnchor.Data;

public sealed class SqliteSessionHistoryRepository : ISessionHistoryRepository
{
    private readonly string _connectionString;

    public SqliteSessionHistoryRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Pooling = false
        }.ToString();

        EnsureSchema();
    }

    public void Save(AttentionReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var session = review.Session;
        var startedAt = session.StartedAt
            ?? throw new ArgumentException("The reviewed session must have a start time.", nameof(review));
        var endedAt = session.EndedAt
            ?? throw new ArgumentException("The reviewed session must have an end time.", nameof(review));

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO focus_sessions (
                intent_description,
                started_at,
                ended_at,
                planned_duration_seconds,
                focused_duration_seconds,
                distraction_count,
                reflection)
            VALUES (
                $intentDescription,
                $startedAt,
                $endedAt,
                $plannedDurationSeconds,
                $focusedDurationSeconds,
                $distractionCount,
                $reflection);
            """;
        command.Parameters.AddWithValue("$intentDescription", session.Intent.Description);
        command.Parameters.AddWithValue("$startedAt", FormatDateTime(startedAt));
        command.Parameters.AddWithValue("$endedAt", FormatDateTime(endedAt));
        command.Parameters.AddWithValue("$plannedDurationSeconds", ToWholeSeconds(session.Duration));
        command.Parameters.AddWithValue("$focusedDurationSeconds", ToWholeSeconds(session.Duration - session.GetRemainingTime(endedAt)));
        command.Parameters.AddWithValue("$distractionCount", session.Distractions.Count);
        command.Parameters.AddWithValue("$reflection", (object?)review.Reflection ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SessionHistoryEntry> GetRecent(int limit = 50)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The history limit must be positive.");
        }

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                intent_description,
                started_at,
                ended_at,
                planned_duration_seconds,
                focused_duration_seconds,
                distraction_count,
                reflection
            FROM focus_sessions
            ORDER BY ended_at DESC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var entries = new List<SessionHistoryEntry>();

        while (reader.Read())
        {
            entries.Add(new SessionHistoryEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                ParseDateTime(reader.GetString(2)),
                ParseDateTime(reader.GetString(3)),
                TimeSpan.FromSeconds(reader.GetInt64(4)),
                TimeSpan.FromSeconds(reader.GetInt64(5)),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return entries;
    }

    public AttentionSummary GetSummary()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*),
                COALESCE(SUM(focused_duration_seconds), 0),
                COALESCE(SUM(distraction_count), 0)
            FROM focus_sessions;
            """;

        using var reader = command.ExecuteReader();
        reader.Read();

        return new AttentionSummary(
            reader.GetInt32(0),
            TimeSpan.FromSeconds(reader.GetInt64(1)),
            reader.GetInt32(2));
    }

    public string? GetPreference(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_preferences WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void SetPreference(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO app_preferences (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS focus_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                intent_description TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                planned_duration_seconds INTEGER NOT NULL,
                focused_duration_seconds INTEGER NOT NULL,
                distraction_count INTEGER NOT NULL,
                reflection TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS app_preferences (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static long ToWholeSeconds(TimeSpan duration)
    {
        return Math.Max(0, (long)Math.Round(duration.TotalSeconds));
    }

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseDateTime(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}

using System.Globalization;
using FocusAnchor.Core;
using Microsoft.Data.Sqlite;

namespace FocusAnchor.Data;

public sealed class SqliteSessionHistoryRepository : ISessionHistoryRepository, ICalendarRepository
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
                reflection,
                focus_plan_id)
            VALUES (
                $intentDescription,
                $startedAt,
                $endedAt,
                $plannedDurationSeconds,
                $focusedDurationSeconds,
                $distractionCount,
                $reflection,
                $focusPlanId);
            """;
        command.Parameters.AddWithValue("$intentDescription", session.Intent.Description);
        command.Parameters.AddWithValue("$startedAt", FormatDateTime(startedAt));
        command.Parameters.AddWithValue("$endedAt", FormatDateTime(endedAt));
        command.Parameters.AddWithValue("$plannedDurationSeconds", ToWholeSeconds(session.Duration));
        command.Parameters.AddWithValue("$focusedDurationSeconds", ToWholeSeconds(session.Duration - session.GetRemainingTime(endedAt)));
        command.Parameters.AddWithValue("$distractionCount", session.Distractions.Count);
        command.Parameters.AddWithValue("$reflection", (object?)review.Reflection ?? DBNull.Value);
        command.Parameters.AddWithValue("$focusPlanId", (object?)session.FocusPlanId ?? DBNull.Value);
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
                reflection,
                focus_plan_id
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
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt64(8)));
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

    public IReadOnlyList<FocusCalendar> GetCalendars()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, color_hex FROM focus_calendars ORDER BY name;";

        using var reader = command.ExecuteReader();
        var calendars = new List<FocusCalendar>();

        while (reader.Read())
        {
            calendars.Add(new FocusCalendar(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        }

        return calendars;
    }

    public FocusCalendar SaveCalendar(FocusCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        if (calendar.Id == 0)
        {
            command.CommandText =
                """
                INSERT INTO focus_calendars (name, color_hex)
                VALUES ($name, $colorHex);
                SELECT last_insert_rowid();
                """;
        }
        else
        {
            command.CommandText =
                """
                UPDATE focus_calendars
                SET name = $name, color_hex = $colorHex
                WHERE id = $id;
                SELECT $id;
                """;
            command.Parameters.AddWithValue("$id", calendar.Id);
        }

        command.Parameters.AddWithValue("$name", calendar.Name);
        command.Parameters.AddWithValue("$colorHex", calendar.ColorHex);
        var id = (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("The calendar could not be saved."));
        return new FocusCalendar(id, calendar.Name, calendar.ColorHex);
    }

    public void DeleteCalendar(long calendarId)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM focus_calendars WHERE id = $id;";
        command.Parameters.AddWithValue("$id", calendarId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<FocusPlan> GetPlans(DateOnly date)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, calendar_id, intent_description, starts_at, duration_seconds
            FROM focus_plans
            WHERE substr(starts_at, 1, 10) = $date
            ORDER BY starts_at;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));

        using var reader = command.ExecuteReader();
        var plans = new List<FocusPlan>();

        while (reader.Read())
        {
            plans.Add(new FocusPlan(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                ParseDateTime(reader.GetString(3)),
                TimeSpan.FromSeconds(reader.GetInt64(4))));
        }

        return plans;
    }

    public FocusPlan SavePlan(FocusPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        if (plan.Id == 0)
        {
            command.CommandText =
                """
                INSERT INTO focus_plans (calendar_id, intent_description, starts_at, duration_seconds)
                VALUES ($calendarId, $intentDescription, $startsAt, $durationSeconds);
                SELECT last_insert_rowid();
                """;
        }
        else
        {
            command.CommandText =
                """
                UPDATE focus_plans
                SET calendar_id = $calendarId,
                    intent_description = $intentDescription,
                    starts_at = $startsAt,
                    duration_seconds = $durationSeconds
                WHERE id = $id;
                SELECT $id;
                """;
            command.Parameters.AddWithValue("$id", plan.Id);
        }

        command.Parameters.AddWithValue("$calendarId", plan.CalendarId);
        command.Parameters.AddWithValue("$intentDescription", plan.IntentDescription);
        command.Parameters.AddWithValue("$startsAt", FormatDateTime(plan.StartsAt));
        command.Parameters.AddWithValue("$durationSeconds", ToWholeSeconds(plan.Duration));
        var id = (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("The plan could not be saved."));
        return new FocusPlan(id, plan.CalendarId, plan.IntentDescription, plan.StartsAt, plan.Duration);
    }

    public void DeletePlan(long planId)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM focus_plans WHERE id = $id;";
        command.Parameters.AddWithValue("$id", planId);
        command.ExecuteNonQuery();
    }

    public DailyGoal? GetDailyGoal(long calendarId, DateOnly date)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT description
            FROM daily_goals
            WHERE calendar_id = $calendarId AND goal_date = $goalDate;
            """;
        command.Parameters.AddWithValue("$calendarId", calendarId);
        command.Parameters.AddWithValue("$goalDate", FormatDate(date));
        var description = command.ExecuteScalar() as string;
        return description is null ? null : new DailyGoal(calendarId, date, description);
    }

    public void SetDailyGoal(DailyGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        if (goal.Description is null)
        {
            command.CommandText = "DELETE FROM daily_goals WHERE calendar_id = $calendarId AND goal_date = $goalDate;";
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO daily_goals (calendar_id, goal_date, description)
                VALUES ($calendarId, $goalDate, $description)
                ON CONFLICT(calendar_id, goal_date) DO UPDATE SET description = excluded.description;
                """;
            command.Parameters.AddWithValue("$description", goal.Description);
        }

        command.Parameters.AddWithValue("$calendarId", goal.CalendarId);
        command.Parameters.AddWithValue("$goalDate", FormatDate(goal.Date));
        command.ExecuteNonQuery();
    }

    public DailyAttentionSummary GetDailySummary(DateOnly date)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM focus_plans WHERE substr(starts_at, 1, 10) = $date),
                (SELECT COUNT(*) FROM focus_sessions WHERE substr(ended_at, 1, 10) = $date),
                (SELECT COALESCE(SUM(focused_duration_seconds), 0) FROM focus_sessions WHERE substr(ended_at, 1, 10) = $date),
                (SELECT COALESCE(SUM(distraction_count), 0) FROM focus_sessions WHERE substr(ended_at, 1, 10) = $date);
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));

        using var reader = command.ExecuteReader();
        reader.Read();

        return new DailyAttentionSummary(
            date,
            reader.GetInt32(0),
            reader.GetInt32(1),
            TimeSpan.FromSeconds(reader.GetInt64(2)),
            reader.GetInt32(3));
    }

    public GoogleCalendarLink? GetGoogleCalendarLink(long calendarId)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT google_calendar_id, google_calendar_name
            FROM calendar_google_links
            WHERE calendar_id = $calendarId;
            """;
        command.Parameters.AddWithValue("$calendarId", calendarId);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new GoogleCalendarLink(calendarId, reader.GetString(0), reader.GetString(1))
            : null;
    }

    public void SaveGoogleCalendarLink(GoogleCalendarLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO calendar_google_links (calendar_id, google_calendar_id, google_calendar_name)
            VALUES ($calendarId, $googleCalendarId, $googleCalendarName)
            ON CONFLICT(calendar_id) DO UPDATE SET
                google_calendar_id = excluded.google_calendar_id,
                google_calendar_name = excluded.google_calendar_name;
            """;
        command.Parameters.AddWithValue("$calendarId", link.CalendarId);
        command.Parameters.AddWithValue("$googleCalendarId", link.GoogleCalendarId);
        command.Parameters.AddWithValue("$googleCalendarName", link.GoogleCalendarName);
        command.ExecuteNonQuery();
    }

    public void DeleteGoogleCalendarLink(long calendarId)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM calendar_google_links WHERE calendar_id = $calendarId;";
        command.Parameters.AddWithValue("$calendarId", calendarId);
        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var connection = CreateOpenConnection();
        EnsureBaseSchema(connection);
        EnsureCalendarSchema(connection);
    }

    private static void EnsureBaseSchema(SqliteConnection connection)
    {
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

    private static void EnsureCalendarSchema(SqliteConnection connection)
    {
        if (!ColumnExists(connection, "focus_sessions", "focus_plan_id"))
        {
            using var addColumn = connection.CreateCommand();
            addColumn.CommandText = "ALTER TABLE focus_sessions ADD COLUMN focus_plan_id INTEGER NULL;";
            addColumn.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS focus_calendars (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                color_hex TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS daily_goals (
                calendar_id INTEGER NOT NULL REFERENCES focus_calendars(id) ON DELETE CASCADE,
                goal_date TEXT NOT NULL,
                description TEXT NOT NULL,
                PRIMARY KEY (calendar_id, goal_date)
            );

            CREATE TABLE IF NOT EXISTS focus_plans (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                calendar_id INTEGER NOT NULL REFERENCES focus_calendars(id) ON DELETE CASCADE,
                intent_description TEXT NOT NULL,
                starts_at TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS calendar_google_links (
                calendar_id INTEGER PRIMARY KEY REFERENCES focus_calendars(id) ON DELETE CASCADE,
                google_calendar_id TEXT NOT NULL,
                google_calendar_name TEXT NOT NULL
            );

            INSERT INTO focus_calendars (name, color_hex)
            SELECT 'Personal', '#295C4D'
            WHERE NOT EXISTS (SELECT 1 FROM focus_calendars);

            PRAGMA user_version = 2;
            """;
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (reader.GetString(1) == columnName)
            {
                return true;
            }
        }

        return false;
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
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

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseDateTime(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}

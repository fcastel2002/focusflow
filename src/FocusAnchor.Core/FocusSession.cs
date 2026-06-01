namespace FocusAnchor.Core;

public sealed class FocusSession
{
    private readonly List<DistractionEntry> _distractions = [];
    private TimeSpan _elapsedFocusTime;
    private DateTimeOffset? _activeSince;
    private DateTimeOffset? _latestEventAt;

    public FocusSession(FocusIntent intent, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "A session duration must be positive.");
        }

        Intent = intent;
        Duration = duration;
    }

    public FocusIntent Intent { get; }

    public TimeSpan Duration { get; }

    public FocusSessionStatus Status { get; private set; } = FocusSessionStatus.Ready;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public IReadOnlyList<DistractionEntry> Distractions => _distractions.AsReadOnly();

    public AttentionReview? Review { get; private set; }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status is not FocusSessionStatus.Ready)
        {
            throw new InvalidOperationException("Only a ready session can be started.");
        }

        StartedAt = startedAt;
        _activeSince = startedAt;
        _latestEventAt = startedAt;
        Status = FocusSessionStatus.Active;
    }

    public void Pause(DateTimeOffset pausedAt)
    {
        EnsureStatus(FocusSessionStatus.Active, "Only an active session can be paused.");
        EnsureChronological(pausedAt, nameof(pausedAt));

        _elapsedFocusTime += pausedAt - _activeSince!.Value;
        _activeSince = null;
        _latestEventAt = pausedAt;
        Status = FocusSessionStatus.Paused;
    }

    public void Resume(DateTimeOffset resumedAt)
    {
        EnsureStatus(FocusSessionStatus.Paused, "Only a paused session can be resumed.");
        EnsureChronological(resumedAt, nameof(resumedAt));

        _activeSince = resumedAt;
        _latestEventAt = resumedAt;
        Status = FocusSessionStatus.Active;
    }

    public void AddDistraction(DistractionEntry distraction)
    {
        ArgumentNullException.ThrowIfNull(distraction);

        EnsureStatus(FocusSessionStatus.Active, "Distractions can only be added to an active session.");
        EnsureChronological(distraction.CapturedAt, nameof(distraction));

        _distractions.Add(distraction);
        _latestEventAt = distraction.CapturedAt;
    }

    public void End(DateTimeOffset endedAt)
    {
        if (Status is not FocusSessionStatus.Active and not FocusSessionStatus.Paused)
        {
            throw new InvalidOperationException("Only an active or paused session can be ended.");
        }

        EnsureChronological(endedAt, nameof(endedAt));

        if (Status is FocusSessionStatus.Active)
        {
            _elapsedFocusTime += endedAt - _activeSince!.Value;
        }

        _activeSince = null;
        _latestEventAt = endedAt;
        EndedAt = endedAt;
        Status = FocusSessionStatus.Completed;
    }

    public AttentionReview CreateReview(string? reflection, DateTimeOffset reviewedAt)
    {
        EnsureStatus(FocusSessionStatus.Completed, "Only a completed session can be reviewed.");

        if (Review is not null)
        {
            throw new InvalidOperationException("The session already has a review.");
        }

        Review = new AttentionReview(this, reflection, reviewedAt);
        return Review;
    }

    public TimeSpan GetRemainingTime(DateTimeOffset currentTime)
    {
        if (Status is FocusSessionStatus.Ready)
        {
            return Duration;
        }

        var elapsedFocusTime = _elapsedFocusTime;

        if (Status is FocusSessionStatus.Active && currentTime > _activeSince)
        {
            elapsedFocusTime += currentTime - _activeSince.Value;
        }

        var remainingTime = Duration - elapsedFocusTime;

        return remainingTime > TimeSpan.Zero
            ? remainingTime
            : TimeSpan.Zero;
    }

    private void EnsureStatus(FocusSessionStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void EnsureChronological(DateTimeOffset eventAt, string parameterName)
    {
        if (eventAt < _latestEventAt)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Session events must be recorded chronologically.");
        }
    }
}

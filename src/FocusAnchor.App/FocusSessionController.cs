using System.Windows.Threading;
using FocusAnchor.Core;

namespace FocusAnchor.App;

public sealed class FocusSessionController
{
    private readonly DispatcherTimer _timer;

    public FocusSessionController()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += Timer_Tick;
    }

    public event EventHandler? StateChanged;

    public FocusSession? CurrentSession { get; private set; }

    public bool HasOpenSession =>
        CurrentSession is { Status: FocusSessionStatus.Active or FocusSessionStatus.Paused };

    public TimeSpan RemainingTime => CurrentSession?.GetRemainingTime(DateTimeOffset.Now) ?? TimeSpan.Zero;

    public double ProgressPercentage
    {
        get
        {
            if (CurrentSession is null)
            {
                return 0;
            }

            var elapsed = CurrentSession.Duration - RemainingTime;
            return Math.Clamp(elapsed.TotalMilliseconds / CurrentSession.Duration.TotalMilliseconds * 100, 0, 100);
        }
    }

    public void StartSession(string description, TimeSpan duration)
    {
        if (CurrentSession is not null)
        {
            throw new InvalidOperationException("Finish the current session before starting another one.");
        }

        var session = new FocusSession(new FocusIntent(description), duration);
        session.Start(DateTimeOffset.Now);
        CurrentSession = session;
        _timer.Start();
        RaiseStateChanged();
    }

    public void TogglePause()
    {
        if (CurrentSession is null)
        {
            return;
        }

        if (CurrentSession.Status is FocusSessionStatus.Active)
        {
            CurrentSession.Pause(DateTimeOffset.Now);
        }
        else if (CurrentSession.Status is FocusSessionStatus.Paused)
        {
            CurrentSession.Resume(DateTimeOffset.Now);
        }

        RaiseStateChanged();
    }

    public void EndSession()
    {
        if (!HasOpenSession)
        {
            return;
        }

        CurrentSession!.End(DateTimeOffset.Now);
        _timer.Stop();
        RaiseStateChanged();
    }

    public void AddDistraction(string description)
    {
        CurrentSession?.AddDistraction(new DistractionEntry(description, DateTimeOffset.Now));
        RaiseStateChanged();
    }

    public void CreateReview(string? reflection)
    {
        CurrentSession?.CreateReview(reflection, DateTimeOffset.Now);
        RaiseStateChanged();
    }

    public void ResetSession()
    {
        if (HasOpenSession)
        {
            throw new InvalidOperationException("An active session cannot be reset.");
        }

        CurrentSession = null;
        _timer.Stop();
        RaiseStateChanged();
    }

    public static string FormatRemainingTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (CurrentSession is { Status: FocusSessionStatus.Active }
            && RemainingTime <= TimeSpan.Zero)
        {
            EndSession();
            return;
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

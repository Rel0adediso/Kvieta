namespace Kvieta.App.ViewModels;

public sealed class FocusSessionGoal
{
    public FocusSessionGoal(int durationMinutes)
    {
        DurationSeconds = Math.Clamp(durationMinutes, 1, 24 * 60) * 60L;
    }

    public long DurationSeconds { get; }
    public long ElapsedSeconds { get; private set; }
    public bool IsCompleted { get; private set; }

    public static FocusSessionGoal Restore(long durationSeconds, long elapsedSeconds)
    {
        int durationMinutes = (int)Math.Clamp(
            Math.Ceiling(Math.Max(1, durationSeconds) / 60d),
            1,
            24 * 60);
        FocusSessionGoal goal = new(durationMinutes);
        goal.Start(elapsedSeconds);
        return goal;
    }

    public void Start(long elapsedSeconds = 0)
    {
        ElapsedSeconds = Math.Clamp(elapsedSeconds, 0, DurationSeconds);
        IsCompleted = ElapsedSeconds >= DurationSeconds;
    }

    public long Accrue(long activeSeconds)
    {
        if (activeSeconds <= 0 || IsCompleted) return 0;
        long accrued = Math.Min(activeSeconds, DurationSeconds - ElapsedSeconds);
        ElapsedSeconds += accrued;
        return accrued;
    }

    public long RemainingSeconds() => Math.Max(0, DurationSeconds - ElapsedSeconds);

    public double ProgressPercent() =>
        Math.Clamp((double)ElapsedSeconds / DurationSeconds * 100, 0, 100);

    public bool CompleteIfReached()
    {
        if (IsCompleted || RemainingSeconds() > 0)
        {
            return false;
        }

        IsCompleted = true;
        return true;
    }
}

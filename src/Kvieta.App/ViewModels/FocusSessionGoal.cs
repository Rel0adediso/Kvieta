namespace Kvieta.App.ViewModels;

public sealed class FocusSessionGoal
{
    public FocusSessionGoal(int durationMinutes)
    {
        DurationSeconds = Math.Clamp(durationMinutes, 1, 24 * 60) * 60L;
    }

    public long DurationSeconds { get; }
    public long BaselineSeconds { get; private set; }
    public bool IsCompleted { get; private set; }

    public void Start(long usedSeconds)
    {
        BaselineSeconds = Math.Max(0, usedSeconds);
        IsCompleted = false;
    }

    public long ElapsedSeconds(long usedSeconds) =>
        Math.Clamp(Math.Max(0, usedSeconds) - BaselineSeconds, 0, DurationSeconds);

    public long RemainingSeconds(long usedSeconds) =>
        Math.Max(0, DurationSeconds - ElapsedSeconds(usedSeconds));

    public double ProgressPercent(long usedSeconds) =>
        Math.Clamp((double)ElapsedSeconds(usedSeconds) / DurationSeconds * 100, 0, 100);

    public bool CompleteIfReached(long usedSeconds)
    {
        if (RemainingSeconds(usedSeconds) > 0)
        {
            return false;
        }

        IsCompleted = true;
        return true;
    }
}

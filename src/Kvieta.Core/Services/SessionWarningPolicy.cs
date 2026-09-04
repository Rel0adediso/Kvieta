namespace Kvieta.Core.Services;

public static class SessionWarningPolicy
{
    public static int? GetDueWarningMinutes(
        long remainingSeconds,
        IEnumerable<int> warningMinutes,
        IReadOnlySet<int> alreadyShown)
    {
        if (remainingSeconds <= 0) return null;

        return warningMinutes
            .Where(minutes => minutes > 0 && remainingSeconds <= minutes * 60L && !alreadyShown.Contains(minutes))
            .OrderBy(minutes => minutes)
            .Cast<int?>()
            .FirstOrDefault();
    }
}

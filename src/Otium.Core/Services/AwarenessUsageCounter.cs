using Otium.Core.Models;

namespace Otium.Core.Services;

public static class AwarenessUsageCounter
{
    public static bool Accrue(UsageLedger ledger, string applicationId, TimeSpan elapsed, DateTimeOffset? observedAt = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return false;
        }

        long seconds = (long)Math.Floor(Math.Max(0, elapsed.TotalSeconds));
        if (seconds <= 0)
        {
            return false;
        }

        string safeId = Path.GetFileName(applicationId.Trim());
        if (string.IsNullOrWhiteSpace(safeId))
        {
            return false;
        }

        ledger.AwarenessUsedSeconds += seconds;
        ledger.ForegroundAppUsedSeconds[safeId] = ledger.ForegroundAppUsedSeconds.GetValueOrDefault(safeId) + seconds;
        int localHour = (observedAt ?? DateTimeOffset.Now).LocalDateTime.Hour;
        ledger.AwarenessHourlyUsedSeconds[localHour] = ledger.AwarenessHourlyUsedSeconds.GetValueOrDefault(localHour) + seconds;
        return true;
    }
}

using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ClockIntegrityMonitor
{
    private static readonly TimeSpan BootEstimateTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WallClockTolerance = TimeSpan.FromMinutes(5);

    public static ClockChangeKind Observe(
        UsageLedger ledger,
        DateTimeOffset now,
        TimeSpan systemUptime,
        string? bootId = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        DateTimeOffset utcNow = now.ToUniversalTime();
        if (ledger.ClockAnomalyRequiresRecovery)
        {
            // A rollback cannot create extra usable time: the session stays blocked
            // until wall time catches the last trusted instant. At that point the
            // clock has safely caught up and no administrator recovery is needed.
            if (ledger.LastClockChange == ClockChangeKind.Rollback &&
                ledger.ClockRollbackUntilUtc is { } rollbackUntil &&
                utcNow >= rollbackUntil)
            {
                ClearAnomaly(ledger, now, systemUptime, bootId);
                return ClockChangeKind.None;
            }

            ledger.State = SessionState.OutsideSchedule;
            return ledger.LastClockChange;
        }
        long monotonicMilliseconds = Math.Max(0, (long)systemUptime.TotalMilliseconds);
        DateTimeOffset bootEstimate = utcNow - TimeSpan.FromMilliseconds(monotonicMilliseconds);
        int offsetMinutes = checked((int)now.Offset.TotalMinutes);

        if (ledger.LastTrustedUtc == DateTimeOffset.MinValue || ledger.EstimatedBootUtc is null ||
            ledger.LastMonotonicMilliseconds is null)
        {
            UpdateTrustedState(ledger, utcNow, bootEstimate, monotonicMilliseconds, offsetMinutes, bootId, ClockChangeKind.None);
            return ClockChangeKind.None;
        }

        bool hasBootIdentity = !string.IsNullOrWhiteSpace(bootId) && !string.IsNullOrWhiteSpace(ledger.LastBootId);
        bool sameBoot = hasBootIdentity
            ? string.Equals(bootId, ledger.LastBootId, StringComparison.Ordinal)
            : Math.Abs((bootEstimate - ledger.EstimatedBootUtc.Value).TotalSeconds) <=
                BootEstimateTolerance.TotalSeconds && monotonicMilliseconds >= ledger.LastMonotonicMilliseconds.Value;
        ClockChangeKind change;
        if (!sameBoot)
        {
            change = utcNow < ledger.LastTrustedUtc - WallClockTolerance
                ? ClockChangeKind.Rollback
                : ClockChangeKind.Reboot;
        }
        else
        {
            DateTimeOffset expectedUtc = ledger.LastTrustedUtc +
                TimeSpan.FromMilliseconds(monotonicMilliseconds - ledger.LastMonotonicMilliseconds.Value);
            TimeSpan difference = utcNow - expectedUtc;
            change = difference < -WallClockTolerance
                ? ClockChangeKind.Rollback
                : difference > WallClockTolerance
                    ? ClockChangeKind.ForwardJump
                    : ledger.LastUtcOffsetMinutes != offsetMinutes
                        ? ClockChangeKind.TimeZoneChanged
                        : ClockChangeKind.None;
        }

        if (change is ClockChangeKind.Rollback or ClockChangeKind.ForwardJump)
        {
            ledger.LastClockChange = change;
            ledger.ClockChangeDetectedAtUtc = utcNow;
            ledger.ClockAnomalyRequiresRecovery = true;
            ledger.ClockRollbackUntilUtc = ledger.LastTrustedUtc;
            ledger.State = SessionState.OutsideSchedule;
            return change;
        }

        UpdateTrustedState(ledger, utcNow, bootEstimate, monotonicMilliseconds, offsetMinutes, bootId, change);
        return change;
    }

    public static void ClearAnomaly(
        UsageLedger ledger,
        DateTimeOffset now,
        TimeSpan systemUptime,
        string? bootId = null)
    {
        ledger.ClockAnomalyRequiresRecovery = false;
        ledger.ClockRollbackUntilUtc = null;
        UpdateTrustedState(
            ledger,
            now.ToUniversalTime(),
            now.ToUniversalTime() - systemUptime,
            Math.Max(0, (long)systemUptime.TotalMilliseconds),
            checked((int)now.Offset.TotalMinutes),
            bootId,
            ClockChangeKind.None);
    }

    private static void UpdateTrustedState(
        UsageLedger ledger,
        DateTimeOffset utcNow,
        DateTimeOffset bootEstimate,
        long monotonicMilliseconds,
        int offsetMinutes,
        string? bootId,
        ClockChangeKind change)
    {
        ledger.LastTrustedUtc = utcNow;
        ledger.EstimatedBootUtc = bootEstimate;
        ledger.LastMonotonicMilliseconds = monotonicMilliseconds;
        ledger.LastBootId = bootId;
        ledger.LastUtcOffsetMinutes = offsetMinutes;
        ledger.LastClockChange = change;
        if (change is ClockChangeKind.Reboot or ClockChangeKind.TimeZoneChanged)
        {
            ledger.ClockChangeDetectedAtUtc = utcNow;
        }
    }
}

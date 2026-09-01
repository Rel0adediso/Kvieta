namespace Kvieta.App.Services;

public enum SystemInterruptionKind
{
    SessionLock,
    SessionUnlock,
    PowerSuspend,
    PowerResume
}

public sealed record SystemInterruptionState(
    bool SessionLocked = false,
    bool PowerSuspended = false,
    bool ResumeAfterPower = false);

public sealed record SystemInterruptionDecision(
    SystemInterruptionState State,
    bool ShouldPause,
    bool ShouldResume,
    bool ShouldRefreshSurfaces);

public static class SystemInterruptionPolicy
{
    public static SystemInterruptionDecision Evaluate(
        SystemInterruptionState state,
        SystemInterruptionKind kind,
        bool sessionIsActive)
    {
        return kind switch
        {
            SystemInterruptionKind.SessionLock => new(
                state with { SessionLocked = true, ResumeAfterPower = false },
                sessionIsActive,
                false,
                false),
            SystemInterruptionKind.SessionUnlock => new(
                state with { SessionLocked = false, ResumeAfterPower = false },
                false,
                false,
                true),
            SystemInterruptionKind.PowerSuspend => new(
                state with
                {
                    PowerSuspended = true,
                    ResumeAfterPower = state.ResumeAfterPower || sessionIsActive
                },
                sessionIsActive,
                false,
                false),
            SystemInterruptionKind.PowerResume => Resume(state),
            _ => new(state, false, false, false)
        };
    }

    private static SystemInterruptionDecision Resume(SystemInterruptionState state)
    {
        bool shouldResume = state.ResumeAfterPower && !state.SessionLocked;
        return new(
            state with
            {
                PowerSuspended = false,
                ResumeAfterPower = shouldResume ? false : state.ResumeAfterPower
            },
            false,
            shouldResume,
            true);
    }
}

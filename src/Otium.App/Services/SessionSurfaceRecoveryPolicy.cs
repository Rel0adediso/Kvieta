namespace Otium.App.Services;

public static class SessionSurfaceRecoveryPolicy
{
    public static bool ShouldCoverAllDisplays(
        bool shouldShowSessionSurfaces,
        bool isFullSurfaceRequired,
        bool isControlCenterOpen,
        bool keepSessionBehindControlCenter)
    {
        return shouldShowSessionSurfaces &&
            (isFullSurfaceRequired && !isControlCenterOpen ||
             isControlCenterOpen && keepSessionBehindControlCenter);
    }

    public static bool ShouldKeepVisibleBehindControlCenter(
        bool isGuardedPersonalMode,
        bool isFullSurfaceForced,
        bool isSessionActive)
    {
        return isGuardedPersonalMode || isFullSurfaceForced || !isSessionActive;
    }

    public static bool ShouldRecover(
        bool shouldShowSessionSurfaces,
        bool isSurfaceVisible,
        bool isFullSurfaceRequired,
        bool isControlCenterOpen,
        bool isModalDialogOpen,
        bool isTransitionInProgress)
    {
        return shouldShowSessionSurfaces &&
            isSurfaceVisible &&
            isFullSurfaceRequired &&
            !isControlCenterOpen &&
            !isModalDialogOpen &&
            !isTransitionInProgress;
    }
}

namespace Kvieta.App.Services;

public static class SessionSurfaceRecoveryPolicy
{
    public static bool ShouldResumeAfterControlCenterDismissal(bool isFamilyMode) =>
        !isFamilyMode;

    public static bool ShouldCoverAllDisplays(
        bool shouldShowSessionSurfaces,
        bool isFullSurfaceRequired,
        bool isControlCenterOpen)
    {
        return shouldShowSessionSurfaces &&
            isFullSurfaceRequired &&
            !isControlCenterOpen;
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

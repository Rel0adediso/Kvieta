using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Otium.App.Services;
using Otium.App.ViewModels;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App;

public partial class CafeWindow : Window
{
    private readonly CafeViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private readonly SessionShortcutGuard _shortcutGuard;
    private readonly SessionDisplayShieldManager _displayShieldManager;
    private SessionWidgetWindow? _widget;
    private bool _allowClose;
    private bool _tickInProgress;
    private bool _modalDialogOpen;
    private readonly SemaphoreSlim _systemInterruptionGate = new(1, 1);
    private SystemInterruptionState _systemInterruptionState = new();
    private readonly bool _requirePinToExit;
    private readonly AdminCredential? _exitCredentialOverride;
    private readonly bool _isDirectSession;
    private readonly bool _startHidden;
    private bool _returnToControlCenter;
    private bool _forceSurfaceVisible;
    private bool _controlCenterOpen;
    private bool _keepSessionBehindControlCenter;
    private bool _limitActionHandled;
    private bool _surfaceTransitionInProgress;
    private bool _surfaceRecoveryQueued;
    private bool _surfaceRecoveryInProgress;

    public CafeWindow(
        bool isDirectSession = false,
        bool requirePinToExit = false,
        bool returnToControlCenter = false,
        bool startHidden = false,
        AdminCredential? exitCredentialOverride = null,
        CafeViewModel? viewModel = null)
    {
        InitializeComponent();
        Title = $"Otium · {LocalizationService.Get("SessionScreen")}";
        _viewModel = viewModel ?? new CafeViewModel();
        _isDirectSession = isDirectSession;
        _startHidden = startHidden;
        _requirePinToExit = requirePinToExit;
        _exitCredentialOverride = exitCredentialOverride;
        _returnToControlCenter = returnToControlCenter;
        _shortcutGuard = new SessionShortcutGuard(ShouldRecoverSessionSurface);
        _displayShieldManager = new SessionDisplayShieldManager(this, ShouldCoverAllDisplays);
        ExitButton.Content = LocalizationService.Get(
            returnToControlCenter ? "ControlCenter" : !isDirectSession ? "ExitPreview" : requirePinToExit ? "AdminExit" : "ExitOtium");
        DataContext = _viewModel;

        if (_startHidden)
        {
            WindowState = WindowState.Normal;
            Opacity = 0;
            ShowActivated = false;
            ShowInTaskbar = false;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _viewModel.SessionStateChanged += ViewModel_SessionStateChanged;

        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        if (_isDirectSession)
        {
            await _viewModel.StartOrResumeAsync();
        }

        _timer.Start();
        EnsureCorrectSurface();
        if (_startHidden)
        {
            WindowState = WindowState.Maximized;
            Opacity = 1;
            ShowActivated = true;
            ShowInTaskbar = true;
        }
        MotionService.Enter(SessionSurface, 0, 9, 220);
        await HandleLimitReachedAsync();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_tickInProgress)
        {
            return;
        }

        _tickInProgress = true;
        try
        {
            await _viewModel.TickAsync();
            EnsureCorrectSurface();
        }
        catch (Exception exception)
        {
            _viewModel.ReportRuntimeError(exception);
            EnsureCorrectSurface();
        }
        finally
        {
            _tickInProgress = false;
        }
    }

    private async void ViewModel_SessionStateChanged(object? sender, EventArgs e)
    {
        EnsureCorrectSurface();
        await HandleLimitReachedAsync();
    }

    private async Task HandleLimitReachedAsync()
    {
        if (_viewModel.State != SessionState.TimeExpired)
        {
            _limitActionHandled = false;
            return;
        }

        if (_limitActionHandled)
        {
            return;
        }

        _limitActionHandled = true;
        SystemMediaController.StopPlayback();
        await _viewModel.SaveAsync();
        switch (_viewModel.LimitAction)
        {
            case LimitReachedAction.LockWindows:
                SystemPowerController.LockWindows();
                break;
            case LimitReachedAction.SignOut:
                SystemPowerController.SignOut();
                break;
        }
    }

    private async void StartOrResume_Click(object sender, RoutedEventArgs e)
    {
        if (_surfaceTransitionInProgress)
        {
            return;
        }

        _surfaceTransitionInProgress = true;
        try
        {
            if (await _viewModel.StartOrResumeAsync())
            {
                _forceSurfaceVisible = false;
                await ShowWidgetSurfaceAsync();
            }
        }
        finally
        {
            _surfaceTransitionInProgress = false;
            EnsureCorrectSurface();
        }
    }

    private async void EndSession_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.EndSessionAsync();
        EnsureCorrectSurface();
    }

    private async void RequestTime_Click(object sender, RoutedEventArgs e)
    {
        AdminCredential credential = _exitCredentialOverride
            ?? (await new JsonSettingsStore().LoadAsync()).AdminPin;
        if (credential.IsConfigured)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(pin => AdminPinService.Verify(pin, credential));
            verification.Owner = this;
            _modalDialogOpen = true;
            try
            {
                if (verification.ShowDialog() != true)
                {
                    return;
                }
            }
            finally
            {
                _modalDialogOpen = false;
            }
        }

        BonusTimeWindow selector = new() { Owner = this };
        _modalDialogOpen = true;
        try
        {
            if (selector.ShowDialog() == true)
            {
                await _viewModel.AddBonusMinutesAsync(selector.SelectedMinutes);
            }
        }
        finally
        {
            _modalDialogOpen = false;
            EnsureCorrectSurface();
        }
    }

    private void PowerMenu_Click(object sender, RoutedEventArgs e)
    {
        PowerOverlay.Visibility = Visibility.Visible;
        MotionService.Enter(PowerCard, 0, 8, 180);
    }

    private void ClosePowerMenu_Click(object sender, RoutedEventArgs e)
    {
        PowerOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Sleep_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        PowerOverlay.Visibility = Visibility.Collapsed;
        SystemPowerController.Sleep();
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmPowerAction(IsEnglish ? "Restart the computer?" : "Bilgisayar yeniden başlatılsın mı?"))
        {
            return;
        }

        await _viewModel.EndSessionAsync();
        _allowClose = true;
        SystemPowerController.Restart();
    }

    private async void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmPowerAction(IsEnglish
                ? "Shut down the computer? Your remaining time will continue later."
                : "Bilgisayar kapatılsın mı? Kalan süren daha sonra devam edecek."))
        {
            return;
        }

        await _viewModel.EndSessionAsync();
        _allowClose = true;
        SystemPowerController.ShutDown();
    }

    private bool ConfirmPowerAction(string message)
    {
        return System.Windows.MessageBox.Show(
            this,
            message,
            "Otium",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static bool IsEnglish => LocalizationService.CurrentLanguage == LanguagePreference.English;

    private async void PauseFromWidget(object? sender, EventArgs e)
    {
        if (_surfaceTransitionInProgress)
        {
            return;
        }

        _surfaceTransitionInProgress = true;
        try
        {
            if (await _viewModel.PauseAsync())
            {
                _forceSurfaceVisible = true;
                await ShowBreakSurfaceAsync();
            }
        }
        finally
        {
            _surfaceTransitionInProgress = false;
            EnsureCorrectSurface();
        }
    }

    private async Task ShowBreakSurfaceAsync()
    {
        Task hideWidget = _widget?.HideSmoothAsync() ?? Task.CompletedTask;
        bool wasVisible = IsVisible;
        if (!wasVisible)
        {
            Show();
        }

        WindowState = WindowState.Maximized;
        Activate();
        if (!wasVisible)
        {
            MotionService.Enter(SessionSurface, 0, 7, 210);
        }

        await hideWidget;
    }

    private async Task ShowWidgetSurfaceAsync()
    {
        if (IsVisible)
        {
            await MotionService.ExitAsync(SessionSurface, 0, -6, 145);
            Hide();
        }

        _widget ??= CreateWidget();
        _widget.ShowSmooth();
    }

    private void EnsureCorrectSurface()
    {
        // Modal dialogs must keep keyboard focus. The one-second session tick also
        // calls this method, so activating the session surface here would otherwise
        // steal focus while the administrator is typing a PIN.
        if (_modalDialogOpen)
        {
            return;
        }

        if (_surfaceTransitionInProgress)
        {
            return;
        }

        _displayShieldManager.Refresh();

        if (_controlCenterOpen)
        {
            _widget?.Hide();
            if (_keepSessionBehindControlCenter)
            {
                if (!IsVisible)
                {
                    Show();
                }
                WindowState = WindowState.Maximized;
            }
            else
            {
                Hide();
            }
            return;
        }

        if (!_viewModel.ShouldShowSessionSurfaces)
        {
            _forceSurfaceVisible = false;
            _widget?.Hide();
            Hide();
            return;
        }

        if (_viewModel.IsActive && !_forceSurfaceVisible)
        {
            _widget ??= CreateWidget();
            if (!_widget.IsVisible)
            {
                _widget.ShowSmooth();
            }

            Hide();
            return;
        }

        _widget?.Hide();
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Maximized;
        Activate();
    }

    private bool ShouldRecoverSessionSurface()
    {
        return SessionSurfaceRecoveryPolicy.ShouldRecover(
            _viewModel.ShouldShowSessionSurfaces,
            IsVisible,
            _forceSurfaceVisible || !_viewModel.IsActive,
            _controlCenterOpen,
            _modalDialogOpen,
            _surfaceTransitionInProgress);
    }

    private bool ShouldCoverAllDisplays()
    {
        return SessionSurfaceRecoveryPolicy.ShouldCoverAllDisplays(
            _viewModel.ShouldShowSessionSurfaces,
            _forceSurfaceVisible || !_viewModel.IsActive,
            _controlCenterOpen,
            _keepSessionBehindControlCenter);
    }

    private void QueueSessionSurfaceRecovery()
    {
        if (_surfaceRecoveryQueued || _surfaceRecoveryInProgress || !ShouldRecoverSessionSurface())
        {
            return;
        }

        _surfaceRecoveryQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _surfaceRecoveryQueued = false;
            RecoverSessionSurface();
        }, DispatcherPriority.ApplicationIdle);
    }

    private void RecoverSessionSurface()
    {
        if (_surfaceRecoveryInProgress || !ShouldRecoverSessionSurface())
        {
            return;
        }

        _surfaceRecoveryInProgress = true;
        try
        {
            if (!IsVisible)
            {
                Show();
            }

            ShowInTaskbar = true;
            WindowState = WindowState.Maximized;
            Topmost = true;
            Activate();
            Focus();
        }
        finally
        {
            _surfaceRecoveryInProgress = false;
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            QueueSessionSurfaceRecovery();
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        QueueSessionSurfaceRecovery();
    }

    public void ShowSessionSurface()
    {
        _controlCenterOpen = false;
        if (!_viewModel.ShouldShowSessionSurfaces)
        {
            Hide();
            return;
        }

        _forceSurfaceVisible = true;
        _displayShieldManager.Refresh();
        _ = _widget?.HideSmoothAsync();
        bool wasVisible = IsVisible;
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Maximized;
        Activate();
        if (!wasVisible)
        {
            MotionService.Enter(SessionSurface, 0, 7, 210);
        }
    }

    public void EnableControlCenterReturn()
    {
        _returnToControlCenter = true;
        ExitButton.Content = LocalizationService.Get("ControlCenter");
        SuspendForControlCenter();
    }

    public void ResumeFromControlCenter()
    {
        _controlCenterOpen = false;
        _keepSessionBehindControlCenter = false;
        EnsureCorrectSurface();
    }

    public async Task CloseFromControllerAsync()
    {
        await _viewModel.SaveAsync();
        _allowClose = true;
        Close();
    }

    public async Task ReloadSettingsAsync()
    {
        await _viewModel.ReloadSettingsAsync();
        EnsureCorrectSurface();
    }

    public async Task ReloadUsageAfterClearAsync()
    {
        await _viewModel.ReloadUsageAfterClearAsync();
        EnsureCorrectSurface();
    }

    private SessionWidgetWindow CreateWidget()
    {
        SessionWidgetWindow widget = new(_viewModel);
        widget.PauseRequested += PauseFromWidget;
        return widget;
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        SystemInterruptionKind? kind = e.Reason switch
        {
            SessionSwitchReason.SessionLock => SystemInterruptionKind.SessionLock,
            SessionSwitchReason.SessionUnlock => SystemInterruptionKind.SessionUnlock,
            _ => null
        };
        if (kind is not null)
        {
            Dispatcher.InvokeAsync(() => HandleSystemInterruptionAsync(kind.Value));
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        SystemInterruptionKind? kind = e.Mode switch
        {
            PowerModes.Suspend => SystemInterruptionKind.PowerSuspend,
            PowerModes.Resume => SystemInterruptionKind.PowerResume,
            _ => null
        };
        if (kind is not null)
        {
            Dispatcher.InvokeAsync(() => HandleSystemInterruptionAsync(kind.Value));
        }
    }

    private async Task HandleSystemInterruptionAsync(SystemInterruptionKind kind)
    {
        await _systemInterruptionGate.WaitAsync();
        try
        {
            SystemInterruptionDecision decision = SystemInterruptionPolicy.Evaluate(
                _systemInterruptionState,
                kind,
                _viewModel.IsActive);
            _systemInterruptionState = decision.State;

            bool paused = decision.ShouldPause && _viewModel.PauseForSystemInterruption();
            if (paused)
            {
                await _viewModel.SaveAsync();
            }

            bool resumed = decision.ShouldResume && !_controlCenterOpen &&
                await _viewModel.StartOrResumeAsync();
            if (decision.ShouldRefreshSurfaces || resumed)
            {
                EnsureCorrectSurface();
                _displayShieldManager.Refresh();
            }

            string auditEvent = kind switch
            {
                SystemInterruptionKind.SessionLock => "lifecycle.session.lock",
                SystemInterruptionKind.SessionUnlock => "lifecycle.session.unlock",
                SystemInterruptionKind.PowerSuspend => "lifecycle.power.suspend",
                _ => "lifecycle.power.resume"
            };
            await AppendLifecycleAuditAsync(
                auditEvent,
                resumed ? "resumed" : paused ? "paused" : "observed");
        }
        finally
        {
            _systemInterruptionGate.Release();
        }
    }

    private static async Task AppendLifecycleAuditAsync(string auditEvent, string outcome)
    {
        try
        {
            string auditPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Otium",
                "lifecycle-audit.jsonl");
            await new SecurityAuditLog(auditPath).AppendAsync(auditEvent, outcome);
        }
        catch
        {
            // Lifecycle recovery must never depend on optional diagnostic logging.
        }
    }

    private async void ExitPrototype_Click(object sender, RoutedEventArgs e)
    {
        if (_returnToControlCenter)
        {
            if (_controlCenterOpen)
            {
                ControlCenterRequested?.Invoke(this, new ControlCenterRequestEventArgs());
                return;
            }

            SuspendForControlCenter();
            ControlCenterRequested?.Invoke(this, new ControlCenterRequestEventArgs());
            return;
        }

        if (_requirePinToExit)
        {
            AdminCredential credential = _exitCredentialOverride
                ?? (await new JsonSettingsStore().LoadAsync()).AdminPin;
            if (credential.IsConfigured)
            {
                AdminPinWindow verification = AdminPinWindow.CreateVerification(
                    pin => AdminPinService.Verify(pin, credential));
                verification.Owner = this;
                bool verified;
                _modalDialogOpen = true;
                try
                {
                    verified = verification.ShowDialog() == true;
                }
                finally
                {
                    _modalDialogOpen = false;
                    EnsureCorrectSurface();
                }

                if (!verified)
                {
                    return;
                }

                SuspendForControlCenter();
                ControlCenterRequested?.Invoke(
                    this,
                    new ControlCenterRequestEventArgs(verification.ResultPin));
                return;
            }
        }

        if (_viewModel.IsActive)
        {
            await _viewModel.PauseAsync();
        }

        await _viewModel.SaveAsync();
        _allowClose = true;
        Close();
    }

    public event EventHandler<ControlCenterRequestEventArgs>? ControlCenterRequested;

    public bool KeepsSessionBehindControlCenter => _keepSessionBehindControlCenter;

    private void SuspendForControlCenter()
    {
        _keepSessionBehindControlCenter = SessionSurfaceRecoveryPolicy.ShouldKeepVisibleBehindControlCenter(
            _viewModel.IsGuardedPersonalMode,
            _forceSurfaceVisible,
            _viewModel.IsActive);
        _controlCenterOpen = true;
        _forceSurfaceVisible = _keepSessionBehindControlCenter;
        _widget?.Hide();
        if (_keepSessionBehindControlCenter)
        {
            if (!IsVisible)
            {
                Show();
            }
            WindowState = WindowState.Maximized;
        }
        else
        {
            Hide();
        }
        _displayShieldManager.Refresh();
    }

    public async Task SwitchToUserSettingsStoreAsync()
    {
        await _viewModel.SwitchToUserSettingsStoreAsync();
        EnsureCorrectSurface();
    }

#if OTIUM_DEVELOPMENT_BUILD
    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
#else
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
#endif
    {
#if OTIUM_DEVELOPMENT_BUILD
        if (e.Key != Key.F12 ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        await _viewModel.ForceUnlockForTestingAsync();
        _forceSurfaceVisible = false;
        EnsureCorrectSurface();
        ControlCenterRequested?.Invoke(this, new ControlCenterRequestEventArgs());
#endif
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            QueueSessionSurfaceRecovery();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _shortcutGuard.Dispose();
        _displayShieldManager.Dispose();
        _viewModel.SessionStateChanged -= ViewModel_SessionStateChanged;
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        if (_widget is not null)
        {
            _widget.PauseRequested -= PauseFromWidget;
            _widget.CloseFromController();
        }

        Owner?.Show();
        Owner?.Activate();
    }
}

public sealed class ControlCenterRequestEventArgs(string? verifiedPin = null) : EventArgs
{
    public string? VerifiedPin { get; } = verifiedPin;
}

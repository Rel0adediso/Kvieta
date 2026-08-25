using System.ComponentModel;
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
    private SessionWidgetWindow? _widget;
    private bool _allowClose;
    private bool _tickInProgress;
    private bool _modalDialogOpen;
    private bool _sessionLocked;
    private bool _powerSuspended;
    private bool _resumeAfterSystemInterruption;
    private readonly bool _requirePinToExit;
    private readonly AdminCredential? _exitCredentialOverride;
    private readonly bool _isDirectSession;
    private readonly bool _startHidden;
    private bool _returnToControlCenter;
    private bool _forceSurfaceVisible;
    private bool _controlCenterOpen;
    private bool _limitActionHandled;
    private bool _surfaceTransitionInProgress;

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

        if (_controlCenterOpen)
        {
            _widget?.Hide();
            if (_viewModel.IsGuardedPersonalMode)
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

    public void ShowSessionSurface()
    {
        _controlCenterOpen = false;
        if (!_viewModel.ShouldShowSessionSurfaces)
        {
            Hide();
            return;
        }

        _forceSurfaceVisible = true;
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
        Dispatcher.InvokeAsync(async () =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _sessionLocked = true;
                _resumeAfterSystemInterruption = false;
                bool paused = _viewModel.PauseForSystemInterruption();
                if (paused)
                {
                    await _viewModel.SaveAsync();
                }
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _sessionLocked = false;
                EnsureCorrectSurface();
            }
        });
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            void SuspendImmediately()
            {
                _powerSuspended = true;
                _resumeAfterSystemInterruption |= _viewModel.PauseForSystemInterruption();
                _ = _viewModel.SaveAsync();
            }

            if (Dispatcher.CheckAccess())
            {
                SuspendImmediately();
            }
            else
            {
                Dispatcher.Invoke(SuspendImmediately);
            }

            return;
        }

        Dispatcher.InvokeAsync(async () =>
        {
            if (e.Mode == PowerModes.Resume)
            {
                _powerSuspended = false;
                await ResumeAfterSystemInterruptionAsync();
            }
        });
    }

    private async Task ResumeAfterSystemInterruptionAsync()
    {
        if (!_resumeAfterSystemInterruption || _sessionLocked || _powerSuspended)
        {
            return;
        }

        _resumeAfterSystemInterruption = false;
        await _viewModel.StartOrResumeAsync();
        EnsureCorrectSurface();
    }

    private async void ExitPrototype_Click(object sender, RoutedEventArgs e)
    {
        if (_returnToControlCenter)
        {
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

    public bool KeepsSessionBehindControlCenter => _viewModel.IsGuardedPersonalMode;

    private void SuspendForControlCenter()
    {
        _controlCenterOpen = true;
        _forceSurfaceVisible = _viewModel.IsGuardedPersonalMode;
        _widget?.Hide();
        if (_viewModel.IsGuardedPersonalMode)
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
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
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

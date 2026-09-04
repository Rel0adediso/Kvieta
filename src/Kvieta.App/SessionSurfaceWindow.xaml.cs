using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Kvieta.App.Services;
using Kvieta.App.ViewModels;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App;

public partial class SessionSurfaceWindow : Window
{
    private readonly SessionViewModel _viewModel;
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
    private AdminCredential? _exitCredentialOverride;
    private readonly bool _isDirectSession;
    private readonly bool _startHidden;
    private bool _returnToControlCenter;
    private bool _forceSurfaceVisible;
    private bool _controlCenterOpen;
    private bool _limitActionHandled;
    private bool _surfaceTransitionInProgress;
    private bool _surfaceRecoveryQueued;
    private bool _surfaceRecoveryInProgress;

    public SessionSurfaceWindow(
        bool isDirectSession = false,
        bool requirePinToExit = false,
        bool returnToControlCenter = false,
        bool startHidden = false,
        AdminCredential? exitCredentialOverride = null,
        SessionViewModel? viewModel = null)
    {
        InitializeComponent();
        Title = $"Kvieta · {(LocalizationService.CurrentLanguage == LanguagePreference.English ? "Focus session" : "Odak oturumu")}";
        _viewModel = viewModel ?? new SessionViewModel();
        _isDirectSession = isDirectSession;
        _startHidden = startHidden;
        _requirePinToExit = requirePinToExit;
        _exitCredentialOverride = exitCredentialOverride;
        // A PIN-protected exit is a transition to management, not a request to
        // tear down the Guardian service that owns this session.
        _returnToControlCenter = returnToControlCenter || requirePinToExit;
        _shortcutGuard = new SessionShortcutGuard(ShouldRecoverSessionSurface);
        _displayShieldManager = new SessionDisplayShieldManager(this, ShouldCoverAllDisplays);
        ExitButton.Content = LocalizationService.Get(
            requirePinToExit ? "AdminExit" : returnToControlCenter ? "ControlCenter" : !isDirectSession ? "ExitPreview" : "ExitKvieta");
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
            await HandleLimitReachedAsync();
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

        if (ProtectionPolicyChannel.IsAdministrativeActivityActive())
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
        AdminCredential credential = await LoadExitCredentialAsync();
        if (!credential.IsConfigured && _requirePinToExit)
        {
            ShowMissingAdministratorCredential();
            return;
        }

        if (credential.IsConfigured)
        {
            AdminPinWindow verification = PrepareSessionModal(AdminPinWindow.CreateVerification(
                pin => VerifyExitPinAsync(pin, credential),
                RecoverExitPinAsync));
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

        BonusTimeWindow selector = PrepareSessionModal(new BonusTimeWindow());
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
        PowerOverlay.Visibility = Visibility.Collapsed;
        await RunBeforePowerActionAsync(_viewModel.SaveAsync);
        TryPowerAction(SystemPowerController.Sleep);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmPowerAction(IsEnglish ? "Restart the computer?" : "Bilgisayar yeniden başlatılsın mı?"))
        {
            return;
        }

        PowerOverlay.Visibility = Visibility.Collapsed;
        await RunBeforePowerActionAsync(_viewModel.EndSessionAsync);
        if (TryPowerAction(SystemPowerController.Restart))
        {
            _allowClose = true;
        }
    }

    private async void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmPowerAction(IsEnglish
                ? "Shut down the computer? Your remaining time will continue later."
                : "Bilgisayar kapatılsın mı? Kalan süren daha sonra devam edecek."))
        {
            return;
        }

        PowerOverlay.Visibility = Visibility.Collapsed;
        await RunBeforePowerActionAsync(_viewModel.EndSessionAsync);
        if (TryPowerAction(SystemPowerController.ShutDown))
        {
            _allowClose = true;
        }
    }

    private async Task RunBeforePowerActionAsync(Func<Task> saveAction)
    {
        try
        {
            await saveAction().WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
        }
        catch (Exception exception)
        {
            _viewModel.ReportRuntimeError(exception);
        }
    }

    private bool TryPowerAction(Func<bool> powerAction)
    {
        try
        {
            if (powerAction())
            {
                return true;
            }

            ShowPowerActionFailure();
            return false;
        }
        catch (Exception exception)
        {
            ShowPowerActionFailure(exception.Message);
            return false;
        }
    }

    private void ShowPowerActionFailure(string? details = null)
    {
        PowerOverlay.Visibility = Visibility.Collapsed;
        System.Windows.MessageBox.Show(
            this,
            IsEnglish
                ? $"Windows could not complete this action.{Environment.NewLine}{details}"
                : $"Windows bu işlemi tamamlayamadı.{Environment.NewLine}{details}",
            IsEnglish ? "Kvieta · Action unavailable" : "Kvieta · İşlem kullanılamıyor",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        EnsureCorrectSurface();
    }

    private bool ConfirmPowerAction(string message)
    {
        _modalDialogOpen = true;
        bool confirmed = false;
        try
        {
            confirmed = System.Windows.MessageBox.Show(
                this,
                message,
                "Kvieta",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
            return confirmed;
        }
        finally
        {
            _modalDialogOpen = false;
            if (!confirmed)
            {
                EnsureCorrectSurface();
            }
        }
    }

    private async void TrustCurrentClock_Click(object sender, RoutedEventArgs e)
    {
        AdminCredential credential = await LoadExitCredentialAsync();
        if (!credential.IsConfigured)
        {
            ShowMissingAdministratorCredential();
            return;
        }

        AdminPinWindow verification = PrepareSessionModal(AdminPinWindow.CreateVerification(
            pin => VerifyExitPinAsync(pin, credential),
            RecoverExitPinAsync,
            LocalizationService.Get("TrustCurrentClockTitle"),
            LocalizationService.Get("TrustCurrentClockRequirement")));
        _modalDialogOpen = true;
        try
        {
            if (verification.ShowDialog() != true)
            {
                return;
            }

            if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.clock-anomaly.clear"))
            {
                return;
            }

            await _viewModel.ClearClockAnomalyAsync();
            string auditPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kvieta",
                "security-audit.jsonl");
            await new SecurityAuditLog(auditPath).AppendAsync("clock.anomaly", "admin-cleared");
            EnsureCorrectSurface();
        }
        finally
        {
            _modalDialogOpen = false;
        }
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

        if (ProtectionPolicyChannel.IsAdministrativeActivityActive())
        {
            _widget?.Hide();
            Hide();
            return;
        }

        _displayShieldManager.Refresh();

        if (_controlCenterOpen)
        {
            _widget?.Hide();
            Hide();
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
            _controlCenterOpen);
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
        _viewModel.ResumeUsageAfterAdministration();
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
        ExitButton.Content = LocalizationService.Get(
            _requirePinToExit ? "AdminExit" : "ControlCenter");
        if (!_controlCenterOpen)
        {
            SuspendForControlCenter();
        }
    }

    public void ResumeFromControlCenter()
    {
        _controlCenterOpen = false;
        _viewModel.ResumeUsageAfterAdministration();
        ExitButton.Content = LocalizationService.Get(
            _requirePinToExit ? "AdminExit" : _returnToControlCenter ? "ControlCenter" : "ExitKvieta");
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
                "Kvieta",
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
        try
        {
            string? verifiedPin = null;
            if (_requirePinToExit)
            {
                AdminCredential credential = await LoadExitCredentialAsync();
                if (!credential.IsConfigured)
                {
                    ShowMissingAdministratorCredential();
                    return;
                }

                AdminPinWindow verification = PrepareSessionModal(AdminPinWindow.CreateVerification(
                    pin => VerifyExitPinAsync(pin, credential),
                    RecoverExitPinAsync,
                    LocalizationService.CurrentLanguage == LanguagePreference.English
                        ? "Open Control Center"
                        : "Kontrol Merkezi'ni aç",
                    LocalizationService.CurrentLanguage == LanguagePreference.English
                        ? "Enter the administrator PIN to leave the session screen and manage Kvieta. Guardian protection will remain active."
                        : "Oturum ekranından çıkıp Kvieta'yı yönetmek için yönetici PIN'ini gir. Guardian koruması açık kalacak."));
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

                verifiedPin = verification.ResultPin;
            }

            if (_returnToControlCenter)
            {
                if (_controlCenterOpen)
                {
                    ControlCenterRequested?.Invoke(
                        this,
                        new ControlCenterRequestEventArgs(verifiedPin, _requirePinToExit));
                    return;
                }

                SuspendForControlCenter();
                ControlCenterRequested?.Invoke(
                    this,
                    new ControlCenterRequestEventArgs(verifiedPin, _requirePinToExit));
                return;
            }

            if (_viewModel.IsActive)
            {
                await _viewModel.PauseAsync();
            }

            await _viewModel.SaveAsync();
            _allowClose = true;
            Close();
        }
        catch (Exception exception)
        {
            _modalDialogOpen = true;
            try
            {
                System.Windows.MessageBox.Show(
                    this,
                    LocalizationService.CurrentLanguage == LanguagePreference.English
                        ? $"An error occurred during exit: {exception.Message}"
                        : $"Çıkış sırasında bir hata oluştu: {exception.Message}",
                    "Kvieta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _modalDialogOpen = false;
                EnsureCorrectSurface();
            }
        }
    }

    public event EventHandler<ControlCenterRequestEventArgs>? ControlCenterRequested;

    private T PrepareSessionModal<T>(T dialog) where T : Window
    {
        dialog.Owner = this;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.ShowInTaskbar = false;
        // The protected session surface is itself topmost. An owned WPF window
        // does not reliably join that z-order group on every Windows build, so a
        // focused PIN dialog can otherwise exist invisibly behind its owner.
        dialog.Topmost = Topmost;
        return dialog;
    }

    private void SuspendForControlCenter()
    {
        _viewModel.SuspendUsageForAdministration();
        _controlCenterOpen = true;
        _forceSurfaceVisible = false;
        _widget?.Hide();
        Hide();
        _displayShieldManager.Refresh();
    }

    public async Task SwitchToUserSettingsStoreAsync()
    {
        await _viewModel.SwitchToUserSettingsStoreAsync();
        EnsureCorrectSurface();
    }

#if KVIETA_DEVELOPMENT_BUILD
    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
#else
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
#endif
    {
#if KVIETA_DEVELOPMENT_BUILD
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

    private async Task<string?> RecoverExitPinAsync(Window owner)
    {
        string? newPin = await ((App)System.Windows.Application.Current)
            .RunPinRecoveryForCurrentPolicyAsync(owner);
        if (string.IsNullOrWhiteSpace(newPin))
        {
            return null;
        }

        _exitCredentialOverride = AdminPinService.Create(newPin);
        return newPin;
    }

    private Task<bool> VerifyExitPinAsync(string pin, AdminCredential credential) =>
        _requirePinToExit && ProtectionServiceManager.GetState() == ProtectionServiceState.Running
            ? ProtectionPolicyChannel.VerifyPinAsync(pin)
            : Task.FromResult(AdminPinService.Verify(pin, credential));

    private async Task<AdminCredential> LoadExitCredentialAsync()
    {
        if (_exitCredentialOverride?.IsConfigured == true)
        {
            return _exitCredentialOverride;
        }

        if (File.Exists(ProtectionServiceManager.ProtectedSettingsPath))
        {
            try
            {
                ControlSettings protectedSettings = await new JsonSettingsStore(
                    ProtectionServiceManager.ProtectedSettingsPath,
                    readOnly: true).LoadAsync();
                if (protectedSettings.AdminPin.IsConfigured)
                {
                    return protectedSettings.AdminPin;
                }
            }
            catch
            {
                // Fall through to the user copy; protected exit still fails closed
                // if neither authoritative source contains a usable credential.
            }
        }

        try
        {
            return (await new JsonSettingsStore().LoadAsync()).AdminPin;
        }
        catch
        {
            return new AdminCredential();
        }
    }

    private void ShowMissingAdministratorCredential()
    {
        _modalDialogOpen = true;
        try
        {
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.Get("AdminCredentialUnavailableDescription"),
                LocalizationService.Get("AdminCredentialUnavailableTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _modalDialogOpen = false;
            EnsureCorrectSurface();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _shortcutGuard.Dispose();
        _displayShieldManager.Dispose();
        _viewModel.Dispose();
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

public sealed class ControlCenterRequestEventArgs(
    string? verifiedPin = null,
    bool administratorVerified = false) : EventArgs
{
    public string? VerifiedPin { get; } = verifiedPin;
    public bool AdministratorVerified { get; } = administratorVerified;
}

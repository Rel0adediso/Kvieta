using System.Windows;
using System.IO;
using System.Text;
using Kvieta.App.Services;
using Kvieta.App.ViewModels;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App;

public partial class App : System.Windows.Application
{
    private readonly SystemThemeService _themeService = new();
    private SingleInstanceCoordinator? _singleInstance;
    private SingleInstanceCoordinator? _controlCenterWindowInstance;
    private MainWindow? _activatedControlCenter;
    private bool _controlCenterActivationInProgress;
    private AdminCredential? _guardianCredential;

    public SystemThemeService ThemeService => _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(argument => string.Equals(argument, "--uninstall-worker", StringComparison.OrdinalIgnoreCase)))
        {
            StartUninstallWorker(e.Args);
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--windows-admin-verification", StringComparison.OrdinalIgnoreCase)))
        {
            await HandleWindowsAdministratorVerificationAsync(e.Args);
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            _themeService.Start(this);
            await HandleUninstallRequestAsync();
            Shutdown();
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--guardian-service", StringComparison.OrdinalIgnoreCase)))
        {
            KvietaGuardianService.RunService();
            Shutdown();
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--install-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            string? payload = e.Args.SkipWhile(argument => !string.Equals(argument, "--install-guardian", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
            Shutdown(ProtectionServiceManager.ExecuteInstallerCommand(install: true, payload));
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--provision-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            string? payload = e.Args.SkipWhile(argument => !string.Equals(argument, "--provision-guardian", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
            Shutdown(ProtectionServiceManager.ExecuteProvisioningCommand(payload));
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--remove-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(ProtectionServiceManager.ExecuteInstallerCommand(install: false));
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--stop-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(ProtectionServiceManager.ExecuteAuthorizedStopCommand());
            return;
        }

        bool guardianSession = e.Args.Any(argument =>
            string.Equals(argument, "--guardian-session", StringComparison.OrdinalIgnoreCase));
        bool directSessionRequested = e.Args.Any(argument =>
            string.Equals(argument, "--session", StringComparison.OrdinalIgnoreCase));
        bool postInstallControlCenter = e.Args.Any(argument =>
            string.Equals(argument, "--post-install-control-center", StringComparison.OrdinalIgnoreCase)) &&
            File.Exists(ProtectionServiceManager.PostInstallControlCenterPath);
        bool pairManagerDeviceAfterInstall = postInstallControlCenter && e.Args.Any(argument =>
            string.Equals(argument, "--pair-manager-device", StringComparison.OrdinalIgnoreCase));
        _singleInstance = new SingleInstanceCoordinator(
            guardianSession || directSessionRequested ? "GuardianSession" : "ControlCenter");
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalPrimary();
            Shutdown();
            return;
        }

        _singleInstance.ActivationRequested += SingleInstance_ActivationRequested;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _themeService.Start(this);

        bool protectedPolicyAvailable =
            ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
            File.Exists(ProtectionServiceManager.ProtectedSettingsPath);
        JsonSettingsStore settingsStore = guardianSession && File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
            ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
            : protectedPolicyAvailable && File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
                ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
                : new JsonSettingsStore();
        ControlSettings settings;
        try
        {
            settings = await settingsStore.LoadAsync();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Kvieta ayarları ve son sağlam kopya okunamadı. Güvenlik için başlangıç durduruldu.\n\nKvieta settings and the last-known-good copy could not be read. Startup was stopped for safety.\n\n{exception.Message}",
                "Kvieta · Recovery required",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        LocalizationService.SetLanguage(this, settings.Language);
        _themeService.SetPreference(settings.Theme);

        if (!guardianSession && protectedPolicyAvailable && settings.Mode != UsageMode.Family)
        {
            await RestoreProtectedSettingsToUserProfileAsync();
        }

        if (protectedPolicyAvailable &&
            ProtectionServiceManager.GetVersionCompatibility() == ProtectionVersionCompatibility.Mismatch)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.Get("GuardianVersionMismatchDescription"),
                LocalizationService.Get("GuardianVersionMismatchTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (guardianSession)
        {
            if (!settings.AdminPin.IsConfigured)
            {
                Shutdown();
                return;
            }

            if (!settings.RequiresGuardian)
            {
                Shutdown();
                return;
            }
            settings.SetupCompleted = true;
            _guardianCredential = settings.AdminPin;
        }

        if (!settings.SetupCompleted)
        {
            ModeSelectionWindow modeWindow = new();
            if (modeWindow.ShowDialog() != true || modeWindow.SelectedMode is null)
            {
                Shutdown();
                return;
            }

            settings.Mode = modeWindow.SelectedMode.Value;
            settings.PersonalProtectionLevel = modeWindow.SelectedPersonalProtectionLevel;
            settings.StrictPersonalMode = settings.PersonalProtectionLevel != PersonalProtectionLevel.Flexible;
            if (settings.Mode == UsageMode.Family)
            {
                AdminPinWindow setup = AdminPinWindow.CreateSetup();
                setup.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                setup.ShowInTaskbar = true;
                if (setup.ShowDialog() != true || setup.ResultPin is null)
                {
                    Shutdown();
                    return;
                }

                settings.AdminPin = AdminPinService.Create(setup.ResultPin);
            }
            else if (settings.RequiresGuardian)
            {
                settings.AdminPin = AdminPinService.CreateInternalCredential();
            }

            settings.SchemaVersion = 9;
            settings.SetupCompleted = true;
            await settingsStore.SaveAsync(settings);
        }

        if (!guardianSession && settings.RequiresGuardian &&
            ProtectionServiceManager.GetState() != ProtectionServiceState.Running)
        {
            bool guardianReady = await ProtectionServiceManager.RunElevatedInstallerAsync(install: true) &&
                ProtectionServiceManager.GetState() == ProtectionServiceState.Running;
            if (!guardianReady)
            {
                System.Windows.MessageBox.Show(
                    settings.Language == LanguagePreference.English
                        ? "Protected mode cannot start without the Kvieta Guardian service. Installation was cancelled or failed."
                        : "Kvieta Guardian servisi olmadan Korumalı mod başlatılamaz. Kurulum iptal edildi veya başarısız oldu.",
                    settings.Language == LanguagePreference.English
                        ? "Kvieta · Protection required"
                        : "Kvieta · Koruma gerekli",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            protectedPolicyAvailable = true;
        }

        try
        {
            StartupRegistrationService.Apply(settings.StartWithWindows);
        }
        catch
        {
            // The control center will surface registry errors on the next explicit save.
        }

        if (guardianSession || directSessionRequested)
        {
            if (settings.RequiresGuardian && !settings.AdminPin.IsConfigured)
            {
                System.Windows.MessageBox.Show(
                    settings.Language == LanguagePreference.English
                        ? "Create an administrator PIN in the control center before using protected session mode."
                        : "Doğrudan oturum modunu kullanmadan önce yönetim panelinden bir yönetici PIN'i oluştur.",
                    settings.Language == LanguagePreference.English ? "Kvieta · PIN required" : "Kvieta · PIN gerekli",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            SessionSurfaceWindow sessionWindow = new(
                    isDirectSession: true,
                requirePinToExit: settings.Mode == UsageMode.Family,
                    returnToControlCenter: settings.RequiresGuardian,
                    exitCredentialOverride: _guardianCredential,
                viewModel: guardianSession ? new SessionViewModel(settingsStore) : null);
            sessionWindow.ControlCenterRequested += DirectSession_ControlCenterRequested;
            MainWindow = sessionWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            sessionWindow.Show();
            return;
        }

        if (!TryClaimControlCenterWindow())
        {
            Shutdown();
            return;
        }

        if (settings.Mode == UsageMode.Family &&
            settings.AdminPin.IsConfigured &&
            !postInstallControlCenter)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                pin => VerifyAdminPinAsync(pin, settings),
                owner => RunRecoveryPinResetAsync(owner, settingsStore, settings, protectedPolicyAvailable),
                settings.Language == LanguagePreference.English
                    ? "Control Center is locked"
                    : "Kontrol Merkezi kilitli",
                settings.Language == LanguagePreference.English
                    ? "Enter the administrator PIN you created during setup to view or change protected Kvieta settings."
                    : "Korumalı Kvieta ayarlarını görüntülemek veya değiştirmek için kurulumda oluşturduğun yönetici PIN'ini gir.");
            verification.ShowInTaskbar = true;
            verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (verification.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            _guardianCredential = settings.AdminPin;
            string? verifiedPin = verification.ResultPin;
            await RestoreProtectedSettingsToUserProfileAsync();
            MainWindow protectedManagementWindow = new(managementPin: verifiedPin);
            MainWindow = protectedManagementWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            protectedManagementWindow.Show();
            return;
        }

        if (postInstallControlCenter &&
            settings.Mode == UsageMode.Family &&
            settings.AdminPin.IsConfigured)
        {
            _guardianCredential = settings.AdminPin;
            await RestoreProtectedSettingsToUserProfileAsync();
        }

        MainWindow managementWindow = new(openManagerDeviceOnLoad: pairManagerDeviceAfterInstall);
        MainWindow = managementWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        managementWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstance is not null)
        {
            _singleInstance.ActivationRequested -= SingleInstance_ActivationRequested;
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        ReleaseControlCenterWindow();

        _themeService.Dispose();
        base.OnExit(e);
    }

    private async void SingleInstance_ActivationRequested(object? sender, EventArgs e)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                await OpenControlCenterFromExternalRequestAsync();
            }
            else
            {
                Task activation = await Dispatcher.InvokeAsync(
                    () => OpenControlCenterFromExternalRequestAsync());
                await activation;
            }
        }
        catch (Exception exception)
        {
            ShowControlCenterActivationError(null, exception);
        }
    }

    private async Task OpenControlCenterFromExternalRequestAsync(
        string? verifiedPin = null,
        bool administratorVerified = false,
        bool openPlan = false)
    {
        if (MainWindow is MainWindow controlCenter && controlCenter.IsLoaded)
        {
            controlCenter.ActivateFromExternalRequest(openPlan);
            return;
        }

        if (_activatedControlCenter is not null && _activatedControlCenter.IsLoaded)
        {
            _activatedControlCenter.ActivateFromExternalRequest(openPlan);
            return;
        }

        if (MainWindow is not SessionSurfaceWindow sessionWindow || !sessionWindow.IsLoaded)
        {
            await OpenRecoveredControlCenterAsync(openPlan);
            return;
        }

        if (!TryClaimControlCenterWindow())
        {
            sessionWindow.EnableControlCenterReturn();
            return;
        }

        if (_controlCenterActivationInProgress)
        {
            return;
        }

        _controlCenterActivationInProgress = true;
        try
        {
            ControlSettings settings;
            if (_guardianCredential?.IsConfigured == true)
            {
                settings = File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
                    ? await new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true).LoadAsync()
            : new ControlSettings { SetupCompleted = true, Mode = UsageMode.Family };
                settings.AdminPin = _guardianCredential;
            }
            else
            {
                settings = await new JsonSettingsStore().LoadAsync();
            }
            string? managementPin = null;
            if (settings.Mode == UsageMode.Family && settings.AdminPin.IsConfigured)
            {
                if (administratorVerified && !string.IsNullOrWhiteSpace(verifiedPin))
                {
                    managementPin = verifiedPin;
                }
                else
                {
                    bool recoveryGuardianAvailable =
                        ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
                        File.Exists(ProtectionServiceManager.ProtectedSettingsPath);
                    AdminPinWindow verification = AdminPinWindow.CreateVerification(
                        pin => VerifyAdminPinAsync(pin, settings),
                        owner => RunRecoveryPinResetAsync(
                            owner,
                            recoveryGuardianAvailable
                                ? new JsonSettingsStore(
                                    ProtectionServiceManager.ProtectedSettingsPath,
                                    readOnly: true)
                                : new JsonSettingsStore(),
                            settings,
                            recoveryGuardianAvailable));
                    verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    verification.ShowInTaskbar = true;
                    verification.Topmost = true;
                    if (verification.ShowDialog() != true)
                    {
                        ReleaseControlCenterWindow();
                        return;
                    }

                    managementPin = verification.ResultPin;
                }

                _guardianCredential = settings.AdminPin;
                await RestoreProtectedSettingsToUserProfileAsync();
            }

            sessionWindow.EnableControlCenterReturn();
            _activatedControlCenter = new MainWindow(sessionWindow, managementPin);
            _activatedControlCenter.Closed += (_, _) =>
            {
                _activatedControlCenter = null;
                ReleaseControlCenterWindow();
                sessionWindow.ResumeFromControlCenter();
            };
            _activatedControlCenter.Show();
            _activatedControlCenter.ActivateFromExternalRequest(openPlan);
        }
        catch (Exception exception)
        {
            if (_activatedControlCenter is null)
            {
                ReleaseControlCenterWindow();
            }
            sessionWindow.ResumeFromControlCenter();
            ShowControlCenterActivationError(sessionWindow, exception);
        }
        finally
        {
            _controlCenterActivationInProgress = false;
        }
    }

    private bool TryClaimControlCenterWindow()
    {
        if (_controlCenterWindowInstance?.IsPrimary == true)
        {
            return true;
        }

        _controlCenterWindowInstance?.Dispose();
        SingleInstanceCoordinator candidate = new("ControlCenterWindow");
        if (!candidate.IsPrimary)
        {
            candidate.SignalPrimary();
            candidate.Dispose();
            return false;
        }

        _controlCenterWindowInstance = candidate;
        _controlCenterWindowInstance.ActivationRequested += SingleInstance_ActivationRequested;
        return true;
    }

    private void ReleaseControlCenterWindow()
    {
        if (_controlCenterWindowInstance is null)
        {
            return;
        }

        _controlCenterWindowInstance.ActivationRequested -= SingleInstance_ActivationRequested;
        _controlCenterWindowInstance.Dispose();
        _controlCenterWindowInstance = null;
    }

    private async Task OpenRecoveredControlCenterAsync(bool openPlan = false)
    {
        if (_controlCenterActivationInProgress)
        {
            return;
        }

        _controlCenterActivationInProgress = true;
        try
        {
            bool protectedPolicyAvailable =
                ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
                File.Exists(ProtectionServiceManager.ProtectedSettingsPath);
            JsonSettingsStore settingsStore = protectedPolicyAvailable
                ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
                : new JsonSettingsStore();
            ControlSettings settings = await settingsStore.LoadAsync();
            LocalizationService.SetLanguage(this, settings.Language);
            _themeService.SetPreference(settings.Theme);

            string? managementPin = null;
            if (settings.Mode == UsageMode.Family && settings.AdminPin.IsConfigured)
            {
                AdminPinWindow verification = AdminPinWindow.CreateVerification(
                    pin => VerifyAdminPinAsync(pin, settings),
                    owner => RunRecoveryPinResetAsync(owner, settingsStore, settings, protectedPolicyAvailable));
                verification.ShowInTaskbar = true;
                verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                verification.Topmost = true;
                if (verification.ShowDialog() != true)
                {
                    return;
                }

                managementPin = verification.ResultPin;
                _guardianCredential = settings.AdminPin;
                await RestoreProtectedSettingsToUserProfileAsync();
            }

            MainWindow recoveredControlCenter = new(managementPin: managementPin);
            MainWindow = recoveredControlCenter;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            recoveredControlCenter.Show();
            recoveredControlCenter.ActivateFromExternalRequest(openPlan);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Kvieta could not restore the control center.\n\n{exception.Message}",
                "Kvieta · Startup recovery",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _controlCenterActivationInProgress = false;
        }
    }

    private async void DirectSession_ControlCenterRequested(object? sender, ControlCenterRequestEventArgs e)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                await OpenControlCenterFromExternalRequestAsync(
                    e.VerifiedPin,
                    e.AdministratorVerified,
                    e.OpenPlan);
            }
            else
            {
                Task activation = await Dispatcher.InvokeAsync(
                    () => OpenControlCenterFromExternalRequestAsync(
                        e.VerifiedPin,
                        e.AdministratorVerified,
                        e.OpenPlan));
                await activation;
            }
        }
        catch (Exception exception)
        {
            if (sender is SessionSurfaceWindow sessionWindow)
            {
                sessionWindow.ResumeFromControlCenter();
            }
            ShowControlCenterActivationError(sender as Window, exception);
        }
    }

    private static void ShowControlCenterActivationError(Window? owner, Exception exception)
    {
        string message = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? $"The control center could not be opened. Protection stayed active.\n\n{exception.Message}"
            : $"Kontrol Merkezi açılamadı. Koruma açık kaldı.\n\n{exception.Message}";
        if (owner is null)
        {
            System.Windows.MessageBox.Show(
                message,
                "Kvieta · Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        System.Windows.MessageBox.Show(
            owner,
            message,
            "Kvieta · Control Center",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static async Task RestoreProtectedSettingsToUserProfileAsync()
    {
        if (!File.Exists(ProtectionServiceManager.ProtectedSettingsPath))
        {
            return;
        }

        ControlSettings protectedSettings = await new JsonSettingsStore(
            ProtectionServiceManager.ProtectedSettingsPath,
            readOnly: true).LoadAsync();
        await new JsonSettingsStore().SaveAsync(
            ProtectionPolicyChannel.CreatePublicPolicy(protectedSettings));
    }

    internal async Task<string?> RunPinRecoveryForCurrentPolicyAsync(Window owner)
    {
        bool guardianAvailable =
            ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
            File.Exists(ProtectionServiceManager.ProtectedSettingsPath);
        JsonSettingsStore settingsStore = guardianAvailable
            ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
            : new JsonSettingsStore();
        try
        {
            ControlSettings settings = await settingsStore.LoadAsync();
            return await RunRecoveryPinResetAsync(
                owner,
                settingsStore,
                settings,
                guardianAvailable);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                owner,
                LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? $"PIN recovery could not be started.\n\n{exception.Message}"
                    : $"PIN kurtarma başlatılamadı.\n\n{exception.Message}",
                LocalizationService.Get("ResetPinWithRecoveryCode"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }
    }

    private static async Task<string?> RunRecoveryPinResetAsync(
        Window owner,
        JsonSettingsStore settingsStore,
        ControlSettings settings,
        bool guardianAvailable)
    {
        ManagerDeviceEnrollment? managerDevice = guardianAvailable
            ? ManagerDeviceEnrollmentStore.Load()
            : null;
        bool hasUnusedRecoveryCode = settings.RecoveryCodes.Any(code => code.UsedAtUtc is null);
        if (!hasUnusedRecoveryCode && managerDevice?.IsActive != true)
        {
            System.Windows.MessageBox.Show(
                owner,
                LocalizationService.Get("PinRecoveryUnavailableDescription"),
                LocalizationService.Get("PinRecoveryUnavailableTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        Func<string, Task<bool>>? managerDeviceReset = managerDevice?.IsActive == true
            ? async newPin =>
            {
                if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.manager-device.consume"))
                {
                    return false;
                }

                try
                {
                    AdminCredential credential = AdminPinService.Create(newPin);
                    RecoveryChallenge challenge = ManagerDeviceRecoveryService.CreatePinResetChallenge(
                        managerDevice.DeviceId,
                        credential,
                        DateTimeOffset.UtcNow);
                    ManagerDeviceApprovalWindow? approvalWindow = null;
                    TaskCompletionSource<bool> authorizationResult = new(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    LocalRecoveryEndpoint endpoint = LocalRecoveryEndpoint.Start(
                        managerDevice,
                        challenge,
                        async response =>
                        {
                            bool accepted = await ProtectionPolicyChannel.ResetPinWithManagerDeviceAsync(
                                challenge,
                                response,
                                credential);
                            authorizationResult.TrySetResult(accepted);
                            approvalWindow?.Complete(accepted);
                            return accepted;
                        },
                        DateTimeOffset.UtcNow);
                    approvalWindow = new ManagerDeviceApprovalWindow(
                        endpoint.RecoveryUri,
                        challenge.ExpiresAtUtc,
                        verificationCode: ManagerDeviceVerificationCode.ForRecoveryChallenge(challenge))
                    {
                        Owner = owner
                    };
                    bool acceptedByDialog = approvalWindow.ShowDialog() == true;
                    await endpoint.DisposeAsync();
                    bool accepted = acceptedByDialog ||
                        authorizationResult.Task.IsCompletedSuccessfully && authorizationResult.Task.Result;
                    if (accepted)
                    {
                        settings.AdminPin = credential;
                        await RestoreProtectedSettingsToUserProfileAsync();
                    }

                    return accepted;
                }
                catch (Exception exception)
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        LocalizationService.CurrentLanguage == LanguagePreference.English
                            ? $"Could not start manager recovery: {exception.Message}"
                            : $"Yönetici cihazı kurtarma sunucusu başlatılamadı: {exception.Message}",
                        "Kvieta",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }
        : null;

        RecoveryResetWindow recoveryWindow = new(async (code, newPin) =>
        {
            if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.code.consume"))
            {
                return false;
            }

            AdminCredential credential = AdminPinService.Create(newPin);
            bool reset;
            if (guardianAvailable)
            {
                reset = await ProtectionPolicyChannel.ResetPinWithRecoveryCodeAsync(code, credential);
            }
            else
            {
                string localAuditPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kvieta",
                    "security-audit.jsonl");
                reset = await new RecoveryManager(settingsStore, new SecurityAuditLog(localAuditPath))
                    .TryResetPinAsync(code, credential);
            }

            if (reset)
            {
                settings.AdminPin = credential;
                if (guardianAvailable)
                {
                    await RestoreProtectedSettingsToUserProfileAsync();
                }
            }

            return reset;
        }, managerDeviceReset, hasUnusedRecoveryCode, managerDevice?.DeviceName)
        {
            Owner = owner
        };

        return recoveryWindow.ShowDialog() == true ? recoveryWindow.ResultPin : null;
    }

    internal async Task<bool> HandleUninstallRequestAsync(Window? owner = null)
    {
        JsonSettingsStore settingsStore = File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
            ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
            : new JsonSettingsStore();
        ControlSettings settings;
        try
        {
            settings = await settingsStore.LoadAsync();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Ayarlar doğrulanamadığı için kaldırma başlatılmadı.\n\nUninstall was not started because settings could not be verified.\n\n{exception.Message}",
                "Kvieta · Recovery required",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        LocalizationService.SetLanguage(this, settings.Language);
        _themeService.SetPreference(settings.Theme);

        if (!ProtectionServiceManager.IsInstallerManaged)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.Get("UninstallNotAvailableDescription"),
                LocalizationService.Get("UninstallTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (ProtectionServiceManager.RequiresPinForUninstall(settings))
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                pin => VerifyAdminPinAsync(pin, settings),
                owner => RunRecoveryPinResetAsync(
                    owner,
                    settingsStore,
                    settings,
                    ProtectionServiceManager.GetState() == ProtectionServiceState.Running));
            verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (owner is not null)
            {
                verification.Owner = owner;
                verification.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            verification.ShowInTaskbar = owner is null;
            if (verification.ShowDialog() != true)
            {
                return false;
            }
        }

        UninstallWindow confirmation = new()
        {
            Owner = owner,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = owner is null
        };
        if (confirmation.ShowDialog() != true)
        {
            return false;
        }

        try
        {
            if (ProtectionServiceManager.LaunchProductUninstallWorker(
                    confirmation.RemoveLocalData,
                    settings.Language,
                    settings.Theme))
            {
                return true;
            }
        }
        catch
        {
            // The same actionable message is shown for staging and elevation failures.
        }

        System.Windows.MessageBox.Show(
            LocalizationService.Get("UninstallLaunchFailed"),
            LocalizationService.Get("UninstallTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return false;
    }

    private void StartUninstallWorker(IReadOnlyList<string> arguments)
    {
        int marker = arguments.ToList().FindIndex(argument =>
            string.Equals(argument, "--uninstall-worker", StringComparison.OrdinalIgnoreCase));
        if (marker < 0 || arguments.Count < marker + 7)
        {
            Shutdown(87);
            return;
        }

        try
        {
            string productCode = arguments[marker + 1];
            bool removeLocalData = arguments[marker + 2] == "1";
            string localDataPath = Encoding.UTF8.GetString(Convert.FromBase64String(arguments[marker + 3]));
            string userSid = Encoding.UTF8.GetString(Convert.FromBase64String(arguments[marker + 4]));
            _ = Enum.TryParse(arguments[marker + 5], ignoreCase: true, out LanguagePreference language);
            _ = Enum.TryParse(arguments[marker + 6], ignoreCase: true, out ThemePreference theme);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _themeService.Start(this);
            LocalizationService.SetLanguage(this, language);
            _themeService.SetPreference(theme);

            UninstallWindow worker = new(productCode, removeLocalData, localDataPath, userSid);
            MainWindow = worker;
            worker.Closed += (_, _) =>
            {
                ProtectionServiceManager.ScheduleCurrentUninstallWorkerCleanup();
                Shutdown();
            };
            worker.Show();
        }
        catch
        {
            Shutdown(87);
        }
    }

    private static Task<bool> VerifyAdminPinAsync(string pin, ControlSettings settings) =>
            settings.Mode == UsageMode.Family &&
        ProtectionServiceManager.GetState() == ProtectionServiceState.Running
            ? ProtectionPolicyChannel.VerifyPinAsync(pin)
            : Task.FromResult(AdminPinService.Verify(pin, settings.AdminPin));

    private async Task HandleWindowsAdministratorVerificationAsync(IReadOnlyList<string> arguments)
    {
        if (!WindowsAdministratorVerificationService.IsAdministrator())
        {
            Shutdown(5);
            return;
        }

        int eventIndex = arguments.ToList().FindIndex(argument =>
            string.Equals(argument, "--audit-event", StringComparison.OrdinalIgnoreCase));
        string? auditEvent = eventIndex >= 0 && eventIndex + 1 < arguments.Count
            ? arguments[eventIndex + 1]
            : null;
        if (!WindowsAdministratorVerificationService.IsAllowedAuditEvent(auditEvent))
        {
            Shutdown(2);
            return;
        }

        try
        {
            await new SecurityAuditLog().AppendAsync(auditEvent!, "windows-admin-authorized");
            if (auditEvent is "recovery.manager-device.enroll" or
                "recovery.manager-device.transfer" or
                "recovery.manager-device.consume")
            {
                bool firewallReady = await WindowsAdministratorVerificationService
                    .EnsureLocalCompanionFirewallRuleAsync();
                await new SecurityAuditLog().AppendAsync(
                    auditEvent!,
                    firewallReady ? "local-companion-firewall-ready" : "local-companion-firewall-failed");
                if (!firewallReady)
                {
                    Shutdown(1);
                    return;
                }
            }
            if (string.Equals(auditEvent, "recovery.installer.repair", StringComparison.Ordinal))
            {
                bool repaired = await ProtectionServiceManager.RunProductRepairAsync(requestElevation: false);
                await new SecurityAuditLog().AppendAsync(
                    "recovery.installer.repair",
                    repaired ? "accepted" : "rejected");
                Shutdown(repaired ? 0 : 1);
                return;
            }
            Shutdown(0);
        }
        catch
        {
            Shutdown(1);
        }
    }

}

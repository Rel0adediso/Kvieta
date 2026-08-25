using System.Windows;
using System.IO;
using Otium.App.Services;
using Otium.App.ViewModels;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App;

public partial class App : System.Windows.Application
{
    private readonly SystemThemeService _themeService = new();
    private SingleInstanceCoordinator? _singleInstance;
    private MainWindow? _activatedControlCenter;
    private bool _controlCenterActivationInProgress;
    private AdminCredential? _guardianCredential;

    public SystemThemeService ThemeService => _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            OtiumGuardianService.RunService();
            Shutdown();
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--install-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            string? payload = e.Args.SkipWhile(argument => !string.Equals(argument, "--install-guardian", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
            Shutdown(ProtectionServiceManager.ExecuteInstallerCommand(install: true, payload));
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--remove-guardian", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(ProtectionServiceManager.ExecuteInstallerCommand(install: false));
            return;
        }

        bool guardianSession = e.Args.Any(argument => string.Equals(argument, "--guardian-session", StringComparison.OrdinalIgnoreCase));
        _singleInstance = new SingleInstanceCoordinator(guardianSession ? "GuardianSession" : "ControlCenter");
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
                $"Otium ayarları ve son sağlam kopya okunamadı. Güvenlik için başlangıç durduruldu.\n\nOtium settings and the last-known-good copy could not be read. Startup was stopped for safety.\n\n{exception.Message}",
                "Otium · Recovery required",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        LocalizationService.SetLanguage(this, settings.Language);
        _themeService.SetPreference(settings.Theme);

        if (!guardianSession && protectedPolicyAvailable && settings.Mode != ControlMode.Protected)
        {
            RestoreProtectedSettingsToUserProfile();
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
            GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
            if (enrollment is null || !enrollment.AdminPin.IsConfigured)
            {
                Shutdown();
                return;
            }

            if (!settings.RequiresGuardian)
            {
                Shutdown();
                return;
            }
            settings.AdminPin = settings.AdminPin.IsConfigured ? settings.AdminPin : enrollment.AdminPin;
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
            if (settings.Mode == ControlMode.Protected)
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
                        ? "Protected mode cannot start without the Otium Guardian service. Installation was cancelled or failed."
                        : "Otium Guardian servisi olmadan Korumalı mod başlatılamaz. Kurulum iptal edildi veya başarısız oldu.",
                    settings.Language == LanguagePreference.English
                        ? "Otium · Protection required"
                        : "Otium · Koruma gerekli",
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

        if (guardianSession || e.Args.Any(argument => string.Equals(argument, "--session", StringComparison.OrdinalIgnoreCase)))
        {
            if (settings.RequiresGuardian && !settings.AdminPin.IsConfigured)
            {
                System.Windows.MessageBox.Show(
                    settings.Language == LanguagePreference.English
                        ? "Create an administrator PIN in the control center before using protected session mode."
                        : "Doğrudan oturum modunu kullanmadan önce yönetim panelinden bir yönetici PIN'i oluştur.",
                    settings.Language == LanguagePreference.English ? "Otium · PIN required" : "Otium · PIN gerekli",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            CafeWindow sessionWindow = new(
                isDirectSession: true,
                requirePinToExit: settings.Mode == ControlMode.Protected,
                returnToControlCenter: settings.Mode == ControlMode.Personal &&
                    settings.PersonalProtectionLevel == PersonalProtectionLevel.Guarded,
                exitCredentialOverride: _guardianCredential,
                viewModel: guardianSession ? new CafeViewModel(settingsStore) : null);
            sessionWindow.ControlCenterRequested += DirectSession_ControlCenterRequested;
            MainWindow = sessionWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            sessionWindow.Show();
            return;
        }

        if (settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                pin => AdminPinService.Verify(pin, settings.AdminPin),
                owner => RunRecoveryPinResetAsync(owner, settingsStore, settings, protectedPolicyAvailable));
            verification.ShowInTaskbar = true;
            verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (verification.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            _guardianCredential = settings.AdminPin;
            string? verifiedPin = verification.ResultPin;
            RestoreProtectedSettingsToUserProfile();
            MainWindow protectedManagementWindow = new(managementPin: verifiedPin);
            MainWindow = protectedManagementWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            protectedManagementWindow.Show();
            return;
        }

        MainWindow managementWindow = new();
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

        _themeService.Dispose();
        base.OnExit(e);
    }

    private void SingleInstance_ActivationRequested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => OpenControlCenterFromExternalRequestAsync());
    }

    private async Task OpenControlCenterFromExternalRequestAsync(string? verifiedPin = null)
    {
        if (MainWindow is MainWindow controlCenter)
        {
            controlCenter.ActivateFromExternalRequest();
            return;
        }

        if (_activatedControlCenter is not null)
        {
            _activatedControlCenter.ActivateFromExternalRequest();
            return;
        }

        if (MainWindow is not CafeWindow sessionWindow)
        {
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
                    : new ControlSettings { SetupCompleted = true, Mode = ControlMode.Protected };
                settings.AdminPin = _guardianCredential;
            }
            else
            {
                settings = await new JsonSettingsStore().LoadAsync();
            }
            string? managementPin = null;
            if (settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured)
            {
                if (!string.IsNullOrWhiteSpace(verifiedPin) && AdminPinService.Verify(verifiedPin, settings.AdminPin))
                {
                    managementPin = verifiedPin;
                }
                else
                {
                    AdminPinWindow verification = AdminPinWindow.CreateVerification(
                        pin => AdminPinService.Verify(pin, settings.AdminPin),
                        owner => RunRecoveryPinResetAsync(
                            owner,
                            File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
                                ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath)
                                : new JsonSettingsStore(),
                            settings,
                            ProtectionServiceManager.GetState() == ProtectionServiceState.Running));
                    verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    verification.ShowInTaskbar = true;
                    verification.Topmost = true;
                    if (verification.ShowDialog() != true)
                    {
                        return;
                    }

                    managementPin = verification.ResultPin;
                }

                _guardianCredential = settings.AdminPin;
                RestoreProtectedSettingsToUserProfile();
            }

            sessionWindow.EnableControlCenterReturn();
            _activatedControlCenter = new MainWindow(sessionWindow, managementPin);
            if (sessionWindow.KeepsSessionBehindControlCenter)
            {
                _activatedControlCenter.Owner = sessionWindow;
                _activatedControlCenter.Topmost = true;
                _activatedControlCenter.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            _activatedControlCenter.Closed += (_, _) =>
            {
                _activatedControlCenter = null;
                sessionWindow.ResumeFromControlCenter();
            };
            _activatedControlCenter.Show();
            _activatedControlCenter.Activate();
        }
        finally
        {
            _controlCenterActivationInProgress = false;
        }
    }

    private void DirectSession_ControlCenterRequested(object? sender, ControlCenterRequestEventArgs e)
    {
        Dispatcher.InvokeAsync(() => OpenControlCenterFromExternalRequestAsync(e.VerifiedPin));
    }

    private static void RestoreProtectedSettingsToUserProfile()
    {
        if (!File.Exists(ProtectionServiceManager.ProtectedSettingsPath))
        {
            return;
        }

        string destination = new JsonSettingsStore().FilePath;
        string? directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(ProtectionServiceManager.ProtectedSettingsPath, destination, overwrite: true);
    }

    private static async Task<string?> RunRecoveryPinResetAsync(
        Window owner,
        JsonSettingsStore settingsStore,
        ControlSettings settings,
        bool guardianAvailable)
    {
        if (settings.RecoveryCodes.All(code => code.UsedAtUtc is not null))
        {
            return null;
        }

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
                    "Otium",
                    "security-audit.jsonl");
                reset = await new RecoveryManager(settingsStore, new SecurityAuditLog(localAuditPath))
                    .TryResetPinAsync(code, credential);
            }

            if (reset)
            {
                settings.AdminPin = credential;
                if (guardianAvailable)
                {
                    RestoreProtectedSettingsToUserProfile();
                }
            }

            return reset;
        })
        {
            Owner = owner
        };

        return recoveryWindow.ShowDialog() == true ? recoveryWindow.ResultPin : null;
    }

    private async Task HandleUninstallRequestAsync()
    {
        JsonSettingsStore settingsStore = File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
            ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath)
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
                "Otium · Recovery required",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
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
            return;
        }

        if (ProtectionServiceManager.RequiresPinForUninstall(settings))
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                pin => AdminPinService.Verify(pin, settings.AdminPin));
            verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            verification.ShowInTaskbar = true;
            if (verification.ShowDialog() != true)
            {
                return;
            }
        }

        if (System.Windows.MessageBox.Show(
                LocalizationService.Get("UninstallConfirmation"),
                LocalizationService.Get("UninstallTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!ProtectionServiceManager.LaunchProductUninstall())
        {
            System.Windows.MessageBox.Show(
                LocalizationService.Get("UninstallLaunchFailed"),
                LocalizationService.Get("UninstallTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

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

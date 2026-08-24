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
    private AdminCredential? _guardianCredential;

    public SystemThemeService ThemeService => _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        JsonSettingsStore settingsStore = (guardianSession || protectedPolicyAvailable) &&
            File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
                ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath)
                : new JsonSettingsStore();
        ControlSettings settings;
        try
        {
            settings = await settingsStore.LoadAsync();
        }
        catch
        {
            settings = new ControlSettings();
        }

        LocalizationService.SetLanguage(this, settings.Language);
        _themeService.SetPreference(settings.Theme);

        if (guardianSession)
        {
            GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
            if (enrollment is null || !enrollment.AdminPin.IsConfigured)
            {
                Shutdown();
                return;
            }

            settings.Mode = ControlMode.Protected;
            settings.AdminPin = enrollment.AdminPin;
            settings.SetupCompleted = true;
            _guardianCredential = enrollment.AdminPin;
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

            settings.SchemaVersion = 6;
            settings.SetupCompleted = true;
            await settingsStore.SaveAsync(settings);
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
            if (settings.Mode == ControlMode.Protected && !settings.AdminPin.IsConfigured)
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
                requirePinToExit: guardianSession || settings.Mode == ControlMode.Protected,
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
                pin => AdminPinService.Verify(pin, settings.AdminPin));
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
        Dispatcher.InvokeAsync(OpenControlCenterFromExternalRequestAsync);
    }

    private async Task OpenControlCenterFromExternalRequestAsync()
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

        ControlSettings settings;
        if (_guardianCredential?.IsConfigured == true)
        {
            settings = new ControlSettings
            {
                SetupCompleted = true,
                Mode = ControlMode.Protected,
                AdminPin = _guardianCredential
            };
        }
        else
        {
            settings = await new JsonSettingsStore().LoadAsync();
        }
        string? managementPin = null;
        if (settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                pin => AdminPinService.Verify(pin, settings.AdminPin));
            verification.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            verification.ShowInTaskbar = true;
            verification.Topmost = true;
            if (verification.ShowDialog() != true)
            {
                return;
            }

            _guardianCredential = settings.AdminPin;
            managementPin = verification.ResultPin;
            RestoreProtectedSettingsToUserProfile();
        }

        sessionWindow.EnableControlCenterReturn();
        _activatedControlCenter = new MainWindow(sessionWindow, managementPin);
        _activatedControlCenter.Closed += (_, _) => _activatedControlCenter = null;
        _activatedControlCenter.Show();
        _activatedControlCenter.Activate();
    }

    private void DirectSession_ControlCenterRequested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(OpenControlCenterFromExternalRequestAsync);
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

}

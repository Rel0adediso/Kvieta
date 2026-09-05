using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.ServiceProcess;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.SetupApp;

public partial class SetupWindow : Window
{
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly SetupPlan _plan = new();
    private ControlSettings? _existingSettings;
    private bool _hasExistingSettings;
    private bool _existingPolicyLocked;
    private bool _brokenProtectedCredential;
    private readonly Version _packageVersion = GetPackageVersion();
    private Version? _installedVersion;
    private string? _installedReleaseLabel;
    private SetupPackageAction _packageAction = SetupPackageAction.FreshInstall;
    private bool _openedForExistingInstallation;
    private bool _installationInProgress;
    private bool _languageChosen;
    private bool _pinVisible;
    private bool _pinRepeatVisible;
    private WizardPage _page = WizardPage.Language;

    public SetupWindow()
    {
        InitializeComponent();
        ScheduleDaysControl.ItemsSource = _plan.Schedule;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        DeviceNameBox.Text = Environment.MachineName;
        VersionText.Text = $"Kvieta · {GetDisplayVersion()}";
        RefreshLanguage();
        UpdateSelectionStyles();

        _installedVersion = ReadInstalledVersion();
        _installedReleaseLabel = ReadInstallerValue("InstalledReleaseLabel") as string;
        if (string.IsNullOrWhiteSpace(_installedReleaseLabel) &&
            string.Equals(ReadInstallerValue("SignerThumbprint") as string, "UNSIGNED-DEVELOPMENT-BUILD", StringComparison.Ordinal))
        {
            _installedReleaseLabel = $"{_installedVersion?.ToString(3)}-alpha";
        }
        _packageAction = SetupPlan.DeterminePackageAction(_installedVersion, _packageVersion);
        _openedForExistingInstallation = _installedVersion is not null;

        try
        {
            string protectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Kvieta",
                "protected-settings.json");
            _existingPolicyLocked = File.Exists(protectedPath);
            if (_existingPolicyLocked)
            {
                _existingSettings = await new JsonSettingsStore(protectedPath, readOnly: true).LoadAsync();
                _hasExistingSettings = true;
            }
            else
            {
                bool settingsFileExists = File.Exists(_settingsStore.FilePath);
                _existingSettings = await _settingsStore.LoadAsync();
                _hasExistingSettings = settingsFileExists && _existingSettings.SetupCompleted;
            }
        }
        catch
        {
            _existingSettings = _existingPolicyLocked
            ? new ControlSettings { SetupCompleted = true, Mode = UsageMode.Family }
                : null;
            _hasExistingSettings = _existingPolicyLocked;
        }

        if (_openedForExistingInstallation)
        {
            _plan.Language = _existingSettings?.Language == LanguagePreference.English
                ? SetupLanguage.English
                : SetupLanguage.Turkish;
            _languageChosen = true;
            _hasExistingSettings = true;
            ShowPage(WizardPage.Existing);
        }
        else
        {
            ShowPage(WizardPage.Language);
        }
    }

    private void Window_Closed(object? sender, EventArgs e) =>
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_installationInProgress) e.Cancel = true;
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            ApplySystemTheme();
            UpdateSelectionStyles();
        });

    private void ApplySystemTheme()
    {
        bool light = ReadAppsUseLightTheme();
        SetBrush("BackgroundBrush", light ? "#D8D6CC" : "#171813");
        SetBrush("SurfaceBrush", light ? "#EAE6DC" : "#1F201A");
        SetBrush("SurfaceHoverBrush", light ? "#DAD6CB" : "#2B2D24");
        SetBrush("BorderBrush", light ? "#BDBCB0" : "#37392F");
        SetBrush("PrimaryBrush", light ? "#4E5A35" : "#B4BC82");
        SetBrush("PrimaryForegroundBrush", light ? "#F6F5EC" : "#202217");
        SetBrush("PrimarySoftBrush", light ? "#CDD3B4" : "#343725");
        SetBrush("TextBrush", light ? "#22231D" : "#F1EEE3");
        SetBrush("MutedTextBrush", light ? "#505248" : "#B0AC9F");
        SetBrush("SidebarBrush", light ? "#D0D1C6" : "#1A1B16");
        SetBrush("FaintTextBrush", light ? "#606257" : "#949184");
        SetBrush("ControlBorderBrush", light ? "#777A6C" : "#7D806F");
        SetBrush("FocusRingBrush", light ? "#4E5A35" : "#B4BC82");
        SetBrush("ErrorTextBrush", light ? "#9E342E" : "#D86B64");
        SetBrush("LogoBackgroundBrush", "#F2EEE3");
        SetBrush("LogoInkBrush", "#22231D");
        SetBrush("LogoAccentBrush", "#4E5A35");
    }

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SetBrush(string key, string color) =>
        System.Windows.Application.Current.Resources[key] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private void Turkish_Click(object sender, RoutedEventArgs e) => SelectLanguage(SetupLanguage.Turkish);
    private void English_Click(object sender, RoutedEventArgs e) => SelectLanguage(SetupLanguage.English);

    private void SelectLanguage(SetupLanguage language)
    {
        _plan.Language = language;
        _languageChosen = true;
        LanguageNextButton.IsEnabled = true;
        RefreshLanguage();
        UpdateSelectionStyles();
    }

    private void LanguageNext_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Welcome);
    private void WelcomeBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Language);
    private void WelcomeNext_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_hasExistingSettings ? WizardPage.Existing : WizardPage.Mode);
    private void ExistingBack_Click(object sender, RoutedEventArgs e)
    {
        if (_openedForExistingInstallation)
        {
            Close();
            return;
        }

        ShowPage(WizardPage.Welcome);
    }

    private async void KeepExisting_Click(object sender, RoutedEventArgs e)
    {
        if (_packageAction == SetupPackageAction.DowngradeBlocked) return;
        _plan.ExistingChoice = SetupChoice.KeepExisting;
        if (_openedForExistingInstallation)
        {
            await InstallAsync();
            return;
        }

        PopulateSummary();
        ShowPage(WizardPage.Summary);
    }

    private void ConfigureNew_Click(object sender, RoutedEventArgs e)
    {
        if (_existingPolicyLocked || _existingSettings?.RequiresGuardian == true)
        {
            ExistingSecurityNotice.Text = T(
                "Bu ayarlar Guardian tarafından korunuyor. Kurulumu mevcut ayarlarla tamamla; mod değişikliğini Kvieta Kontrol Merkezi'nde doğrulama yaptıktan sonra gerçekleştir.",
                "These settings are protected by Guardian. Finish setup with existing settings, then change mode from the Kvieta Control Center after verification.");
            return;
        }

        _plan.ExistingChoice = SetupChoice.ConfigureNew;
        ExistingSecurityNotice.Text = string.Empty;
        ShowPage(WizardPage.Mode);
    }

    private async void ResetBrokenProtection_Click(object sender, RoutedEventArgs e)
    {
        if (!_brokenProtectedCredential) return;
        MessageBoxResult confirmation = MessageBox.Show(
            this,
            T(
                "Eski Guardian kimliği eksik olduğu için mevcut PIN kurtarılamıyor. Bu işlem eski PIN'i, kurtarma kodlarını ve telefon eşleştirmesini siler. Kullanım geçmişine dokunmaz. Devam edilsin mi?",
                "The old Guardian identity is missing, so the existing PIN cannot be recovered. This removes the old PIN, recovery codes, and phone pairing. Usage history is kept. Continue?"),
            "Kvieta",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        ResetBrokenProtectionButton.IsEnabled = false;
        try
        {
            int exitCode = await RunElevatedBrokenProtectionResetAsync();
            if (exitCode != 0)
            {
                throw new InvalidOperationException(T(
                    "Bozuk Guardian kaydı temizlenemedi. Bilgisayarı yeniden başlatıp tekrar dene.",
                    "The broken Guardian record could not be cleared. Restart the computer and try again."));
            }

            DeleteLocalSettingsForProtectionReset();

            _existingSettings = null;
            _hasExistingSettings = false;
            _existingPolicyLocked = false;
            _brokenProtectedCredential = false;
            _plan.ExistingChoice = SetupChoice.ConfigureNew;
            ExistingSecurityNotice.Text = string.Empty;
            ShowPage(WizardPage.Mode);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            ExistingSecurityNotice.Text = T(
                "Windows yönetici izni iptal edildi; hiçbir şey değiştirilmedi.",
                "Windows administrator permission was cancelled; nothing was changed.");
        }
        catch (Exception exception)
        {
            ExistingSecurityNotice.Text = exception.Message;
        }
        finally
        {
            ResetBrokenProtectionButton.IsEnabled = true;
        }
    }

    private async Task<int> RunElevatedBrokenProtectionResetAsync()
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(T("Kurulum yolu bulunamadı.", "The setup path could not be resolved."));
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--elevated-reset-broken-protection");
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(T("Kurtarma aracı başlatılamadı.", "The recovery tool could not be started."));
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private void DeleteLocalSettingsForProtectionReset()
    {
        foreach (string path in new[]
        {
            _settingsStore.FilePath,
            _settingsStore.BackupPath,
            _settingsStore.FilePath + ".lock"
        })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private void Insights_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Insights);
    private void Personal_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Personal);
    private void Family_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Family);

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string templateName } ||
            !Enum.TryParse(templateName, out SetupTemplate template))
        {
            return;
        }

        _plan.ApplyTemplate(template);
        ModeNextButton.IsEnabled = true;
        TrackingBox.IsChecked = true;
        TrackingBox.IsEnabled = _plan.Mode != UsageMode.Insights;
        UpdateModeDependentPreferences();
        ScheduleDaysControl.Items.Refresh();
        UpdateSelectionStyles();
    }

    private void SelectMode(UsageMode mode)
    {
        _plan.ClearSelectedTemplate();
        _plan.Mode = mode;
        ModeNextButton.IsEnabled = true;
        TrackingBox.IsChecked = true;
        TrackingBox.IsEnabled = mode != UsageMode.Insights;
        UpdateModeDependentPreferences();
        UpdateSelectionStyles();
    }

    private void UpdateModeDependentPreferences()
    {
        DailyLimitSection.Visibility = Visibility.Collapsed;
    }

    private void ModeBack_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_hasExistingSettings ? WizardPage.Existing : WizardPage.Welcome);

    private void ModeNext_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.Mode switch
        {
            UsageMode.Personal => WizardPage.Personal,
            UsageMode.Family => WizardPage.Schedule,
            _ => WizardPage.Preferences
        });

    private void Flexible_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Flexible);
    private void Balanced_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Balanced);
    private void ProtectedLevel_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Protected);

    private void SelectPersonalLevel(PersonalProtectionLevel level)
    {
        _plan.ClearSelectedTemplate();
        _plan.PersonalLevel = level;
        UpdateSelectionStyles();
    }

    private void PersonalBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Mode);
    private void PersonalNext_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.UsesScheduledPlan ? WizardPage.Schedule : WizardPage.Preferences);
    private void ScheduleBack_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.Mode == UsageMode.Personal ? WizardPage.Personal : WizardPage.Mode);

    private void ScheduleNext_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        if (!TryReadSchedule()) return;
        ShowPage(WizardPage.Preferences);
    }

    private bool TryReadSchedule()
    {
        string[] timeFormats = ["H:mm", "HH:mm"];
        foreach (SetupScheduleDayRow day in _plan.Schedule.Where(item => item.IsEnabled))
        {
            if (!TimeOnly.TryParseExact(day.AllowedFromText.Trim(), timeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly allowedFrom) ||
                !TimeOnly.TryParseExact(day.AllowedUntilText.Trim(), timeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly allowedUntil))
            {
                ScheduleErrorText.Text = T(
                    $"{day.DayName} için saatleri SS:DD biçiminde gir.",
                    $"Enter the times for {day.DayName} in HH:MM format.");
                return false;
            }

            if (!int.TryParse(day.DailyLimitText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int limit) || limit is < 1 or > 1440)
            {
                ScheduleErrorText.Text = T(
                    $"{day.DayName} için günlük limit 1 ile 1440 dakika arasında olmalı.",
                    $"The daily limit for {day.DayName} must be between 1 and 1440 minutes.");
                return false;
            }

            day.AllowedFromText = allowedFrom.ToString("HH:mm", CultureInfo.InvariantCulture);
            day.AllowedUntilText = allowedUntil.ToString("HH:mm", CultureInfo.InvariantCulture);
            day.DailyLimitText = limit.ToString(CultureInfo.InvariantCulture);
        }

        _plan.HasCustomSchedule = true;
        _plan.DailyLimitMinutes = _plan.Schedule
            .Where(item => item.IsEnabled)
            .Select(item => int.TryParse(item.DailyLimitText, out int limit) ? limit : 180)
            .FirstOrDefault(180);
        ScheduleErrorText.Text = string.Empty;
        ScheduleDaysControl.Items.Refresh();
        return true;
    }

    private void PreferencesBack_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.Mode switch
        {
            UsageMode.Insights => WizardPage.Mode,
            UsageMode.Personal when !_plan.UsesScheduledPlan => WizardPage.Personal,
            _ => WizardPage.Schedule
        });

    private void PreferencesNext_Click(object sender, RoutedEventArgs e)
    {
        ReadPreferences();
        if (_plan.RequiresUserPin)
        {
            ShowPage(WizardPage.Pin);
            return;
        }

        PopulateSummary();
        ShowPage(WizardPage.Summary);
    }

    private void Pin_Changed(object sender, RoutedEventArgs e)
    {
        ValidatePinFields();
    }

    private void VisiblePin_Changed(object sender, TextChangedEventArgs e)
    {
        if (_pinVisible && ReferenceEquals(sender, VisiblePinBox))
        {
            PinBox.Password = VisiblePinBox.Text;
        }
        else if (_pinRepeatVisible && ReferenceEquals(sender, VisiblePinRepeatBox))
        {
            PinRepeatBox.Password = VisiblePinRepeatBox.Text;
        }
        ValidatePinFields();
    }

    private void ValidatePinFields()
    {
        string pin = CurrentPin;
        string repeat = CurrentPinRepeat;
        bool formatValid = AdminPinService.IsValidFormat(pin);
        bool match = string.Equals(pin, repeat, StringComparison.Ordinal);
        PinNextButton.IsEnabled = formatValid && match;
        PinErrorText.Text = string.IsNullOrEmpty(pin) && string.IsNullOrEmpty(repeat)
            ? string.Empty
            : !formatValid
                ? T("PIN 4–8 rakamdan oluşmalı.", "PIN must contain 4–8 digits.")
                : !match
                    ? T("PIN'ler aynı değil.", "PINs do not match.")
                    : string.Empty;
    }

    private string CurrentPin => _pinVisible ? VisiblePinBox.Text : PinBox.Password;
    private string CurrentPinRepeat => _pinRepeatVisible ? VisiblePinRepeatBox.Text : PinRepeatBox.Password;

    private void TogglePinVisibility_Click(object sender, RoutedEventArgs e)
    {
        _pinVisible = !_pinVisible;
        TogglePinField(PinBox, VisiblePinBox, _pinVisible);
    }

    private void TogglePinRepeatVisibility_Click(object sender, RoutedEventArgs e)
    {
        _pinRepeatVisible = !_pinRepeatVisible;
        TogglePinField(PinRepeatBox, VisiblePinRepeatBox, _pinRepeatVisible);
    }

    private static void TogglePinField(PasswordBox hidden, TextBox visible, bool show)
    {
        if (show)
        {
            visible.Text = hidden.Password;
            visible.Visibility = Visibility.Visible;
            hidden.Visibility = Visibility.Collapsed;
            visible.Focus();
            visible.CaretIndex = visible.Text.Length;
            return;
        }

        hidden.Password = visible.Text;
        hidden.Visibility = Visibility.Visible;
        visible.Visibility = Visibility.Collapsed;
        hidden.Focus();
    }

    private void PinBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Preferences);

    private void PinNext_Click(object sender, RoutedEventArgs e)
    {
        _plan.AdminPin = CurrentPin;
        RecoveryCodesBox.Text = string.Join(Environment.NewLine, _plan.EnsureRecoveryCodes());
        RecoveryNextButton.IsEnabled = RecoveryAcknowledgementBox.IsChecked == true;
        ShowPage(WizardPage.Recovery);
    }

    private void RecoveryAcknowledgement_Changed(object sender, RoutedEventArgs e) =>
        RecoveryNextButton.IsEnabled = RecoveryAcknowledgementBox.IsChecked == true;

    private void RecoveryBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Pin);

    private void RecoveryNext_Click(object sender, RoutedEventArgs e)
    {
        PopulateSummary();
        ShowPage(WizardPage.Summary);
    }

    private void CopyRecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(RecoveryCodesBox.Text);
            CopyRecoveryCodesButton.Content = T("Kopyalandı", "Copied");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                T($"Kodlar panoya kopyalanamadı: {ex.Message}", $"The codes could not be copied: {ex.Message}"),
                "Kvieta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SaveRecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Title = T("Kurtarma kodlarını kaydet", "Save recovery codes"),
            FileName = "Kvieta-recovery-codes.txt",
            Filter = T("Metin dosyası (*.txt)|*.txt", "Text file (*.txt)|*.txt"),
            AddExtension = true,
            DefaultExt = ".txt"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, RecoveryCodesBox.Text);
            SaveRecoveryCodesButton.Content = T("Kaydedildi", "Saved");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                T($"Kodlar kaydedilemedi: {ex.Message}", $"The codes could not be saved: {ex.Message}"),
                "Kvieta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SummaryBack_Click(object sender, RoutedEventArgs e)
    {
        if (_plan.ExistingChoice == SetupChoice.KeepExisting && _hasExistingSettings)
        {
            ShowPage(WizardPage.Existing);
        }
        else
        {
            ShowPage(_plan.RequiresUserPin ? WizardPage.Recovery : WizardPage.Preferences);
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e) => await InstallAsync();
    private void ErrorBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Summary);
    private async void Retry_Click(object sender, RoutedEventArgs e) => await InstallAsync();

    private async Task InstallAsync()
    {
        _installationInProgress = true;
        CloseButton.IsEnabled = false;
        ShowPage(WizardPage.Installing);
        SettingsFilesSnapshot? settingsSnapshot = null;
        bool settingsStaged = false;
        try
        {
            ReadPreferences();
            ControlSettings settings = _plan.ComposeSettings(_existingSettings);
            bool requiresGuardian = settings.RequiresGuardian;
            settingsSnapshot = await CaptureSettingsSnapshotAsync();
            await _settingsStore.SaveAsync(settings);
            settingsStaged = true;
            string? guardianPayload = requiresGuardian ? CreateGuardianPayload() : null;
            int exitCode = await RunElevatedInstallerAsync(guardianPayload);
            if (exitCode == ElevatedPackageInstaller.GuardianProvisioningFailedExitCode)
            {
                throw new InvalidOperationException(
                    T("Kvieta dosyaları kuruldu ancak korumalı ayarlar Guardian'a aktarılamadı. Mevcut güvenlik verileri geri yüklendi.",
                      "Kvieta files were installed, but protected settings could not be provisioned to Guardian. Existing security data was restored."));
            }
            if (exitCode == ElevatedPackageInstaller.GuardianCredentialUnavailableExitCode)
            {
                if (settingsStaged && settingsSnapshot is not null)
                {
                    await RestoreSettingsSnapshotAsync(settingsSnapshot);
                    settingsStaged = false;
                }
                _brokenProtectedCredential = true;
                RefreshExistingInstallationText();
                ShowPage(WizardPage.Existing);
                return;
            }
            if (exitCode == ElevatedPackageInstaller.GuardianStartFailedExitCode)
            {
                throw new InvalidOperationException(
                    T("Kvieta dosyaları kuruldu ancak Guardian servisi başlatılamadı. Mevcut güvenlik verileri geri yüklendi.",
                      "Kvieta files were installed, but the Guardian service could not be started. Existing security data was restored."));
            }
            if (exitCode == ElevatedPackageInstaller.OrphanedGuardianCleanupFailedExitCode)
            {
                throw new InvalidOperationException(
                    T("Eski kurulumdan kalan bozuk Guardian servisi kaldırılamadı. Bilgisayarı yeniden başlatıp kurulumu tekrar dene. Ayrıntılar ProgramData\\Kvieta\\SetupLogs klasöründe.",
                      "The broken Guardian service left by the old installation could not be removed. Restart the computer and run setup again. Details are in ProgramData\\Kvieta\\SetupLogs."));
            }
            if (exitCode is not (0 or 1641 or 3010))
            {
                throw new InvalidOperationException(
                    T($"Windows Installer {exitCode} koduyla durdu. Tanılama günlükleri ProgramData\\Kvieta\\SetupLogs klasöründe.",
                      $"Windows Installer stopped with code {exitCode}. Diagnostics are in ProgramData\\Kvieta\\SetupLogs."));
            }

            if (requiresGuardian && !await WaitForGuardianServiceAsync())
            {
                throw new InvalidOperationException(
                    T("Guardian servisi başlatılamadı. Korumalı ayarlar kaydedilmedi ve Kvieta başlatılmadı.",
                      "The Guardian service could not be started. Protected settings were not saved and Kvieta was not launched."));
            }

            string launchArguments = requiresGuardian
                ? "--post-install-control-center"
                : _plan.LaunchArguments;
            if (_plan.RequiresUserPin && _plan.PairManagerDeviceAfterInstall)
            {
                launchArguments += " --pair-manager-device";
            }
            LaunchInstalledKvieta(launchArguments.Trim());
            settingsStaged = false;
            await Task.Delay(650);
            _installationInProgress = false;
            Close();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            if (settingsStaged && settingsSnapshot is not null)
            {
                await RestoreSettingsSnapshotAsync(settingsSnapshot);
            }
            ShowInstallError(T("Yönetici izni iptal edildi. Hiçbir ayar değiştirilmedi.",
                               "Administrator permission was cancelled. No settings were changed."));
        }
        catch (Exception exception)
        {
            if (settingsStaged && settingsSnapshot is not null)
            {
                await RestoreSettingsSnapshotAsync(settingsSnapshot);
            }
            ShowInstallError(exception.Message);
        }
        finally
        {
            _installationInProgress = false;
            CloseButton.IsEnabled = true;
        }
    }

    private async Task<int> RunElevatedInstallerAsync(string? guardianPayload)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(T("Kurulum yolu bulunamadı.", "The setup path could not be resolved."));
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--elevated-install");
        if (_packageAction == SetupPackageAction.Repair)
        {
            startInfo.ArgumentList.Add("--force-reinstall");
        }
        if (_plan.DesktopShortcut) startInfo.ArgumentList.Add("--desktop-shortcut");
        if (!string.IsNullOrWhiteSpace(guardianPayload))
        {
            startInfo.ArgumentList.Add("--guardian-payload");
            startInfo.ArgumentList.Add(guardianPayload);
        }
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(T("Windows Installer başlatılamadı.", "Windows Installer could not be started."));
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private string CreateGuardianPayload()
    {
        string userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException(T(
                "Geçerli Windows kullanıcı kimliği okunamadı.",
                "The current Windows user identity could not be read."));
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            UserSid = userSid,
            SettingsPath = _settingsStore.FilePath
        });
        return Convert.ToBase64String(json);
    }

    private async Task<SettingsFilesSnapshot> CaptureSettingsSnapshotAsync() => new(
        await ReadFileIfPresentAsync(_settingsStore.FilePath),
        await ReadFileIfPresentAsync(_settingsStore.BackupPath));

    private async Task RestoreSettingsSnapshotAsync(SettingsFilesSnapshot snapshot)
    {
        await RestoreFileAsync(_settingsStore.FilePath, snapshot.Settings);
        await RestoreFileAsync(_settingsStore.BackupPath, snapshot.Backup);
    }

    private static async Task<byte[]?> ReadFileIfPresentAsync(string path) =>
        File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;

    private static async Task RestoreFileAsync(string path, byte[]? content)
    {
        if (content is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + ".setup-rollback";
        await File.WriteAllBytesAsync(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void LaunchInstalledKvieta(string arguments)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Kvieta",
            "Kvieta.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Installed Kvieta executable was not found.", executable);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    private async Task<bool> WaitForGuardianServiceAsync()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using ServiceController service = new("KvietaGuardian");
                service.Refresh();
                string protectionDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Kvieta");
                if (service.Status == ServiceControllerStatus.Running &&
                    File.Exists(Path.Combine(protectionDirectory, "guardian-enrollment.json")) &&
                    File.Exists(Path.Combine(protectionDirectory, "protected-settings.json")))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
            }

            await Task.Delay(250);
        }

        return false;
    }

    private sealed record SettingsFilesSnapshot(byte[]? Settings, byte[]? Backup);

    private void ReadPreferences()
    {
        _plan.DeviceName = string.IsNullOrWhiteSpace(DeviceNameBox.Text)
            ? Environment.MachineName
            : DeviceNameBox.Text.Trim();
        _plan.DailyLimitMinutes = DailyLimitBox.SelectedItem is ComboBoxItem item &&
                                  int.TryParse(item.Tag?.ToString(), out int minutes)
            ? minutes
            : 180;
        _plan.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _plan.DesktopShortcut = DesktopShortcutBox.IsChecked == true;
        _plan.AwarenessTracking = TrackingBox.IsChecked == true;
        _plan.PairManagerDeviceAfterInstall = PairManagerDeviceBox.IsChecked == true;
    }

    private void PopulateSummary()
    {
        ReadPreferences();
        bool english = IsEnglish;
        if (_plan.ExistingChoice == SetupChoice.KeepExisting && _existingSettings is not null)
        {
            SetSummaryLimitVisibility(true);
            SummaryModeValue.Text = T("Mevcut ayarlar korunacak", "Existing settings will be kept");
            SummaryDeviceValue.Text = _existingSettings.DeviceName;
            SummaryLimitValue.Text = FormatMinutes(_existingSettings.DefaultDailyLimitMinutes, english);
            SummaryOptionsValue.Text = T("Yalnız uygulama dosyaları yenilenecek", "Only application files will be refreshed");
            return;
        }

        SummaryModeValue.Text = ModeName(_plan.Mode, _plan.PersonalLevel);
        SummaryDeviceValue.Text = _plan.DeviceName;
        int enabledDays = _plan.Schedule.Count(day => day.IsEnabled);
        SummaryLimitLabel.Text = T("Haftalık plan", "Weekly plan");
        SummaryLimitValue.Text = T($"{enabledDays} gün etkin", $"{enabledDays} days active");
        SetSummaryLimitVisibility(_plan.UsesScheduledPlan);
        List<string> options = [];
        if (_plan.StartWithWindows) options.Add(T("Windows ile başlangıç", "Start with Windows"));
        if (_plan.DesktopShortcut) options.Add(T("masaüstü kısayolu", "desktop shortcut"));
        if (_plan.AwarenessTracking || _plan.Mode == UsageMode.Insights) options.Add(T("yerel ölçüm", "local tracking"));
        options.Add(_plan.Mode switch
        {
            UsageMode.Insights => T("ilk ritim hedefi: günlük özeti incele", "first rhythm goal: review the daily summary"),
            UsageMode.Personal when _plan.PersonalLevel == PersonalProtectionLevel.Flexible =>
                T("ilk ritim hedefi: 25 dakika odak", "first rhythm goal: 25 minutes of focus"),
            _ => T("ilk ritim hedefi: günlük dengeni koru", "first rhythm goal: keep your daily balance")
        });
        if (_plan.RequiresGuardian) options.Add("Guardian");
        if (_plan.RequiresUserPin) options.Add(T("8 tek kullanımlık kurtarma kodu", "8 one-time recovery codes"));
        if (_plan.RequiresUserPin && _plan.PairManagerDeviceAfterInstall) options.Add(T("isteğe bağlı telefon eşleştirme", "optional phone pairing"));
        SummaryOptionsValue.Text = options.Count == 0 ? T("Ek seçenek yok", "No optional features") : string.Join(" · ", options);
    }

    private void SetSummaryLimitVisibility(bool visible)
    {
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SummaryLimitLabel.Visibility = visibility;
        SummaryLimitValue.Visibility = visibility;
    }

    private void ShowInstallError(string message)
    {
        ErrorDescription.Text = message;
        ShowPage(WizardPage.Error);
    }

    private void ShowPage(WizardPage page)
    {
        _page = page;
        foreach (Grid panel in new[]
        {
            LanguagePanel, WelcomePanel, ExistingPanel, ModePanel, PersonalPanel,
            SchedulePanel, PreferencesPanel, PinPanel, RecoveryPanel, SummaryPanel, InstallingPanel, ErrorPanel
        })
        {
            panel.Visibility = Visibility.Collapsed;
        }

        Grid selected = page switch
        {
            WizardPage.Language => LanguagePanel,
            WizardPage.Welcome => WelcomePanel,
            WizardPage.Existing => ExistingPanel,
            WizardPage.Mode => ModePanel,
            WizardPage.Personal => PersonalPanel,
            WizardPage.Schedule => SchedulePanel,
            WizardPage.Preferences => PreferencesPanel,
            WizardPage.Pin => PinPanel,
            WizardPage.Recovery => RecoveryPanel,
            WizardPage.Summary => SummaryPanel,
            WizardPage.Installing => InstallingPanel,
            _ => ErrorPanel
        };
        selected.Visibility = Visibility.Visible;
        PairManagerDeviceFeature.Visibility = _plan.RequiresUserPin ? Visibility.Visible : Visibility.Collapsed;
        UpdateModeDependentPreferences();
        AnimatePageEntrance(selected);
        UpdateStepStatus();
        RefreshLanguage();
        UpdateSelectionStyles();
    }

    private static void AnimatePageEntrance(Grid panel)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            panel.Opacity = 1;
            panel.RenderTransform = Transform.Identity;
            return;
        }

        TranslateTransform slide = new(0, 12);
        panel.RenderTransform = slide;
        panel.Opacity = 0;
        panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
        {
            DecelerationRatio = 0.8
        });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220))
        {
            DecelerationRatio = 0.85
        });
    }

    private void UpdateStepStatus()
    {
        int step = _page switch
        {
            WizardPage.Language => 1,
            WizardPage.Welcome => 2,
            WizardPage.Existing or WizardPage.Mode or WizardPage.Personal => 3,
            WizardPage.Schedule => 4,
            WizardPage.Preferences => 5,
            WizardPage.Pin => 6,
            WizardPage.Recovery => 7,
            WizardPage.Summary => 8,
            WizardPage.Installing or WizardPage.Error => 9,
            _ => 1
        };
        StepProgress.Maximum = 9;
        StepProgress.Value = step;
        StepLabel.Text = T($"ADIM {step} / 9", $"STEP {step} / 9");
        (StepTitle.Text, StepDescription.Text) = _page switch
        {
            WizardPage.Language => (T("Dil seçimi", "Language"), T("Kurulum ve Kvieta aynı dilde devam eder.", "Setup and Kvieta continue in the same language.")),
            WizardPage.Welcome => (T("Kvieta'yı tanı", "Meet Kvieta"), T("Ne kurulduğunu ve verilerin nasıl işlendiğini gör.", "See what is installed and how your data is handled.")),
            WizardPage.Existing => (T("Mevcut kurulum", "Existing setup"), T("Ayarlarını koru veya yeniden yapılandır.", "Keep or reconfigure your settings.")),
            WizardPage.Mode or WizardPage.Personal => (T("Kullanım biçimi", "Usage mode"), T("İhtiyacına uygun koruma düzeyini seç.", "Choose the protection level that fits you.")),
            WizardPage.Schedule => (T("Haftalık plan", "Weekly plan"), T("Günleri, saatleri ve limitleri belirle.", "Set days, hours, and limits.")),
            WizardPage.Preferences when _plan.Mode == UsageMode.Insights => (T("Başlangıç ayarları", "Essentials"), T("Cihaz ve başlangıç seçenekleri.", "Device and startup options.")),
            WizardPage.Preferences => (T("Başlangıç ayarları", "Essentials"), T("Cihaz ve başlangıç seçenekleri.", "Device and startup options.")),
            WizardPage.Pin => (T("Yönetici güvenliği", "Administrator security"), T("Korumalı kullanım için PIN oluştur.", "Create a PIN for protected use.")),
            WizardPage.Recovery => (T("PIN kurtarma", "PIN recovery"), T("Tek kullanımlık kurtarma kodlarını güvenli biçimde sakla.", "Store the one-time recovery codes securely.")),
            WizardPage.Summary => (T("Son kontrol", "Final review"), T("Kurulumdan önce seçimlerini doğrula.", "Confirm your choices before installation.")),
            WizardPage.Installing => (T("Kurulum", "Installation"), T("Kvieta ve Guardian hazırlanıyor.", "Kvieta and Guardian are being prepared.")),
            _ => (T("Kurulum sorunu", "Setup issue"), T("Tanılamayı inceleyip yeniden dene.", "Review diagnostics and try again."))
        };
    }

    private void RefreshLanguage()
    {
        SidebarTagline.Text = T("Her şeyin bir zamanı var.", "Everything has its time.");
        LocalDataText.Text = T("Veriler yalnızca bu cihazda", "Data stays on this device");
        LanguageTitle.Text = T("Dilini seç", "Choose your language");
        LanguageDescription.Text = T("Kurulum ve ilk ayarlar seçtiğin dilde devam edecek.", "Setup and first-time configuration will continue in your chosen language.");
        LanguageNextButton.Content = T("Devam et", "Continue");
        WelcomeEyebrow.Text = T("SAKİN · YEREL · SENİN KONTROLÜNDE", "CALM · LOCAL · IN YOUR CONTROL");
        WelcomeTitle.Text = T("Kvieta'ya hoş geldin.", "Welcome to Kvieta.");
        WelcomeDescription.Text = T("Kvieta ekran süreni cihazında ölçer, günlük ritmini görünür kılar ve istersen kendi koyduğun kurallara bağlı kalmana yardım eder. Nasıl davranacağını sen seçersin.", "Kvieta measures screen time on your device, makes your daily rhythm visible, and can help you stay with the rules you set. You choose how it behaves.");
        FeatureLocalTitle.Text = T("Yerel ölçüm", "Local tracking"); FeatureLocalText.Text = T("Uygulama adı ve kullanım süresi cihazında ölçülür. Hesap veya bulut gerekmez.", "App names and usage time are measured on your device. No account or cloud required.");
        FeatureChoiceTitle.Text = T("Üç kullanım biçimi", "Three usage modes"); FeatureChoiceText.Text = T("Yalnız takip et, kendi planına destek al veya kuralları Guardian ile koru.", "Track only, get support for your own plan, or protect rules with Guardian.");
        FeaturePrivateTitle.Text = T("İçerik kaydı yok", "No content capture"); FeaturePrivateText.Text = T("Pencere başlıklarını, yazdıklarını veya ekran görüntülerini hiçbir zaman saklamaz.", "Never stores window titles, what you type, or screenshots.");
        WelcomeBackButton.Content = BackText; WelcomeNextButton.Content = ContinueText;
        RefreshExistingInstallationText();
        ConfigureNewTitle.Text = T("Ayarları baştan yapılandır", "Configure settings again"); ConfigureNewText.Text = T("Mod ve başlangıç ayarlarını yeniden seç; geçmiş verilerine dokunma.", "Choose mode and essentials again without touching usage history.");
        if (_existingPolicyLocked || _existingSettings?.RequiresGuardian == true)
        {
            ConfigureNewText.Text = T("Guardian koruması nedeniyle mod değişikliği Kontrol Merkezi'nde doğrulama gerektirir.", "Guardian protection requires Control Center verification for mode changes.");
        }
        ExistingBackButton.Content = _openedForExistingInstallation ? T("İptal", "Cancel") : BackText;
        ModeTitle.Text = ProductText("ChooseModeTitle"); ModeDescription.Text = ProductText("ChooseModeDescription");
        UnderstandTemplateButton.Content = T("Kullanımımı gör", "See my usage");
        FocusTemplateButton.Content = T("Odaklan", "Focus");
        GamingTemplateButton.Content = T("Oyun düzeni", "Gaming routine");
        EveningTemplateButton.Content = T("Akşam bırak", "Wind down");
        FamilyTemplateButton.Content = T("Aile düzeni", "Family routine");
        InsightsTitle.Text = ProductText("InsightsMode"); InsightsText.Text = ProductText("InsightsModeDescription");
        PersonalTitle.Text = ProductText("PersonalMode"); PersonalText.Text = ProductText("PersonalModeDescription");
        FamilyTitle.Text = ProductText("FamilyMode"); FamilyText.Text = ProductText("FamilyModeDescription");
        InsightsDetails.Text = T("• Engel veya zorunlu mola yok\n• Başlangıç ritmi ve haftalık eğilim\n• Tüm veriler yalnız bu cihazda", "• No blocking or forced breaks\n• Baseline rhythm and weekly trends\n• All data stays on this device");
        InsightsBestFor.Text = T("En hafif başlangıç", "The lightest way to start");
        PersonalDetails.Text = T("• Günlük plan, limit ve uygulama kuralları\n• Esnek, Dengeli veya Korumalı düzey\n• Gevşetmeler seçtiğin süre kadar bekler", "• Daily plan, limits, and app rules\n• Flexible, Balanced, or Protected level\n• Relaxations wait for your chosen delay");
        PersonalBestFor.Text = T("Kendi kararlarına destek", "Support for your own decisions");
        FamilyDetails.Text = T("• Ayarlar ve çıkış yönetici PIN'iyle korunur\n• Guardian kapatılan oturumu yeniden açar\n• Yönetici onayı olmadan gevşetilemez", "• Settings and exit require the administrator PIN\n• Guardian reopens a closed session\n• Rules cannot be relaxed without approval");
        FamilyBestFor.Text = T("Yönetilen Windows hesabı", "A managed Windows account");
        ModeBackButton.Content = BackText; ModeNextButton.Content = ContinueText;
        PersonalLevelTitle.Text = T("Ne kadar destek istersin?", "How much support do you want?"); PersonalLevelDescription.Text = T("Kişisel kullanımın davranışını seç.", "Choose how Personal mode should behave.");
        FlexibleTitle.Text = T("Esnek", "Flexible"); FlexibleText.Text = T("Manuel odak oturumlarını istediğin zaman başlat, duraklat ve bitir.", "Start, pause, and end manual focus sessions whenever you want.");
        BalancedTitle.Text = T("Dengeli · Önerilen", "Balanced · Recommended"); BalancedText.Text = T("Planını uygular; kural gevşetmelerinde bekleme süresi kullanır.", "Applies your plan and delays rule relaxations.");
        ProtectedLevelTitle.Text = T("Korumalı", "Protected"); ProtectedLevelText.Text = T("Dengeli davranışa Windows Guardian süreç korumasını ekler.", "Adds Windows Guardian process protection to Balanced behavior.");
        PersonalBackButton.Content = BackText; PersonalNextButton.Content = ContinueText;
        ScheduleTitle.Text = T("Haftalık planını oluştur", "Create your weekly plan");
        ScheduleDescription.Text = T("Her gün için kullanılabilecek saat aralığını ve toplam süreyi belirle.", "Set the allowed hours and total time for each day.");
        ScheduleEnabledHeader.Text = T("AÇIK", "ON"); ScheduleDayHeader.Text = T("GÜN", "DAY"); ScheduleStartHeader.Text = T("BAŞLANGIÇ", "START"); ScheduleEndHeader.Text = T("BİTİŞ", "END"); ScheduleLimitHeader.Text = T("LİMİT (DK)", "LIMIT (MIN)");
        ScheduleBackButton.Content = BackText; ScheduleNextButton.Content = ContinueText;
        RefreshScheduleLabels();
        PreferencesTitle.Text = T("Başlangıç ayarları", "Essentials"); PreferencesDescription.Text = T("İlk kullanım için temel tercihleri belirle.", "Set the essentials for first use.");
        DeviceNameLabel.Text = T("Cihaz adı", "Device name"); DailyLimitLabel.Text = T("Günlük süre", "Daily time");
        StartWithWindowsBox.Content = T("Windows ile başlat", "Start with Windows"); StartWithWindowsHint.Text = T("Oturum açınca Kvieta arka planda hazır olur.", "Kvieta is ready in the background after sign-in.");
        DesktopShortcutBox.Content = T("Masaüstü kısayolu oluştur", "Create a desktop shortcut"); DesktopShortcutHint.Text = T("Kvieta'ya masaüstünden hızlı erişim ekler.", "Adds quick desktop access to Kvieta.");
        TrackingBox.Content = T("Yerel uygulama ölçümünü etkinleştir", "Enable local app tracking"); TrackingHint.Text = T("Yalnız uygulama adı ve süre; pencere başlığı veya içerik yok.", "App name and duration only; no window titles or content.");
        PreferencesBackButton.Content = BackText; PreferencesNextButton.Content = ContinueText;
        PinTitle.Text = T("Yönetici PIN'i oluştur", "Create an administrator PIN"); PinDescription.Text = T("Korumalı ayarları ve yönetici çıkışını güvenceye almak için 4–8 rakam belirle.", "Choose 4–8 digits to secure protected settings and administrator exit."); PinLabel.Text = "PIN"; PinRepeatLabel.Text = T("PIN'i tekrar gir", "Repeat PIN"); PinBackButton.Content = BackText; PinNextButton.Content = ContinueText;
        string pinVisibilityText = T("PIN'i göster/gizle", "Show/hide PIN");
        PinVisibilityButton.ToolTip = pinVisibilityText;
        PinRepeatVisibilityButton.ToolTip = pinVisibilityText;
        System.Windows.Automation.AutomationProperties.SetName(PinVisibilityButton, pinVisibilityText);
        System.Windows.Automation.AutomationProperties.SetName(PinRepeatVisibilityButton, pinVisibilityText);
        RecoveryTitle.Text = T("Kurtarma kodlarını kaydet", "Save recovery codes");
        RecoveryDescription.Text = T("PIN'ini unutursan bu tek kullanımlık kodlardan biriyle kurtarma yapabilirsin. Kodlar tekrar gösterilmeyecek.", "If you forget your PIN, you can recover it with one of these one-time codes. The codes will not be shown again.");
        CopyRecoveryCodesButton.Content = T("Kodları kopyala", "Copy codes");
        SaveRecoveryCodesButton.Content = T("Dosyaya kaydet", "Save to file");
        RecoveryAcknowledgementBox.Content = T("Kodları güvenli ve cihazdan ayrı bir yere kaydettim", "I stored the codes securely and away from this device");
        PairManagerDeviceBox.Content = T("Kurulumdan sonra güvenilir telefonu eşleştir", "Pair a trusted phone after installation");
        PairManagerDeviceHint.Text = T("İsteğe bağlıdır; PIN kurtarma yetkisi daha sonra da eklenebilir.", "Optional; PIN recovery access can also be added later.");
        RecoveryBackButton.Content = BackText;
        RecoveryNextButton.Content = ContinueText;
        SummaryTitle.Text = T("Kuruluma hazır", "Ready to install"); SummaryDescription.Text = T("Seçimlerini kontrol et. Kur düğmesi yönetici izni isteyecek.", "Review your choices. Install will request administrator permission.");
        SummaryModeLabel.Text = T("Kullanım biçimi", "Usage mode"); SummaryDeviceLabel.Text = T("Cihaz", "Device"); SummaryLimitLabel.Text = T("Günlük süre", "Daily time"); SummaryOptionsLabel.Text = T("Seçenekler", "Options"); SummaryBackButton.Content = BackText; InstallButton.Content = T("Kvieta'yı kur", "Install Kvieta");
        InstallingEyebrow.Text = T("KVIETA KURULUYOR", "INSTALLING KVIETA"); InstallingTitle.Text = T("Her şeyi senin için hazırlıyoruz.", "We're preparing everything for you."); InstallingDescription.Text = T("Bu pencereyi kapatma. Windows yönetici izni isteyebilir.", "Keep this window open. Windows may request administrator permission.");
        ErrorTitle.Text = T("Kurulum tamamlanamadı.", "Setup could not be completed."); ErrorBackButton.Content = T("Geri dön", "Go back"); RetryButton.Content = T("Tekrar dene", "Try again");
        UpdateStepStatus();
        if (_page == WizardPage.Summary) PopulateSummary();
    }

    private void UpdateSelectionStyles()
    {
        TurkishCheck.Visibility = _languageChosen && _plan.Language == SetupLanguage.Turkish ? Visibility.Visible : Visibility.Collapsed;
        EnglishCheck.Visibility = _languageChosen && _plan.Language == SetupLanguage.English ? Visibility.Visible : Visibility.Collapsed;
        SetSelected(TurkishButton, _languageChosen && _plan.Language == SetupLanguage.Turkish);
        SetSelected(EnglishButton, _languageChosen && _plan.Language == SetupLanguage.English);
        SetSelected(InsightsButton, ModeNextButton.IsEnabled && _plan.Mode == UsageMode.Insights);
        SetSelected(PersonalButton, ModeNextButton.IsEnabled && _plan.Mode == UsageMode.Personal);
        SetSelected(FamilyButton, ModeNextButton.IsEnabled && _plan.Mode == UsageMode.Family);
        SetSelected(UnderstandTemplateButton, _plan.SelectedTemplate == SetupTemplate.UnderstandUsage);
        SetSelected(FocusTemplateButton, _plan.SelectedTemplate == SetupTemplate.Focus);
        SetSelected(GamingTemplateButton, _plan.SelectedTemplate == SetupTemplate.GamingRoutine);
        SetSelected(EveningTemplateButton, _plan.SelectedTemplate == SetupTemplate.EveningWindDown);
        SetSelected(FamilyTemplateButton, _plan.SelectedTemplate == SetupTemplate.FamilyRoutine);
        SetSelected(FlexibleButton, _plan.PersonalLevel == PersonalProtectionLevel.Flexible);
        SetSelected(BalancedButton, _plan.PersonalLevel == PersonalProtectionLevel.Balanced);
        SetSelected(ProtectedLevelButton, _plan.PersonalLevel == PersonalProtectionLevel.Protected);
    }

    private void RefreshScheduleLabels()
    {
        string[] turkishDays = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];
        string[] englishDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
        foreach (SetupScheduleDayRow row in _plan.Schedule)
        {
            int index = row.Day == DayOfWeek.Sunday ? 6 : (int)row.Day - 1;
            row.DayName = IsEnglish ? englishDays[index] : turkishDays[index];
            row.EnabledAutomationName = T($"{row.DayName} planını etkinleştir", $"Enable the plan for {row.DayName}");
            row.StartAutomationName = T($"{row.DayName} başlangıç saati", $"Start time for {row.DayName}");
            row.EndAutomationName = T($"{row.DayName} bitiş saati", $"End time for {row.DayName}");
            row.LimitAutomationName = T($"{row.DayName} günlük dakika limiti", $"Daily minute limit for {row.DayName}");
        }
        ScheduleDaysControl.Items.Refresh();
    }

    private void SetSelected(Button button, bool selected)
    {
        button.BorderBrush = (Brush)FindResource(selected ? "PrimaryBrush" : "BorderBrush");
        button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
    }

    private bool IsEnglish => _plan.Language == SetupLanguage.English;
    private string T(string turkish, string english) => IsEnglish ? english : turkish;
    private string ProductText(string key) => ProductTerminology.Get(key, _plan.Language == SetupLanguage.English
        ? LanguagePreference.English
        : LanguagePreference.Turkish);
    private string BackText => T("Geri", "Back");
    private string ContinueText => T("Devam et", "Continue");

    private string ModeName(UsageMode mode, PersonalProtectionLevel level) => mode switch
    {
        UsageMode.Insights => ProductText("InsightsModeShort"),
        UsageMode.Family => ProductText("FamilyModeShort"),
        _ => $"{ProductText("PersonalModeShort")} · {level switch
        {
            PersonalProtectionLevel.Flexible => T("Esnek", "Flexible"),
            PersonalProtectionLevel.Protected => T("Korumalı", "Protected"),
            _ => T("Dengeli", "Balanced")
        }}"
    };

    private static string FormatMinutes(int minutes, bool english) =>
        minutes % 60 == 0
            ? english ? $"{minutes / 60} hours" : $"{minutes / 60} saat"
            : english ? $"{minutes} minutes" : $"{minutes} dakika";

    private static string GetDisplayVersion() => FormatReleaseLabel(
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0-alpha");

    private static string FormatReleaseLabel(string releaseLabel)
    {
        Match alphaMatch = Regex.Match(
            releaseLabel,
            @"^(?:v?\d+\.\d+\.\d+-)?alpha[.-]?(?<number>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return alphaMatch.Success
            ? $"Alpha {alphaMatch.Groups["number"].Value}"
            : releaseLabel;
    }

    private static Version GetPackageVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : new Version(1, 0, 0);

    private static Version? ReadInstalledVersion()
    {
        return ReadInstallerValue("InstalledVersion") is string value && Version.TryParse(value, out Version? version)
            ? version
            : null;
    }

    private static object? ReadInstallerValue(string name)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"Software\Kvieta");
            return key?.GetValue(name);
        }
        catch { return null; }
    }

    private void RefreshExistingInstallationText()
    {
        InstalledVersionLabel.Text = T("KURULU", "INSTALLED");
        PackageVersionLabel.Text = T("BU PAKET", "THIS PACKAGE");
        InstalledVersionValue.Text = _installedReleaseLabel is { Length: > 0 } releaseLabel
            ? FormatReleaseLabel(releaseLabel)
            : _installedVersion?.ToString(3) ?? T("Ayarlar bulundu", "Settings found");
        PackageVersionValue.Text = GetDisplayVersion();
        ExistingVersionCard.Visibility = _openedForExistingInstallation ? Visibility.Visible : Visibility.Collapsed;

        switch (_packageAction)
        {
            case SetupPackageAction.Update:
                ExistingTitle.Text = T("Kvieta güncellemeye hazır.", "Kvieta is ready to update.");
                ExistingDescription.Text = T("Modun, kuralların, PIN'in ve kullanım geçmişin korunacak. Yalnız uygulama dosyaları güncellenecek.", "Your mode, rules, PIN, and usage history will be kept. Only application files will be updated.");
                KeepExistingTitle.Text = T("Kvieta'yı güncelle", "Update Kvieta");
                KeepExistingText.Text = T("Windows yönetici izninden sonra güncelleme doğrudan başlayacak.", "The update starts immediately after Windows administrator approval.");
                break;
            case SetupPackageAction.Repair:
                ExistingTitle.Text = T("Kvieta zaten bu bilgisayarda kurulu.", "Kvieta is already installed on this computer.");
                ExistingDescription.Text = T("Aynı alpha sürümünün daha yeni test paketini kurabilir veya uygulama dosyalarını onarabilirsin. Ayarların ve kullanım geçmişin korunur.", "Install a newer test build of the same alpha version or repair application files. Your settings and usage history are preserved.");
                KeepExistingTitle.Text = T("Güncelle / Onar", "Update / Repair");
                KeepExistingText.Text = T("Uygulama dosyalarını bu paketteki kopyalarla güvenli biçimde yenile.", "Safely refresh application files with the copies in this package.");
                break;
            case SetupPackageAction.DowngradeBlocked:
                ExistingTitle.Text = T("Daha yeni bir Kvieta sürümü zaten kurulu.", "A newer version of Kvieta is already installed.");
                ExistingDescription.Text = T("Bu eski paket mevcut kurulumu değiştiremez. Güncel veya daha yeni bir kurulum paketi kullan.", "This older package cannot change the current installation. Use a current or newer setup package.");
                KeepExistingTitle.Text = T("Eski sürüm engellendi", "Older version blocked");
                KeepExistingText.Text = T("Ayarların ve uygulama dosyaların değiştirilmedi.", "Your settings and application files were not changed.");
                break;
            default:
                ExistingTitle.Text = T("Bu cihazda Kvieta ayarları bulundu.", "Kvieta settings were found on this device.");
                ExistingDescription.Text = T("Kullanım geçmişin silinmez. Mevcut ayarlarla devam edebilir veya ayarları baştan yapılandırabilirsin.", "Your usage history will not be deleted. Keep your current settings or configure them again.");
                KeepExistingTitle.Text = T("Mevcut ayarlarla devam et", "Continue with existing settings");
                KeepExistingText.Text = T("Kurulumu yenile; modunu, kurallarını ve geçmişini koru.", "Refresh the installation while keeping your mode, rules, and history.");
                break;
        }

        ResetBrokenProtectionButton.Visibility = _brokenProtectedCredential
            ? Visibility.Visible
            : Visibility.Collapsed;
        ResetBrokenProtectionTitle.Text = T(
            "Bozuk korumayı sıfırla",
            "Reset broken protection");
        ResetBrokenProtectionText.Text = T(
            "Kurtarılamayan eski PIN ve Guardian kaydını temizle; kullanım geçmişini koru.",
            "Clear the unrecoverable old PIN and Guardian record while keeping usage history.");
        if (_brokenProtectedCredential)
        {
            ExistingTitle.Text = T(
                "Eski Guardian kimliği eksik.",
                "The old Guardian identity is missing.");
            ExistingDescription.Text = T(
                "Korunan ayarlar duruyor ancak PIN'i doğrulayacak gizli kayıt silinmiş. Normal onarım bu bilgiyi geri üretemez.",
                "Protected settings remain, but the secret record required to verify the PIN was deleted. A normal repair cannot recreate it.");
            ExistingSecurityNotice.Text = T(
                "Devam etmek için bozuk korumayı Windows yönetici izniyle sıfırlayıp yeni bir PIN belirle.",
                "To continue, reset the broken protection with Windows administrator permission and create a new PIN.");
        }

        KeepExistingButton.IsEnabled = _packageAction != SetupPackageAction.DowngradeBlocked && !_brokenProtectedCredential;
        ConfigureNewButton.IsEnabled = _packageAction != SetupPackageAction.DowngradeBlocked && !_brokenProtectedCredential;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) { if (!_installationInProgress) Close(); }

    private enum WizardPage
    {
        Language,
        Welcome,
        Existing,
        Mode,
        Personal,
        Schedule,
        Preferences,
        Pin,
        Recovery,
        Summary,
        Installing,
        Error
    }
}

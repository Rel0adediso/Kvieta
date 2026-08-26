using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.SetupApp;

public partial class SetupWindow : Window
{
    private static readonly string[] EmbeddedMsiNames = ["Otium.Payload.msi"];
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly SetupPlan _plan = new();
    private ControlSettings? _existingSettings;
    private bool _hasExistingSettings;
    private bool _existingPolicyLocked;
    private bool _installationInProgress;
    private bool _languageChosen;
    private WizardPage _page = WizardPage.Language;

    public SetupWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        DeviceNameBox.Text = Environment.MachineName;
        VersionText.Text = $"Otium · {GetDisplayVersion()}";
        RefreshLanguage();
        UpdateSelectionStyles();

        try
        {
            string protectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Otium",
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
                ? new ControlSettings { SetupCompleted = true, Mode = ControlMode.Protected }
                : null;
            _hasExistingSettings = _existingPolicyLocked;
        }

        ShowPage(WizardPage.Language);
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
        SetBrush("BackgroundBrush", light ? "#F5F2E8" : "#11130F");
        SetBrush("SurfaceBrush", light ? "#FFFCF4" : "#1A1D16");
        SetBrush("SurfaceHoverBrush", light ? "#F0ECDD" : "#23271D");
        SetBrush("BorderBrush", light ? "#D7D2C0" : "#343A2A");
        SetBrush("PrimaryBrush", light ? "#748044" : "#C7D18E");
        SetBrush("PrimarySoftBrush", light ? "#E5EACF" : "#303721");
        SetBrush("TextBrush", light ? "#202318" : "#F3F0E5");
        SetBrush("MutedTextBrush", light ? "#626757" : "#A9AD9D");
        SetBrush("SidebarBrush", light ? "#ECE9DC" : "#171A13");
        SetBrush("FaintTextBrush", light ? "#7D816F" : "#747A69");
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
    private void ExistingBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Welcome);

    private void KeepExisting_Click(object sender, RoutedEventArgs e)
    {
        _plan.ExistingChoice = SetupChoice.KeepExisting;
        PopulateSummary();
        ShowPage(WizardPage.Summary);
    }

    private void ConfigureNew_Click(object sender, RoutedEventArgs e)
    {
        if (_existingPolicyLocked || _existingSettings?.RequiresGuardian == true)
        {
            ExistingSecurityNotice.Text = T(
                "Bu ayarlar Guardian tarafından korunuyor. Kurulumu mevcut ayarlarla tamamla; mod değişikliğini Otium Kontrol Merkezi'nde doğrulama yaptıktan sonra gerçekleştir.",
                "These settings are protected by Guardian. Finish setup with existing settings, then change mode from the Otium Control Center after verification.");
            return;
        }

        _plan.ExistingChoice = SetupChoice.ConfigureNew;
        ExistingSecurityNotice.Text = string.Empty;
        ShowPage(WizardPage.Mode);
    }

    private void Awareness_Click(object sender, RoutedEventArgs e) => SelectMode(ControlMode.Awareness);
    private void Personal_Click(object sender, RoutedEventArgs e) => SelectMode(ControlMode.Personal);
    private void Protected_Click(object sender, RoutedEventArgs e) => SelectMode(ControlMode.Protected);

    private void SelectMode(ControlMode mode)
    {
        _plan.Mode = mode;
        ModeNextButton.IsEnabled = true;
        TrackingBox.IsChecked = true;
        TrackingBox.IsEnabled = mode != ControlMode.Awareness;
        UpdateSelectionStyles();
    }

    private void ModeBack_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_hasExistingSettings ? WizardPage.Existing : WizardPage.Welcome);

    private void ModeNext_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.Mode == ControlMode.Personal ? WizardPage.Personal : WizardPage.Preferences);

    private void Flexible_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Flexible);
    private void Balanced_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Balanced);
    private void Guarded_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Guarded);

    private void SelectPersonalLevel(PersonalProtectionLevel level)
    {
        _plan.PersonalLevel = level;
        UpdateSelectionStyles();
    }

    private void PersonalBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Mode);
    private void PersonalNext_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Preferences);
    private void PreferencesBack_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_plan.Mode == ControlMode.Personal ? WizardPage.Personal : WizardPage.Mode);

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
        string pin = PinBox.Password;
        string repeat = PinRepeatBox.Password;
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

    private void PinBack_Click(object sender, RoutedEventArgs e) => ShowPage(WizardPage.Preferences);

    private void PinNext_Click(object sender, RoutedEventArgs e)
    {
        _plan.AdminPin = PinBox.Password;
        PopulateSummary();
        ShowPage(WizardPage.Summary);
    }

    private void SummaryBack_Click(object sender, RoutedEventArgs e)
    {
        if (_plan.ExistingChoice == SetupChoice.KeepExisting && _hasExistingSettings)
        {
            ShowPage(WizardPage.Existing);
        }
        else
        {
            ShowPage(_plan.RequiresUserPin ? WizardPage.Pin : WizardPage.Preferences);
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
        string? stagingDirectory = null;

        try
        {
            ReadPreferences();
            ControlSettings settings = _plan.ComposeSettings(_existingSettings);
            CloseVisibleOtiumWindow();
            (string msiPath, string directory) = await ExtractInstallerAsync();
            stagingDirectory = directory;
            string logPath = Path.Combine(directory, "Otium-setup.log");
            int exitCode = await RunMsiAsync(msiPath, logPath);
            if (exitCode is not (0 or 1641 or 3010))
            {
                throw new InvalidOperationException(
                    T($"Windows Installer {exitCode} koduyla durdu. Tanılama: {logPath}",
                      $"Windows Installer stopped with code {exitCode}. Diagnostics: {logPath}"));
            }

            await _settingsStore.SaveAsync(settings);
            LaunchInstalledOtium(_plan.LaunchArguments);
            await Task.Delay(650);
            _installationInProgress = false;
            Close();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            ShowInstallError(T("Yönetici izni iptal edildi. Hiçbir ayar değiştirilmedi.",
                               "Administrator permission was cancelled. No settings were changed."));
        }
        catch (Exception exception)
        {
            ShowInstallError(exception.Message);
        }
        finally
        {
            _installationInProgress = false;
            CloseButton.IsEnabled = true;
            if (stagingDirectory is not null && Directory.Exists(stagingDirectory) && _page != WizardPage.Error)
            {
                TryDeleteDirectory(stagingDirectory);
            }
        }
    }

    private async Task<(string MsiPath, string Directory)> ExtractInstallerAsync()
    {
        string stagingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "OtiumSetupTemp",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        string msiPath = Path.Combine(stagingDirectory, "Otium.msi");

        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream? payload = EmbeddedMsiNames
            .Select(assembly.GetManifestResourceStream)
            .FirstOrDefault(stream => stream is not null);
        if (payload is not null)
        {
            await using (payload)
            await using (FileStream output = File.Create(msiPath))
            {
                await payload.CopyToAsync(output);
            }
            return (msiPath, stagingDirectory);
        }

        string? adjacentMsi = Directory.GetFiles(AppContext.BaseDirectory, "Otium-*-win-x64.msi")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (adjacentMsi is null)
        {
            throw new FileNotFoundException(T(
                "Kurulum paketi bu geliştirme önizlemesine gömülmemiş.",
                "The installer package is not embedded in this development preview."));
        }

        File.Copy(adjacentMsi, msiPath, overwrite: true);
        return (msiPath, stagingDirectory);
    }

    private async Task<int> RunMsiAsync(string msiPath, string logPath)
    {
        string features = _plan.DesktopShortcut
            ? "MainFeature,DesktopShortcutFeature"
            : "MainFeature";
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
            Arguments = $"/i \"{msiPath}\" /qn /norestart ADDLOCAL={features} /L*v \"{logPath}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(T("Windows Installer başlatılamadı.", "Windows Installer could not be started."));
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static void CloseVisibleOtiumWindow()
    {
        foreach (Process process in Process.GetProcessesByName("Otium"))
        {
            using (process)
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                process.CloseMainWindow();
                process.WaitForExit(3000);
                if (!process.HasExited)
                {
                    throw new InvalidOperationException(
                        "Otium is still open. Close the application and run setup again. / Otium hâlâ açık; uygulamayı kapatıp kurulumu yeniden çalıştır.");
                }
            }
        }
    }

    private static void LaunchInstalledOtium(string arguments)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Otium",
            "Otium.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Installed Otium executable was not found.", executable);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = true
        });
    }

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
    }

    private void PopulateSummary()
    {
        ReadPreferences();
        bool english = IsEnglish;
        if (_plan.ExistingChoice == SetupChoice.KeepExisting && _existingSettings is not null)
        {
            SummaryModeValue.Text = T("Mevcut ayarlar korunacak", "Existing settings will be kept");
            SummaryDeviceValue.Text = _existingSettings.DeviceName;
            SummaryLimitValue.Text = FormatMinutes(_existingSettings.DefaultDailyLimitMinutes, english);
            SummaryOptionsValue.Text = T("Yalnız uygulama dosyaları yenilenecek", "Only application files will be refreshed");
            return;
        }

        SummaryModeValue.Text = ModeName(_plan.Mode, _plan.PersonalLevel);
        SummaryDeviceValue.Text = _plan.DeviceName;
        SummaryLimitValue.Text = FormatMinutes(_plan.DailyLimitMinutes, english);
        List<string> options = [];
        if (_plan.StartWithWindows) options.Add(T("Windows ile başlangıç", "Start with Windows"));
        if (_plan.DesktopShortcut) options.Add(T("masaüstü kısayolu", "desktop shortcut"));
        if (_plan.AwarenessTracking || _plan.Mode == ControlMode.Awareness) options.Add(T("yerel ölçüm", "local tracking"));
        if (_plan.RequiresGuardian) options.Add("Guardian");
        SummaryOptionsValue.Text = options.Count == 0 ? T("Ek seçenek yok", "No optional features") : string.Join(" · ", options);
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
            PreferencesPanel, PinPanel, SummaryPanel, InstallingPanel, ErrorPanel
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
            WizardPage.Preferences => PreferencesPanel,
            WizardPage.Pin => PinPanel,
            WizardPage.Summary => SummaryPanel,
            WizardPage.Installing => InstallingPanel,
            _ => ErrorPanel
        };
        selected.Visibility = Visibility.Visible;
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
            WizardPage.Preferences => 4,
            WizardPage.Pin => 5,
            WizardPage.Summary => 6,
            WizardPage.Installing or WizardPage.Error => 7,
            _ => 1
        };
        StepProgress.Value = step;
        StepLabel.Text = T($"ADIM {step} / 7", $"STEP {step} / 7");
        (StepTitle.Text, StepDescription.Text) = _page switch
        {
            WizardPage.Language => (T("Dil seçimi", "Language"), T("Kurulum ve Otium aynı dilde devam eder.", "Setup and Otium continue in the same language.")),
            WizardPage.Welcome => (T("Otium'u tanı", "Meet Otium"), T("Ne kurulduğunu ve verilerin nasıl işlendiğini gör.", "See what is installed and how your data is handled.")),
            WizardPage.Existing => (T("Mevcut kurulum", "Existing setup"), T("Ayarlarını koru veya yeniden yapılandır.", "Keep or reconfigure your settings.")),
            WizardPage.Mode or WizardPage.Personal => (T("Kullanım biçimi", "Usage mode"), T("İhtiyacına uygun koruma düzeyini seç.", "Choose the protection level that fits you.")),
            WizardPage.Preferences => (T("Başlangıç ayarları", "Essentials"), T("Cihaz, süre ve başlangıç seçenekleri.", "Device, time, and startup options.")),
            WizardPage.Pin => (T("Yönetici güvenliği", "Administrator security"), T("Korumalı kullanım için PIN oluştur.", "Create a PIN for protected use.")),
            WizardPage.Summary => (T("Son kontrol", "Final review"), T("Kurulumdan önce seçimlerini doğrula.", "Confirm your choices before installation.")),
            WizardPage.Installing => (T("Kurulum", "Installation"), T("Otium ve Guardian hazırlanıyor.", "Otium and Guardian are being prepared.")),
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
        WelcomeTitle.Text = T("Otium'a hoş geldin.", "Welcome to Otium.");
        WelcomeDescription.Text = T("Otium ekran süreni cihazında ölçer, günlük ritmini görünür kılar ve istersen kendi koyduğun kurallara bağlı kalmana yardım eder. Nasıl davranacağını sen seçersin.", "Otium measures screen time on your device, makes your daily rhythm visible, and can help you stay with the rules you set. You choose how it behaves.");
        FeatureLocalTitle.Text = T("Yerel ölçüm", "Local tracking"); FeatureLocalText.Text = T("Uygulama adı ve kullanım süresi cihazında ölçülür. Hesap veya bulut gerekmez.", "App names and usage time are measured on your device. No account or cloud required.");
        FeatureChoiceTitle.Text = T("Üç kullanım biçimi", "Three usage modes"); FeatureChoiceText.Text = T("Yalnız takip et, kendi planına destek al veya kuralları Guardian ile koru.", "Track only, get support for your own plan, or protect rules with Guardian.");
        FeaturePrivateTitle.Text = T("İçerik kaydı yok", "No content capture"); FeaturePrivateText.Text = T("Pencere başlıklarını, yazdıklarını veya ekran görüntülerini hiçbir zaman saklamaz.", "Never stores window titles, what you type, or screenshots.");
        WelcomeBackButton.Content = BackText; WelcomeNextButton.Content = ContinueText;
        ExistingTitle.Text = T("Bu cihazda Otium ayarları bulundu.", "Otium settings were found on this device.");
        ExistingDescription.Text = T("Kullanım geçmişin silinmez. Mevcut ayarlarla devam edebilir veya ayarları baştan yapılandırabilirsin.", "Your usage history will not be deleted. Keep your current settings or configure them again.");
        KeepExistingTitle.Text = T("Mevcut ayarlarla devam et", "Continue with existing settings"); KeepExistingText.Text = T("Kurulumu yenile; modunu, kurallarını ve geçmişini koru.", "Refresh the installation while keeping your mode, rules, and history.");
        ConfigureNewTitle.Text = T("Ayarları baştan yapılandır", "Configure settings again"); ConfigureNewText.Text = T("Mod ve başlangıç ayarlarını yeniden seç; geçmiş verilerine dokunma.", "Choose mode and essentials again without touching usage history.");
        if (_existingPolicyLocked || _existingSettings?.RequiresGuardian == true)
        {
            ConfigureNewText.Text = T("Guardian koruması nedeniyle mod değişikliği Kontrol Merkezi'nde doğrulama gerektirir.", "Guardian protection requires Control Center verification for mode changes.");
        }
        ExistingBackButton.Content = BackText;
        ModeTitle.Text = T("Otium'u nasıl kullanacaksın?", "How will you use Otium?"); ModeDescription.Text = T("İhtiyacına en yakın biçimi seç; ayrıntıları daha sonra değiştirebilirsin.", "Choose the closest fit; you can change details later.");
        AwarenessTitle.Text = T("Sadece takip", "Tracking only"); AwarenessText.Text = T("Kısıtlama olmadan hangi uygulamayı ne kadar kullandığını gör.", "See which apps you use and for how long without restrictions.");
        PersonalTitle.Text = T("Kendim için", "For myself"); PersonalText.Text = T("Odaklan ve kendi koyduğun sınırlara seçtiğin güçte bağlı kal.", "Focus and stay with your own limits at the strength you choose.");
        ProtectedTitle.Text = T("Yönettiğim biri için", "For someone I manage"); ProtectedText.Text = T("Kuralları yönetici PIN'i ve Guardian ile koru.", "Protect rules with an administrator PIN and Guardian.");
        AwarenessDetails.Text = T("• Engel veya zorunlu mola yok\n• Başlangıç ritmi ve haftalık eğilim\n• Tüm veriler yalnız bu cihazda", "• No blocking or forced breaks\n• Baseline rhythm and weekly trends\n• All data stays on this device");
        AwarenessBestFor.Text = T("En hafif başlangıç", "The lightest way to start");
        PersonalDetails.Text = T("• Günlük plan, limit ve uygulama kuralları\n• Esnek, Dengeli veya Gözetimli düzey\n• Gevşetmeler seçtiğin süre kadar bekler", "• Daily plan, limits, and app rules\n• Flexible, Balanced, or Guarded level\n• Relaxations wait for your chosen delay");
        PersonalBestFor.Text = T("Kendi kararlarına destek", "Support for your own decisions");
        ProtectedDetails.Text = T("• Ayarlar ve çıkış yönetici PIN'iyle korunur\n• Guardian kapatılan oturumu yeniden açar\n• Yönetici onayı olmadan gevşetilemez", "• Settings and exit require the administrator PIN\n• Guardian reopens a closed session\n• Rules cannot be relaxed without approval");
        ProtectedBestFor.Text = T("Yönetilen Windows hesabı", "A managed Windows account");
        ModeBackButton.Content = BackText; ModeNextButton.Content = ContinueText;
        PersonalLevelTitle.Text = T("Ne kadar destek istersin?", "How much support do you want?"); PersonalLevelDescription.Text = T("Kendim için modunun davranışını seç.", "Choose how personal mode should behave.");
        FlexibleTitle.Text = T("Esnek", "Flexible"); FlexibleText.Text = T("Manuel odak oturumlarını istediğin zaman başlat, duraklat ve bitir.", "Start, pause, and end manual focus sessions whenever you want.");
        BalancedTitle.Text = T("Dengeli · Önerilen", "Balanced · Recommended"); BalancedText.Text = T("Planını uygular; kural gevşetmelerinde bekleme süresi kullanır.", "Applies your plan and delays rule relaxations.");
        GuardedTitle.Text = T("Gözetimli", "Guarded"); GuardedText.Text = T("Dengeli davranışa Windows Guardian süreç korumasını ekler.", "Adds Windows Guardian process protection to Balanced behavior.");
        PersonalBackButton.Content = BackText; PersonalNextButton.Content = ContinueText;
        PreferencesTitle.Text = T("Başlangıç ayarları", "Essentials"); PreferencesDescription.Text = T("İlk kullanım için temel tercihleri belirle.", "Set the essentials for first use.");
        DeviceNameLabel.Text = T("Cihaz adı", "Device name"); DailyLimitLabel.Text = T("Günlük süre", "Daily time");
        StartWithWindowsBox.Content = T("Windows ile başlat", "Start with Windows"); StartWithWindowsHint.Text = T("Oturum açınca Otium arka planda hazır olur.", "Otium is ready in the background after sign-in.");
        DesktopShortcutBox.Content = T("Masaüstü kısayolu oluştur", "Create a desktop shortcut"); DesktopShortcutHint.Text = T("Otium'a masaüstünden hızlı erişim ekler.", "Adds quick desktop access to Otium.");
        TrackingBox.Content = T("Yerel uygulama ölçümünü etkinleştir", "Enable local app tracking"); TrackingHint.Text = T("Yalnız uygulama adı ve süre; pencere başlığı veya içerik yok.", "App name and duration only; no window titles or content.");
        PreferencesBackButton.Content = BackText; PreferencesNextButton.Content = ContinueText;
        PinTitle.Text = T("Yönetici PIN'i oluştur", "Create an administrator PIN"); PinDescription.Text = T("Korumalı ayarları ve yönetici çıkışını güvenceye almak için 4–8 rakam belirle.", "Choose 4–8 digits to secure protected settings and administrator exit."); PinLabel.Text = "PIN"; PinRepeatLabel.Text = T("PIN'i tekrar gir", "Repeat PIN"); PinBackButton.Content = BackText; PinNextButton.Content = ContinueText;
        SummaryTitle.Text = T("Kuruluma hazır", "Ready to install"); SummaryDescription.Text = T("Seçimlerini kontrol et. Kur düğmesi yönetici izni isteyecek.", "Review your choices. Install will request administrator permission.");
        SummaryModeLabel.Text = T("Kullanım biçimi", "Usage mode"); SummaryDeviceLabel.Text = T("Cihaz", "Device"); SummaryLimitLabel.Text = T("Günlük süre", "Daily time"); SummaryOptionsLabel.Text = T("Seçenekler", "Options"); SummaryBackButton.Content = BackText; InstallButton.Content = T("Otium'u kur", "Install Otium");
        InstallingEyebrow.Text = T("OTIUM KURULUYOR", "INSTALLING OTIUM"); InstallingTitle.Text = T("Her şeyi senin için hazırlıyoruz.", "We're preparing everything for you."); InstallingDescription.Text = T("Bu pencereyi kapatma. Windows yönetici izni isteyebilir.", "Keep this window open. Windows may request administrator permission.");
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
        SetSelected(AwarenessButton, ModeNextButton.IsEnabled && _plan.Mode == ControlMode.Awareness);
        SetSelected(PersonalButton, ModeNextButton.IsEnabled && _plan.Mode == ControlMode.Personal);
        SetSelected(ProtectedButton, ModeNextButton.IsEnabled && _plan.Mode == ControlMode.Protected);
        SetSelected(FlexibleButton, _plan.PersonalLevel == PersonalProtectionLevel.Flexible);
        SetSelected(BalancedButton, _plan.PersonalLevel == PersonalProtectionLevel.Balanced);
        SetSelected(GuardedButton, _plan.PersonalLevel == PersonalProtectionLevel.Guarded);
    }

    private void SetSelected(Button button, bool selected)
    {
        button.BorderBrush = (Brush)FindResource(selected ? "PrimaryBrush" : "BorderBrush");
        button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
    }

    private bool IsEnglish => _plan.Language == SetupLanguage.English;
    private string T(string turkish, string english) => IsEnglish ? english : turkish;
    private string BackText => T("Geri", "Back");
    private string ContinueText => T("Devam et", "Continue");

    private string ModeName(ControlMode mode, PersonalProtectionLevel level) => mode switch
    {
        ControlMode.Awareness => T("Sadece takip", "Tracking only"),
        ControlMode.Protected => T("Yönettiğim biri için · Korumalı", "For someone I manage · Protected"),
        _ => $"{T("Kendim için", "For myself")} · {level switch
        {
            PersonalProtectionLevel.Flexible => T("Esnek", "Flexible"),
            PersonalProtectionLevel.Guarded => T("Gözetimli", "Guarded"),
            _ => T("Dengeli", "Balanced")
        }}"
    };

    private static string FormatMinutes(int minutes, bool english) =>
        minutes % 60 == 0
            ? english ? $"{minutes / 60} hours" : $"{minutes / 60} saat"
            : english ? $"{minutes} minutes" : $"{minutes} dakika";

    private static string GetDisplayVersion() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0-alpha";

    private static void TryDeleteDirectory(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch { }
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
        Preferences,
        Pin,
        Summary,
        Installing,
        Error
    }
}

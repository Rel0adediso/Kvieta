using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kvieta.App.ViewModels;
using Kvieta.App.Services;
using Microsoft.Win32;
using Kvieta.Core.Models;
using Kvieta.Core.Services;
using Forms = System.Windows.Forms;

namespace Kvieta.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly JsonFocusPreferencesStore _focusPreferencesStore = new();
    private readonly JsonRhythmPreferencesStore _rhythmPreferencesStore = new();
    private int _lastFocusDurationMinutes = 25;
    private string? _managementPin;
    private readonly DispatcherTimer _overviewTimer;
    private SessionSurfaceWindow? _backgroundSessionWindow;
    private bool _ownsBackgroundSessionWindow;
    private bool _sessionEventsAttached;
    private bool _allowCloseForUninstall;
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayIconImage;
    private TrayMenuWindow? _trayMenuWindow;
    private bool _isInitializing = true;
    private bool _applicationDetailsOpen;
    private IInputElement? _applicationDetailsPreviousFocus;
    private bool _sidebarAnimationRunning;
    private bool _baselineMilestoneShown;
    private bool _goalMilestoneShown;
    private bool _sessionSurfaceTransitionInProgress;
    private bool _openManagerDeviceOnLoad;
    private bool _protectionActionInProgress;

    public MainWindow(
        SessionSurfaceWindow? existingSessionWindow = null,
        string? managementPin = null,
        bool openManagerDeviceOnLoad = false)
    {
        InitializeComponent();
        _managementPin = managementPin;
        _backgroundSessionWindow = existingSessionWindow;
        _openManagerDeviceOnLoad = openManagerDeviceOnLoad;
        Title = $"Kvieta · {LocalizationService.Get("ControlCenter")}";
        DataContext = _viewModel;

        _overviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _overviewTimer.Tick += async (_, _) =>
        {
            ControlSettings rollbackSettings = _viewModel.CreateSettingsSnapshot();
            if (await _viewModel.ApplyPendingIfDueAsync())
            {
                if (!await EnsureGuardianForFamilyModeAsync(rollbackSettings))
                {
                    return;
                }

                try
                {
                    StartupRegistrationService.Apply(_viewModel.AppliedStartWithWindows);
                }
                catch (Exception exception)
                {
                    _viewModel.StatusMessage = $"Windows başlangıcı ayarlanamadı: {exception.Message}";
                }

                if (_viewModel.IsGuardianRequired)
                {
                    await CloseOwnedBackgroundSessionAsync();
                }
                else
                {
                    if (_backgroundSessionWindow is not null && !_ownsBackgroundSessionWindow)
                    {
                        await _backgroundSessionWindow.SwitchToUserSettingsStoreAsync();
                    }
                    EnsurePersonalBackgroundSession();
                }

                ResetSettingsScrollPosition();
            }

            await _viewModel.ReloadUsageAsync();
            _viewModel.RefreshOverview();
            RefreshProtectionStatus();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWindowAsync();
        }
        catch (Exception exception)
        {
            _isInitializing = false;
            try
            {
                await new SecurityAuditLog().AppendAsync(
                    "control-center.startup",
                    $"failed.{exception.GetType().Name}");
            }
            catch
            {
                // The diagnostic path must not hide the original startup failure.
            }

            System.Windows.MessageBox.Show(
                this,
                LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? $"The control center could not finish starting.\n\n{exception.Message}"
                    : $"Kontrol Merkezi başlatılamadı.\n\n{exception.Message}",
                "Kvieta · Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private async Task InitializeWindowAsync()
    {
        ConstrainToWorkArea();
        if (ActualWidth < 960)
        {
            _viewModel.IsSidebarExpanded = false;
            UpdateSidebarVisuals();
        }

        await _viewModel.InitializeAsync();
        try
        {
            FocusPreferences focusPreferences = await _focusPreferencesStore.LoadAsync();
            _lastFocusDurationMinutes = focusPreferences.LastDurationMinutes;
            RepeatLastDurationRun.Text = _lastFocusDurationMinutes.ToString();
        }
        catch
        {
            _lastFocusDurationMinutes = 25;
            RepeatLastDurationRun.Text = "25";
        }
        try
        {
            RhythmPreferences rhythmPreferences = await _rhythmPreferencesStore.LoadAsync();
            RhythmInsightCard.Visibility = rhythmPreferences.ShouldShowSuggestion(DateTimeOffset.UtcNow)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch
        {
            RhythmInsightCard.Visibility = Visibility.Visible;
        }
        if (!await EnsureGuardianForFamilyModeAsync(_viewModel.CreateSettingsSnapshot()))
        {
            _isInitializing = false;
            return;
        }
        MotionService.SetUserPreference(_viewModel.AnimationsEnabled);
        ((App)System.Windows.Application.Current).ThemeService.SetPreference(MainViewModel.FromDisplayTheme(_viewModel.ThemeMode));
        RefreshProtectionStatus();
        EnsurePersonalBackgroundSession();
        ResetSettingsScrollPosition();
        _overviewTimer.Start();
        _isInitializing = false;
        if (_openManagerDeviceOnLoad && _viewModel.IsFamilyMode)
        {
            _openManagerDeviceOnLoad = false;
            await TryOpenManagerDeviceAsync(startPairing: true);
        }
    }

    private async void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitializing || e.NewSize.Width >= 960 || !_viewModel.IsSidebarExpanded || _sidebarAnimationRunning)
        {
            return;
        }

        _sidebarAnimationRunning = true;
        try
        {
            await MotionService.AnimateColumnWidthAsync(SidebarColumn, 64);
            _viewModel.IsSidebarExpanded = false;
            UpdateSidebarVisuals();
        }
        finally
        {
            _sidebarAnimationRunning = false;
        }
    }

    private async void AwarenessTracking_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (!await _viewModel.SaveAsync() || !await SyncProtectedPolicyAsync())
        {
            return;
        }

        if (_backgroundSessionWindow is not null)
        {
            await _backgroundSessionWindow.ReloadSettingsAsync();
        }
    }

    private async void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_isInitializing)
        {
            await MotionService.FadeThemeAsync(
                RootFrame,
                () => ((App)System.Windows.Application.Current).ThemeService.SetPreference(
                    MainViewModel.FromDisplayTheme(_viewModel.ThemeMode)));
        }
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != MainTabs)
        {
            return;
        }

        int nextIndex = MainTabs.SelectedIndex;
        Dispatcher.BeginInvoke(() =>
        {
            if (MainTabs.SelectedContent is FrameworkElement selectedPage)
            {
                MotionService.Enter(selectedPage, 0, 3, 150);
            }

            if (nextIndex == 3)
            {
                ShowRhythmMilestoneIfNeeded();
            }
        }, DispatcherPriority.Loaded);
    }

    private void ShowRhythmMilestoneIfNeeded()
    {
        bool shouldCelebrate = (!_baselineMilestoneShown && _viewModel.IsRhythmBaselineReady) ||
            (!_goalMilestoneShown && _viewModel.IsRhythmGoalMet);
        _baselineMilestoneShown |= _viewModel.IsRhythmBaselineReady;
        _goalMilestoneShown |= _viewModel.IsRhythmGoalMet;
        if (!shouldCelebrate)
        {
            return;
        }

        System.Windows.Media.Color accent = FindResource("PrimaryBrush") is SolidColorBrush brush
            ? brush.Color
            : System.Windows.Media.Color.FromRgb(180, 188, 130);
        MotionService.Highlight(RhythmInsightCard, accent);
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.ChangeLanguage(MainViewModel.FromDisplayLanguage(_viewModel.LanguageMode));
        Title = $"Kvieta · {LocalizationService.Get("ControlCenter")}";
        SidebarToggle.ToolTip = _viewModel.IsSidebarExpanded
            ? LocalizationService.Get("CollapseMenu")
            : LocalizationService.Get("ExpandMenu");
        RefreshProtectionStatus();
    }

    private async void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (_sidebarAnimationRunning)
        {
            return;
        }

        _sidebarAnimationRunning = true;
        bool expand = !_viewModel.IsSidebarExpanded;
        try
        {
            if (expand)
            {
                _viewModel.IsSidebarExpanded = true;
                UpdateSidebarVisuals(updateWidth: false);
            }

            await MotionService.AnimateColumnWidthAsync(SidebarColumn, expand ? 184 : 64);
            if (!expand)
            {
                _viewModel.IsSidebarExpanded = false;
            }

            UpdateSidebarVisuals();
        }
        finally
        {
            _sidebarAnimationRunning = false;
        }
    }

    private void UpdateSidebarVisuals(bool updateWidth = true)
    {
        if (updateWidth)
        {
            SidebarColumn.Width = new GridLength(_viewModel.IsSidebarExpanded ? 184 : 64);
        }

        SidebarToggle.HorizontalAlignment = _viewModel.IsSidebarExpanded
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Center;
        SidebarToggle.Margin = _viewModel.IsSidebarExpanded
            ? new Thickness(0, 0, 10, 0)
            : new Thickness(0);
        SidebarToggle.ToolTip = _viewModel.IsSidebarExpanded
            ? LocalizationService.Get("CollapseMenu")
            : LocalizationService.Get("ExpandMenu");
    }

    private void HistoryDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: UsageHistoryDayRow day })
        {
            _viewModel.SelectHistoryDay(day);
            e.Handled = true;
        }
    }

    private void HistoryApplicationsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.HistoryAllApplications.Count > 0)
        {
            ShowApplicationDetails();
        }

        e.Handled = true;
    }

    private void ShowApplicationDetails()
    {
        _applicationDetailsPreviousFocus = Keyboard.FocusedElement;
        _applicationDetailsOpen = true;
        ApplicationDetailsOverlay.Visibility = Visibility.Visible;
        ApplicationDetailsOverlay.IsHitTestVisible = true;
        ApplicationDetailsOverlay.BeginAnimation(OpacityProperty, null);
        ApplicationDetailsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        Dispatcher.BeginInvoke(
            () => ApplicationDetailsCloseButton.Focus(),
            DispatcherPriority.Input);

        if (!MotionService.IsEnabled)
        {
            ApplicationDetailsOverlay.Opacity = 1;
            ApplicationDetailsTranslate.X = 0;
            return;
        }

        ApplicationDetailsOverlay.Opacity = 0;
        ApplicationDetailsTranslate.X = 520;
        ApplicationDetailsOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ApplicationDetailsTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(520, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void CloseApplicationDetails()
    {
        if (!_applicationDetailsOpen)
        {
            return;
        }

        _applicationDetailsOpen = false;
        if (!MotionService.IsEnabled)
        {
            ApplicationDetailsOverlay.Visibility = Visibility.Collapsed;
            ApplicationDetailsOverlay.IsHitTestVisible = false;
            RestoreApplicationDetailsFocus();
            return;
        }

        DoubleAnimation fade = new(0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            ApplicationDetailsOverlay.Visibility = Visibility.Collapsed;
            ApplicationDetailsOverlay.IsHitTestVisible = false;
            ApplicationDetailsOverlay.BeginAnimation(OpacityProperty, null);
            ApplicationDetailsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ApplicationDetailsOverlay.Opacity = 0;
            ApplicationDetailsTranslate.X = 520;
            RestoreApplicationDetailsFocus();
        };
        ApplicationDetailsOverlay.BeginAnimation(OpacityProperty, fade);
        ApplicationDetailsTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 520, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }

    private void ApplicationDetailsScrim_Click(object sender, MouseButtonEventArgs e) => CloseApplicationDetails();

    private void ApplicationDetailsClose_Click(object sender, RoutedEventArgs e) => CloseApplicationDetails();

    private void RestoreApplicationDetailsFocus()
    {
        if (_applicationDetailsPreviousFocus is { } previousFocus)
        {
            Keyboard.Focus(previousFocus);
        }

        _applicationDetailsPreviousFocus = null;
    }

    private void ConstrainToWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        MaxWidth = Math.Max(320, workArea.Width - 16);
        MaxHeight = Math.Max(240, workArea.Height - 16);
        MinWidth = Math.Min(MinWidth, MaxWidth);
        MinHeight = Math.Min(MinHeight, MaxHeight);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
#if KVIETA_DEVELOPMENT_BUILD
        if (e.Key == Key.F12 &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            ForceApplyPendingForTestingAsync();
            return;
        }
#endif

        if (e.Key == Key.Escape && _applicationDetailsOpen)
        {
            CloseApplicationDetails();
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void UninstallKvieta_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
        {
            System.Windows.MessageBox.Show(LocalizationService.Get("UninstallLaunchFailed"), LocalizationService.Get("UninstallTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (await app.HandleUninstallRequestAsync(this))
        {
            _allowCloseForUninstall = true;
            Close();
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ControlSettings rollbackSettings = _viewModel.CreateSettingsSnapshot();
        if (!await _viewModel.SaveAsync())
        {
            return;
        }

        if (!await EnsureGuardianForFamilyModeAsync(rollbackSettings))
        {
            return;
        }

        if (!await SyncProtectedPolicyAsync())
        {
            return;
        }

        try
        {
            StartupRegistrationService.Apply(_viewModel.AppliedStartWithWindows);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"Windows başlangıcı ayarlanamadı: {exception.Message}";
        }

        if (_viewModel.IsGuardianRequired)
        {
            await CloseOwnedBackgroundSessionAsync();
        }
        else if (_backgroundSessionWindow is not null && !_ownsBackgroundSessionWindow)
        {
            await _backgroundSessionWindow.SwitchToUserSettingsStoreAsync();
        }
        else if (_backgroundSessionWindow is not null)
        {
            await _backgroundSessionWindow.ReloadSettingsAsync();
        }

        RefreshProtectionStatus();
        EnsurePersonalBackgroundSession();

        SaveButton.IsEnabled = false;
        try
        {
            await MotionService.ShowSaveConfirmationAsync(SaveLabel, SaveDonePanel);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Animations_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        MotionService.SetUserPreference(_viewModel.AnimationsEnabled);
        if (_viewModel.AnimationsEnabled && sender is FrameworkElement toggle)
        {
            MotionService.Pulse(toggle);
        }
    }

    private void ReductionGoal_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing && IsLoaded)
        {
            MotionService.Pulse(RhythmInsightCard);
        }
    }

    private async void ExportUsage_Click(object sender, RoutedEventArgs e)
    {
        bool csv = sender is System.Windows.Controls.Button { Tag: "csv" };
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = LocalizationService.Get("ExportUsage"),
            FileName = $"kvieta-usage-{DateTime.Now:yyyy-MM-dd}",
            DefaultExt = csv ? ".csv" : ".json",
            Filter = csv ? "CSV (*.csv)|*.csv" : "JSON (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string content = csv
                ? await _viewModel.ExportUsageCsvAsync()
                : await _viewModel.ExportUsageJsonAsync();
            await System.IO.File.WriteAllTextAsync(dialog.FileName, content);
            _viewModel.StatusMessage = LocalizationService.Get("ExportCompleted");
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("ExportFailed")}: {exception.Message}";
        }
    }

    private async void ClearUsageHistory_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult confirmation = System.Windows.MessageBox.Show(
            LocalizationService.Get("ClearHistoryConfirmation"),
            LocalizationService.Get("ClearHistory"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _viewModel.ClearUsageHistoryAsync();
            if (_backgroundSessionWindow is not null)
            {
                await _backgroundSessionWindow.ReloadUsageAfterClearAsync();
            }
            _viewModel.RefreshOverview();
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("ClearHistoryFailed")}: {exception.Message}";
        }
    }

    private async void AdminPin_Click(object sender, RoutedEventArgs e)
    {
        string? authorizationPin = _managementPin;
        if (_viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                _viewModel.VerifyAdminPinAsync,
                RecoverAdminPinAsync);
            verification.Owner = this;
            if (verification.ShowDialog() != true)
            {
                return;
            }

            authorizationPin = verification.ResultPin;
            if (verification.CredentialWasRecovered)
            {
                _managementPin = authorizationPin;
                RefreshProtectionStatus();
                _viewModel.RefreshOverview();
                return;
            }
        }

        AdminPinWindow setup = AdminPinWindow.CreateSetup();
        setup.Owner = this;
        if (setup.ShowDialog() == true && setup.ResultPin is not null)
        {
            if (!await _viewModel.SetAdminPinAsync(setup.ResultPin))
            {
                return;
            }

            if (ProtectionServiceManager.GetState() == ProtectionServiceState.Running)
            {
                if (authorizationPin is null ||
                    !await ProtectionPolicyChannel.SyncAsync(_viewModel.ExportSettingsJson(), authorizationPin))
                {
                    await RestoreAuthoritativeProtectedPolicyAsync();
                    return;
                }
            }

            _managementPin = setup.ResultPin;
        }
    }

    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = LocalizationService.Get("ExportDiagnostics"),
            FileName = $"kvieta-diagnostics-{DateTime.Now:yyyy-MM-dd-HHmm}",
            DefaultExt = ".json",
            Filter = "JSON (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, await _viewModel.ExportDiagnosticsJsonAsync());
            _viewModel.StatusMessage = LocalizationService.Get("DiagnosticsExported");
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("DiagnosticsExportFailed")}: {exception.Message}";
        }
    }

    private async void RecoveryCodes_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasAdminPin)
        {
            _viewModel.StatusMessage = LocalizationService.Get("RecoveryCodesRequirePin");
            return;
        }

        AdminPinWindow verification = AdminPinWindow.CreateVerification(
            _viewModel.VerifyAdminPinAsync,
            RecoverAdminPinAsync);
        verification.Owner = this;
        if (verification.ShowDialog() != true || string.IsNullOrWhiteSpace(verification.ResultPin))
        {
            return;
        }

        if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.codes.generate"))
        {
            _viewModel.StatusMessage = LocalizationService.Get("WindowsAdminVerificationFailed");
            return;
        }

        IReadOnlyList<string> codes = await _viewModel.GenerateRecoveryCodesAsync();
        if (ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
            !await ProtectionPolicyChannel.SyncAsync(_viewModel.ExportSettingsJson(), verification.ResultPin))
        {
            await RestoreAuthoritativeProtectedPolicyAsync();
            return;
        }

        RecoveryCodesWindow window = new(codes) { Owner = this };
        window.ShowDialog();
        _viewModel.StatusMessage = LocalizationService.Get("RecoveryCodesGenerated");
    }

    private void ApplyRhythmSuggestion_Click(object sender, RoutedEventArgs e)
    {
        string? gentleGoal = _viewModel.ReductionGoalOptions.Skip(1).FirstOrDefault();
        if (gentleGoal is not null) _viewModel.ReductionGoal = gentleGoal;
        _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "A gentle rhythm goal is ready. Press Save to apply it."
            : "Nazik ritim hedefi hazır. Uygulamak için Kaydet'e bas.";
    }

    private async void RhythmSuggestionLater_Click(object sender, RoutedEventArgs e)
    {
        RhythmInsightCard.Visibility = Visibility.Collapsed;
        await _rhythmPreferencesStore.SaveAsync(
            RhythmSuggestionPreference.RemindLater,
            DateTimeOffset.UtcNow.AddDays(1));
        _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "The rhythm suggestion will return tomorrow."
            : "Ritim önerisi yarın yeniden görünecek.";
    }

    private async void HideRhythmSuggestion_Click(object sender, RoutedEventArgs e)
    {
        RhythmInsightCard.Visibility = Visibility.Collapsed;
        await _rhythmPreferencesStore.SaveAsync(RhythmSuggestionPreference.Hidden);
        _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "Rhythm suggestions are hidden on this device."
            : "Ritim önerileri bu cihazda gizlendi.";
    }

    private void ShareRhythm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Media.Imaging.BitmapSource card = RhythmShareCardRenderer.Create(
                LocalizationService.CurrentLanguage == LanguagePreference.English ? "My Weekly Rhythm" : "Haftalık Ritimim",
                _viewModel.RhythmStreakText,
                _viewModel.RhythmBestStreakText,
                _viewModel.RhythmWeekChangeText,
                _viewModel.RhythmFocusText,
                LocalizationService.CurrentLanguage == LanguagePreference.English);
            System.Windows.Clipboard.SetImage(card);
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Privacy-safe weekly rhythm card copied as an image."
                : "Gizlilik güvenli haftalık ritim kartı görsel olarak kopyalandı.";
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"The rhythm card could not be copied: {exception.Message}"
                : $"Ritim kartı kopyalanamadı: {exception.Message}";
        }
    }

    private async void ManagerDevice_Click(object sender, RoutedEventArgs e) =>
        await TryOpenManagerDeviceAsync();

    private async Task TryOpenManagerDeviceAsync(bool startPairing = false)
    {
        try
        {
            await OpenManagerDeviceAsync(startPairing);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"Phone pairing could not be opened: {exception.Message}"
                : $"Telefon eşleştirme açılamadı: {exception.Message}";
            System.Windows.MessageBox.Show(
                this,
                _viewModel.StatusMessage,
                LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? "Kvieta · Phone pairing"
                    : "Kvieta · Telefon eşleştirme",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task OpenManagerDeviceAsync(bool startPairing)
    {
        if (!_viewModel.HasAdminPin)
        {
            _viewModel.StatusMessage = LocalizationService.Get("RecoveryCodesRequirePin");
            return;
        }

        if (ProtectionServiceManager.GetState() != ProtectionServiceState.Running)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Guardian must be running to manage a trusted phone."
                : "Güvenilir telefonu yönetmek için Guardian çalışıyor olmalı.";
            return;
        }

        if (_viewModel.UnusedRecoveryCodeCount > 0 &&
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.Get("RecoveryCodesReplaceWarning"),
                LocalizationService.Get("RecoveryCodesReplaceTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        AdminPinWindow verification = AdminPinWindow.CreateVerification(
            _viewModel.VerifyAdminPinAsync,
            RecoverAdminPinAsync,
            LocalizationService.CurrentLanguage == LanguagePreference.English
                ? startPairing ? "Connect a trusted phone" : "Trusted phone settings"
                : startPairing ? "Güvenilir telefonunu bağla" : "Güvenilir telefon ayarları",
            LocalizationService.CurrentLanguage == LanguagePreference.English
                ? startPairing
                    ? "Enter the administrator PIN you created during setup. This optional step authorizes adding a phone that can help reset your PIN; you can cancel and do it later."
                    : "Enter the administrator PIN to view or change the trusted phone."
                : startPairing
                    ? "Kurulumda oluşturduğun yönetici PIN'ini gir. Bu isteğe bağlı adım, PIN sıfırlamaya yardımcı olacak telefonu yetkilendirir; iptal edip daha sonra da yapabilirsin."
                    : "Güvenilir telefonu görüntülemek veya değiştirmek için yönetici PIN'ini gir.");
        verification.Owner = this;
        if (verification.ShowDialog() != true || string.IsNullOrWhiteSpace(verification.ResultPin))
        {
            return;
        }

        string authorizationPin = verification.ResultPin;
        if (!await ProtectionPolicyChannel.BeginAdministrativeActivityAsync(authorizationPin))
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Guardian could not start the protected pairing session."
                : "Guardian korumalı eşleştirme oturumunu başlatamadı.";
            return;
        }

        try
        {
            ManagerDeviceWindow window = new(
                ManagerDeviceEnrollmentStore.Load(),
                async () =>
                {
                    if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.manager-device.revoke"))
                    {
                        return false;
                    }

                    return await ProtectionPolicyChannel.RevokeManagerDeviceAsync(authorizationPin);
                },
                async request =>
                {
                    if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.manager-device.transfer"))
                    {
                        return false;
                    }

                    return await ProtectionPolicyChannel.TransferManagerDeviceAsync(request, authorizationPin);
                },
                async owner =>
                {
                    if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.manager-device.enroll"))
                    {
                        return false;
                    }

                    try
                    {
                        ManagerDeviceApprovalWindow? approvalWindow = null;
                        TaskCompletionSource<bool> enrollmentResult = new(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        await using LocalManagerDeviceEnrollmentEndpoint endpoint =
                            LocalManagerDeviceEnrollmentEndpoint.Start(
                                async request =>
                                {
                                    bool accepted = await ProtectionPolicyChannel.EnrollManagerDeviceAsync(
                                        request,
                                        authorizationPin);
                                    enrollmentResult.TrySetResult(accepted);
                                    approvalWindow?.Complete(accepted);
                                    return accepted;
                                },
                                DateTimeOffset.UtcNow);
                        approvalWindow = new ManagerDeviceApprovalWindow(
                            endpoint.PairingUri,
                            endpoint.ExpiresAtUtc,
                            enrollment: true)
                        {
                            Owner = owner
                        };
                        endpoint.EnrollmentProposed += approvalWindow.ShowEnrollmentProposal;
                        approvalWindow.ConfigureEnrollmentConfirmation(endpoint.ConfirmPendingAsync);
                        bool acceptedByDialog = approvalWindow.ShowDialog() == true;
                        if (!acceptedByDialog) endpoint.RejectPending();
                        return acceptedByDialog ||
                            enrollmentResult.Task.IsCompletedSuccessfully && enrollmentResult.Task.Result;
                    }
                    catch (Exception exception)
                    {
                        System.Windows.MessageBox.Show(
                            owner,
                            LocalizationService.CurrentLanguage == LanguagePreference.English
                                ? $"Could not start local pairing server: {exception.Message}"
                                : $"Yerel eşleştirme sunucusu başlatılamadı: {exception.Message}",
                            "Kvieta",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                },
                startPairing)
            {
                Owner = this
            };
            if (window.ShowDialog() == true)
            {
                _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? "The manager device record was updated."
                    : "Yönetici cihazı kaydı güncellendi.";
            }
        }
        finally
        {
            await ProtectionPolicyChannel.EndAdministrativeActivityAsync(authorizationPin);
        }
    }

    private async void RestoreLastKnownGood_Click(object sender, RoutedEventArgs e)
    {
        string? authorizationPin = _managementPin;
        if (_viewModel.IsFamilyMode && _viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                _viewModel.VerifyAdminPinAsync,
                RecoverAdminPinAsync);
            verification.Owner = this;
            if (verification.ShowDialog() != true || string.IsNullOrWhiteSpace(verification.ResultPin)) return;
            authorizationPin = verification.ResultPin;
        }

        if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.last-known-good.restore"))
        {
            _viewModel.StatusMessage = LocalizationService.Get("WindowsAdminVerificationFailed");
            return;
        }

        try
        {
            await _viewModel.RestoreLastKnownGoodSettingsAsync();
            if (ProtectionServiceManager.GetState() == ProtectionServiceState.Running &&
                (string.IsNullOrWhiteSpace(authorizationPin) ||
                 !await ProtectionPolicyChannel.SyncAsync(_viewModel.ExportSettingsJson(), authorizationPin)))
            {
                await RestoreAuthoritativeProtectedPolicyAsync();
                return;
            }

            _managementPin = authorizationPin;
            _viewModel.StatusMessage = LocalizationService.Get("LastKnownGoodRestored");
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("LastKnownGoodRestoreFailed")}: {exception.Message}";
        }
    }

#if KVIETA_DEVELOPMENT_BUILD
    private async void ForceApplyPendingForTestingAsync()
    {
        ControlSettings rollbackSettings = _viewModel.CreateSettingsSnapshot();
        if (!await _viewModel.ForceApplyPendingForTestingAsync())
        {
            return;
        }

        if (!await EnsureGuardianForFamilyModeAsync(rollbackSettings))
        {
            return;
        }

        try
        {
            StartupRegistrationService.Apply(_viewModel.AppliedStartWithWindows);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"Windows başlangıcı ayarlanamadı: {exception.Message}";
        }

        if (_backgroundSessionWindow is not null)
        {
            await _backgroundSessionWindow.ReloadSettingsAsync();
        }

        EnsurePersonalBackgroundSession();
        RefreshProtectionStatus();
        _viewModel.RefreshOverview();
        ResetSettingsScrollPosition();
    }
#endif

    private void RecoveryCenter_Click(object sender, RoutedEventArgs e)
    {
        RecoveryCenterWindow window;
        try
        {
            ProtectionInstallationIdentity identity = ProtectionServiceManager.GetInstallationIdentity();
            ProtectionHealthReport health = ProtectionServiceManager.GetHealthReport();
            bool english = LocalizationService.CurrentLanguage == LanguagePreference.English;
            string release = identity.ReleaseLabel is { Length: > 0 } releaseLabel
                ? BuildInfo.ToDisplayReleaseName(releaseLabel)
                : identity.RegisteredVersion?.ToString(3) ?? "—";
            window = new RecoveryCenterWindow(new SystemHealthSnapshot(
                $"{BuildInfo.DisplayVersion} · {BuildInfo.DisplayRevision}",
                BuildInfo.IsDevelopmentBuild ? (english ? "Development/Test" : "Geliştirme/Test") : (english ? "Public release" : "Public sürüm"),
                release,
                identity.Compatibility == ProtectionVersionCompatibility.Compatible ? (english ? "Versions matched" : "Sürümler eşleşiyor") : (english ? "Needs attention" : "Kontrol gerekli"),
                identity.InstalledBinaryVersion?.ToString(3) ?? "—",
                health.IsHealthy ? (english ? "Healthy" : "Sağlıklı") : BuildProtectionHealthDetails(health),
                english ? "Settings + usage" : "Ayarlar + kullanım",
                _viewModel.LocalDataHealthText))
            {
                Owner = this
            };
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("RecoveryCenterOpenFailed")}: {exception.Message}";
            return;
        }

        if (window.ShowDialog() != true || window.SelectedAction is null)
        {
            return;
        }

        switch (window.SelectedAction.Value)
        {
            case RecoveryCenterAction.ExportDiagnostics:
                ExportDiagnostics_Click(sender, e);
                break;
            case RecoveryCenterAction.TrustCurrentClock:
                ResetClockProtection_Click(sender, e);
                break;
            case RecoveryCenterAction.RestoreSettings:
                RestoreLastKnownGood_Click(sender, e);
                break;
            case RecoveryCenterAction.RepairInstallation:
                RepairInstallation_Click(sender, e);
                break;
        }
    }

    private async void ResetClockProtection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsFamilyMode && _viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                _viewModel.VerifyAdminPinAsync,
                RecoverAdminPinAsync);
            verification.Owner = this;
            if (verification.ShowDialog() != true) return;
        }

        if (!await WindowsAdministratorVerificationService.RequestAsync("recovery.clock-anomaly.clear"))
        {
            _viewModel.StatusMessage = LocalizationService.Get("WindowsAdminVerificationFailed");
            return;
        }

        await _viewModel.ClearClockAnomalyAsync();
        if (_backgroundSessionWindow is not null)
        {
            await _backgroundSessionWindow.ReloadUsageAfterClearAsync();
        }
        string auditPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kvieta",
            "security-audit.jsonl");
        await new SecurityAuditLog(auditPath).AppendAsync("clock.anomaly", "admin-cleared");
        _viewModel.StatusMessage = LocalizationService.Get("ClockProtectionReset");
    }

    private async void RepairInstallation_Click(object sender, RoutedEventArgs e)
    {
        bool repaired = await WindowsAdministratorVerificationService.RequestAsync("recovery.installer.repair");
        if (!repaired)
        {
            _viewModel.StatusMessage = LocalizationService.Get("InstallationRepairFailed");
        }
        else
        {
            await _viewModel.MarkTodayRhythmExcusedAsync();
            _viewModel.StatusMessage = LocalizationService.Get("InstallationRepaired");
        }
        RefreshProtectionStatus();
    }

    private async Task<bool> SyncProtectedPolicyAsync(AdminCredential? guardedAuthorizationCredential = null)
    {
        if (!_viewModel.IsGuardianRequired ||
            ProtectionServiceManager.GetState() != ProtectionServiceState.Running)
        {
            return true;
        }

        if (guardedAuthorizationCredential is not null ||
            _viewModel.IsPersonalMode &&
                   _viewModel.PersonalProtectionLevel == PersonalProtectionLevel.Protected)
        {
            bool guardedSynced = await ProtectionPolicyChannel.SyncGuardedPersonalAsync(
                _viewModel.ExportSettingsJson(),
                guardedAuthorizationCredential ?? _viewModel.CreateSettingsSnapshot().AdminPin);
            if (!guardedSynced)
            {
                await RestoreAuthoritativeProtectedPolicyAsync();
            }

            return guardedSynced;
        }

        if (string.IsNullOrWhiteSpace(_managementPin))
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                _viewModel.VerifyAdminPinAsync,
                RecoverAdminPinAsync);
            verification.Owner = this;
            if (verification.ShowDialog() != true || string.IsNullOrWhiteSpace(verification.ResultPin))
            {
                return false;
            }

            _managementPin = verification.ResultPin;
        }

        bool synced = await ProtectionPolicyChannel.SyncAsync(_viewModel.ExportSettingsJson(), _managementPin);
        if (!synced)
        {
            await RestoreAuthoritativeProtectedPolicyAsync();
        }

        return synced;
    }

    private async Task RestoreAuthoritativeProtectedPolicyAsync()
    {
        try
        {
            ControlSettings authoritative = await new JsonSettingsStore(
                ProtectionServiceManager.ProtectedSettingsPath,
                readOnly: true).LoadAsync();
            await _viewModel.RestoreSettingsAsync(authoritative);
            ((App)System.Windows.Application.Current).ThemeService.SetPreference(
                MainViewModel.FromDisplayTheme(_viewModel.ThemeMode));
            ResetSettingsScrollPosition();
            _viewModel.StatusMessage = LocalizationService.Get("ProtectedPolicySyncFailed");
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"{LocalizationService.Get("ProtectedPolicySyncFailed")}: {exception.Message}";
        }
    }

    private async Task<string?> RecoverAdminPinAsync(Window owner)
    {
        string? newPin = await ((App)System.Windows.Application.Current)
            .RunPinRecoveryForCurrentPolicyAsync(owner);
        if (string.IsNullOrWhiteSpace(newPin))
        {
            return null;
        }

        JsonSettingsStore recoveredSettingsStore = File.Exists(ProtectionServiceManager.ProtectedSettingsPath)
            ? new JsonSettingsStore(ProtectionServiceManager.ProtectedSettingsPath, readOnly: true)
            : new JsonSettingsStore();
        ControlSettings recoveredSettings = await recoveredSettingsStore.LoadAsync();
        await _viewModel.RestoreSettingsAsync(recoveredSettings);
        _managementPin = newPin;
        RefreshProtectionStatus();
        _viewModel.RefreshOverview();
        return newPin;
    }

    private void ChangeMode_Click(object sender, RoutedEventArgs e)
    {
        ModeSelectionWindow selection = new(
            _viewModel.SelectedUsageMode,
            _viewModel.PersonalProtectionLevel)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        if (selection.ShowDialog() != true || selection.SelectedMode is null)
        {
            return;
        }

        UsageMode targetMode = selection.SelectedMode.Value;
        PersonalProtectionLevel targetPersonalLevel = selection.SelectedPersonalProtectionLevel;
        if (targetMode == _viewModel.SelectedUsageMode &&
            (targetMode != UsageMode.Personal || targetPersonalLevel == _viewModel.PersonalProtectionLevel))
        {
            return;
        }

        if (_viewModel.IsFamilyMode && _viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(
                _viewModel.VerifyAdminPinAsync,
                RecoverAdminPinAsync);
            verification.Owner = this;
            if (verification.ShowDialog() != true)
            {
                return;
            }
        }

        string? newPin = null;
        AdminCredential? newCredential = null;
        if (targetMode == UsageMode.Family && !_viewModel.IsFamilyMode)
        {
            AdminPinWindow setup = AdminPinWindow.CreateSetup();
            setup.Owner = this;
            if (setup.ShowDialog() != true || setup.ResultPin is null)
            {
                return;
            }

            newPin = setup.ResultPin;
            _managementPin = newPin;
        }
        else if (targetMode == UsageMode.Personal &&
                 targetPersonalLevel == PersonalProtectionLevel.Protected &&
                 !_viewModel.HasAdminPin)
        {
            newCredential = AdminPinService.CreateInternalCredential();
        }

        _viewModel.StageUsageMode(targetMode, targetPersonalLevel, newPin, newCredential);
        ResetSettingsScrollPosition();
    }

    private async void ProtectionAction_Click(object sender, RoutedEventArgs e)
    {
        if (_protectionActionInProgress)
        {
            return;
        }

        _protectionActionInProgress = true;
        ProtectionActionButton.IsEnabled = false;
        ProtectionActionButton.Content = LocalizationService.Get("ProtectionWorking");
        _viewModel.StatusMessage = LocalizationService.Get("ProtectionWorking");
        try
        {
            ProtectionHealthReport health = ProtectionServiceManager.GetHealthReport();
            bool repaired = true;
            if (ProtectionServiceManager.RequiresProductRepair(health))
            {
                repaired = await WindowsAdministratorVerificationService.RequestAsync(
                    "recovery.installer.repair");
            }

            bool started = repaired;
            health = ProtectionServiceManager.GetHealthReport();
            if (started && !health.IsHealthy)
            {
                started = await ProtectionServiceManager.RunElevatedInstallerAsync(install: true);
            }

            health = ProtectionServiceManager.GetHealthReport();
            if (started && health.IsHealthy)
            {
                _viewModel.StatusMessage = LocalizationService.Get("ProtectionReady");
                return;
            }

            _viewModel.StatusMessage = LocalizationService.Get("ProtectionInstallFailed");
            string message = string.Format(
                LocalizationService.Get("ProtectionActionFailedDescription"),
                BuildProtectionHealthDetails(health),
                ProtectionServiceManager.InstallLogPath);
            System.Windows.MessageBox.Show(
                this,
                message,
                LocalizationService.Get("ProtectionActionFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = LocalizationService.Get("ProtectionInstallFailed");
            System.Windows.MessageBox.Show(
                this,
                $"{LocalizationService.Get("ProtectionInstallFailed")}\n\n{exception.Message}",
                LocalizationService.Get("ProtectionActionFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _protectionActionInProgress = false;
            RefreshProtectionStatus();
        }
    }

    private void RefreshProtectionStatus()
    {
        if (ProtectionStatusText is null || ProtectionActionButton is null || ProtectionStatusDot is null)
        {
            return;
        }

        ProtectionHealthReport health = ProtectionServiceManager.GetHealthReport();
        string statusKey = health.IsHealthy
            ? "ProtectionActive"
            : health.ServiceState switch
            {
                ProtectionServiceState.Stopped => "ProtectionStopped",
                _ => "ProtectionInactive"
            };
        string actionKey = health.IsHealthy
            ? "ProtectionActive"
            : ProtectionServiceManager.RequiresProductRepair(health)
                ? "RepairProtection"
                : health.ServiceState == ProtectionServiceState.NotInstalled
                    ? "InstallProtection"
                    : "StartProtection";
        ProtectionStatusText.Text = LocalizationService.Get(statusKey);
        ProtectionStatusText.ToolTip = BuildProtectionHealthDetails(health);
        ProtectionActionButton.Content = LocalizationService.Get(actionKey);
        ProtectionActionButton.IsEnabled = !health.IsHealthy && !_protectionActionInProgress;
        ProtectionStatusDot.Background = (System.Windows.Media.Brush)FindResource(
            health.IsHealthy ? "SuccessBrush" : "WarningBrush");
    }

    private static string BuildProtectionHealthDetails(ProtectionHealthReport health)
    {
        if (health.IsHealthy)
        {
            return LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Guardian service, protected policy, version and session watchdog are healthy."
                : "Guardian servisi, korunan ayar, sürüm ve oturum bekçisi sağlıklı.";
        }

        Dictionary<ProtectionHealthIssue, (string Tr, string En)> labels = new()
        {
            [ProtectionHealthIssue.ServiceNotInstalled] = ("Guardian kurulu değil", "Guardian is not installed"),
            [ProtectionHealthIssue.ServiceStopped] = ("Guardian servisi çalışmıyor", "Guardian service is not running"),
            [ProtectionHealthIssue.ExecutableMissing] = ("Kurulu Kvieta dosyası eksik", "Installed Kvieta executable is missing"),
            [ProtectionHealthIssue.EnrollmentMissing] = ("Guardian kayıt bilgisi eksik", "Guardian enrollment is missing"),
            [ProtectionHealthIssue.ProtectedPolicyMissing] = ("Korunan ayar dosyası eksik", "Protected policy is missing"),
            [ProtectionHealthIssue.VersionMismatch] = ("Uygulama ve Guardian sürümleri farklı", "App and Guardian versions differ"),
            [ProtectionHealthIssue.VersionUnknown] = ("Guardian sürümü doğrulanamadı", "Guardian version could not be verified"),
            [ProtectionHealthIssue.StartupNotAutomatic] = ("Guardian otomatik başlamıyor", "Guardian is not configured for automatic start"),
            [ProtectionHealthIssue.GuardianSessionMissing] = ("Korunan oturum bekçisi çalışmıyor", "Protected session watchdog is not running")
        };
        bool english = LocalizationService.CurrentLanguage == LanguagePreference.English;
        return string.Join(Environment.NewLine, health.Issues.Select(issue => $"• {(english ? labels[issue].En : labels[issue].Tr)}"));
    }

    private async void CancelPending_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CancelPendingChangeAsync();
    }

    private void PendingHeader_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasPendingChange)
        {
            return;
        }

        _viewModel.SelectedPageIndex = 0;
        Dispatcher.BeginInvoke(() =>
        {
            PendingChangesCard.BringIntoView();

            System.Windows.Media.Color glowColor = FindResource("PrimaryBrush") is SolidColorBrush brush
                ? brush.Color
                : System.Windows.Media.Color.FromRgb(180, 188, 130);
            MotionService.Highlight(PendingChangesCard, glowColor);
        }, DispatcherPriority.Loaded);
    }

    private void AddTemporaryAllowance_Click(object sender, RoutedEventArgs e)
    {
        TemporaryAllowanceWindow dialog = new() { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            _viewModel.AddTemporaryAllowance(dialog.Result);
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Temporary allowance is ready to save"
                : "Geçici izin kaydedilmeye hazır";
        }
    }

    private void RemoveTemporaryAllowance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TemporaryAllowanceRow allowance })
        {
            _viewModel.RemoveTemporaryAllowance(allowance);
        }
    }

    private void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveSelectedApplication();
    }

    private void AddApplication_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = LocalizationService.Get("AddApplication"),
            Filter = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Applications (*.exe)|*.exe"
                : "Uygulamalar (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _viewModel.AddApplication(dialog.FileName);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"The application could not be added: {exception.Message}"
                : $"Uygulama eklenemedi: {exception.Message}";
        }
    }

    private void ApplicationMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox
            {
                DataContext: AppRuleRow rule,
                SelectedItem: AppRuleModeOption { IsRemove: true }
            })
        {
            _viewModel.RemoveApplication(rule);
        }
    }

    private async Task<bool> EnsureGuardianForFamilyModeAsync(ControlSettings rollbackSettings)
    {
        if (!_viewModel.IsGuardianRequired)
        {
            if (ProtectionServiceManager.GetState() != ProtectionServiceState.NotInstalled)
            {
                bool removed = await ProtectionServiceManager.RunElevatedInstallerAsync(install: false);
                RefreshProtectionStatus();
                if (!removed)
                {
                    _viewModel.StatusMessage = LocalizationService.Get("ProtectionRemoveFailed");
                }
            }

            return true;
        }

        if (ProtectionServiceManager.GetState() == ProtectionServiceState.Running)
        {
            return true;
        }

        bool installed = await ProtectionServiceManager.RunElevatedInstallerAsync(install: true);
        RefreshProtectionStatus();
        if (installed && ProtectionServiceManager.GetState() == ProtectionServiceState.Running)
        {
            _viewModel.StatusMessage = LocalizationService.Get("ProtectionActive");
            return true;
        }

        if (rollbackSettings.RequiresGuardian)
        {
            await _viewModel.MarkTodayRhythmExcusedAsync();
            bool returnsToProtectedSession = _backgroundSessionWindow is not null;
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? returnsToProtectedSession
                        ? "Guardian is temporarily unavailable. The control center will close and the protected session will remain active."
                        : "Guardian is unavailable. The protected control center will close instead of continuing without protection."
                    : returnsToProtectedSession
                        ? "Guardian geçici olarak kullanılamıyor. Kontrol Merkezi kapatılacak ve korumalı oturum açık kalacak."
                        : "Guardian kullanılamıyor. Korumalı Kontrol Merkezi korumasız devam etmek yerine kapatılacak.",
                LocalizationService.CurrentLanguage == LanguagePreference.English
                    ? "Kvieta · Protection required"
                    : "Kvieta · Koruma gerekli",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
            return false;
        }

        await _viewModel.RestoreSettingsAsync(rollbackSettings);
        await _viewModel.MarkTodayRhythmExcusedAsync();
        RefreshProtectionStatus();
        _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "Protected mode was not enabled because Guardian could not be installed."
            : "Guardian kurulamadığı için Korumalı mod etkinleştirilmedi.";
        return false;
    }

    private async void OpenSessionSurface_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionSurfaceTransitionInProgress)
        {
            return;
        }

        _sessionSurfaceTransitionInProgress = true;
        SessionScreenButton.IsEnabled = false;
        try
        {
            if (!await _viewModel.SaveAsync())
            {
                return;
            }

            EnsurePersonalBackgroundSession();
            if (_backgroundSessionWindow is not null)
            {
                ShowInTaskbar = false;
                Hide();
                _backgroundSessionWindow.ShowSessionSurface();
                return;
            }

            SessionSurfaceWindow sessionWindow = new();
            sessionWindow.Closed += async (_, _) =>
            {
                ShowInTaskbar = true;
                Show();
                Activate();
                await _viewModel.ReloadUsageAsync();
                _viewModel.RefreshOverview();
            };

            ShowInTaskbar = false;
            Hide();
            sessionWindow.Show();
        }
        finally
        {
            SessionScreenButton.IsEnabled = true;
            _sessionSurfaceTransitionInProgress = false;
        }
    }

    private void EnsurePersonalBackgroundSession()
    {
        if (_viewModel.IsGuardianRequired ||
            !_viewModel.IsPersonalMode && !_viewModel.IsInsightsMode)
        {
            return;
        }

        if (_backgroundSessionWindow is null)
        {
            _backgroundSessionWindow = new SessionSurfaceWindow(
                isDirectSession: !_viewModel.IsFlexiblePersonalMode,
                requirePinToExit: false,
                returnToControlCenter: true,
                startHidden: _viewModel.IsInsightsMode);
            _ownsBackgroundSessionWindow = true;
            AttachSessionEvents();
            _backgroundSessionWindow.Show();
            _backgroundSessionWindow.Hide();
        }
        else
        {
            _backgroundSessionWindow.EnableControlCenterReturn();
            AttachSessionEvents();
        }

        EnsureTrayIcon();
    }

    private void AttachSessionEvents()
    {
        if (_backgroundSessionWindow is null || _sessionEventsAttached)
        {
            return;
        }

        _backgroundSessionWindow.ControlCenterRequested += BackgroundSession_ControlCenterRequested;
        _sessionEventsAttached = true;
    }

    private async Task CloseOwnedBackgroundSessionAsync()
    {
        if (!_ownsBackgroundSessionWindow || _backgroundSessionWindow is null)
        {
            return;
        }

        SessionSurfaceWindow window = _backgroundSessionWindow;
        if (_sessionEventsAttached)
        {
            window.ControlCenterRequested -= BackgroundSession_ControlCenterRequested;
            _sessionEventsAttached = false;
        }

        _backgroundSessionWindow = null;
        _ownsBackgroundSessionWindow = false;
        await window.CloseFromControllerAsync();
    }

    private async void BackgroundSession_ControlCenterRequested(object? sender, ControlCenterRequestEventArgs e)
    {
        RestoreControlCenter();
        await _viewModel.InitializeAsync();
        _viewModel.RefreshOverview();
        if (e.OpenPlan && _viewModel.HasScheduledPlan) _viewModel.SelectedPageIndex = 1;
        ResetSettingsScrollPosition();
    }

    private void ResetSettingsScrollPosition()
    {
        Dispatcher.BeginInvoke(() => SettingsScrollViewer.ScrollToTop(), DispatcherPriority.Loaded);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowCloseForUninstall && _backgroundSessionWindow is not null)
        {
            e.Cancel = true;
            HideControlCenterToTray();
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        _overviewTimer.Stop();
        DisposeTrayIcon();
        if (_backgroundSessionWindow is null)
        {
            return;
        }

        if (_sessionEventsAttached)
        {
            _backgroundSessionWindow.ControlCenterRequested -= BackgroundSession_ControlCenterRequested;
            _sessionEventsAttached = false;
        }

        if (_ownsBackgroundSessionWindow)
        {
            await _backgroundSessionWindow.CloseFromControllerAsync();
        }

        _backgroundSessionWindow = null;
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIconImage = TryLoadApplicationIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Kvieta",
            Icon = _trayIconImage ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Right)
            {
                Dispatcher.Invoke(ShowTrayMenu);
            }
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreControlCenter);
    }

    private void ShowTrayMenu()
    {
        _trayMenuWindow?.Close();

        TrayMenuWindow menu = new(
            showSessionScreen: _viewModel.HasRestrictions,
            showQuickFocus: _viewModel.IsPersonalMode);
        _trayMenuWindow = menu;
        menu.ControlCenterRequested += (_, _) => RestoreControlCenter();
        menu.SessionScreenRequested += (_, _) =>
        {
            HideControlCenterToTray();
            _backgroundSessionWindow?.ShowSessionSurface();
        };
        menu.QuickFocusRequested += durationMinutes => _ = StartQuickFocusAsync(durationMinutes);
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenuWindow, menu))
            {
                _trayMenuWindow = null;
            }
        };
        menu.Show();
        menu.Activate();
    }

    private void HideControlCenterToTray()
    {
        EnsureTrayIcon();
        ShowInTaskbar = false;
        Hide();
        if (Owner is SessionSurfaceWindow)
        {
            Owner = null;
        }
        Topmost = false;
        if (SessionSurfaceRecoveryPolicy.ShouldResumeAfterControlCenterDismissal(
                _viewModel.IsFamilyMode))
        {
            _backgroundSessionWindow?.ResumeFromControlCenter();
        }
    }

    private async void StartQuickFocus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string durationText } ||
            !int.TryParse(durationText, out int durationMinutes) ||
            !_viewModel.IsPersonalMode)
        {
            return;
        }

        await StartQuickFocusAsync(durationMinutes);
    }

    private async void CustomFocus_Click(object sender, RoutedEventArgs e)
    {
        BonusTimeWindow selector = new(selectFocusDuration: true) { Owner = this };
        if (selector.ShowDialog() == true)
        {
            await StartQuickFocusAsync(selector.SelectedMinutes);
        }
    }

    private async void RepeatLastFocus_Click(object sender, RoutedEventArgs e) =>
        await StartQuickFocusAsync(_lastFocusDurationMinutes);

    private void CreateRuleMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasRestrictions)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Application rules are available in Personal and Family modes."
                : "Uygulama kuralları Kişisel ve Aile kullanımında kullanılabilir.";
            return;
        }

        if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null) return;
        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void UsageRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string modeName, DataContext: AppUsageHistoryRow application } ||
            !Enum.TryParse(modeName, out AppRuleMode mode)) return;

        string? existingRulePath = _viewModel.FindApplicationRulePath(application.Name);
        if (mode == AppRuleMode.Unlimited && string.IsNullOrWhiteSpace(existingRulePath))
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"{application.Name} is already unrestricted."
                : $"{application.Name} zaten sınırsız.";
            return;
        }

        string? executablePath = existingRulePath ??
            ApplicationIdentityService.TryResolveRunningExecutablePath(application.ApplicationId);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"Open {application.Name}, then try creating the rule again. Kvieta does not store executable paths in usage history."
                : $"{application.Name} uygulamasını açıp kuralı yeniden dene. Kvieta kullanım geçmişinde dosya yolu saklamaz.";
            return;
        }

        int dailyLimitMinutes = 60;
        if (mode == AppRuleMode.Limited)
        {
            BonusTimeWindow selector = new(selectAppLimit: true) { Owner = this };
            if (selector.ShowDialog() != true) return;
            dailyLimitMinutes = selector.SelectedMinutes;
        }

        try
        {
            _viewModel.AddApplication(executablePath, mode, dailyLimitMinutes);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"The application rule could not be created: {exception.Message}"
                : $"Uygulama kuralı oluşturulamadı: {exception.Message}";
        }
    }

    private async Task StartQuickFocusAsync(int durationMinutes)
    {
        if (_sessionSurfaceTransitionInProgress || !_viewModel.IsPersonalMode)
        {
            return;
        }

        _sessionSurfaceTransitionInProgress = true;
        try
        {
            if (!await _viewModel.SaveAsync())
            {
                return;
            }

            _lastFocusDurationMinutes = Math.Clamp(durationMinutes, 1, 24 * 60);
            RepeatLastDurationRun.Text = _lastFocusDurationMinutes.ToString();
            try
            {
                await _focusPreferencesStore.SaveLastDurationAsync(_lastFocusDurationMinutes);
            }
            catch
            {
                // A convenience preference must never prevent a focus session from starting.
            }

            await CloseOwnedBackgroundSessionAsync();
            SessionSurfaceWindow sessionWindow = new(
                isDirectSession: true,
                requirePinToExit: false,
                returnToControlCenter: true,
                viewModel: new SessionViewModel(focusDurationMinutes: durationMinutes));
            bool returningToControlCenter = false;

            sessionWindow.ControlCenterRequested += async (_, _) =>
            {
                if (returningToControlCenter)
                {
                    return;
                }

                returningToControlCenter = true;
                await sessionWindow.CloseFromControllerAsync();
            };
            sessionWindow.Closed += async (_, _) =>
            {
                ShowInTaskbar = true;
                Show();
                Activate();
                await _viewModel.ReloadUsageAsync();
                _viewModel.RefreshOverview();
                EnsurePersonalBackgroundSession();
            };

            ShowInTaskbar = false;
            Hide();
            sessionWindow.Show();
        }
        finally
        {
            _sessionSurfaceTransitionInProgress = false;
        }
    }

    private void RestoreControlCenter()
    {
        _backgroundSessionWindow?.EnableControlCenterReturn();
        ConfigureControlCenterForSession();
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ConfigureControlCenterForSession()
    {
        if (Owner is SessionSurfaceWindow)
        {
            Owner = null;
        }
        Topmost = false;
    }

    public void ActivateFromExternalRequest(bool openPlan = false)
    {
        RestoreControlCenter();
        if (openPlan && _viewModel.HasScheduledPlan) _viewModel.SelectedPageIndex = 1;
    }

    private void DisposeTrayIcon()
    {
        _trayMenuWindow?.Close();
        _trayMenuWindow = null;

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _trayIconImage?.Dispose();
        _trayIconImage = null;
    }

    private static System.Drawing.Icon? TryLoadApplicationIcon()
    {
        try
        {
            string? processPath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(processPath)
                ? null
                : System.Drawing.Icon.ExtractAssociatedIcon(processPath);
        }
        catch (Exception exception) when (exception is ArgumentException or FileNotFoundException)
        {
            return null;
        }
    }
}

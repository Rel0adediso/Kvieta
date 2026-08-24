using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using KardesKilidi.App.ViewModels;
using KardesKilidi.App.Services;
using Microsoft.Win32;
using KardesKilidi.Core.Models;
using Forms = System.Windows.Forms;

namespace KardesKilidi.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private string? _managementPin;
    private readonly DispatcherTimer _overviewTimer;
    private CafeWindow? _backgroundSessionWindow;
    private bool _ownsBackgroundSessionWindow;
    private bool _sessionEventsAttached;
    private Forms.NotifyIcon? _trayIcon;
    private TrayMenuWindow? _trayMenuWindow;

    public MainWindow(CafeWindow? existingSessionWindow = null, string? managementPin = null)
    {
        InitializeComponent();
        _managementPin = managementPin;
        _backgroundSessionWindow = existingSessionWindow;
        Title = $"Otium · {LocalizationService.Get("ControlCenter")}";
        DataContext = _viewModel;

        _overviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _overviewTimer.Tick += async (_, _) =>
        {
            if (await _viewModel.ApplyPendingIfDueAsync())
            {
                try
                {
                    StartupRegistrationService.Apply(_viewModel.AppliedStartWithWindows);
                }
                catch (Exception exception)
                {
                    _viewModel.StatusMessage = $"Windows başlangıcı ayarlanamadı: {exception.Message}";
                }
            }

            await _viewModel.ReloadUsageAsync();
            _viewModel.RefreshOverview();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        ((App)System.Windows.Application.Current).ThemeService.SetPreference(MainViewModel.FromDisplayTheme(_viewModel.ThemeMode));
        RefreshProtectionStatus();
        EnsurePersonalBackgroundSession();
        _overviewTimer.Start();
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ((App)System.Windows.Application.Current).ThemeService.SetPreference(MainViewModel.FromDisplayTheme(_viewModel.ThemeMode));
        }
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.ChangeLanguage(MainViewModel.FromDisplayLanguage(_viewModel.LanguageMode));
        Title = $"Otium · {LocalizationService.Get("ControlCenter")}";
        SidebarToggle.ToolTip = _viewModel.IsSidebarExpanded
            ? LocalizationService.Get("CollapseMenu")
            : LocalizationService.Get("ExpandMenu");
        RefreshProtectionStatus();
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsSidebarExpanded = !_viewModel.IsSidebarExpanded;
        SidebarColumn.Width = new GridLength(_viewModel.IsSidebarExpanded ? 184 : 64);
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

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!await _viewModel.SaveAsync())
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

        if (_backgroundSessionWindow is not null)
        {
            await _backgroundSessionWindow.ReloadSettingsAsync();
        }
    }

    private async void AdminPin_Click(object sender, RoutedEventArgs e)
    {
        string? authorizationPin = _managementPin;
        if (_viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(_viewModel.VerifyAdminPin);
            verification.Owner = this;
            if (verification.ShowDialog() != true)
            {
                return;
            }

            authorizationPin = verification.ResultPin;
        }

        AdminPinWindow setup = AdminPinWindow.CreateSetup();
        setup.Owner = this;
        if (setup.ShowDialog() == true && setup.ResultPin is not null)
        {
            await _viewModel.SetAdminPinAsync(setup.ResultPin);
            if (ProtectionServiceManager.GetState() == ProtectionServiceState.Running)
            {
                if (authorizationPin is null ||
                    !await ProtectionPolicyChannel.SyncAsync(_viewModel.ExportSettingsJson(), authorizationPin))
                {
                    _viewModel.StatusMessage = LocalizationService.Get("ProtectedPolicySyncFailed");
                    return;
                }
            }

            _managementPin = setup.ResultPin;
        }
    }

    private async Task<bool> SyncProtectedPolicyAsync()
    {
        if (!_viewModel.IsProtectedMode ||
            ProtectionServiceManager.GetState() != ProtectionServiceState.Running)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_managementPin))
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(_viewModel.VerifyAdminPin);
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
            _viewModel.StatusMessage = LocalizationService.Get("ProtectedPolicySyncFailed");
        }

        return synced;
    }

    private async void ChangeMode_Click(object sender, RoutedEventArgs e)
    {
        ModeSelectionWindow selection = new(_viewModel.SelectedControlMode)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        if (selection.ShowDialog() != true || selection.SelectedMode is null)
        {
            return;
        }

        ControlMode targetMode = selection.SelectedMode.Value;
        if (targetMode == _viewModel.SelectedControlMode)
        {
            return;
        }

        if (_viewModel.SelectedControlMode == ControlMode.Protected && _viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(_viewModel.VerifyAdminPin);
            verification.Owner = this;
            if (verification.ShowDialog() != true)
            {
                return;
            }
        }

        string? newPin = null;
        if (targetMode == ControlMode.Protected && !_viewModel.HasAdminPin)
        {
            AdminPinWindow setup = AdminPinWindow.CreateSetup();
            setup.Owner = this;
            if (setup.ShowDialog() != true || setup.ResultPin is null)
            {
                return;
            }

            newPin = setup.ResultPin;
        }

        if (targetMode == ControlMode.Personal &&
            ProtectionServiceManager.GetState() != ProtectionServiceState.NotInstalled)
        {
            if (!await ProtectionServiceManager.RunElevatedInstallerAsync(install: false))
            {
                _viewModel.StatusMessage = LocalizationService.Get("ProtectionRemoveFailed");
                RefreshProtectionStatus();
                return;
            }
        }

        await _viewModel.SetControlModeAsync(targetMode, newPin);
        RefreshProtectionStatus();
        EnsurePersonalBackgroundSession();
    }

    private async void ProtectionAction_Click(object sender, RoutedEventArgs e)
    {
        ProtectionServiceState state = ProtectionServiceManager.GetState();
        bool install = state != ProtectionServiceState.Running;

        if (!install && _viewModel.HasAdminPin)
        {
            AdminPinWindow verification = AdminPinWindow.CreateVerification(_viewModel.VerifyAdminPin);
            verification.Owner = this;
            if (verification.ShowDialog() != true)
            {
                return;
            }
        }

        ProtectionActionButton.IsEnabled = false;
        bool succeeded = await ProtectionServiceManager.RunElevatedInstallerAsync(install);
        ProtectionActionButton.IsEnabled = true;
        RefreshProtectionStatus();

        if (!succeeded)
        {
            _viewModel.StatusMessage = LocalizationService.Get(install
                ? "ProtectionInstallFailed"
                : "ProtectionRemoveFailed");
        }
    }

    private void RefreshProtectionStatus()
    {
        if (ProtectionStatusText is null || ProtectionActionButton is null || ProtectionStatusDot is null)
        {
            return;
        }

        ProtectionServiceState state = ProtectionServiceManager.GetState();
        string statusKey = state switch
        {
            ProtectionServiceState.Running => "ProtectionActive",
            ProtectionServiceState.Stopped => "ProtectionStopped",
            _ => "ProtectionInactive"
        };
        string actionKey = state switch
        {
            ProtectionServiceState.Running => "RemoveProtection",
            ProtectionServiceState.Stopped => "RepairProtection",
            _ => "InstallProtection"
        };
        ProtectionStatusText.Text = LocalizationService.Get(statusKey);
        ProtectionActionButton.Content = LocalizationService.Get(actionKey);
        ProtectionStatusDot.Background = (System.Windows.Media.Brush)FindResource(
            state == ProtectionServiceState.Running ? "SuccessBrush" : "FaintTextBrush");
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
            DropShadowEffect glow = new()
            {
                Color = glowColor,
                BlurRadius = 22,
                ShadowDepth = 0,
                Opacity = 0
            };
            PendingChangesCard.Effect = glow;

            DoubleAnimation pulse = new()
            {
                From = 0,
                To = 0.72,
                Duration = TimeSpan.FromMilliseconds(260),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                FillBehavior = FillBehavior.Stop
            };
            pulse.Completed += (_, _) => PendingChangesCard.Effect = null;
            glow.BeginAnimation(DropShadowEffect.OpacityProperty, pulse);
        }, DispatcherPriority.Loaded);
    }

    private void AddApplication_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = LocalizationService.CurrentLanguage == KardesKilidi.Core.Models.LanguagePreference.English
                ? "Choose an application"
                : "Kısıtlanacak uygulamayı seç",
            Filter = LocalizationService.CurrentLanguage == KardesKilidi.Core.Models.LanguagePreference.English
                ? "Windows applications (*.exe)|*.exe"
                : "Windows uygulamaları (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.AddApplication(dialog.FileName);
        }
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

    private async void OpenCafeMode_Click(object sender, RoutedEventArgs e)
    {
        if (!await _viewModel.SaveAsync())
        {
            return;
        }

        if (_backgroundSessionWindow is not null)
        {
            Hide();
            _backgroundSessionWindow.ShowSessionSurface();
            return;
        }

        CafeWindow cafeWindow = new();
        cafeWindow.Closed += async (_, _) =>
        {
            Show();
            Activate();
            await _viewModel.ReloadUsageAsync();
            _viewModel.RefreshOverview();
        };

        Hide();
        cafeWindow.Show();
    }

    private void EnsurePersonalBackgroundSession()
    {
        if (!_viewModel.IsPersonalMode)
        {
            return;
        }

        if (_backgroundSessionWindow is null)
        {
            _backgroundSessionWindow = new CafeWindow(
                isDirectSession: true,
                requirePinToExit: false,
                returnToControlCenter: true);
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

    private async void BackgroundSession_ControlCenterRequested(object? sender, EventArgs e)
    {
        RestoreControlCenter();
        await _viewModel.InitializeAsync();
        _viewModel.RefreshOverview();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.IsPersonalMode && _backgroundSessionWindow is not null)
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

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Otium",
            Icon = System.Drawing.SystemIcons.Application,
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

        TrayMenuWindow menu = new();
        _trayMenuWindow = menu;
        menu.ControlCenterRequested += (_, _) => RestoreControlCenter();
        menu.SessionScreenRequested += (_, _) =>
        {
            HideControlCenterToTray();
            _backgroundSessionWindow?.ShowSessionSurface();
        };
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
    }

    private void RestoreControlCenter()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ActivateFromExternalRequest()
    {
        RestoreControlCenter();
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
    }
}

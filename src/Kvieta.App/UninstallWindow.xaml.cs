using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Kvieta.App.Services;

namespace Kvieta.App;

public partial class UninstallWindow : Window
{
    private readonly string? _productCode;
    private readonly string? _localDataPath;
    private readonly string? _userSid;
    private readonly bool _removeLocalData;
    private bool _isWorking;

    public UninstallWindow()
    {
        InitializeComponent();
    }

    public UninstallWindow(string productCode, bool removeLocalData, string localDataPath, string userSid)
        : this()
    {
        _productCode = productCode;
        _removeLocalData = removeLocalData;
        _localDataPath = localDataPath;
        _userSid = userSid;
        ChoicePanel.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Visible;
        _isWorking = true;
        TitleCloseButton.IsEnabled = false;
        Loaded += WorkerWindow_Loaded;
        Closing += WorkerWindow_Closing;
    }

    public bool RemoveLocalData => RemoveDataBox.IsChecked == true;

    private void RemoveData_Changed(object sender, RoutedEventArgs e)
    {
        ConfirmButton.Content = LocalizationService.Get(
            RemoveLocalData ? "UninstallConfirmWithDataAction" : "UninstallConfirmAction");
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!_isWorking)
        {
            Close();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void WorkerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            int exitCode = await Task.Run(() => ProtectionServiceManager.RunProductUninstall(_productCode!));
            if (exitCode is 0 or 1641 or 3010)
            {
                UninstallCleanupResult cleanup = await Task.Run(() =>
                    UninstallDataCleaner.Clean(_localDataPath!, _userSid!, _removeLocalData));
                ShowSuccess(cleanup);
                return;
            }

            if (exitCode == 1602)
            {
                ShowFinished(
                    LocalizationService.Get("UninstallCancelledTitle"),
                    LocalizationService.Get("UninstallCancelledDescription"),
                    success: false);
                return;
            }

            ShowFinished(
                LocalizationService.Get("UninstallFailedTitle"),
                string.Format(LocalizationService.Get("UninstallFailedDescription"), exitCode),
                success: false);
        }
        catch (Exception exception)
        {
            ShowFinished(
                LocalizationService.Get("UninstallFailedTitle"),
                $"{LocalizationService.Get("UninstallLaunchFailed")}\n\n{exception.Message}",
                success: false);
        }
    }

    private void ShowSuccess(UninstallCleanupResult cleanup)
    {
        if (!cleanup.Succeeded)
        {
            ShowFinished(
                LocalizationService.Get("UninstallRemovedWithWarningTitle"),
                LocalizationService.Get("UninstallPartialCleanup"),
                success: false);
            return;
        }

        string description = _removeLocalData
            ? LocalizationService.Get("UninstallDataRemoved")
            : LocalizationService.Get("UninstallDataPreserved");
        ShowFinished(LocalizationService.Get("UninstallSuccessTitle"), description, success: true);
    }

    private void ShowFinished(string title, string description, bool success)
    {
        _isWorking = false;
        TitleCloseButton.IsEnabled = true;
        StatusTitle.Text = title;
        StatusDescription.Text = description;
        UninstallProgress.Visibility = Visibility.Collapsed;
        DoneButton.Visibility = Visibility.Visible;
        SuccessMark.Visibility = success ? Visibility.Visible : Visibility.Collapsed;
        FailureMark.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
        StatusIconSurface.Fill = (System.Windows.Media.Brush)FindResource(success ? "SuccessSoftBrush" : "WarningSoftBrush");
        AutomationProperties.SetName(StatusPanel, $"{title}. {description}");
        DoneButton.Focus();
    }

    private void WorkerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isWorking)
        {
            e.Cancel = true;
        }
    }
}

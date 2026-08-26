using System.Windows;
using System.Windows.Input;

namespace Otium.App;

public enum RecoveryCenterAction
{
    ExportDiagnostics,
    TrustCurrentClock,
    RestoreSettings,
    RepairInstallation
}

public sealed record SystemHealthSnapshot(
    string Application,
    string ApplicationState,
    string Installer,
    string InstallerState,
    string Guardian,
    string GuardianState,
    string LocalData,
    string LocalDataState);

public partial class RecoveryCenterWindow : Window
{
    public RecoveryCenterWindow(SystemHealthSnapshot snapshot)
    {
        InitializeComponent();
        ApplicationHealthValue.Text = snapshot.Application;
        ApplicationHealthState.Text = snapshot.ApplicationState;
        InstallerHealthValue.Text = snapshot.Installer;
        InstallerHealthState.Text = snapshot.InstallerState;
        GuardianHealthValue.Text = snapshot.Guardian;
        GuardianHealthState.Text = snapshot.GuardianState;
        DataHealthValue.Text = snapshot.LocalData;
        DataHealthState.Text = snapshot.LocalDataState;

        bool english = Otium.App.Services.LocalizationService.CurrentLanguage == Otium.Core.Models.LanguagePreference.English;
        ExportReportButton.Content = english ? "Diagnostics report" : "Tanılama raporu";
        ApplicationHealthLabel.Text = english ? "APPLICATION" : "UYGULAMA";
        InstallerHealthLabel.Text = "INSTALLER";
        GuardianHealthLabel.Text = "GUARDIAN";
        DataHealthLabel.Text = english ? "LOCAL DATA" : "YEREL VERİ";
        Loaded += (_, _) => FitToWorkingArea();
    }

    public RecoveryCenterAction? SelectedAction { get; private set; }

    private void FitToWorkingArea()
    {
        System.Drawing.Rectangle area = System.Windows.Forms.Screen.FromHandle(
            new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        MaxWidth = Math.Max(MinWidth, area.Width);
        MaxHeight = Math.Max(MinHeight, area.Height);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    private void TrustClock_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.TrustCurrentClock);
    private void RestoreSettings_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.RestoreSettings);
    private void RepairInstallation_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.RepairInstallation);
    private void ExportReport_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.ExportDiagnostics);

    private void Select(RecoveryCenterAction action)
    {
        SelectedAction = action;
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}

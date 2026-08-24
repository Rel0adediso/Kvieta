using System.Windows;
using System.Windows.Input;

namespace Otium.App;

public enum RecoveryCenterAction
{
    TrustCurrentClock,
    RestoreSettings,
    RepairInstallation
}

public partial class RecoveryCenterWindow : Window
{
    public RecoveryCenterWindow()
    {
        InitializeComponent();
    }

    public RecoveryCenterAction? SelectedAction { get; private set; }

    private void TrustClock_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.TrustCurrentClock);
    private void RestoreSettings_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.RestoreSettings);
    private void RepairInstallation_Click(object sender, RoutedEventArgs e) => Select(RecoveryCenterAction.RepairInstallation);

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

using System.Windows;
using System.Windows.Input;

namespace KardesKilidi.App;

public partial class BonusTimeWindow : Window
{
    public BonusTimeWindow()
    {
        InitializeComponent();
        string unit = Services.LocalizationService.Get("MinuteShort");
        Minutes15.Content = $"+15 {unit}";
        Minutes30.Content = $"+30 {unit}";
        Minutes60.Content = $"+60 {unit}";
    }
    public int SelectedMinutes { get; private set; }
    private void Select_Click(object sender, RoutedEventArgs e) { SelectedMinutes = int.Parse((string)((System.Windows.Controls.Button)sender).Tag); DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
}

using System.Windows;
using System.Windows.Input;
using Kvieta.App.Services;
using Kvieta.Core.Models;

namespace Kvieta.App;

public partial class BonusTimeWindow : Window
{
    public BonusTimeWindow()
    {
        InitializeComponent();
        string unit = Services.LocalizationService.Get("MinuteShort");
        Minutes15.Content = $"+15 {unit}";
        Minutes30.Content = $"+30 {unit}";
        Minutes60.Content = $"+60 {unit}";
        CustomMinutesButton.Content = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "Grant time"
            : "Süreyi ver";
    }
    public int SelectedMinutes { get; private set; }
    private void Select_Click(object sender, RoutedEventArgs e)
    {
        SelectedMinutes = int.Parse((string)((System.Windows.Controls.Button)sender).Tag);
        DialogResult = true;
    }

    private void CustomMinutes_Click(object sender, RoutedEventArgs e) => ApplyCustomMinutes();

    private void CustomMinutesInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        ApplyCustomMinutes();
    }

    private void ApplyCustomMinutes()
    {
        if (!int.TryParse(CustomMinutesInput.Text.Trim(), out int minutes) || minutes is < 1 or > 1440)
        {
            ErrorText.Text = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Enter a value from 1 to 1440 minutes."
                : "1 ile 1440 dakika arasında bir değer gir.";
            CustomMinutesInput.Focus();
            CustomMinutesInput.SelectAll();
            return;
        }

        SelectedMinutes = minutes;
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
}

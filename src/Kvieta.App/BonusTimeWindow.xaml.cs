using System.Windows;
using System.Windows.Input;
using Kvieta.App.Services;
using Kvieta.Core.Models;

namespace Kvieta.App;

public partial class BonusTimeWindow : Window
{
    public BonusTimeWindow(bool selectFocusDuration = false, bool selectAppLimit = false)
    {
        InitializeComponent();
        string unit = Services.LocalizationService.Get("MinuteShort");
        string prefix = selectFocusDuration ? string.Empty : "+";
        Minutes15.Content = $"{prefix}15 {unit}";
        Minutes30.Content = $"{prefix}30 {unit}";
        Minutes60.Content = $"{prefix}60 {unit}";
        string titleKey = selectAppLimit ? "DailyAppLimit" : selectFocusDuration ? "CustomFocusTitle" : "GrantExtraTime";
        string descriptionKey = selectAppLimit ? "DailyAppLimitDescription" : selectFocusDuration ? "CustomFocusDescription" : "GrantExtraTimeDescription";
        string actionKey = selectAppLimit ? "CreateRule" : selectFocusDuration ? "StartFocus" : "GrantTime";
        TitleText.Text = LocalizationService.Get(titleKey);
        DescriptionText.Text = LocalizationService.Get(descriptionKey);
        CustomMinutesButton.Content = LocalizationService.Get(actionKey);
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

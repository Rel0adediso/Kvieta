using System.Globalization;
using System.Windows;
using System.Windows.Input;
using KardesKilidi.App.Services;
using KardesKilidi.Core.Models;

namespace KardesKilidi.App;

public partial class TemporaryAllowanceWindow : Window
{
    public TemporaryAllowanceWindow()
    {
        InitializeComponent();
        DateInput.SelectedDate = DateTime.Today.AddDays(1);
    }

    public TemporaryAllowance? Result { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DateInput.SelectedDate is not DateTime date || date.Date < DateTime.Today ||
            !TimeOnly.TryParseExact(StartInput.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly from) ||
            !TimeOnly.TryParseExact(EndInput.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly until) ||
            !int.TryParse(MinutesInput.Text.Trim(), out int minutes) || minutes is < 1 or > 1440)
        {
            ErrorText.Text = LocalizationService.CurrentLanguage == LanguagePreference.English
                ? "Choose a valid date, use HH:mm for hours, and enter 1–1440 minutes."
                : "Geçerli bir tarih seç; saatleri HH:mm, ek süreyi 1–1440 dakika olarak gir.";
            return;
        }

        Result = new TemporaryAllowance
        {
            Date = DateOnly.FromDateTime(date),
            AllowedFrom = from,
            AllowedUntil = until,
            BonusMinutes = minutes,
            Note = NoteInput.Text.Trim()
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
}

using System.Windows;
using Otium.App.Services;
using Otium.Core.Services;

namespace Otium.App;

public partial class RecoveryResetWindow : Window
{
    private readonly Func<string, string, Task<bool>> _reset;

    public RecoveryResetWindow(Func<string, string, Task<bool>> reset)
    {
        InitializeComponent();
        _reset = reset ?? throw new ArgumentNullException(nameof(reset));
        Loaded += (_, _) => CodeBox.Focus();
    }

    public string? ResultPin { get; private set; }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        string pin = PinBox.Password;
        if (!AdminPinService.IsValidFormat(pin))
        {
            ErrorText.Text = LocalizationService.Get("PinFormatError");
            return;
        }

        if (!string.Equals(pin, ConfirmPinBox.Password, StringComparison.Ordinal))
        {
            ErrorText.Text = LocalizationService.Get("PinMismatch");
            return;
        }

        ResetButton.IsEnabled = false;
        try
        {
            if (await _reset(CodeBox.Text, pin))
            {
                ResultPin = pin;
                DialogResult = true;
                return;
            }

            ErrorText.Text = LocalizationService.Get("RecoveryCodeInvalid");
        }
        finally
        {
            ResetButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

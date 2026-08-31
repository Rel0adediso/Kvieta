using System.Windows;
using Otium.App.Services;
using Otium.Core.Services;

namespace Otium.App;

public partial class RecoveryResetWindow : Window
{
    private readonly Func<string, string, Task<bool>> _reset;
    private readonly Func<string, Task<bool>>? _managerDeviceReset;

    public RecoveryResetWindow(
        Func<string, string, Task<bool>> reset,
        Func<string, Task<bool>>? managerDeviceReset = null,
        bool recoveryCodeAvailable = true,
        string? managerDeviceName = null)
    {
        InitializeComponent();
        _reset = reset ?? throw new ArgumentNullException(nameof(reset));
        _managerDeviceReset = managerDeviceReset;
        bool managerDeviceAvailable = managerDeviceReset is not null;
        RecoveryCodePanel.Visibility = recoveryCodeAvailable ? Visibility.Visible : Visibility.Collapsed;
        ResetButton.Visibility = recoveryCodeAvailable ? Visibility.Visible : Visibility.Collapsed;
        ManagerDevicePanel.Visibility = managerDeviceAvailable ? Visibility.Visible : Visibility.Collapsed;
        if (managerDeviceAvailable)
        {
            string displayName = string.IsNullOrWhiteSpace(managerDeviceName)
                ? LocalizationService.Get("TrustedDeviceGenericName")
                : managerDeviceName.Trim();
            ManagerDeviceDescriptionText.Text = string.Format(
                LocalizationService.Get("TrustedDeviceRecoveryDescription"),
                displayName);
        }
        if (!recoveryCodeAvailable && managerDeviceAvailable)
        {
            DescriptionText.Text = LocalizationService.Get("TrustedDeviceOnlyRecoveryDescription");
        }
        Loaded += (_, _) =>
        {
            if (recoveryCodeAvailable)
            {
                CodeBox.Focus();
            }
            else
            {
                PinBox.Focus();
            }
        };
    }

    public string? ResultPin { get; private set; }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPin(out string pin))
        {
            return;
        }

        SetBusy(true);
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
        catch
        {
            ErrorText.Text = LocalizationService.CurrentLanguage == Otium.Core.Models.LanguagePreference.English
                ? "PIN recovery could not be started."
                : "PIN kurtarma işlemi başlatılamadı.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ManagerDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_managerDeviceReset is null || !TryGetPin(out string pin))
        {
            return;
        }

        SetBusy(true);
        try
        {
            if (await _managerDeviceReset(pin))
            {
                ResultPin = pin;
                DialogResult = true;
                return;
            }

            ErrorText.Text = LocalizationService.CurrentLanguage == Otium.Core.Models.LanguagePreference.English
                ? "The manager device did not approve the reset or Guardian rejected it."
                : "Yönetici cihazı sıfırlamayı onaylamadı veya Guardian isteği reddetti.";
        }
        catch
        {
            ErrorText.Text = LocalizationService.CurrentLanguage == Otium.Core.Models.LanguagePreference.English
                ? "Manager-device recovery could not be started on the local network."
                : "Yönetici cihazı kurtarması yerel ağda başlatılamadı.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool TryGetPin(out string pin)
    {
        pin = PinBox.Password;
        if (!AdminPinService.IsValidFormat(pin))
        {
            ErrorText.Text = LocalizationService.Get("PinFormatError");
            return false;
        }

        if (!string.Equals(pin, ConfirmPinBox.Password, StringComparison.Ordinal))
        {
            ErrorText.Text = LocalizationService.Get("PinMismatch");
            return false;
        }

        return true;
    }

    private void SetBusy(bool busy)
    {
        ResetButton.IsEnabled = !busy;
        ManagerDeviceButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

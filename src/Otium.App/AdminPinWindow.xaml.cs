using System.Windows;
using System.Windows.Input;
using Otium.Core.Services;
using Otium.App.Services;

namespace Otium.App;

public partial class AdminPinWindow : Window
{
    private readonly bool _isSetup;
    private readonly Func<string, Task<bool>>? _verifier;
    private readonly Func<Window, Task<string?>>? _recoveryAction;
    private int _failedAttempts;
    private bool _verificationInProgress;

    private AdminPinWindow(
        bool isSetup,
        Func<string, Task<bool>>? verifier,
        Func<Window, Task<string?>>? recoveryAction = null)
    {
        InitializeComponent();
        Title = $"Otium · {LocalizationService.Get("AdminPin")}";
        _isSetup = isSetup;
        _verifier = verifier;
        _recoveryAction = recoveryAction;

        if (isSetup)
        {
            TitleText.Text = LocalizationService.Get("CreateAdminPin");
            DescriptionText.Text = LocalizationService.Get("CreateAdminPinDescription");
            ConfirmButton.Content = LocalizationService.Get("SavePin");
        }
        else
        {
            Height = 270;
            TitleText.Text = LocalizationService.Get("AdminVerification");
            DescriptionText.Text = LocalizationService.Get("AdminVerificationDescription");
            ConfirmPanel.Visibility = Visibility.Collapsed;
            ConfirmButton.Content = LocalizationService.Get("Unlock");
            RecoveryButton.Visibility = recoveryAction is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string? ResultPin { get; private set; }

    public static AdminPinWindow CreateSetup() => new(true, null);

    public static AdminPinWindow CreateVerification(
        Func<string, bool> verifier,
        Func<Window, Task<string?>>? recoveryAction = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return new AdminPinWindow(false, pin => Task.FromResult(verifier(pin)), recoveryAction);
    }

    public static AdminPinWindow CreateVerification(
        Func<string, Task<bool>> verifier,
        Func<Window, Task<string?>>? recoveryAction = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return new AdminPinWindow(false, verifier, recoveryAction);
    }

    private async void Recovery_Click(object sender, RoutedEventArgs e)
    {
        if (_recoveryAction is null)
        {
            return;
        }

        RecoveryButton.IsEnabled = false;
        ConfirmButton.IsEnabled = false;
        try
        {
            string? newPin = await _recoveryAction(this);
            if (newPin is not null)
            {
                ResultPin = newPin;
                DialogResult = true;
            }
        }
        finally
        {
            RecoveryButton.IsEnabled = true;
            ConfirmButton.IsEnabled = true;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => PinBox.Focus();

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_verificationInProgress || !ConfirmButton.IsEnabled) return;
        _verificationInProgress = true;
        string pin = PinBox.Password;
        try
        {
            if (!AdminPinService.IsValidFormat(pin))
            {
                ShowError(LocalizationService.Get("PinFormatError"));
                return;
            }

            if (_isSetup)
            {
                if (!string.Equals(pin, ConfirmPinBox.Password, StringComparison.Ordinal))
                {
                    ShowError(LocalizationService.Get("PinMismatch"));
                    ConfirmPinBox.Clear();
                    ConfirmPinBox.Focus();
                    return;
                }

                ResultPin = pin;
                DialogResult = true;
                return;
            }

            ConfirmButton.IsEnabled = false;
            if (_verifier is not null && await _verifier(pin))
            {
                ResultPin = pin;
                DialogResult = true;
                return;
            }

            _failedAttempts++;
            ShowError(LocalizationService.Get("WrongPin"));
            PinBox.Clear();
            PinBox.Focus();

            if (_failedAttempts >= 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, _failedAttempts)));
            }
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
            _verificationInProgress = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ConfirmButton.IsEnabled && !_verificationInProgress)
        {
            e.Handled = true;
            Confirm_Click(ConfirmButton, new RoutedEventArgs());
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PinBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => !char.IsAsciiDigit(character));
    }

    private void ShowError(string message) => ErrorText.Text = message;
}

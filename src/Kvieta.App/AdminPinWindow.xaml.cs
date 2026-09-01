using System.Windows;
using System.Windows.Input;
using Kvieta.Core.Services;
using Kvieta.App.Services;

namespace Kvieta.App;

public partial class AdminPinWindow : Window
{
    private readonly bool _isSetup;
    private readonly Func<string, Task<bool>>? _verifier;
    private readonly Func<Window, Task<string?>>? _recoveryAction;
    private int _failedAttempts;
    private bool _verificationInProgress;
    private bool _pinVisible;
    private bool _confirmPinVisible;

    private AdminPinWindow(
        bool isSetup,
        Func<string, Task<bool>>? verifier,
        Func<Window, Task<string?>>? recoveryAction = null,
        string? verificationTitle = null,
        string? verificationDescription = null)
    {
        InitializeComponent();
        Title = $"Kvieta · {LocalizationService.Get("AdminPin")}";
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
            Height = 370;
            TitleText.Text = verificationTitle ?? LocalizationService.Get("AdminVerification");
            DescriptionText.Text = verificationDescription ?? LocalizationService.Get("AdminVerificationDescription");
            ConfirmPanel.Visibility = Visibility.Collapsed;
            ConfirmButton.Content = LocalizationService.Get("Unlock");
            RecoveryButton.Visibility = recoveryAction is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string? ResultPin { get; private set; }
    public bool CredentialWasRecovered { get; private set; }

    public static AdminPinWindow CreateSetup() => new(true, null);

    public static AdminPinWindow CreateVerification(
        Func<string, bool> verifier,
        Func<Window, Task<string?>>? recoveryAction = null,
        string? verificationTitle = null,
        string? verificationDescription = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return new AdminPinWindow(
            false,
            pin => Task.FromResult(verifier(pin)),
            recoveryAction,
            verificationTitle,
            verificationDescription);
    }

    public static AdminPinWindow CreateVerification(
        Func<string, Task<bool>> verifier,
        Func<Window, Task<string?>>? recoveryAction = null,
        string? verificationTitle = null,
        string? verificationDescription = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return new AdminPinWindow(
            false,
            verifier,
            recoveryAction,
            verificationTitle,
            verificationDescription);
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
                CredentialWasRecovered = true;
                DialogResult = true;
            }
        }
        catch
        {
            ShowError(LocalizationService.Get("AdminVerificationUnavailable"));
        }
        finally
        {
            RecoveryButton.IsEnabled = true;
            ConfirmButton.IsEnabled = true;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => PinBox.Focus();

    private void TogglePinVisibility_Click(object sender, RoutedEventArgs e)
    {
        _pinVisible = !_pinVisible;
        if (_pinVisible)
        {
            VisiblePinBox.Text = PinBox.Password;
            VisiblePinBox.Visibility = Visibility.Visible;
            PinBox.Visibility = Visibility.Collapsed;
            VisiblePinBox.Focus();
            VisiblePinBox.CaretIndex = VisiblePinBox.Text.Length;
        }
        else
        {
            PinBox.Password = VisiblePinBox.Text;
            PinBox.Visibility = Visibility.Visible;
            VisiblePinBox.Visibility = Visibility.Collapsed;
            PinBox.Focus();
        }
    }

    private void ToggleConfirmPinVisibility_Click(object sender, RoutedEventArgs e)
    {
        _confirmPinVisible = !_confirmPinVisible;
        if (_confirmPinVisible)
        {
            VisibleConfirmPinBox.Text = ConfirmPinBox.Password;
            VisibleConfirmPinBox.Visibility = Visibility.Visible;
            ConfirmPinBox.Visibility = Visibility.Collapsed;
            VisibleConfirmPinBox.Focus();
            VisibleConfirmPinBox.CaretIndex = VisibleConfirmPinBox.Text.Length;
        }
        else
        {
            ConfirmPinBox.Password = VisibleConfirmPinBox.Text;
            ConfirmPinBox.Visibility = Visibility.Visible;
            VisibleConfirmPinBox.Visibility = Visibility.Collapsed;
            ConfirmPinBox.Focus();
        }
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_verificationInProgress || !ConfirmButton.IsEnabled) return;
        _verificationInProgress = true;
        string pin = _pinVisible ? VisiblePinBox.Text : PinBox.Password;
        try
        {
            if (!AdminPinService.IsValidFormat(pin))
            {
                ShowError(LocalizationService.Get("PinFormatError"));
                return;
            }

            if (_isSetup)
            {
                string confirmation = _confirmPinVisible ? VisibleConfirmPinBox.Text : ConfirmPinBox.Password;
                if (!string.Equals(pin, confirmation, StringComparison.Ordinal))
                {
                    ShowError(LocalizationService.Get("PinMismatch"));
                    ConfirmPinBox.Clear();
                    VisibleConfirmPinBox.Clear();
                    if (_confirmPinVisible)
                    {
                        VisibleConfirmPinBox.Focus();
                    }
                    else
                    {
                        ConfirmPinBox.Focus();
                    }
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
            VisiblePinBox.Clear();
            if (_pinVisible)
            {
                VisiblePinBox.Focus();
            }
            else
            {
                PinBox.Focus();
            }

            if (_failedAttempts >= 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, _failedAttempts)));
            }
        }
        catch
        {
            ShowError(LocalizationService.Get("AdminVerificationUnavailable"));
            PinBox.Clear();
            VisiblePinBox.Clear();
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

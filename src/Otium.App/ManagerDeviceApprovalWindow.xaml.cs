using System.Windows;
using System.Windows.Threading;
using Otium.App.Services;
using Otium.Core.Models;

namespace Otium.App;

public partial class ManagerDeviceApprovalWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DateTimeOffset _expiresAtUtc;
    private readonly bool _transfer;

    public ManagerDeviceApprovalWindow(
        Uri recoveryUri,
        DateTimeOffset expiresAtUtc,
        bool enrollment = false,
        string? verificationCode = null,
        bool transfer = false)
    {
        InitializeComponent();
        ArgumentNullException.ThrowIfNull(recoveryUri);
        _expiresAtUtc = expiresAtUtc;
        _transfer = transfer;
        bool english = LocalizationService.CurrentLanguage == LanguagePreference.English;
        TitleText.Text = transfer
            ? english ? "Transfer manager phone" : "Yönetici telefonunu aktar"
            : enrollment
            ? english ? "Pair manager phone" : "Yönetici telefonunu eşleştir"
            : english ? "Approve with manager phone" : "Yönetici telefonuyla onayla";
        DescriptionText.Text = transfer
            ? english
                ? "Open the local page on the new phone first, then scan the same QR with the currently enrolled phone. Both devices must sign."
                : "Yerel sayfayı önce yeni telefonda aç, sonra aynı QR'ı mevcut kayıtlı telefonla tara. İki cihaz da imzalamalı."
            : enrollment
            ? english
                ? "Scan this QR code with the phone camera and open the local page. The browser creates the manager key on this device."
                : "Bu QR kodu telefonun kamerasıyla tara ve yerel sayfayı aç. Tarayıcı yönetici anahtarını bu cihazda oluşturur."
            : english
                ? "Scan this QR code with the enrolled phone and approve the PIN reset on the local page."
                : "Bu QR kodu kayıtlı telefonla tara ve yerel sayfada PIN sıfırlamayı onayla.";
        CopyButton.Content = english ? "Copy address" : "Adresi kopyala";
        CancelButton.Content = english ? "Cancel" : "Vazgeç";
        UriBox.Text = recoveryUri.AbsoluteUri;
        VerificationCodeText.Text = verificationCode ?? string.Empty;
        QrImage.Source = QrCodeImageService.Create(recoveryUri.AbsoluteUri);
        _timer.Tick += Timer_Tick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        UpdateRemaining();
    }

    public void Complete(bool accepted)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (IsVisible)
            {
                DialogResult = accepted;
            }
        });
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _timer.Stop();
            DialogResult = false;
            return;
        }

        UpdateRemaining();
    }

    private void UpdateRemaining()
    {
        int seconds = Math.Max(0, (int)Math.Ceiling((_expiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds));
        StatusText.Text = _transfer
            ? LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"Waiting for the new phone, then the enrolled phone · {seconds} seconds remaining"
                : $"Önce yeni, sonra kayıtlı telefon bekleniyor · {seconds} saniye kaldı"
            : LocalizationService.CurrentLanguage == LanguagePreference.English
                ? $"Waiting for the enrolled phone · {seconds} seconds remaining"
                : $"Kayıtlı telefon bekleniyor · {seconds} saniye kaldı";
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => System.Windows.Clipboard.SetText(UriBox.Text);
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

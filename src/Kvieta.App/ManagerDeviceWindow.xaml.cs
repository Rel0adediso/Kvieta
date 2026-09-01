using System.Windows;
using Kvieta.App.Services;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App;

public partial class ManagerDeviceWindow : Window
{
    private readonly Func<Task<bool>> _revoke;
    private readonly Func<ManagerDeviceTransferRequest, Task<bool>> _transfer;
    private readonly Func<Window, Task<bool>> _pair;
    private readonly ManagerDeviceEnrollment? _current;
    private bool _startPairing;

    public ManagerDeviceWindow(
        ManagerDeviceEnrollment? current,
        Func<Task<bool>> revoke,
        Func<ManagerDeviceTransferRequest, Task<bool>> transfer,
        Func<Window, Task<bool>> pair,
        bool startPairing = false)
    {
        InitializeComponent();
        _revoke = revoke ?? throw new ArgumentNullException(nameof(revoke));
        _transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
        _pair = pair ?? throw new ArgumentNullException(nameof(pair));
        _current = current;
        _startPairing = startPairing;
        bool english = LocalizationService.CurrentLanguage == LanguagePreference.English;
        TitleText.Text = english ? "Manager device" : "Yönetici cihazı";
        DescriptionText.Text = english
            ? "Enroll one trusted phone for PIN recovery through a local page opened from the QR code."
            : "PIN kurtarma için QR kodundan açılan yerel sayfayla tek bir güvenilir telefon kaydet.";
        CurrentDeviceLabel.Text = english ? "ACTIVE DEVICE" : "AKTİF CİHAZ";
        NoDeviceTitle.Text = english ? "No trusted phone paired" : "Henüz güvenilir telefon yok";
        NoDeviceDescription.Text = english
            ? "Choose Pair with QR. Keep the computer and phone on the same private Wi-Fi or local network."
            : "QR ile eşleştire bas. Bilgisayar ve telefonu aynı özel Wi-Fi veya yerel ağda tut.";
        CloseButton.Content = english ? "Close" : "Kapat";
        RevokeButton.Content = english ? "Revoke device" : "Cihazı iptal et";
        TransferButton.Content = english ? "Transfer with QR" : "QR ile aktar";
        PairButton.Content = english ? "Pair with QR" : "QR ile eşleştir";

        if (current?.IsActive == true)
        {
            CurrentDevicePanel.Visibility = Visibility.Visible;
            CurrentDeviceName.Text = GetFriendlyDeviceName(current.DeviceName, english);
            CurrentDeviceId.Text = current.DeviceId;
            RevokeButton.Visibility = Visibility.Visible;
            TransferButton.Visibility = Visibility.Visible;
            PairButton.Visibility = Visibility.Collapsed;
            EnrollmentPanel.Visibility = Visibility.Collapsed;
        }
    }

    public static string GetFriendlyDeviceName(string deviceName, bool english)
    {
        string value = deviceName.Trim();
        if (value.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("Linux arm", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("Linux aarch", StringComparison.OrdinalIgnoreCase))
        {
            return english ? "Android phone" : "Android telefon";
        }

        return string.IsNullOrWhiteSpace(value)
            ? english ? "Phone browser" : "Telefon tarayıcısı"
            : value;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (!_startPairing)
        {
            return;
        }

        _startPairing = false;
        await RunPairingAsync();
    }

    private async void Revoke_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            if (await _revoke())
            {
                DialogResult = true;
                return;
            }

            ShowError("Guardian cihaz iptalini reddetti.", "Guardian rejected the device revocation.");
        }
        catch
        {
            ShowError("Cihaz iptali başlatılamadı.", "Device revocation could not be started.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Transfer_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (_current is null) return;

        SetBusy(true);
        try
        {
            ManagerDeviceApprovalWindow? approvalWindow = null;
            TaskCompletionSource<bool> transferResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using LocalManagerDeviceTransferEndpoint endpoint =
                LocalManagerDeviceTransferEndpoint.Start(
                    _current,
                    async request =>
                    {
                        bool accepted = await _transfer(request);
                        transferResult.TrySetResult(accepted);
                        approvalWindow?.Complete(accepted);
                        return accepted;
                    },
                    DateTimeOffset.UtcNow);
            approvalWindow = new ManagerDeviceApprovalWindow(
                endpoint.TransferUri,
                endpoint.ExpiresAtUtc,
                verificationCode: endpoint.VerificationCode,
                transfer: true)
            {
                Owner = this
            };
            bool acceptedByDialog = approvalWindow.ShowDialog() == true;
            if (acceptedByDialog || transferResult.Task.IsCompletedSuccessfully && transferResult.Task.Result)
            {
                DialogResult = true;
                return;
            }
            ShowError("Cihaz aktarımı tamamlanmadı.", "The device transfer did not complete.");
        }
        catch
        {
            ShowError("Cihaz aktarımı başlatılamadı.", "Device transfer could not be started.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Pair_Click(object sender, RoutedEventArgs e) => await RunPairingAsync();

    private async Task RunPairingAsync()
    {
        SetBusy(true);
        try
        {
            if (await _pair(this))
            {
                DialogResult = true;
                return;
            }

            ShowError("Telefon eşleştirmesi tamamlanmadı.", "Phone pairing did not complete.");
        }
        catch (Exception exception)
        {
            ShowError(
                $"Yerel ağ eşleştirmesi başlatılamadı: {exception.Message}",
                $"Local-network pairing could not be started: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        RevokeButton.IsEnabled = !busy;
        TransferButton.IsEnabled = !busy;
        PairButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
    }

    private void ShowError(string turkish, string english) =>
        ErrorText.Text = LocalizationService.CurrentLanguage == LanguagePreference.English ? english : turkish;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace Otium.App.Services;

public static class QrCodeImageService
{
    public static BitmapImage Create(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("QR content is required.", nameof(content));
        }

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(
            content,
            QRCodeGenerator.ECCLevel.Q,
            forceUtf8: true,
            utf8BOM: false);
        using PngByteQRCode code = new(data);
        byte[] png = code.GetGraphic(8);
        using MemoryStream stream = new(png, writable: false);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

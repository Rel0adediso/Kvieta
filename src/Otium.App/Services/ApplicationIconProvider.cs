using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Otium.App.Services;

public static class ApplicationIconProvider
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string applicationName)
    {
        string processName = Path.GetFileNameWithoutExtension(applicationName.Trim());
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        lock (Sync)
        {
            if (Cache.TryGetValue(processName, out ImageSource? cached))
            {
                return cached;
            }
        }

        ImageSource? icon = TryExtractRunningProcessIcon(processName);
        if (icon is not null)
        {
            lock (Sync)
            {
                Cache[processName] = icon;
            }
        }

        return icon;
    }

    public static System.Windows.Media.Brush GetFallbackBrush(string applicationName)
    {
        System.Windows.Media.Color[] colors =
        [
            System.Windows.Media.Color.FromRgb(116, 122, 85),
            System.Windows.Media.Color.FromRgb(91, 111, 103),
            System.Windows.Media.Color.FromRgb(132, 105, 82),
            System.Windows.Media.Color.FromRgb(104, 96, 126),
            System.Windows.Media.Color.FromRgb(128, 117, 67)
        ];
        int hash = applicationName.Aggregate(17, (current, character) => unchecked((current * 31) + char.ToUpperInvariant(character)));
        SolidColorBrush brush = new(colors[(hash & int.MaxValue) % colors.Length]);
        brush.Freeze();
        return brush;
    }

    private static ImageSource? TryExtractRunningProcessIcon(string processName)
    {
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    using Icon? icon = Icon.ExtractAssociatedIcon(path);
                    if (icon is null)
                    {
                        continue;
                    }

                    BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    source.Freeze();
                    return source;
                }
                catch
                {
                    // Some protected and packaged processes do not expose their executable icon.
                }
            }
        }

        return null;
    }
}

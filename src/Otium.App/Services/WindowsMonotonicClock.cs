using Microsoft.Win32;

namespace Otium.App.Services;

public static class WindowsMonotonicClock
{
    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static string? GetBootId()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
            return key?.GetValue("BootId")?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

using System.Diagnostics;
using Microsoft.Win32;

namespace Kvieta.App.Services;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Kvieta";

    public static void Apply(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows başlangıç ayarı açılamadı.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        string executablePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Kvieta'nın çalışma dosyası bulunamadı.");

        key.SetValue(ValueName, $"\"{executablePath}\" --session", RegistryValueKind.String);
    }
}

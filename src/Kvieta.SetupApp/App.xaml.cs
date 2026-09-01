using System.Reflection;

namespace Kvieta.SetupApp;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(argument => string.Equals(argument, "--verify-package", StringComparison.OrdinalIgnoreCase)))
        {
            bool payloadExists = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Contains("Kvieta.Payload.msi", StringComparer.Ordinal);
            Shutdown(payloadExists ? 0 : 2);
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--elevated-install", StringComparison.OrdinalIgnoreCase)))
        {
            bool desktopShortcut = e.Args.Any(argument =>
                string.Equals(argument, "--desktop-shortcut", StringComparison.OrdinalIgnoreCase));
            bool forceReinstall = e.Args.Any(argument =>
                string.Equals(argument, "--force-reinstall", StringComparison.OrdinalIgnoreCase));
            int guardianPayloadIndex = e.Args.ToList().FindIndex(argument =>
                string.Equals(argument, "--guardian-payload", StringComparison.OrdinalIgnoreCase));
            string? guardianPayload = guardianPayloadIndex >= 0 && guardianPayloadIndex + 1 < e.Args.Length
                ? e.Args[guardianPayloadIndex + 1]
                : null;
            try
            {
                Shutdown(await ElevatedPackageInstaller.RunAsync(
                    desktopShortcut,
                    guardianPayload,
                    forceReinstall));
            }
            catch
            {
                Shutdown(1603);
            }
            return;
        }

        if (e.Args.Any(argument => string.Equals(argument, "--elevated-reset-broken-protection", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(ElevatedPackageInstaller.ResetBrokenProtection());
            return;
        }

        MainWindow = new SetupWindow();
        MainWindow.Show();
    }
}

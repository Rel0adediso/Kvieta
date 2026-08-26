using System.Reflection;

namespace Otium.SetupApp;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(argument => string.Equals(argument, "--verify-package", StringComparison.OrdinalIgnoreCase)))
        {
            bool payloadExists = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Contains("Otium.Payload.msi", StringComparer.Ordinal);
            Shutdown(payloadExists ? 0 : 2);
            return;
        }

        MainWindow = new SetupWindow();
        MainWindow.Show();
    }
}

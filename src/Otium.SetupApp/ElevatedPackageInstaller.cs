using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Otium.SetupApp;

internal static class ElevatedPackageInstaller
{
    private const string PayloadResourceName = "Otium.Payload.msi";

    public static async Task<int> RunAsync(bool desktopShortcut)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Otium",
            "InstallerTemp");
        string stagingDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            HardenDirectory(stagingDirectory);
            string msiPath = Path.Combine(stagingDirectory, "Otium.msi");
            await ExtractAndVerifyPayloadAsync(msiPath);

            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Otium",
                "SetupLogs");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"Otium-setup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            string features = desktopShortcut ? "MainFeature,DesktopShortcutFeature" : "MainFeature";

            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (string argument in new[] { "/i", msiPath, "/qn", "/norestart", $"ADDLOCAL={features}", "/L*v", logPath })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows Installer could not be started.");
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static async Task ExtractAndVerifyPayloadAsync(string destination)
    {
        await using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new FileNotFoundException("The signed setup executable does not contain its MSI payload.");
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[128 * 1024];
            int read;
            while ((read = await payload.ReadAsync(buffer)) > 0)
            {
                sourceHash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read));
            }
            await output.FlushAsync();
        }

        await using FileStream extracted = File.OpenRead(destination);
        byte[] extractedHash = await SHA256.HashDataAsync(extracted);
        if (!CryptographicOperations.FixedTimeEquals(sourceHash.GetHashAndReset(), extractedHash))
        {
            throw new InvalidDataException("The extracted installer payload failed its integrity check.");
        }
    }

    private static void HardenDirectory(string directory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            directory, "/inheritance:r", "/grant:r", "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-544:(OI)(CI)F"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Installer staging permissions could not be configured.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Installer staging permissions could not be configured.");
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // The MSI is no longer executable from this process; Windows will clean up temporary files later.
        }
    }
}

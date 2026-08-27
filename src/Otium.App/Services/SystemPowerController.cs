using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Otium.App.Services;

public static partial class SystemPowerController
{
    public static bool Sleep()
    {
        try
        {
            return SetSuspendState(false, false, false);
        }
        catch
        {
            return false;
        }
    }

    public static bool Restart()
    {
        return StartShutdownProcess("/r /t 0");
    }

    public static bool ShutDown()
    {
        return StartShutdownProcess("/s /t 0");
    }

    public static void LockWindows()
    {
        LockWorkStation();
    }

    private static bool StartShutdownProcess(string arguments)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Windows power command could not be started.");
            process.WaitForExit(3000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LockWorkStation();
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Otium.App.Services;

public static partial class SystemPowerController
{
    public static void Sleep()
    {
        SetSuspendState(false, false, false);
    }

    public static void Restart()
    {
        StartShutdownProcess("/r /t 0");
    }

    public static void ShutDown()
    {
        StartShutdownProcess("/s /t 0");
    }

    public static void SignOut()
    {
        StartShutdownProcess("/l");
    }

    public static void LockWindows()
    {
        LockWorkStation();
    }

    private static void StartShutdownProcess(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
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

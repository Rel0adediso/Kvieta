using System.Runtime.InteropServices;

namespace Otium.App.Services;

public static class SystemMediaController
{
    private const uint WmAppCommand = 0x0319;
    private const int AppCommandMediaPause = 47;
    private const uint SmtoAbortIfHung = 0x0002;
    private static readonly nint HwndBroadcast = new(0xffff);

    public static void PausePlayback()
    {
        nint command = new(AppCommandMediaPause << 16);
        _ = SendMessageTimeout(
            HwndBroadcast,
            WmAppCommand,
            0,
            command,
            SmtoAbortIfHung,
            1000,
            out _);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);
}

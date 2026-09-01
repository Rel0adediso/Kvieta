using System.Runtime.InteropServices;

namespace Kvieta.App.Services;

public sealed class SessionShortcutGuard : IDisposable
{
    public const int VirtualKeyTab = 0x09;
    public const int VirtualKeyEscape = 0x1B;
    public const int VirtualKeyLeftWindows = 0x5B;
    public const int VirtualKeyRightWindows = 0x5C;

    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSystemKeyDown = 0x0104;
    private const int WmSystemKeyUp = 0x0105;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyMenu = 0x12;
    private const int VirtualKeyShift = 0x10;

    private readonly Func<bool> _isProtectionRequired;
    private readonly LowLevelKeyboardProcedure _callback;
    private nint _hook;

    public SessionShortcutGuard(Func<bool> isProtectionRequired)
    {
        ArgumentNullException.ThrowIfNull(isProtectionRequired);
        _isProtectionRequired = isProtectionRequired;
        _callback = KeyboardHookCallback;
        _hook = SetWindowsHookEx(WhKeyboardLowLevel, _callback, GetModuleHandle(null), 0);
    }

    public static bool ShouldBlockShortcut(
        int virtualKey,
        bool controlPressed,
        bool altPressed,
        bool shiftPressed)
    {
        _ = shiftPressed;
        return virtualKey is VirtualKeyLeftWindows or VirtualKeyRightWindows ||
            virtualKey == VirtualKeyEscape && controlPressed ||
            virtualKey == VirtualKeyTab && altPressed;
    }

    public void Dispose()
    {
        if (_hook == 0)
        {
            return;
        }

        UnhookWindowsHookEx(_hook);
        _hook = 0;
        GC.SuppressFinalize(this);
    }

    private nint KeyboardHookCallback(int code, nint message, nint data)
    {
        if (code >= 0 &&
            IsKeyboardMessage(message) &&
            _isProtectionRequired())
        {
            int virtualKey = Marshal.ReadInt32(data);
            if (ShouldBlockShortcut(
                    virtualKey,
                    IsKeyPressed(VirtualKeyControl),
                    IsKeyPressed(VirtualKeyMenu),
                    IsKeyPressed(VirtualKeyShift)))
            {
                return 1;
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private static bool IsKeyboardMessage(nint message)
    {
        int value = unchecked((int)message);
        return value is WmKeyDown or WmKeyUp or WmSystemKeyDown or WmSystemKeyUp;
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private delegate nint LowLevelKeyboardProcedure(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProcedure callback,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
}

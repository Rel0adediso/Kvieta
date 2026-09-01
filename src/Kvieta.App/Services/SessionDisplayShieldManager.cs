using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Kvieta.App.Services;

public sealed class SessionDisplayShieldManager : IDisposable
{
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);

    private readonly Window _dispatcherOwner;
    private readonly Func<bool> _shouldCoverAllDisplays;
    private readonly Dictionary<string, SessionDisplayShieldWindow> _shields = new(StringComparer.Ordinal);
    private bool _disposed;

    public SessionDisplayShieldManager(Window dispatcherOwner, Func<bool> shouldCoverAllDisplays)
    {
        ArgumentNullException.ThrowIfNull(dispatcherOwner);
        ArgumentNullException.ThrowIfNull(shouldCoverAllDisplays);
        _dispatcherOwner = dispatcherOwner;
        _shouldCoverAllDisplays = shouldCoverAllDisplays;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        if (!_dispatcherOwner.Dispatcher.CheckAccess())
        {
            _dispatcherOwner.Dispatcher.InvokeAsync(Refresh);
            return;
        }

        if (!_shouldCoverAllDisplays())
        {
            foreach (SessionDisplayShieldWindow shield in _shields.Values)
            {
                shield.Hide();
            }
            return;
        }

        Forms.Screen[] secondaryDisplays = Forms.Screen.AllScreens
            .Where(screen => !screen.Primary)
            .ToArray();
        HashSet<string> connectedDisplays = secondaryDisplays
            .Select(screen => screen.DeviceName)
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string deviceName, SessionDisplayShieldWindow shield) in _shields.ToArray())
        {
            if (connectedDisplays.Contains(deviceName))
            {
                continue;
            }

            shield.CloseFromManager();
            _shields.Remove(deviceName);
        }

        foreach (Forms.Screen display in secondaryDisplays)
        {
            if (!_shields.TryGetValue(display.DeviceName, out SessionDisplayShieldWindow? shield))
            {
                shield = new SessionDisplayShieldWindow();
                _shields.Add(display.DeviceName, shield);
            }

            if (!shield.IsVisible)
            {
                shield.Show();
            }

            PositionShield(shield, display.Bounds);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        foreach (SessionDisplayShieldWindow shield in _shields.Values)
        {
            shield.CloseFromManager();
        }
        _shields.Clear();
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        _dispatcherOwner.Dispatcher.InvokeAsync(Refresh);
    }

    private static void PositionShield(SessionDisplayShieldWindow shield, System.Drawing.Rectangle bounds)
    {
        nint handle = new WindowInteropHelper(shield).Handle;
        SetWindowPos(
            handle,
            HwndTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}

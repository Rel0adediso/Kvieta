using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Otium.App;

public partial class TrayMenuWindow : Window
{
    public TrayMenuWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? ControlCenterRequested;
    public event EventHandler? SessionScreenRequested;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Forms.Screen screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double cursorX = Forms.Cursor.Position.X / dpi.DpiScaleX;
        double cursorY = Forms.Cursor.Position.Y / dpi.DpiScaleY;
        double workLeft = screen.WorkingArea.Left / dpi.DpiScaleX;
        double workTop = screen.WorkingArea.Top / dpi.DpiScaleY;
        double workRight = screen.WorkingArea.Right / dpi.DpiScaleX;
        double workBottom = screen.WorkingArea.Bottom / dpi.DpiScaleY;

        Left = Math.Clamp(cursorX - ActualWidth + 14, workLeft + 6, workRight - ActualWidth - 6);
        Top = Math.Clamp(cursorY - ActualHeight + 8, workTop + 6, workBottom - ActualHeight - 6);
    }

    private void OpenControlCenter_Click(object sender, RoutedEventArgs e)
    {
        ControlCenterRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OpenSessionScreen_Click(object sender, RoutedEventArgs e)
    {
        SessionScreenRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Close();
    }
}

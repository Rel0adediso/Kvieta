using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Kvieta.App.Services;
using Kvieta.App.ViewModels;

namespace Kvieta.App;

public partial class SessionWidgetWindow : Window
{
    private bool _allowClose;

    public SessionWidgetWindow(CafeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler? PauseRequested;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Left = SystemParameters.WorkArea.Right - ActualWidth - 18;
        Top = SystemParameters.WorkArea.Top + 18;
        MotionService.Enter(WidgetSurface, 8, 0, 190);
    }

    public void CloseFromController()
    {
        _allowClose = true;
        Close();
    }

    public void ShowSmooth()
    {
        if (!IsVisible)
        {
            Show();
            MotionService.Enter(WidgetSurface, 7, 0, 185);
        }
    }

    public async Task HideSmoothAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        await MotionService.ExitAsync(WidgetSurface, 7, 0, 135);
        Hide();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }
}

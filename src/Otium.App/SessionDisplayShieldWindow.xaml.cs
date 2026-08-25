using System.ComponentModel;
using System.Windows;

namespace Otium.App;

public partial class SessionDisplayShieldWindow : Window
{
    private bool _allowClose;

    public SessionDisplayShieldWindow()
    {
        InitializeComponent();
    }

    public void CloseFromManager()
    {
        _allowClose = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }
}

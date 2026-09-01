using System.Windows;

namespace Kvieta.App;

public partial class RecoveryCodesWindow : Window
{
    public RecoveryCodesWindow(IEnumerable<string> codes)
    {
        InitializeComponent();
        CodesTextBox.Text = string.Join(Environment.NewLine, codes);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(CodesTextBox.Text); }
        catch { }
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}

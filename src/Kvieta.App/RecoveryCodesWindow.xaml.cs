using System.Windows;
using System.IO;

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = Services.LocalizationService.Get("SaveRecoveryCodes"),
            FileName = $"Kvieta-recovery-codes-{DateTime.Now:yyyy-MM-dd}.txt",
            DefaultExt = ".txt",
            Filter = "Text file (*.txt)|*.txt"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, CodesTextBox.Text);
            SaveButton.Content = Services.LocalizationService.Get("SavedShort");
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"{Services.LocalizationService.Get("RecoveryCodesSaveFailed")}\n\n{exception.Message}",
                "Kvieta",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}

using System.Windows;
using System.Windows.Input;
using Otium.App.Services;
using Otium.App.ViewModels;

namespace Otium.App;

public partial class ApplicationUsageDetailsWindow : Window
{
    public ApplicationUsageDetailsWindow(IEnumerable<AppUsageHistoryRow> applications)
    {
        InitializeComponent();
        Title = $"Otium · {LocalizationService.Get("HistoryApplicationDetails")}";
        ApplicationsList.ItemsSource = applications.ToList();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

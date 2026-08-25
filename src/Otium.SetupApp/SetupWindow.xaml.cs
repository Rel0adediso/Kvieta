using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Otium.SetupApp;

public partial class SetupWindow : Window
{
    private SetupLanguage? _selectedLanguage;

    public SetupWindow()
    {
        InitializeComponent();
    }

    private void Turkish_Click(object sender, RoutedEventArgs e) => SelectLanguage(SetupLanguage.Turkish);
    private void English_Click(object sender, RoutedEventArgs e) => SelectLanguage(SetupLanguage.English);

    private void SelectLanguage(SetupLanguage language)
    {
        _selectedLanguage = language;
        bool turkish = language == SetupLanguage.Turkish;
        TurkishCheck.Visibility = turkish ? Visibility.Visible : Visibility.Collapsed;
        EnglishCheck.Visibility = turkish ? Visibility.Collapsed : Visibility.Visible;
        TurkishButton.BorderBrush = turkish ? FindBrush("PrimaryBrush") : FindBrush("BorderBrush");
        EnglishButton.BorderBrush = turkish ? FindBrush("BorderBrush") : FindBrush("PrimaryBrush");
        TurkishButton.BorderThickness = turkish ? new Thickness(2) : new Thickness(1);
        EnglishButton.BorderThickness = turkish ? new Thickness(1) : new Thickness(2);
        ContinueButton.Content = turkish ? "Devam et" : "Continue";
        PrivacyText.Text = turkish
            ? "Dil seçimini daha sonra Ayarlar'dan değiştirebilirsin."
            : "You can change the language later in Settings.";
        ContinueButton.IsEnabled = true;
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLanguage is null) return;
        bool turkish = _selectedLanguage == SetupLanguage.Turkish;
        WelcomeEyebrow.Text = turkish ? "KURULUMA HAZIR" : "READY TO SET UP";
        WelcomeTitle.Text = turkish ? "Otium'a hoş geldin." : "Welcome to Otium.";
        WelcomeDescription.Text = turkish
            ? "Sonraki ekranda kullanım biçimini ve başlangıç ayarlarını birlikte seçeceğiz."
            : "Next, we'll choose how you want to use Otium and configure the essentials.";
        BackButton.Content = turkish ? "Dili değiştir" : "Change language";
        LanguagePanel.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Visible;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        LanguagePanel.Visibility = Visibility.Visible;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private enum SetupLanguage
    {
        Turkish,
        English
    }
}

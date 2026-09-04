using System.Windows;
using System.Windows.Input;
using Kvieta.Core.Models;

namespace Kvieta.App;

public partial class ModeSelectionWindow : Window
{
    public ModeSelectionWindow(UsageMode? currentMode = null, PersonalProtectionLevel currentPersonalLevel = PersonalProtectionLevel.Balanced)
    {
        InitializeComponent();
        SelectedMode = currentMode;
        SelectedPersonalProtectionLevel = currentPersonalLevel;
        UpdateSelectionStyles();
        UpdatePersonalSelectionStyles();
    }

    public UsageMode? SelectedMode { get; private set; }
    public PersonalProtectionLevel SelectedPersonalProtectionLevel { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        MaxWidth = Math.Max(320, workArea.Width - 16);
        MaxHeight = Math.Max(240, workArea.Height - 16);
        MinWidth = Math.Min(MinWidth, MaxWidth);
        MinHeight = Math.Min(MinHeight, MaxHeight);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    private void Insights_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Insights);
    private void Personal_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Personal);
    private void Family_Click(object sender, RoutedEventArgs e) => SelectMode(UsageMode.Family);

    private void SelectMode(UsageMode mode)
    {
        SelectedMode = mode;
        UpdateSelectionStyles();
    }

    private void UpdateSelectionStyles()
    {
        Style normal = (Style)InsightsButton.FindResource("ModeCardStyle");
        Style selected = (Style)InsightsButton.FindResource("SelectedModeCardStyle");
        InsightsButton.Style = SelectedMode == UsageMode.Insights ? selected : normal;
        PersonalButton.Style = SelectedMode == UsageMode.Personal ? selected : normal;
        FamilyButton.Style = SelectedMode == UsageMode.Family ? selected : normal;
        ConfirmModeButton.IsEnabled = SelectedMode is not null;
    }

    private void Flexible_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Flexible);
    private void Balanced_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Balanced);
    private void ProtectedLevel_Click(object sender, RoutedEventArgs e) => SelectPersonalLevel(PersonalProtectionLevel.Protected);

    private void SelectPersonalLevel(PersonalProtectionLevel level)
    {
        SelectedPersonalProtectionLevel = level;
        UpdatePersonalSelectionStyles();
    }

    private void UpdatePersonalSelectionStyles()
    {
        Style normal = (Style)FlexibleButton.FindResource("ModeCardStyle");
        Style selected = (Style)FlexibleButton.FindResource("SelectedModeCardStyle");
        FlexibleButton.Style = SelectedPersonalProtectionLevel == PersonalProtectionLevel.Flexible ? selected : normal;
        BalancedButton.Style = SelectedPersonalProtectionLevel == PersonalProtectionLevel.Balanced ? selected : normal;
        ProtectedLevelButton.Style = SelectedPersonalProtectionLevel == PersonalProtectionLevel.Protected ? selected : normal;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMode == UsageMode.Personal)
        {
            UsageStepPanel.Visibility = Visibility.Collapsed;
            PersonalStepPanel.Visibility = Visibility.Visible;
            return;
        }

        if (SelectedMode is not null)
        {
            DialogResult = true;
        }
    }

    private void ConfirmPersonal_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        PersonalStepPanel.Visibility = Visibility.Collapsed;
        UsageStepPanel.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

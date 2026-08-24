using System.Windows;
using System.Windows.Input;
using Otium.Core.Models;

namespace Otium.App;

public partial class ModeSelectionWindow : Window
{
    public ModeSelectionWindow(ControlMode? currentMode = null)
    {
        InitializeComponent();
        SelectedMode = currentMode;
        UpdateSelectionStyles();
    }

    public ControlMode? SelectedMode { get; private set; }

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

    private void Awareness_Click(object sender, RoutedEventArgs e)
    {
        SelectMode(ControlMode.Awareness);
    }

    private void Personal_Click(object sender, RoutedEventArgs e)
    {
        SelectMode(ControlMode.Personal);
    }

    private void Protected_Click(object sender, RoutedEventArgs e)
    {
        SelectMode(ControlMode.Protected);
    }

    private void SelectMode(ControlMode mode)
    {
        SelectedMode = mode;
        UpdateSelectionStyles();
    }

    private void UpdateSelectionStyles()
    {
        Style normal = (Style)AwarenessButton.FindResource("ModeCardStyle");
        Style selected = (Style)AwarenessButton.FindResource("SelectedModeCardStyle");
        AwarenessButton.Style = SelectedMode == ControlMode.Awareness ? selected : normal;
        PersonalButton.Style = SelectedMode == ControlMode.Personal ? selected : normal;
        ProtectedButton.Style = SelectedMode == ControlMode.Protected ? selected : normal;
        ConfirmModeButton.IsEnabled = SelectedMode is not null;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMode is not null)
        {
            DialogResult = true;
        }
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

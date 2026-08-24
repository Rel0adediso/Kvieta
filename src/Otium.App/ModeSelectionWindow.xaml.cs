using System.Windows;
using System.Windows.Input;
using Otium.Core.Models;

namespace Otium.App;

public partial class ModeSelectionWindow : Window
{
    public ModeSelectionWindow(ControlMode? currentMode = null)
    {
        InitializeComponent();
        if (currentMode == ControlMode.Awareness)
        {
            AwarenessButton.Style = (Style)AwarenessButton.FindResource("SelectedModeCardStyle");
        }
        else if (currentMode == ControlMode.Personal)
        {
            PersonalButton.Style = (Style)PersonalButton.FindResource("SelectedModeCardStyle");
        }
        else if (currentMode == ControlMode.Protected)
        {
            ProtectedButton.Style = (Style)ProtectedButton.FindResource("SelectedModeCardStyle");
        }
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
        SelectedMode = ControlMode.Awareness;
        DialogResult = true;
    }

    private void Personal_Click(object sender, RoutedEventArgs e)
    {
        SelectedMode = ControlMode.Personal;
        DialogResult = true;
    }

    private void Protected_Click(object sender, RoutedEventArgs e)
    {
        SelectedMode = ControlMode.Protected;
        DialogResult = true;
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

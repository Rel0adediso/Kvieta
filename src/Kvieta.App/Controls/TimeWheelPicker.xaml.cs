using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Kvieta.App.Controls;

public partial class TimeWheelPicker : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText), typeof(string), typeof(TimeWheelPicker),
        new FrameworkPropertyMetadata("00:00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, TimeTextChanged));

    private bool _isSynchronizing;

    public TimeWheelPicker()
    {
        Hours = Enumerable.Range(0, 24).Select(value => value.ToString("00", CultureInfo.InvariantCulture)).ToArray();
        Minutes = Enumerable.Range(0, 60).Select(value => value.ToString("00", CultureInfo.InvariantCulture)).ToArray();
        InitializeComponent();
        SynchronizeSelectors(TimeText);
    }

    public IReadOnlyList<string> Hours { get; }
    public IReadOnlyList<string> Minutes { get; }

    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    private static void TimeTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is TimeWheelPicker picker)
        {
            picker.SynchronizeSelectors(eventArgs.NewValue as string);
        }
    }

    private void SynchronizeSelectors(string? value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
        {
            return;
        }

        _isSynchronizing = true;
        HourInput.SelectedIndex = time.Hour;
        MinuteInput.SelectedIndex = time.Minute;
        _isSynchronizing = false;
    }

    private void TimePart_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || HourInput.SelectedIndex < 0 || MinuteInput.SelectedIndex < 0)
        {
            return;
        }

        SetCurrentValue(TimeTextProperty, $"{HourInput.SelectedIndex:00}:{MinuteInput.SelectedIndex:00}");
    }

    private void DropDownButton_Checked(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            HourInput.ScrollIntoView(HourInput.SelectedItem);
            MinuteInput.ScrollIntoView(MinuteInput.SelectedItem);
        }, DispatcherPriority.Loaded);
    }
}

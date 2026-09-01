using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kvieta.App.Services;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;
using WpfPoint = System.Windows.Point;

namespace Kvieta.App.Controls;

public sealed class SlidingSegmentedControl : WpfListBox
{
    private Border? _indicator;
    private TranslateTransform? _indicatorTranslate;
    private bool _hasPosition;
    private int _positionedIndex = -1;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _indicator = GetTemplateChild("PART_Indicator") as Border;
        _indicatorTranslate = _indicator?.RenderTransform as TranslateTransform;
        if (_indicator is not null && (_indicatorTranslate is null || _indicatorTranslate.IsFrozen))
        {
            _indicatorTranslate = _indicatorTranslate?.Clone() ?? new TranslateTransform();
            _indicator.RenderTransform = _indicatorTranslate;
        }
        ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
        ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        Dispatcher.BeginInvoke(UpdateIndicator, DispatcherPriority.Loaded);
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        Dispatcher.BeginInvoke(UpdateIndicator, DispatcherPriority.Render);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Dispatcher.BeginInvoke(UpdateIndicator, DispatcherPriority.Loaded);
    }

    private void ItemContainerGenerator_StatusChanged(object? sender, EventArgs e)
    {
        if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
        {
            Dispatcher.BeginInvoke(UpdateIndicator, DispatcherPriority.Loaded);
        }
    }

    private void UpdateIndicator()
    {
        if (_indicator is null || _indicatorTranslate is null ||
            ItemContainerGenerator.ContainerFromIndex(SelectedIndex) is not WpfListBoxItem item)
        {
            if (_indicator is not null)
            {
                _indicator.Visibility = Visibility.Collapsed;
            }

            _hasPosition = false;
            _positionedIndex = -1;
            return;
        }

        WpfPoint position = item.TransformToAncestor(this).Transform(new WpfPoint(0, 0));
        double targetX = position.X;
        double targetWidth = item.ActualWidth;
        if (targetWidth <= 0)
        {
            return;
        }

        bool selectionChanged = _hasPosition && _positionedIndex != SelectedIndex;
        if (!selectionChanged && (_indicator.HasAnimatedProperties || _indicatorTranslate.HasAnimatedProperties))
        {
            return;
        }

        _indicator.Visibility = Visibility.Visible;
        double currentWidth = double.IsNaN(_indicator.Width) || _indicator.Width <= 0
            ? _indicator.ActualWidth
            : _indicator.Width;
        double currentX = _indicatorTranslate.X;
        _indicator.BeginAnimation(WidthProperty, null);
        _indicatorTranslate.BeginAnimation(TranslateTransform.XProperty, null);

        if (!selectionChanged || !MotionService.IsEnabled)
        {
            _indicator.Width = targetWidth;
            _indicatorTranslate.X = targetX;
            _hasPosition = true;
            _positionedIndex = SelectedIndex;
            return;
        }

        QuinticEase ease = new() { EasingMode = EasingMode.EaseOut };
        Duration duration = TimeSpan.FromMilliseconds(220);

        _indicator.Width = targetWidth;
        _indicatorTranslate.X = targetX;
        _indicator.BeginAnimation(WidthProperty, new DoubleAnimation(currentWidth, targetWidth, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });
        _indicatorTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(currentX, targetX, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        });
        _hasPosition = true;
        _positionedIndex = SelectedIndex;
    }
}

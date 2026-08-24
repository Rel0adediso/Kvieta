using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfSize = System.Windows.Size;

namespace Otium.App.Services;

public static class MotionService
{
    public const int QuickDuration = 140;
    public const int StandardDuration = 240;
    public const int EmphasisDuration = 340;

    private const string PreferenceKey = @"Software\Otium";
    private const string PreferenceValue = "AnimationsEnabled";
    private static readonly DependencyProperty ConfirmationRunningProperty = DependencyProperty.RegisterAttached(
        "ConfirmationRunning", typeof(bool), typeof(MotionService), new PropertyMetadata(false));
    private static bool _userAnimationsEnabled = ReadUserPreference();

    static MotionService()
    {
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            {
                PreferenceChanged?.Invoke(null, EventArgs.Empty);
            }
        };

        EventManager.RegisterClassHandler(typeof(WpfButton), UIElement.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(Button_MouseEnter));
        EventManager.RegisterClassHandler(typeof(WpfButton), UIElement.MouseLeaveEvent, new System.Windows.Input.MouseEventHandler(Button_MouseLeave));
        EventManager.RegisterClassHandler(typeof(WpfButton), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Button_MouseDown));
        EventManager.RegisterClassHandler(typeof(WpfButton), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Button_MouseUp));
        EventManager.RegisterClassHandler(typeof(ListBoxItem), Selector.SelectedEvent, new RoutedEventHandler(Selection_Selected));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.CheckBox), ToggleButton.CheckedEvent, new RoutedEventHandler(Toggle_Changed));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.CheckBox), ToggleButton.UncheckedEvent, new RoutedEventHandler(Toggle_Changed));
    }

    public static event EventHandler? PreferenceChanged;

    public static bool UserAnimationsEnabled => _userAnimationsEnabled;

    public static bool IsEnabled => _userAnimationsEnabled && SystemParameters.ClientAreaAnimation;

    public static void SetUserPreference(bool enabled)
    {
        if (_userAnimationsEnabled == enabled)
        {
            return;
        }

        _userAnimationsEnabled = enabled;
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(PreferenceKey);
            key.SetValue(PreferenceValue, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Accessibility remains effective for this run even if the local preference cannot be persisted.
        }

        PreferenceChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Enter(FrameworkElement element, double offsetX = 0, double offsetY = 10, int durationMilliseconds = StandardDuration)
    {
        ArgumentNullException.ThrowIfNull(element);
        AnimateReveal(element, offsetX, offsetY, 0.992, durationMilliseconds, 0);
    }

    public static void RevealWindow(FrameworkElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        AnimateReveal(surface, 0, 12, 0.985, 380, 0);
    }

    public static void RevealPage(FrameworkElement page, int direction)
    {
        ArgumentNullException.ThrowIfNull(page);
        FrameworkElement root = page is ScrollViewer { Content: FrameworkElement content } ? content : page;
        List<FrameworkElement> sections = root is System.Windows.Controls.Panel panel
            ? panel.Children.OfType<FrameworkElement>().Where(item => item.Visibility == Visibility.Visible).ToList()
            : [root];

        if (!IsEnabled)
        {
            foreach (FrameworkElement section in sections)
            {
                ResetVisualState(section);
            }

            foreach (WpfProgressBar progressBar in FindVisualChildren<WpfProgressBar>(root))
            {
                AnimateProgress(progressBar, MotionAssist.GetAnimatedValue(progressBar));
            }

            return;
        }

        int horizontalOffset = direction * 14;
        for (int index = 0; index < sections.Count; index++)
        {
            FrameworkElement section = sections[index];
            int delay = index == 0 ? 0 : 38 + ((index - 1) * 46);
            AnimateReveal(
                section,
                index == 0 ? horizontalOffset : direction * 7,
                index == 0 ? 2 : 10,
                index == 0 ? 0.997 : 0.993,
                index == 0 ? 280 : EmphasisDuration,
                delay);
        }

        root.UpdateLayout();
        int progressIndex = 0;
        foreach (WpfProgressBar progressBar in FindVisualChildren<WpfProgressBar>(root).Take(18))
        {
            AnimateProgress(
                progressBar,
                MotionAssist.GetAnimatedValue(progressBar),
                fromZero: true,
                delayMilliseconds: 120 + (progressIndex * 42));
            progressIndex++;
        }
    }

    public static void RevealElements(IEnumerable<FrameworkElement> elements, int baseDelayMilliseconds = 0)
    {
        ArgumentNullException.ThrowIfNull(elements);
        int index = 0;
        foreach (FrameworkElement element in elements.Where(item => item.Visibility == Visibility.Visible))
        {
            AnimateReveal(element, 0, 13, 0.982, EmphasisDuration, baseDelayMilliseconds + (index * 58));
            index++;
        }
    }

    public static void PrepareSidebarReveal(IEnumerable<FrameworkElement> elements)
    {
        foreach (FrameworkElement element in elements)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 0;
            TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = -6;
        }
    }

    public static Task AnimateSidebarElementsAsync(IEnumerable<FrameworkElement> source, bool reveal)
    {
        List<FrameworkElement> elements = source.Where(item => item.Visibility == Visibility.Visible).ToList();
        if (!IsEnabled || elements.Count == 0)
        {
            foreach (FrameworkElement element in elements)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = reveal ? 1 : 0;
                TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.X = 0;
            }

            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<FrameworkElement> ordered = reveal ? elements : [.. elements.AsEnumerable().Reverse()];
        for (int index = 0; index < ordered.Count; index++)
        {
            FrameworkElement element = ordered[index];
            int delay = index * 22;
            TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
            DoubleAnimation opacity = new(reveal ? 0 : Math.Max(0.01, element.Opacity), reveal ? 1 : 0, TimeSpan.FromMilliseconds(reveal ? 210 : 130))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = reveal ? SmoothOut() : SmoothIn(),
                FillBehavior = FillBehavior.Stop
            };
            DoubleAnimation slide = new(reveal ? -6 : 0, reveal ? 0 : -4, TimeSpan.FromMilliseconds(reveal ? 260 : 150))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = reveal ? SmoothOut() : SmoothIn(),
                FillBehavior = FillBehavior.Stop
            };
            element.Opacity = reveal ? 1 : 0;
            translate.X = reveal ? 0 : -4;
            if (index == ordered.Count - 1)
            {
                slide.Completed += (_, _) => completion.TrySetResult();
            }

            element.BeginAnimation(UIElement.OpacityProperty, opacity, HandoffBehavior.SnapshotAndReplace);
            translate.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
        }

        return completion.Task;
    }

    public static void AnimateProgress(
        WpfProgressBar progressBar,
        double target,
        bool fromZero = false,
        int delayMilliseconds = 0)
    {
        ArgumentNullException.ThrowIfNull(progressBar);
        double safeTarget = Math.Clamp(target, progressBar.Minimum, progressBar.Maximum);
        double start = fromZero ? progressBar.Minimum : Math.Clamp(progressBar.Value, progressBar.Minimum, progressBar.Maximum);
        progressBar.BeginAnimation(RangeBase.ValueProperty, null);

        if (!IsEnabled || Math.Abs(start - safeTarget) < 0.01)
        {
            progressBar.SetCurrentValue(RangeBase.ValueProperty, safeTarget);
            return;
        }

        DoubleAnimation animation = new(start, safeTarget, TimeSpan.FromMilliseconds(fromZero ? 520 : 280))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            progressBar.BeginAnimation(RangeBase.ValueProperty, null);
            progressBar.SetCurrentValue(RangeBase.ValueProperty, safeTarget);
        };
        progressBar.BeginAnimation(RangeBase.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    public static void AnimateTextChange(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);
        textBlock.BeginAnimation(UIElement.OpacityProperty, null);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(textBlock);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        if (!IsEnabled || !textBlock.IsVisible)
        {
            textBlock.Opacity = 1;
            translate.Y = 0;
            return;
        }

        textBlock.Opacity = 1;
        translate.Y = 0;
        textBlock.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(1.2, 0, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        });
    }

    public static void Pulse(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;

        if (!IsEnabled || !element.IsVisible)
        {
            return;
        }

        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        DoubleAnimation animation = new(1, 1.018, TimeSpan.FromMilliseconds(130))
        {
            AutoReverse = true,
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    public static void Shake(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsEnabled || !element.IsVisible)
        {
            return;
        }

        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        DoubleAnimationUsingKeyFrames shake = new()
        {
            Duration = TimeSpan.FromMilliseconds(300),
            FillBehavior = FillBehavior.Stop
        };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-6, KeyTime.FromPercent(0.18), SmoothOut()));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(5, KeyTime.FromPercent(0.42), SmoothOut()));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromPercent(0.66), SmoothOut()));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromPercent(0.84), SmoothOut()));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), SmoothOut()));
        translate.BeginAnimation(TranslateTransform.XProperty, shake, HandoffBehavior.SnapshotAndReplace);
    }

    public static async Task ConfirmAsync(WpfButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if ((bool)button.GetValue(ConfirmationRunningProperty))
        {
            return;
        }

        if (!IsEnabled)
        {
            return;
        }

        button.SetValue(ConfirmationRunningProperty, true);
        object originalContent = button.Content;
        bool originalHitTest = button.IsHitTestVisible;
        button.IsHitTestVisible = false;
        try
        {
            await AnimateOpacityAsync(button, button.Opacity, 0.45, 75, SmoothIn());
            button.Content = "✓";
            button.Opacity = 0.45;
            Pulse(button);
            await AnimateOpacityAsync(button, 0.45, 1, 150, SmoothOut());
            await Task.Delay(520);
            await AnimateOpacityAsync(button, 1, 0.45, 75, SmoothIn());
            button.Content = originalContent;
            button.Opacity = 0.45;
            await AnimateOpacityAsync(button, 0.45, 1, 150, SmoothOut());
        }
        finally
        {
            button.BeginAnimation(UIElement.OpacityProperty, null);
            button.Opacity = 1;
            button.Content = originalContent;
            button.IsHitTestVisible = originalHitTest;
            button.SetValue(ConfirmationRunningProperty, false);
        }
    }

    public static void Highlight(FrameworkElement element, WpfColor color)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsEnabled || !element.IsVisible)
        {
            return;
        }

        DropShadowEffect glow = new()
        {
            Color = color,
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0
        };
        element.Effect = glow;
        DoubleAnimation animation = new(0, 0.5, TimeSpan.FromMilliseconds(250))
        {
            AutoReverse = true,
            EasingFunction = SmoothInOut(),
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => element.Effect = null;
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
        Pulse(element);
    }

    public static Task AnimateColumnWidthAsync(ColumnDefinition column, double targetWidth, int durationMilliseconds = 270)
    {
        ArgumentNullException.ThrowIfNull(column);
        double startWidth = column.ActualWidth > 0 ? column.ActualWidth : column.Width.Value;
        if (!IsEnabled || Math.Abs(startWidth - targetWidth) < 0.5)
        {
            column.BeginAnimation(ColumnDefinition.WidthProperty, null);
            column.Width = new GridLength(targetWidth);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GridLengthAnimation animation = new()
        {
            From = new GridLength(startWidth),
            To = new GridLength(targetWidth),
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            column.BeginAnimation(ColumnDefinition.WidthProperty, null);
            column.Width = new GridLength(targetWidth);
            completion.TrySetResult();
        };
        column.BeginAnimation(ColumnDefinition.WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
        return completion.Task;
    }

    public static Task CrossfadeThemeAsync(FrameworkElement surface, Action applyTheme)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(applyTheme);
        if (!IsEnabled || !surface.IsVisible || surface.ActualWidth < 1 || surface.ActualHeight < 1)
        {
            applyTheme();
            return Task.CompletedTask;
        }

        try
        {
            PresentationSource? source = PresentationSource.FromVisual(surface);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1;
            RenderTargetBitmap snapshot = new(
                Math.Max(1, (int)Math.Ceiling(surface.ActualWidth * dpiX)),
                Math.Max(1, (int)Math.Ceiling(surface.ActualHeight * dpiY)),
                96 * dpiX,
                96 * dpiY,
                PixelFormats.Pbgra32);
            snapshot.Render(surface);

            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(surface);
            if (layer is null)
            {
                applyTheme();
                Enter(surface, 0, 0, 220);
                return Task.CompletedTask;
            }

            SnapshotAdorner adorner = new(surface, snapshot);
            layer.Add(adorner);
            applyTheme();
            surface.UpdateLayout();

            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            DoubleAnimation fade = new(1, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = SmoothOut(),
                FillBehavior = FillBehavior.Stop
            };
            fade.Completed += (_, _) =>
            {
                layer.Remove(adorner);
                completion.TrySetResult();
            };
            adorner.BeginAnimation(UIElement.OpacityProperty, fade);
            return completion.Task;
        }
        catch
        {
            applyTheme();
            return Task.CompletedTask;
        }
    }

    public static void ShowOverlay(FrameworkElement overlay, FrameworkElement panel)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(panel);
        overlay.Visibility = Visibility.Visible;
        overlay.IsHitTestVisible = true;
        overlay.BeginAnimation(UIElement.OpacityProperty, null);
        if (!IsEnabled)
        {
            overlay.Opacity = 1;
            ResetVisualState(panel);
            return;
        }

        overlay.Opacity = 1;
        overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = SmoothOut(),
            FillBehavior = FillBehavior.Stop
        });
        AnimateReveal(panel, 0, 15, 0.972, 330, 35);
    }

    public static Task HideOverlayAsync(FrameworkElement overlay, FrameworkElement panel)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(panel);
        if (!IsEnabled)
        {
            overlay.Visibility = Visibility.Collapsed;
            overlay.IsHitTestVisible = false;
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(panel);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(panel);
        DoubleAnimation fade = new(overlay.Opacity, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = SmoothIn(),
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) =>
        {
            overlay.BeginAnimation(UIElement.OpacityProperty, null);
            overlay.Opacity = 1;
            overlay.Visibility = Visibility.Collapsed;
            overlay.IsHitTestVisible = false;
            ResetVisualState(panel);
            completion.TrySetResult();
        };
        overlay.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(panel.Opacity, 0.2, TimeSpan.FromMilliseconds(145))
        {
            EasingFunction = SmoothIn(),
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 8, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = SmoothIn(),
            FillBehavior = FillBehavior.Stop
        });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.988, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = SmoothIn(),
            FillBehavior = FillBehavior.Stop
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.988, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = SmoothIn(),
            FillBehavior = FillBehavior.Stop
        });
        return completion.Task;
    }

    public static void SetHoverState(FrameworkElement element, bool hovered)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsEnabled || !element.IsEnabled)
        {
            ResetHoverState(element);
            return;
        }

        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        IEasingFunction easing = SmoothOut();
        int duration = hovered ? 190 : 230;
        AnimateScale(scale, hovered ? 1.014 : 1, duration, easing);
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translate.Y, hovered ? -2.5 : 0, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        translate.Y = hovered ? -2.5 : 0;
    }

    private static void AnimateReveal(
        FrameworkElement element,
        double offsetX,
        double offsetY,
        double fromScale,
        int durationMilliseconds,
        int delayMilliseconds)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

        if (!IsEnabled || !element.IsVisible)
        {
            ResetVisualState(element);
            return;
        }

        IEasingFunction easing = SmoothOut();
        TimeSpan delay = TimeSpan.FromMilliseconds(delayMilliseconds);
        element.Opacity = 1;
        translate.X = 0;
        translate.Y = 0;
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Math.Min(durationMilliseconds, 260)))
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offsetX, 0, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        DoubleAnimation scaleAnimation = new(fromScale, 1, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
    }

    private static Task AnimateOpacityAsync(
        UIElement element,
        double from,
        double to,
        int durationMilliseconds,
        IEasingFunction easing)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DoubleAnimation animation = new(from, to, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        element.Opacity = to;
        animation.Completed += (_, _) => completion.TrySetResult();
        element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        return completion.Task;
    }

    private static void Button_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        if (sender is WpfButton { IsEnabled: true } button && !MotionAssist.GetHoverLift(button))
        {
            AnimateButtonScale(button, 1.008, 170);
        }
    }

    private static void Button_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (sender is WpfButton button && !MotionAssist.GetHoverLift(button))
        {
            AnimateButtonScale(button, 1, 190);
        }
    }

    private static void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfButton { IsEnabled: true } button)
        {
            AnimateButtonScale(button, 0.975, 85);
        }
    }

    private static void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfButton { IsEnabled: true } button)
        {
            AnimateButtonScale(button, button.IsMouseOver ? (MotionAssist.GetHoverLift(button) ? 1.014 : 1.008) : 1, 150);
        }
    }

    private static void Selection_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem { IsLoaded: true, IsVisible: true } item)
        {
            Enter(item, -4, 0, 220);
        }
    }

    private static void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { IsLoaded: true, IsVisible: true } toggle)
        {
            Pulse(toggle);
        }
    }

    private static void AnimateButtonScale(FrameworkElement element, double target, int durationMilliseconds)
    {
        if (!IsEnabled)
        {
            ResetHoverState(element);
            return;
        }

        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        AnimateScale(scale, target, durationMilliseconds, SmoothOut());
    }

    private static void AnimateScale(ScaleTransform scale, double target, int durationMilliseconds, IEasingFunction easing)
    {
        DoubleAnimation animationX = new(scale.ScaleX, target, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        DoubleAnimation animationY = new(scale.ScaleY, target, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        scale.ScaleX = target;
        scale.ScaleY = target;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animationX, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animationY, HandoffBehavior.SnapshotAndReplace);
    }

    private static void ResetHoverState(FrameworkElement element)
    {
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
        translate.Y = 0;
    }

    private static void ResetVisualState(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        ScaleTransform scale = GetOrCreateTransform<ScaleTransform>(element);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.X = 0;
        translate.Y = 0;
        scale.ScaleX = 1;
        scale.ScaleY = 1;
    }

    private static T GetOrCreateTransform<T>(FrameworkElement element) where T : Transform, new()
    {
        if (element.RenderTransform is T direct)
        {
            return direct;
        }

        if (element.RenderTransform is TransformGroup group)
        {
            T? existing = group.Children.OfType<T>().FirstOrDefault();
            if (existing is not null)
            {
                return existing;
            }

            T added = new();
            group.Children.Add(added);
            return added;
        }

        TransformGroup replacement = new();
        if (element.RenderTransform is not null && element.RenderTransform != Transform.Identity)
        {
            replacement.Children.Add(element.RenderTransform);
        }

        T created = new();
        replacement.Children.Add(created);
        element.RenderTransform = replacement;
        return created;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEasingFunction SmoothOut() => new QuinticEase { EasingMode = EasingMode.EaseOut };

    private static IEasingFunction SmoothIn() => new CubicEase { EasingMode = EasingMode.EaseIn };

    private static IEasingFunction SmoothInOut() => new SineEase { EasingMode = EasingMode.EaseInOut };

    private static bool ReadUserPreference()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PreferenceKey);
            return key?.GetValue(PreferenceValue) is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }
}

public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(GridLength), typeof(GridLengthAnimation));
    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(GridLength), typeof(GridLengthAnimation));
    public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
        nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        double progress = animationClock.CurrentProgress ?? 0;
        progress = EasingFunction?.Ease(progress) ?? progress;
        return new GridLength(From.Value + ((To.Value - From.Value) * progress));
    }
}

internal sealed class SnapshotAdorner : Adorner
{
    private readonly WpfImage _image;

    public SnapshotAdorner(UIElement adornedElement, ImageSource snapshot) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _image = new WpfImage { Source = snapshot, Stretch = Stretch.Fill, SnapsToDevicePixels = true };
        AddVisualChild(_image);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => index == 0
        ? _image
        : throw new ArgumentOutOfRangeException(nameof(index));

    protected override WpfSize MeasureOverride(WpfSize constraint)
    {
        _image.Measure(constraint);
        return AdornedElement.RenderSize;
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        _image.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

public static class MotionAssist
{
    public static readonly DependencyProperty AnimatedValueProperty = DependencyProperty.RegisterAttached(
        "AnimatedValue",
        typeof(double),
        typeof(MotionAssist),
        new PropertyMetadata(0d, AnimatedValueChanged));

    public static readonly DependencyProperty AnimateTextChangesProperty = DependencyProperty.RegisterAttached(
        "AnimateTextChanges",
        typeof(bool),
        typeof(MotionAssist),
        new PropertyMetadata(false, AnimateTextChangesChanged));

    public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
        "HoverLift",
        typeof(bool),
        typeof(MotionAssist),
        new PropertyMetadata(false, HoverLiftChanged));

    private static readonly DependencyProperty ProgressHookedProperty = DependencyProperty.RegisterAttached(
        "ProgressHooked", typeof(bool), typeof(MotionAssist), new PropertyMetadata(false));
    private static readonly DependencyProperty TextHookedProperty = DependencyProperty.RegisterAttached(
        "TextHooked", typeof(bool), typeof(MotionAssist), new PropertyMetadata(false));
    private static readonly DependencyProperty TextReadyProperty = DependencyProperty.RegisterAttached(
        "TextReady", typeof(bool), typeof(MotionAssist), new PropertyMetadata(false));

    private static readonly DependencyPropertyDescriptor? TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    public static void SetAnimatedValue(DependencyObject element, double value) => element.SetValue(AnimatedValueProperty, value);

    public static double GetAnimatedValue(DependencyObject element) => (double)element.GetValue(AnimatedValueProperty);

    public static void SetAnimateTextChanges(DependencyObject element, bool value) => element.SetValue(AnimateTextChangesProperty, value);

    public static bool GetAnimateTextChanges(DependencyObject element) => (bool)element.GetValue(AnimateTextChangesProperty);

    public static void SetHoverLift(DependencyObject element, bool value) => element.SetValue(HoverLiftProperty, value);

    public static bool GetHoverLift(DependencyObject element) => (bool)element.GetValue(HoverLiftProperty);

    private static void AnimatedValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not WpfProgressBar progressBar)
        {
            return;
        }

        if (!(bool)progressBar.GetValue(ProgressHookedProperty))
        {
            progressBar.SetValue(ProgressHookedProperty, true);
            progressBar.Loaded += ProgressBar_Loaded;
        }

        if (progressBar.IsLoaded)
        {
            MotionService.AnimateProgress(progressBar, (double)args.NewValue);
        }
    }

    private static void ProgressBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is WpfProgressBar progressBar)
        {
            MotionService.AnimateProgress(progressBar, GetAnimatedValue(progressBar), fromZero: true, delayMilliseconds: 90);
        }
    }

    private static void AnimateTextChangesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBlock textBlock || TextDescriptor is null)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            textBlock.Loaded += TextBlock_Loaded;
            textBlock.Unloaded += TextBlock_Unloaded;
            if (textBlock.IsLoaded)
            {
                HookTextBlock(textBlock);
            }
        }
        else
        {
            textBlock.Loaded -= TextBlock_Loaded;
            textBlock.Unloaded -= TextBlock_Unloaded;
            UnhookTextBlock(textBlock);
        }
    }

    private static void HoverLiftChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            element.MouseEnter += HoverElement_MouseEnter;
            element.MouseLeave += HoverElement_MouseLeave;
        }
        else
        {
            element.MouseEnter -= HoverElement_MouseEnter;
            element.MouseLeave -= HoverElement_MouseLeave;
            MotionService.SetHoverState(element, hovered: false);
        }
    }

    private static void HoverElement_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            MotionService.SetHoverState(element, hovered: true);
        }
    }

    private static void HoverElement_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            MotionService.SetHoverState(element, hovered: false);
        }
    }

    private static void TextBlock_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            HookTextBlock(textBlock);
        }
    }

    private static void TextBlock_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            UnhookTextBlock(textBlock);
        }
    }

    private static void HookTextBlock(TextBlock textBlock)
    {
        if ((bool)textBlock.GetValue(TextHookedProperty) || TextDescriptor is null)
        {
            return;
        }

        textBlock.SetValue(TextHookedProperty, true);
        textBlock.SetValue(TextReadyProperty, true);
        TextDescriptor.AddValueChanged(textBlock, TextBlock_TextChanged);
    }

    private static void UnhookTextBlock(TextBlock textBlock)
    {
        if (!(bool)textBlock.GetValue(TextHookedProperty) || TextDescriptor is null)
        {
            return;
        }

        TextDescriptor.RemoveValueChanged(textBlock, TextBlock_TextChanged);
        textBlock.SetValue(TextHookedProperty, false);
        textBlock.SetValue(TextReadyProperty, false);
    }

    private static void TextBlock_TextChanged(object? sender, EventArgs e)
    {
        if (sender is TextBlock textBlock && (bool)textBlock.GetValue(TextReadyProperty))
        {
            MotionService.AnimateTextChange(textBlock);
        }
    }
}

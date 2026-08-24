using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using WpfColor = System.Windows.Media.Color;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

namespace Otium.App.Services;

public static class MotionService
{
    private const string PreferenceKey = @"Software\Otium";
    private const string PreferenceValue = "AnimationsEnabled";
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

    public static void Enter(FrameworkElement element, double offsetX = 0, double offsetY = 8, int durationMilliseconds = 190)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        TranslateTransform translate = GetOrCreateTransform<TranslateTransform>(element);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        if (!IsEnabled || !element.IsVisible)
        {
            element.Opacity = 1;
            translate.X = 0;
            translate.Y = 0;
            return;
        }

        element.Opacity = 0;
        translate.X = offsetX;
        translate.Y = offsetY;
        CubicEase easing = new() { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Math.Min(durationMilliseconds, 170)))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offsetX, 0, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        element.Opacity = 1;
        translate.X = 0;
        translate.Y = 0;
    }

    public static void AnimateProgress(WpfProgressBar progressBar, double target, bool fromZero = false)
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

        DoubleAnimation animation = new(start, safeTarget, TimeSpan.FromMilliseconds(fromZero ? 420 : 220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
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

        textBlock.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(2, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
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
        DoubleAnimation animation = new(1, 1.035, TimeSpan.FromMilliseconds(110))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
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
            BlurRadius = 20,
            ShadowDepth = 0,
            Opacity = 0
        };
        element.Effect = glow;
        DoubleAnimation animation = new(0, 0.68, TimeSpan.FromMilliseconds(220))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => element.Effect = null;
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, animation);
    }

    public static Task AnimateColumnWidthAsync(ColumnDefinition column, double targetWidth, int durationMilliseconds = 180)
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
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
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

    public static Task FadeThemeAsync(FrameworkElement surface, Action applyTheme)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(applyTheme);
        if (!IsEnabled || !surface.IsVisible)
        {
            applyTheme();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DoubleAnimation fadeOut = new(surface.Opacity, 0.78, TimeSpan.FromMilliseconds(80))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };
        fadeOut.Completed += (_, _) =>
        {
            applyTheme();
            surface.Opacity = 0.78;
            DoubleAnimation fadeIn = new(0.78, 1, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) =>
            {
                surface.BeginAnimation(UIElement.OpacityProperty, null);
                surface.Opacity = 1;
                completion.TrySetResult();
            };
            surface.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        surface.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        return completion.Task;
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
            MotionService.AnimateProgress(progressBar, GetAnimatedValue(progressBar), fromZero: true);
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

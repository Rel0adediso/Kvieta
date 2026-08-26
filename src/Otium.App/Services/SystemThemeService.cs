using System.Windows;
using Otium.Core.Models;
using Microsoft.Win32;

namespace Otium.App.Services;

public sealed class SystemThemeService : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private System.Windows.Application? _application;
    private ResourceDictionary? _activeDictionary;
    private bool? _isLight;
    private bool _isForced;
    private ThemePreference _preference = ThemePreference.System;

    public void Start(System.Windows.Application application)
    {
        _application = application;
        string? forcedTheme = Environment.GetEnvironmentVariable("OTIUM_THEME");
        _isForced = string.Equals(forcedTheme, "light", StringComparison.OrdinalIgnoreCase)
            || string.Equals(forcedTheme, "dark", StringComparison.OrdinalIgnoreCase);

        bool initialTheme = _isForced
            ? string.Equals(forcedTheme, "light", StringComparison.OrdinalIgnoreCase)
            : ReadWindowsLightTheme();
        Apply(initialTheme);

        if (!_isForced)
        {
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

    public void SetPreference(ThemePreference preference)
    {
        _preference = preference;
        if (_isForced)
        {
            return;
        }

        Apply(preference switch
        {
            ThemePreference.Light => true,
            ThemePreference.Dark => false,
            _ => ReadWindowsLightTheme()
        });
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_preference != ThemePreference.System || _application is null ||
            e.Category is not (UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        _application.Dispatcher.BeginInvoke(() =>
        {
            bool isLight = ReadWindowsLightTheme();
            if (_isLight != isLight) Apply(isLight);
        });
    }

    private void Apply(bool isLight)
    {
        if (_application is null || _isLight == isLight)
        {
            return;
        }

        ResourceDictionary dictionary = new()
        {
            Source = new Uri(isLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml", UriKind.Relative)
        };

        if (_activeDictionary is not null)
        {
            _application.Resources.MergedDictionaries.Remove(_activeDictionary);
        }
        else if (_application.Resources.MergedDictionaries.Count > 0)
        {
            _application.Resources.MergedDictionaries.RemoveAt(0);
        }

        _application.Resources.MergedDictionaries.Insert(0, dictionary);
        _activeDictionary = dictionary;
        _isLight = isLight;
    }

    private static bool ReadWindowsLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}

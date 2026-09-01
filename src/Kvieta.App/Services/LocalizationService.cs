using System.Globalization;
using System.Windows;
using Kvieta.Core.Models;

namespace Kvieta.App.Services;

public static class LocalizationService
{
    private const string TurkishDictionary = "Localization/Strings.tr.xaml";
    private const string EnglishDictionary = "Localization/Strings.en.xaml";

    public static LanguagePreference CurrentLanguage { get; private set; } = LanguagePreference.Turkish;

    public static void SetLanguage(System.Windows.Application application, LanguagePreference language)
    {
        CurrentLanguage = language;
        CultureInfo culture = language == LanguagePreference.English
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        ResourceDictionary? oldDictionary = application.Resources.MergedDictionaries
            .FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("Localization/Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (oldDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(oldDictionary);
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(language == LanguagePreference.English ? EnglishDictionary : TurkishDictionary, UriKind.Relative)
        });
    }

    public static string Get(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key)?.ToString() ?? key;
    }
}

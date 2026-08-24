using KardesKilidi.Core.Models;
using KardesKilidi.App.Services;

namespace KardesKilidi.App.ViewModels;

public sealed class AppRuleRow : ObservableObject
{
    private string _name;
    private string _executablePath;
    private string _mode;
    private int _dailyLimitMinutes;

    public AppRuleRow(AppRule rule)
    {
        Id = rule.Id;
        _name = rule.Name;
        _executablePath = rule.ExecutablePath;
        _mode = ToDisplayMode(rule.Mode);
        _dailyLimitMinutes = rule.DailyLimitMinutes;
    }

    public Guid Id { get; }
    public IReadOnlyList<string> Modes => [LocalizationService.Get("Blocked"), LocalizationService.Get("Limited"), LocalizationService.Get("Unlimited")];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public int DailyLimitMinutes
    {
        get => _dailyLimitMinutes;
        set => SetProperty(ref _dailyLimitMinutes, Math.Clamp(value, 0, 1440));
    }

    public AppRule ToModel()
    {
        return new AppRule
        {
            Id = Id,
            Name = Name.Trim(),
            ExecutablePath = ExecutablePath.Trim(),
            Mode = FromDisplayMode(Mode),
            DailyLimitMinutes = DailyLimitMinutes
        };
    }

    private static string ToDisplayMode(AppRuleMode mode) => mode switch
    {
        AppRuleMode.Blocked => LocalizationService.Get("Blocked"),
        AppRuleMode.Limited => LocalizationService.Get("Limited"),
        AppRuleMode.Unlimited => LocalizationService.Get("Unlimited"),
        _ => LocalizationService.Get("Blocked")
    };

    private static AppRuleMode FromDisplayMode(string mode) => mode switch
    {
        var value when value == LocalizationService.Get("Limited") => AppRuleMode.Limited,
        var value when value == LocalizationService.Get("Unlimited") => AppRuleMode.Unlimited,
        _ => AppRuleMode.Blocked
    };
}

using Kvieta.Core.Models;
using Kvieta.App.Services;
using System.IO;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;

namespace Kvieta.App.ViewModels;

public sealed class AppRuleRow : ObservableObject
{
    private string _name;
    private string _executablePath;
    private AppRuleModeOption _mode;
    private int _dailyLimitMinutes;
    private readonly AppRule _identity;
    private readonly IReadOnlyList<AppRuleModeOption> _modes;

    public AppRuleRow(AppRule rule)
    {
        Id = rule.Id;
        _identity = rule;
        _name = rule.Name;
        _executablePath = rule.ExecutablePath;
        _modes =
        [
            new(AppRuleMode.Blocked, LocalizationService.Get("Blocked"), LocalizationService.Get("PermanentBlockModeDescription")),
            new(AppRuleMode.Limited, LocalizationService.Get("Limited"), LocalizationService.Get("LimitedModeDescription")),
            new(AppRuleMode.Unlimited, LocalizationService.Get("Remove"), LocalizationService.Get("RemoveRuleDescription"), IsRemove: true)
        ];
        _mode = _modes.Single(option => option.Value == rule.Mode);
        _dailyLimitMinutes = rule.DailyLimitMinutes;
        Icon = ApplicationIconProvider.GetIcon(rule.ExecutablePath);
        FallbackBrush = ApplicationIconProvider.GetFallbackBrush(rule.Name);
    }

    public Guid Id { get; }
    public ImageSource? Icon { get; }
    public WpfBrush FallbackBrush { get; }
    public bool HasIcon => Icon is not null;
    public bool HasFallbackIcon => Icon is null;
    public bool IsLimitEditable => Mode.Value == AppRuleMode.Limited;
    public bool IsLimitHidden => !IsLimitEditable;
    public string Initials
    {
        get
        {
            string cleanName = Path.GetFileNameWithoutExtension(Name).Trim();
            string[] words = cleanName.Split(['.', ' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 1
                ? string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])))
                : cleanName[..Math.Min(2, cleanName.Length)].ToUpperInvariant();
        }
    }
    public IReadOnlyList<AppRuleModeOption> Modes => _modes;

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

    public AppRuleModeOption Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsLimitEditable));
                OnPropertyChanged(nameof(IsLimitHidden));
            }
        }
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
            OriginalFileName = _identity.OriginalFileName,
            ProductName = _identity.ProductName,
            PublisherName = _identity.PublisherName,
            PublisherThumbprint = _identity.PublisherThumbprint,
            Sha256 = _identity.Sha256,
            RequireSha256 = _identity.RequireSha256,
            PackageFamilyName = _identity.PackageFamilyName,
            IncludeChildProcesses = _identity.IncludeChildProcesses,
            LauncherExecutablePaths = [.. _identity.LauncherExecutablePaths],
            Mode = Mode.Value,
            DailyLimitMinutes = DailyLimitMinutes
        };
    }

}

public sealed record AppRuleModeOption(AppRuleMode Value, string Label, string Description, bool IsRemove = false);

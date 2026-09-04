using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.SetupApp;

public enum SetupLanguage
{
    Turkish,
    English
}

public enum SetupChoice
{
    KeepExisting,
    ConfigureNew
}

public enum SetupPackageAction
{
    FreshInstall,
    Update,
    Repair,
    DowngradeBlocked
}

public enum SetupTemplate
{
    UnderstandUsage,
    Focus,
    GamingRoutine,
    EveningWindDown,
    FamilyRoutine
}

public sealed class SetupPlan
{
    private IReadOnlyList<string>? _plainRecoveryCodes;
    private List<RecoveryCodeRecord> _recoveryCodeRecords = [];

    public static SetupPackageAction DeterminePackageAction(Version? installedVersion, Version packageVersion)
    {
        if (installedVersion is null) return SetupPackageAction.FreshInstall;
        int comparison = packageVersion.CompareTo(installedVersion);
        return comparison > 0
            ? SetupPackageAction.Update
            : comparison == 0
                ? SetupPackageAction.Repair
                : SetupPackageAction.DowngradeBlocked;
    }

    public SetupLanguage Language { get; set; } = SetupLanguage.Turkish;
    public SetupChoice ExistingChoice { get; set; } = SetupChoice.ConfigureNew;
    public UsageMode Mode { get; set; } = UsageMode.Personal;
    public PersonalProtectionLevel PersonalLevel { get; set; } = PersonalProtectionLevel.Balanced;
    public string DeviceName { get; set; } = Environment.MachineName;
    public int DailyLimitMinutes { get; set; } = 180;
    public bool StartWithWindows { get; set; } = true;
    public bool DesktopShortcut { get; set; } = true;
    public bool AwarenessTracking { get; set; } = true;
    public bool PairManagerDeviceAfterInstall { get; set; }
    public string? AdminPin { get; set; }
    public List<SetupScheduleDayRow> Schedule { get; } = ControlSettings.CreateDefaultSchedule()
        .Select(SetupScheduleDayRow.FromModel)
        .ToList();
    public bool HasCustomSchedule { get; set; }
    public SetupTemplate? SelectedTemplate { get; private set; }

    public bool RequiresUserPin => Mode == UsageMode.Family;
    public bool RequiresGuardian =>
        Mode == UsageMode.Family ||
        Mode == UsageMode.Personal && PersonalLevel == PersonalProtectionLevel.Protected;
    public bool UsesScheduledPlan =>
        Mode == UsageMode.Family ||
        Mode == UsageMode.Personal && PersonalLevel != PersonalProtectionLevel.Flexible;

    public void ApplyTemplate(SetupTemplate template)
    {
        SelectedTemplate = template;
        AwarenessTracking = true;
        PairManagerDeviceAfterInstall = false;

        switch (template)
        {
            case SetupTemplate.UnderstandUsage:
                Mode = UsageMode.Insights;
                PersonalLevel = PersonalProtectionLevel.Flexible;
                HasCustomSchedule = false;
                break;
            case SetupTemplate.Focus:
                Mode = UsageMode.Personal;
                PersonalLevel = PersonalProtectionLevel.Flexible;
                HasCustomSchedule = false;
                break;
            case SetupTemplate.GamingRoutine:
                Mode = UsageMode.Personal;
                PersonalLevel = PersonalProtectionLevel.Balanced;
                ApplySchedule(
                    weekdayFrom: "17:00", weekdayUntil: "22:00", weekdayMinutes: 120,
                    weekendFrom: "10:00", weekendUntil: "23:00", weekendMinutes: 180);
                break;
            case SetupTemplate.EveningWindDown:
                Mode = UsageMode.Personal;
                PersonalLevel = PersonalProtectionLevel.Balanced;
                ApplySchedule(
                    weekdayFrom: "08:00", weekdayUntil: "21:30", weekdayMinutes: 180,
                    weekendFrom: "08:00", weekendUntil: "21:30", weekendMinutes: 180);
                break;
            case SetupTemplate.FamilyRoutine:
                Mode = UsageMode.Family;
                PersonalLevel = PersonalProtectionLevel.Balanced;
                ApplySchedule(
                    weekdayFrom: "08:00", weekdayUntil: "20:00", weekdayMinutes: 120,
                    weekendFrom: "09:00", weekendUntil: "21:00", weekendMinutes: 180);
                break;
        }
    }

    public void ClearSelectedTemplate() => SelectedTemplate = null;

    private void ApplySchedule(
        string weekdayFrom,
        string weekdayUntil,
        int weekdayMinutes,
        string weekendFrom,
        string weekendUntil,
        int weekendMinutes)
    {
        foreach (SetupScheduleDayRow day in Schedule)
        {
            bool weekend = day.Day is DayOfWeek.Saturday or DayOfWeek.Sunday;
            day.IsEnabled = true;
            day.AllowedFromText = weekend ? weekendFrom : weekdayFrom;
            day.AllowedUntilText = weekend ? weekendUntil : weekdayUntil;
            day.DailyLimitText = (weekend ? weekendMinutes : weekdayMinutes)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        DailyLimitMinutes = weekdayMinutes;
        HasCustomSchedule = true;
    }

    public IReadOnlyList<string> EnsureRecoveryCodes()
    {
        if (_plainRecoveryCodes is not null)
        {
            return _plainRecoveryCodes;
        }

        ControlSettings recoverySettings = new();
        _plainRecoveryCodes = RecoveryCodeService.Generate(recoverySettings);
        _recoveryCodeRecords = CloneRecoveryCodes(recoverySettings.RecoveryCodes);
        return _plainRecoveryCodes;
    }

    public ControlSettings ComposeSettings(ControlSettings? existing)
    {
        if (ExistingChoice == SetupChoice.KeepExisting && existing is not null)
        {
            existing.Language = ToLanguagePreference(Language);
            existing.SetupCompleted = true;
            return existing;
        }

        List<DaySchedule> schedule = HasCustomSchedule
            ? Schedule.Select(item => item.ToModel()).ToList()
            : ControlSettings.CreateDefaultSchedule()
                .Select(day => new DaySchedule
                {
                    Day = day.Day,
                    IsEnabled = day.IsEnabled,
                    AllowedFrom = day.AllowedFrom,
                    AllowedUntil = day.AllowedUntil,
                    DailyLimitMinutes = DailyLimitMinutes
                })
                .ToList();
        int defaultDailyLimit = schedule.FirstOrDefault(day => day.IsEnabled)?.DailyLimitMinutes
            ?? DailyLimitMinutes;

        ControlSettings settings = new()
        {
            SchemaVersion = 9,
            SetupCompleted = true,
            Mode = Mode,
            PersonalProtectionLevel = Mode == UsageMode.Personal
                ? PersonalLevel
                : PersonalProtectionLevel.Balanced,
            StrictPersonalMode = Mode == UsageMode.Personal &&
                PersonalLevel != PersonalProtectionLevel.Flexible,
            DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName.Trim(),
            DefaultDailyLimitMinutes = defaultDailyLimit,
            Language = ToLanguagePreference(Language),
            StartWithWindows = StartWithWindows,
            AwarenessTrackingEnabled = Mode == UsageMode.Insights || AwarenessTracking,
            Schedule = schedule
        };

        settings.AdminPin = Mode switch
        {
            UsageMode.Family when AdminPinService.IsValidFormat(AdminPin ?? string.Empty) =>
                AdminPinService.Create(AdminPin!),
            UsageMode.Personal when PersonalLevel == PersonalProtectionLevel.Protected =>
                AdminPinService.CreateInternalCredential(),
            _ => new AdminCredential()
        };
        if (Mode == UsageMode.Family)
        {
            EnsureRecoveryCodes();
            settings.RecoveryCodes = CloneRecoveryCodes(_recoveryCodeRecords);
        }
        return settings;
    }

    // Setup always opens the control center. Guardian owns the protected session
    // lifecycle and starts that surface independently when the policy requires it.
    public string LaunchArguments => string.Empty;

    private static LanguagePreference ToLanguagePreference(SetupLanguage language) =>
        language == SetupLanguage.English ? LanguagePreference.English : LanguagePreference.Turkish;

    private static List<RecoveryCodeRecord> CloneRecoveryCodes(IEnumerable<RecoveryCodeRecord> codes) =>
        codes.Select(code => new RecoveryCodeRecord
        {
            Id = code.Id,
            Iterations = code.Iterations,
            SaltBase64 = code.SaltBase64,
            HashBase64 = code.HashBase64,
            CreatedAtUtc = code.CreatedAtUtc,
            UsedAtUtc = code.UsedAtUtc
        }).ToList();
}

public sealed class SetupScheduleDayRow
{
    public DayOfWeek Day { get; init; }
    public bool IsEnabled { get; set; } = true;
    public string DayName { get; set; } = string.Empty;
    public string AllowedFromText { get; set; } = "09:00";
    public string AllowedUntilText { get; set; } = "21:00";
    public string DailyLimitText { get; set; } = "180";
    public string EnabledAutomationName { get; set; } = string.Empty;
    public string StartAutomationName { get; set; } = string.Empty;
    public string EndAutomationName { get; set; } = string.Empty;
    public string LimitAutomationName { get; set; } = string.Empty;

    public static SetupScheduleDayRow FromModel(DaySchedule schedule) => new()
    {
        Day = schedule.Day,
        IsEnabled = schedule.IsEnabled,
        AllowedFromText = schedule.AllowedFrom.ToString("HH:mm"),
        AllowedUntilText = schedule.AllowedUntil.ToString("HH:mm"),
        DailyLimitText = schedule.DailyLimitMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    public DaySchedule ToModel()
    {
        _ = TimeOnly.TryParseExact(
            AllowedFromText,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out TimeOnly allowedFrom);
        _ = TimeOnly.TryParseExact(
            AllowedUntilText,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out TimeOnly allowedUntil);
        _ = int.TryParse(DailyLimitText, out int dailyLimitMinutes);
        return new DaySchedule
        {
            Day = Day,
            IsEnabled = IsEnabled,
            AllowedFrom = allowedFrom,
            AllowedUntil = allowedUntil,
            DailyLimitMinutes = Math.Clamp(dailyLimitMinutes, 1, 1440)
        };
    }
}

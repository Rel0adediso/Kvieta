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
    public ControlMode Mode { get; set; } = ControlMode.Personal;
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

    public bool RequiresUserPin => Mode == ControlMode.Protected;
    public bool RequiresGuardian =>
        Mode == ControlMode.Protected ||
        Mode == ControlMode.Personal && PersonalLevel == PersonalProtectionLevel.Guarded;
    public bool UsesScheduledPlan =>
        Mode == ControlMode.Protected ||
        Mode == ControlMode.Personal && PersonalLevel != PersonalProtectionLevel.Flexible;

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
            PersonalProtectionLevel = Mode == ControlMode.Personal
                ? PersonalLevel
                : PersonalProtectionLevel.Balanced,
            StrictPersonalMode = Mode == ControlMode.Personal &&
                PersonalLevel != PersonalProtectionLevel.Flexible,
            DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName.Trim(),
            DefaultDailyLimitMinutes = defaultDailyLimit,
            Language = ToLanguagePreference(Language),
            StartWithWindows = StartWithWindows,
            AwarenessTrackingEnabled = Mode == ControlMode.Awareness || AwarenessTracking,
            Schedule = schedule
        };

        settings.AdminPin = Mode switch
        {
            ControlMode.Protected when AdminPinService.IsValidFormat(AdminPin ?? string.Empty) =>
                AdminPinService.Create(AdminPin!),
            ControlMode.Personal when PersonalLevel == PersonalProtectionLevel.Guarded =>
                AdminPinService.CreateInternalCredential(),
            _ => new AdminCredential()
        };
        if (Mode == ControlMode.Protected)
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

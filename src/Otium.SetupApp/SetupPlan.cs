using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.SetupApp;

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
    public string? AdminPin { get; set; }

    public bool RequiresUserPin => Mode == ControlMode.Protected;
    public bool RequiresGuardian =>
        Mode == ControlMode.Protected ||
        Mode == ControlMode.Personal && PersonalLevel == PersonalProtectionLevel.Guarded;

    public ControlSettings ComposeSettings(ControlSettings? existing)
    {
        if (ExistingChoice == SetupChoice.KeepExisting && existing is not null)
        {
            existing.Language = ToLanguagePreference(Language);
            existing.SetupCompleted = true;
            return existing;
        }

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
            DefaultDailyLimitMinutes = DailyLimitMinutes,
            Language = ToLanguagePreference(Language),
            StartWithWindows = StartWithWindows,
            AwarenessTrackingEnabled = Mode == ControlMode.Awareness || AwarenessTracking
        };

        foreach (DaySchedule day in settings.Schedule)
        {
            day.DailyLimitMinutes = DailyLimitMinutes;
        }

        settings.AdminPin = Mode switch
        {
            ControlMode.Protected when AdminPinService.IsValidFormat(AdminPin ?? string.Empty) =>
                AdminPinService.Create(AdminPin!),
            ControlMode.Personal when PersonalLevel == PersonalProtectionLevel.Guarded =>
                AdminPinService.CreateInternalCredential(),
            _ => new AdminCredential()
        };
        return settings;
    }

    public string LaunchArguments => ExistingChoice == SetupChoice.KeepExisting
        ? string.Empty
        : Mode == ControlMode.Protected ||
          Mode == ControlMode.Personal && PersonalLevel is PersonalProtectionLevel.Balanced or PersonalProtectionLevel.Guarded
            ? "--session"
            : string.Empty;

    private static LanguagePreference ToLanguagePreference(SetupLanguage language) =>
        language == SetupLanguage.English ? LanguagePreference.English : LanguagePreference.Turkish;
}

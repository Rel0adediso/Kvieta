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

    public bool RequiresUserPin => Mode == ControlMode.Protected;
    public bool RequiresGuardian =>
        Mode == ControlMode.Protected ||
        Mode == ControlMode.Personal && PersonalLevel == PersonalProtectionLevel.Guarded;

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

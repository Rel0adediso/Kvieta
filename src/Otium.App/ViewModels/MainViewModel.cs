using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Otium.Core.Models;
using Otium.Core.Services;
using Otium.App.Services;

namespace Otium.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly JsonSettingsStore _settingsStore;
    private readonly JsonUsageStore _usageStore;
    private ControlSettings _settings = new();
    private int _selectedPageIndex;
    private string _deviceName = string.Empty;
    private int _defaultDailyLimitMinutes;
    private string _limitAction = "Oturumu kapat";
    private string _themeMode = "Sistem";
    private string _languageMode = "Türkçe";
    private bool _animationsEnabled = true;
    private bool _startWithWindows;
    private bool _awarenessTrackingEnabled;
    private bool _strictPersonalMode;
    private string _reductionGoal = "Hedef yok";
    private string _retentionPeriod = "90 gün";
    private ControlMode _controlMode = ControlMode.Protected;
    private string _changeDelay = "1 saat";
    private bool _isSidebarExpanded = true;
    private bool _isRhythmBaselineReady;
    private bool _isRhythmGoalMet;
    private string _statusMessage = "Hazır";
    private AppRuleRow? _selectedAppRule;
    private int _usedTodayMinutes;
    private UsageLedger? _lastUsageLedger;
    private string _selectedHistoryDaySummaryText = "—";

    public MainViewModel(JsonSettingsStore? settingsStore = null, JsonUsageStore? usageStore = null)
    {
        _settingsStore = settingsStore ?? new JsonSettingsStore();
        _usageStore = usageStore ?? new JsonUsageStore();
    }

    public ObservableCollection<DayScheduleRow> ScheduleRows { get; } = [];
    public ObservableCollection<TemporaryAllowanceRow> TemporaryAllowances { get; } = [];
    public ObservableCollection<AppRuleRow> AppRules { get; } = [];
    public ObservableCollection<string> LimitActions { get; } = [];
    public ObservableCollection<string> ThemeModes { get; } = [];
    public IReadOnlyList<string> LanguageModes { get; } = ["Türkçe", "English"];
    public ObservableCollection<string> ChangeDelayOptions { get; } = [];
    public ObservableCollection<string> ReductionGoalOptions { get; } = [];
    public ObservableCollection<string> RetentionOptions { get; } = [];
    public ObservableCollection<UsageHistoryDayRow> HistoryDays { get; } = [];
    public ObservableCollection<AppUsageHistoryRow> HistoryApplications { get; } = [];
    public ObservableCollection<AppUsageHistoryRow> HistoryAllApplications { get; } = [];
    public ObservableCollection<UsageHistoryEventRow> HistoryEvents { get; } = [];

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set => SetProperty(ref _selectedPageIndex, IsAwarenessMode && value is 1 or 2 ? 0 : value);
    }

    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    public int DefaultDailyLimitMinutes
    {
        get => _defaultDailyLimitMinutes;
        set
        {
            if (SetProperty(ref _defaultDailyLimitMinutes, Math.Clamp(value, 0, 1440)))
            {
                RefreshOverview();
            }
        }
    }

    public string LimitAction
    {
        get => _limitAction;
        set => SetProperty(ref _limitAction, value);
    }

    public string ThemeMode
    {
        get => _themeMode;
        set => SetProperty(ref _themeMode, value);
    }

    public string LanguageMode
    {
        get => _languageMode;
        set => SetProperty(ref _languageMode, value);
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set => SetProperty(ref _isSidebarExpanded, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set => SetProperty(ref _animationsEnabled, value);
    }

    public bool AwarenessTrackingEnabled
    {
        get => _awarenessTrackingEnabled;
        set => SetProperty(ref _awarenessTrackingEnabled, value);
    }

    public bool StrictPersonalMode
    {
        get => _strictPersonalMode;
        set => SetProperty(ref _strictPersonalMode, value);
    }

    public string ReductionGoal
    {
        get => _reductionGoal;
        set
        {
            if (SetProperty(ref _reductionGoal, value) && _lastUsageLedger is not null)
            {
                BuildRhythm(_lastUsageLedger);
            }
        }
    }

    public string RetentionPeriod
    {
        get => _retentionPeriod;
        set => SetProperty(ref _retentionPeriod, value);
    }

    public ControlMode SelectedControlMode
    {
        get => _controlMode;
        private set
        {
            if (SetProperty(ref _controlMode, value))
            {
                OnPropertyChanged(nameof(ControlModeText));
                OnPropertyChanged(nameof(IsPersonalMode));
                OnPropertyChanged(nameof(IsProtectedMode));
                OnPropertyChanged(nameof(IsAwarenessMode));
                OnPropertyChanged(nameof(HasRestrictions));
                OnPropertyChanged(nameof(TodayDescriptionText));
                OnPropertyChanged(nameof(RhythmPlanMetricLabel));
                if (value == ControlMode.Awareness && SelectedPageIndex is 1 or 2)
                {
                    SelectedPageIndex = 0;
                }
            }
        }
    }

    public string ControlModeText => ModeDisplayName(SelectedControlMode);
    public string BuildInformationText =>
        $"Otium {BuildInfo.Version} · {LocalizationService.Get(BuildInfo.IsDevelopmentBuild ? "DevelopmentTestBuild" : "PublicReleaseBuild")}";
    public bool IsPersonalMode => SelectedControlMode == ControlMode.Personal;
    public bool IsProtectedMode => SelectedControlMode == ControlMode.Protected;
    public bool IsAwarenessMode => SelectedControlMode == ControlMode.Awareness;
    public bool HasRestrictions => SelectedControlMode != ControlMode.Awareness;
    public string TodayDescriptionText => IsAwarenessMode
        ? L("Bugünkü gerçek uygulama kullanımını tek bakışta gör.", "See today's actual application usage at a glance.")
        : LocalizationService.Get("TodayDescription");
    public string RhythmPlanMetricLabel => LocalizationService.Get(IsAwarenessMode ? "RhythmObservedDays" : "RhythmPlanAligned");

    public string ChangeDelay
    {
        get => _changeDelay;
        set => SetProperty(ref _changeDelay, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HeaderStatusText));
            }
        }
    }

    public AppRuleRow? SelectedAppRule
    {
        get => _selectedAppRule;
        set => SetProperty(ref _selectedAppRule, value);
    }

    public int UsedTodayMinutes
    {
        get => _usedTodayMinutes;
        set
        {
            if (SetProperty(ref _usedTodayMinutes, Math.Max(0, value)))
            {
                RefreshOverview();
            }
        }
    }

    public string UsedTodayText => FormatMinutes(UsedTodayMinutes);
    public string TodayLimitText => IsAwarenessMode ? L("Sınırsız", "Unlimited") : FormatMinutes(GetTodayLimit());
    public string UsedTodayDisplayText => $"{LocalizationService.Get("Used")}  {UsedTodayText}";
    public string TodayLimitDisplayText => $"{LocalizationService.Get("Total")}  {TodayLimitText}";
    public string RemainingText => IsAwarenessMode ? L("Sınırsız", "Unlimited") : FormatMinutes(Math.Max(0, GetTodayLimit() - UsedTodayMinutes));
    public double UsagePercent => IsAwarenessMode || GetTodayLimit() == 0
        ? 0
        : Math.Clamp((double)UsedTodayMinutes / GetTodayLimit() * 100, 0, 100);
    public int BlockedAppCount => AppRules.Count(rule => rule.ToModel().Mode == AppRuleMode.Blocked);
    public int RuleCount => AppRules.Count;
    public bool HasNoAppRules => AppRules.Count == 0;
    public string CurrentWindowStatus { get; private set; } = "Program yükleniyor…";
    public string SettingsPath => _settingsStore.FilePath;
    public bool HasAdminPin => _settings.AdminPin.IsConfigured;
    public string AdminPinActionText => HasAdminPin ? LocalizationService.Get("ChangePin") : LocalizationService.Get("CreatePin");
    public bool HasPendingChange => _settings.PendingChange is not null;
    public string HeaderStatusText => HasPendingChange
        ? LocalizationService.Get("PendingChangesHeader")
        : StatusMessage;
    public bool AppliedStartWithWindows => _settings.StartWithWindows;
    public string PendingChangeText => _settings.PendingChange is null
        ? string.Empty
        : $"{LocalizationService.Get("PendingChangeUntil")} {_settings.PendingChange.ApplyAfterUtc.ToLocalTime():dd.MM HH:mm}";
    public string PendingApplyTimeText => _settings.PendingChange is null
        ? string.Empty
        : _settings.PendingChange.ApplyAfterUtc.ToLocalTime().ToString("dd.MM · HH:mm");
    public string PendingChangeDetails => BuildPendingChangeDetails();
    public string HistoryWeekTotalText { get; private set; } = "—";
    public string HistoryDailyAverageText { get; private set; } = "—";
    public string HistoryMostUsedAppText { get; private set; } = "—";
    public string RhythmBaselineText { get; private set; } = "0/7";
    public string RhythmWeekChangeText { get; private set; } = "—";
    public string RhythmPlanAlignedText { get; private set; } = "—";
    public string RhythmReclaimedText { get; private set; } = "—";
    public string RhythmInsightText { get; private set; } = "—";
    public string RhythmGoalStatusText { get; private set; } = "—";
    public string RhythmPeakHourText { get; private set; } = "—";
    public string RhythmPeakHourDetailText { get; private set; } = "—";
    public string RhythmWeekPatternText { get; private set; } = "—";
    public string RhythmWeekPatternDetailText { get; private set; } = "—";
    public bool HasHistoryApplications => HistoryApplications.Count > 0;
    public bool HasNoHistoryApplications => !HasHistoryApplications;
    public bool HasHistoryEvents => HistoryEvents.Count > 0;
    public bool HasNoHistoryEvents => !HasHistoryEvents;
    public string SelectedHistoryDaySummaryText
    {
        get => _selectedHistoryDaySummaryText;
        private set => SetProperty(ref _selectedHistoryDaySummaryText, value);
    }
    public bool IsRhythmBaselineReady => _isRhythmBaselineReady;
    public bool IsRhythmGoalMet => _isRhythmGoalMet;

    public async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsStore.LoadAsync();
            bool settingsRecovered = _settingsStore.LastLoadRecoveredFromBackup;
            DeviceName = _settings.DeviceName is "Kardeş Bilgisayarı" or "Oyun Bilgisayarı" or "Bu Bilgisayar" or "This Computer"
                ? LocalizationService.Get("DefaultDeviceName")
                : _settings.DeviceName;
            DefaultDailyLimitMinutes = _settings.DefaultDailyLimitMinutes;
            LimitAction = ToDisplayAction(_settings.LimitAction);
            ThemeMode = ToDisplayTheme(_settings.Theme);
            LanguageMode = _settings.Language == LanguagePreference.English ? "English" : "Türkçe";
            AnimationsEnabled = MotionService.UserAnimationsEnabled;
            StartWithWindows = _settings.StartWithWindows;
            AwarenessTrackingEnabled = _settings.AwarenessTrackingEnabled;
            StrictPersonalMode = _settings.StrictPersonalMode;
            ReductionGoal = ToDisplayGoal(_settings.WeeklyReductionGoalPercent);
            RetentionPeriod = ToDisplayRetention(_settings.UsageRetentionDays);
            SelectedControlMode = _settings.Mode;
            ChangeDelay = ToDisplayDelay(_settings.PersonalChangeDelayMinutes);
            RefreshLocalizedCollections(_settings.LimitAction, _settings.Theme, _settings.WeeklyReductionGoalPercent);
            RefreshRetentionOptions(_settings.UsageRetentionDays);
            NotifyPendingChange();
            OnPropertyChanged(nameof(HasAdminPin));
            OnPropertyChanged(nameof(AdminPinActionText));

            LoadPolicyRows(_settings);

            await ReloadUsageAsync();
            bool usageRecovered = _usageStore.LastLoadRecoveredFromBackup;
            RefreshOverview();
            StatusMessage = settingsRecovered || usageRecovered
                ? L("Veriler son sağlam yedekten kurtarıldı", "Data was recovered from the last known good backup")
                : L("Ayarlar yüklendi", "Settings loaded");
        }
        catch (Exception exception)
        {
            StatusMessage = $"{L("Ayarlar okunamadı", "Could not read settings")}: {exception.Message}";
        }
    }

    public async Task ReloadUsageAsync()
    {
        UsageLedger ledger = await _usageStore.LoadAsync();
        _lastUsageLedger = ledger;
        UsedTodayMinutes = ledger.LocalDay == DateOnly.FromDateTime(DateTime.Today)
            ? (int)((IsAwarenessMode ? ledger.AwarenessUsedSeconds : ledger.UsedSeconds) / 60)
            : 0;
        BuildUsageHistory(ledger);
        BuildRhythm(ledger);
    }

    public async Task<bool> ApplyPendingIfDueAsync()
    {
        if (_settings.PendingChange is null || _settings.PendingChange.ApplyAfterUtc > DateTimeOffset.UtcNow)
        {
            return false;
        }

        _settings = await _settingsStore.LoadAsync();
        StartWithWindows = _settings.StartWithWindows;
        AwarenessTrackingEnabled = _settings.AwarenessTrackingEnabled;
        StrictPersonalMode = _settings.StrictPersonalMode;
        ReductionGoal = ToDisplayGoal(_settings.WeeklyReductionGoalPercent);
        RetentionPeriod = ToDisplayRetention(_settings.UsageRetentionDays);
        SelectedControlMode = _settings.Mode;
        ChangeDelay = ToDisplayDelay(_settings.PersonalChangeDelayMinutes);
        LoadPolicyRows(_settings);
        NotifyPendingChange();
        OnPropertyChanged(nameof(HasAdminPin));
        OnPropertyChanged(nameof(AdminPinActionText));
        RefreshOverview();
        StatusMessage = L("Bekleyen değişiklik uygulandı", "Pending change applied");
        OnPropertyChanged(nameof(AppliedStartWithWindows));
        return true;
    }

    public async Task<bool> SaveAsync()
    {
        try
        {
            if (SelectedControlMode == ControlMode.Protected && !HasAdminPin)
            {
                StatusMessage = L("Korumalı kullanım için yönetici PIN'i oluştur.", "Create an administrator PIN for protected mode.");
                return false;
            }

            List<DaySchedule> schedule = [];
            foreach (DayScheduleRow row in ScheduleRows)
            {
                if (!row.TryBuild(out DaySchedule day))
                {
                    StatusMessage = $"{row.DayName}: {L("saat HH:mm biçiminde olmalı", "time must use HH:mm format")}.";
                    return false;
                }

                schedule.Add(day);
            }

            ControlSettings desired = new()
            {
                SchemaVersion = 8,
                SetupCompleted = true,
                Mode = SelectedControlMode,
                DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? LocalizationService.Get("DefaultDeviceName") : DeviceName.Trim(),
                DefaultDailyLimitMinutes = DefaultDailyLimitMinutes,
                LimitAction = FromDisplayAction(LimitAction),
                Theme = FromDisplayTheme(ThemeMode),
                Language = FromDisplayLanguage(LanguageMode),
                StartWithWindows = StartWithWindows,
                AwarenessTrackingEnabled = SelectedControlMode == ControlMode.Awareness || AwarenessTrackingEnabled,
                UsageRetentionDays = FromDisplayRetention(RetentionPeriod),
                PersonalChangeDelayMinutes = FromDisplayDelay(ChangeDelay),
                StrictPersonalMode = StrictPersonalMode,
                WeeklyReductionGoalPercent = FromDisplayGoal(ReductionGoal),
                AdminPin = _settings.AdminPin,
                RecoveryCodes = CloneRecoveryCodes(_settings.RecoveryCodes),
                WarningMinutes = [15, 5, 1],
                Schedule = schedule,
                TemporaryAllowances = TemporaryAllowances.Select(row => row.ToModel()).ToList(),
                AppRules = AppRules.Select(row => row.ToModel()).ToList()
            };
            bool policyChanged = !SettingsEquivalent(_settings, desired);

            if (SelectedControlMode == ControlMode.Personal && SettingsPolicyComparer.HasRelaxation(_settings, desired))
            {
                ControlSettings immediate = CloneSettings(desired);
                immediate.StartWithWindows = _settings.StartWithWindows;
                immediate.PersonalChangeDelayMinutes = _settings.PersonalChangeDelayMinutes;
                immediate.StrictPersonalMode = _settings.StrictPersonalMode || desired.StrictPersonalMode;
                immediate.Schedule = CloneSchedule(_settings.Schedule);
                immediate.TemporaryAllowances = CloneTemporaryAllowances(_settings.TemporaryAllowances);
                immediate.AppRules = CloneAppRules(_settings.AppRules);
                desired.PendingChange = null;
                immediate.PendingChange = new PendingPolicyChange
                {
                    RequestedAtUtc = DateTimeOffset.UtcNow,
                    ApplyAfterUtc = DateTimeOffset.UtcNow.AddMinutes(_settings.PersonalChangeDelayMinutes),
                    TargetSettings = desired
                };
                _settings = immediate;
                await _settingsStore.SaveAsync(_settings);
                await _usageStore.TrimHistoryAsync(_settings.UsageRetentionDays);
                if (policyChanged)
                {
                    await RecordPolicyChangeAsync();
                }
                OnPropertyChanged(nameof(AppliedStartWithWindows));
                StartWithWindows = _settings.StartWithWindows;
                LoadPolicyRows(_settings);
                NotifyPendingChange();
                RefreshOverview();
                StatusMessage = BuildPendingStatusMessage();
                return true;
            }

            if (SelectedControlMode == ControlMode.Personal && _settings.PendingChange is { } existingPending)
            {
                ControlSettings updatedTarget = CloneSettings(existingPending.TargetSettings);
                MergeImmediateChangesIntoPendingTarget(_settings, desired, updatedTarget);
                desired.PendingChange = new PendingPolicyChange
                {
                    Id = existingPending.Id,
                    RequestedAtUtc = existingPending.RequestedAtUtc,
                    ApplyAfterUtc = existingPending.ApplyAfterUtc,
                    TargetSettings = updatedTarget
                };
            }
            else
            {
                desired.PendingChange = null;
            }
            _settings = desired;

            await _settingsStore.SaveAsync(_settings);
            await _usageStore.TrimHistoryAsync(_settings.UsageRetentionDays);
            if (policyChanged)
            {
                await RecordPolicyChangeAsync();
            }
            OnPropertyChanged(nameof(AppliedStartWithWindows));
            NotifyPendingChange();
            RefreshOverview();
            StatusMessage = $"{L("Kaydedildi", "Saved")} · {DateTime.Now:HH:mm:ss}";
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"{L("Kaydetme başarısız", "Save failed")}: {exception.Message}";
            return false;
        }
    }

    public void AddApplication(string executablePath)
    {
        if (AppRules.Any(rule => string.Equals(rule.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = L("Bu uygulama zaten listede.", "This application is already in the list.");
            return;
        }

        AppRules.Add(new AppRuleRow(ApplicationIdentityService.CaptureRule(executablePath)));

        RefreshOverview();
        StatusMessage = L(
            "Program kalıcı kapalı olarak eklendi · Kuralı uygulamak için Kaydet'e bas.",
            "Program added as permanently blocked · Press Save to apply the rule.");
    }

    public void AddTemporaryAllowance(TemporaryAllowance allowance)
    {
        TemporaryAllowances.Add(new TemporaryAllowanceRow(allowance));
        RefreshOverview();
    }

    public void RemoveTemporaryAllowance(TemporaryAllowanceRow allowance)
    {
        TemporaryAllowances.Remove(allowance);
        RefreshOverview();
    }

    public void RemoveSelectedApplication()
    {
        RemoveApplication(SelectedAppRule);
    }

    public void RemoveApplication(AppRuleRow? rule)
    {
        if (rule is null)
        {
            StatusMessage = L("Önce kaldırılacak uygulamayı seç.", "Select an application to remove first.");
            return;
        }

        AppRules.Remove(rule);
        if (ReferenceEquals(SelectedAppRule, rule))
        {
            SelectedAppRule = null;
        }

        RefreshOverview();
        StatusMessage = L(
            "Uygulama kuralı kaldırıldı · Uygulamak için Kaydet'e bas.",
            "Application rule removed · Press Save to apply the change.");
    }

    public bool VerifyAdminPin(string pin)
    {
        return AdminPinService.Verify(pin, _settings.AdminPin);
    }

    public string ExportSettingsJson()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(_settings, options);
    }

    public async Task RestoreSettingsAsync(ControlSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _settingsStore.SaveAsync(CloneSettings(settings));
        await InitializeAsync();
    }

    public async Task RestoreLastKnownGoodSettingsAsync()
    {
        await _settingsStore.RestoreBackupAsync();
        await InitializeAsync();
    }

    public async Task ClearClockAnomalyAsync()
    {
        await _usageStore.ClearClockAnomalyAsync(
            DateTimeOffset.Now,
            WindowsMonotonicClock.Uptime,
            WindowsMonotonicClock.GetBootId());
        await ReloadUsageAsync();
    }

    public async Task<string> ExportUsageJsonAsync()
    {
        UsageLedger ledger = await _usageStore.LoadAsync();
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(ledger, options);
    }

    public async Task<string> ExportUsageCsvAsync()
    {
        UsageLedger ledger = await _usageStore.LoadAsync();
        List<DailyUsageRecord> days = [.. ledger.History];
        if (ledger.LocalDay == DateOnly.FromDateTime(DateTime.Today))
        {
            Dictionary<Guid, string> ruleNames = _settings.AppRules.ToDictionary(rule => rule.Id, rule => rule.Name);
            days.RemoveAll(day => day.LocalDay == ledger.LocalDay);
            days.Add(new DailyUsageRecord
            {
                LocalDay = ledger.LocalDay,
                UsedSeconds = ledger.UsedSeconds,
                AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
                AwarenessHourlyUsedSeconds = new Dictionary<int, long>(ledger.AwarenessHourlyUsedSeconds),
                Applications = ledger.AppUsedSeconds.Select(item => new AppUsageRecord
                {
                    RuleId = item.Key,
                    Name = ruleNames.GetValueOrDefault(item.Key, L("Uygulama", "Application")),
                    UsedSeconds = item.Value
                }).ToList(),
                ForegroundApplications = ledger.ForegroundAppUsedSeconds.Select(item => new AwarenessAppUsageRecord
                {
                    ApplicationId = item.Key,
                    Name = Path.GetFileNameWithoutExtension(item.Key),
                    UsedSeconds = item.Value
                }).ToList()
            });
        }

        StringBuilder csv = new("date,type,name,seconds,minutes\r\n");
        foreach (DailyUsageRecord day in days.OrderBy(item => item.LocalDay))
        {
            AppendCsvRow(csv, day.LocalDay, "session_total", string.Empty, day.UsedSeconds);
            AppendCsvRow(csv, day.LocalDay, "foreground_total", string.Empty, day.AwarenessUsedSeconds);
            foreach (AppUsageRecord application in day.Applications.OrderByDescending(item => item.UsedSeconds))
            {
                AppendCsvRow(csv, day.LocalDay, "rule_application", application.Name, application.UsedSeconds);
            }
            foreach (AwarenessAppUsageRecord application in day.ForegroundApplications.OrderByDescending(item => item.UsedSeconds))
            {
                AppendCsvRow(csv, day.LocalDay, "foreground_application", application.Name, application.UsedSeconds);
            }
            foreach ((int hour, long seconds) in day.AwarenessHourlyUsedSeconds.OrderBy(item => item.Key))
            {
                AppendCsvRow(csv, day.LocalDay, "foreground_hour", $"{hour:00}:00-{(hour + 1) % 24:00}:00", seconds);
            }
        }
        return csv.ToString();
    }

    public async Task ClearUsageHistoryAsync()
    {
        UsageLedger empty = await _usageStore.ClearAsync();
        _lastUsageLedger = empty;
        BuildUsageHistory(empty);
        BuildRhythm(empty);
        UsedTodayMinutes = 0;
        StatusMessage = L("Kullanım geçmişi bu cihazdan silindi", "Usage history was deleted from this device");
    }

    private static void AppendCsvRow(StringBuilder csv, DateOnly date, string type, string name, long seconds)
    {
        string safeName = name.Length > 0 && name[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? $"'{name}"
            : name;
        string escapedName = $"\"{safeName.Replace("\"", "\"\"")}\"";
        csv.Append(date.ToString("yyyy-MM-dd"))
            .Append(',').Append(type)
            .Append(',').Append(escapedName)
            .Append(',').Append(seconds)
            .Append(',').Append((seconds / 60d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
            .Append("\r\n");
    }

    public async Task SetAdminPinAsync(string pin)
    {
        _settings.AdminPin = AdminPinService.Create(pin);
        OnPropertyChanged(nameof(HasAdminPin));
        OnPropertyChanged(nameof(AdminPinActionText));
        await SaveAsync();
    }

    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync()
    {
        IReadOnlyList<string> codes = RecoveryCodeService.Generate(_settings);
        await _settingsStore.SaveAsync(_settings);
        return codes;
    }

    public async Task SetControlModeAsync(ControlMode mode, string? newPin = null)
    {
        if (SelectedControlMode == ControlMode.Personal && mode != ControlMode.Personal)
        {
            ControlSettings target = CloneSettings(_settings.PendingChange?.TargetSettings ?? _settings);
            target.Mode = mode;
            target.AwarenessTrackingEnabled = mode == ControlMode.Awareness || target.AwarenessTrackingEnabled;
            target.PendingChange = null;
            if (!string.IsNullOrWhiteSpace(newPin))
            {
                target.AdminPin = AdminPinService.Create(newPin);
            }

            _settings.PendingChange = new PendingPolicyChange
            {
                RequestedAtUtc = DateTimeOffset.UtcNow,
                ApplyAfterUtc = DateTimeOffset.UtcNow.AddMinutes(_settings.PersonalChangeDelayMinutes),
                TargetSettings = target
            };
            await _settingsStore.SaveAsync(_settings);
            NotifyPendingChange();
            RefreshOverview();
            StatusMessage = BuildPendingStatusMessage();
            return;
        }

        if (!string.IsNullOrWhiteSpace(newPin))
        {
            _settings.AdminPin = AdminPinService.Create(newPin);
            OnPropertyChanged(nameof(HasAdminPin));
            OnPropertyChanged(nameof(AdminPinActionText));
        }

        SelectedControlMode = mode;
        if (mode == ControlMode.Awareness)
        {
            AwarenessTrackingEnabled = true;
        }
        _settings.PendingChange = null;
        await SaveAsync();
        StatusMessage = mode switch
        {
            ControlMode.Protected => L("Korumalı kullanıma geçildi", "Switched to protected mode"),
            ControlMode.Awareness => L("Farkındalık moduna geçildi · Kısıtlama yok", "Switched to awareness mode · No restrictions"),
            _ => L("Kişisel kullanıma geçildi", "Switched to personal mode")
        };
    }

    public void RefreshOverview()
    {
        ControlSettings previewSettings = BuildPreviewSettings();
        ScheduleStatus status = ScheduleEvaluator.Evaluate(previewSettings, DateTimeOffset.Now);
        CurrentWindowStatus = IsAwarenessMode
            ? L("Sadece yerel ölçüm açık · Hiçbir kısıtlama uygulanmıyor", "Local tracking only · No restrictions are applied")
            : status.Reason;

        OnPropertyChanged(nameof(CurrentWindowStatus));
        OnPropertyChanged(nameof(UsedTodayText));
        OnPropertyChanged(nameof(TodayLimitText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(UsagePercent));
        OnPropertyChanged(nameof(UsedTodayDisplayText));
        OnPropertyChanged(nameof(TodayLimitDisplayText));
        OnPropertyChanged(nameof(BlockedAppCount));
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(HasNoAppRules));
        OnPropertyChanged(nameof(PendingChangeText));
        OnPropertyChanged(nameof(PendingChangeDetails));
    }

    private void BuildUsageHistory(UsageLedger ledger)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        List<DailyUsageRecord> records = ledger.History
            .Where(item => item.LocalDay >= today.AddDays(-6) && item.LocalDay < today)
            .ToList();
        if (ledger.LocalDay == today)
        {
            Dictionary<Guid, string> names = _settings.AppRules.ToDictionary(rule => rule.Id, rule => rule.Name);
            records.Add(new DailyUsageRecord
            {
                LocalDay = today,
                UsedSeconds = ledger.UsedSeconds,
                BonusMinutes = ledger.BonusMinutes,
                BreakCount = ledger.BreakCount,
                LimitReachedCount = ledger.LimitReachedCount,
                ExtraTimeGrantCount = ledger.ExtraTimeGrantCount,
                AwarenessUsedSeconds = ledger.AwarenessUsedSeconds,
                AwarenessHourlyUsedSeconds = new Dictionary<int, long>(ledger.AwarenessHourlyUsedSeconds),
                Applications = ledger.AppUsedSeconds.Select(item => new AppUsageRecord
                {
                    RuleId = item.Key,
                    Name = names.GetValueOrDefault(item.Key, L("Uygulama", "Application")),
                    UsedSeconds = item.Value
                }).ToList(),
                ForegroundApplications = ledger.ForegroundAppUsedSeconds.Select(item => new AwarenessAppUsageRecord
                {
                    ApplicationId = item.Key,
                    Name = Path.GetFileNameWithoutExtension(item.Key),
                    UsedSeconds = item.Value
                }).ToList()
            });
        }

        Dictionary<DateOnly, DailyUsageRecord> byDay = records
            .GroupBy(item => item.LocalDay)
            .ToDictionary(group => group.Key, group => group.Last());
        long maximum = Math.Max(1, byDay.Values.Select(item => IsAwarenessMode ? item.AwarenessUsedSeconds : item.UsedSeconds).DefaultIfEmpty(0).Max());
        HistoryDays.Clear();
        for (int offset = 6; offset >= 0; offset--)
        {
            DateOnly day = today.AddDays(-offset);
            DailyUsageRecord record = byDay.GetValueOrDefault(day) ?? new DailyUsageRecord { LocalDay = day };
            long displaySeconds = IsAwarenessMode ? record.AwarenessUsedSeconds : record.UsedSeconds;
            HistoryDays.Add(new UsageHistoryDayRow
            {
                Day = day,
                UsedSeconds = displaySeconds,
                RelativePercent = Math.Clamp(displaySeconds * 100d / maximum, 0, 100),
                BreakCount = record.BreakCount,
                LimitReachedCount = record.LimitReachedCount,
                ExtraTimeGrantCount = record.ExtraTimeGrantCount
            });
        }

        Dictionary<string, DoubleCollection> trends = records
            .SelectMany(record => record.ForegroundApplications.Select(application => new
            {
                record.LocalDay,
                application.Name,
                application.UsedSeconds
            }))
            .GroupBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new DoubleCollection(Enumerable.Range(0, 7)
                    .Select(offset => (double)group.Where(item => item.LocalDay == today.AddDays(offset - 6)).Sum(item => item.UsedSeconds))),
                StringComparer.CurrentCultureIgnoreCase);
        foreach (DoubleCollection trend in trends.Values)
        {
            trend.Freeze();
        }

        List<AppUsageHistoryRow> applications = records
            .SelectMany(item => item.ForegroundApplications)
            .GroupBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => CreateAppUsageRow(0, group.Key, group.Sum(item => item.UsedSeconds), 0, trends.GetValueOrDefault(group.Key)))
            .OrderByDescending(item => item.UsedSeconds)
            .ToList();
        long maximumApp = Math.Max(1, applications.Select(item => item.UsedSeconds).DefaultIfEmpty(0).Max());
        List<AppUsageHistoryRow> rankedApplications = applications
            .Select((application, index) => CreateAppUsageRow(
                index + 1,
                application.Name,
                application.UsedSeconds,
                Math.Clamp(application.UsedSeconds * 100d / maximumApp, 0, 100),
                application.TrendValues))
            .ToList();
        HistoryAllApplications.Clear();
        foreach (AppUsageHistoryRow application in rankedApplications)
        {
            HistoryAllApplications.Add(application);
        }

        HistoryApplications.Clear();
        foreach (AppUsageHistoryRow application in rankedApplications.Take(3))
        {
            HistoryApplications.Add(application);
        }

        HistoryEvents.Clear();
        foreach (UsageEventRecord historyEvent in ledger.RecentEvents.OrderByDescending(item => item.OccurredAtUtc).Take(5))
        {
            HistoryEvents.Add(new UsageHistoryEventRow { Event = historyEvent });
        }

        int elapsedWeekDays = ((int)today.DayOfWeek + 6) % 7 + 1;
        DateOnly weekStart = today.AddDays(-(elapsedWeekDays - 1));
        long weekSeconds = records
            .Where(item => item.LocalDay >= weekStart)
            .Sum(item => IsAwarenessMode ? item.AwarenessUsedSeconds : item.UsedSeconds);
        HistoryWeekTotalText = UsageHistoryFormatting.FormatDuration(weekSeconds);
        HistoryDailyAverageText = UsageHistoryFormatting.FormatDuration(weekSeconds / elapsedWeekDays);
        HistoryMostUsedAppText = rankedApplications.FirstOrDefault()?.Name ?? "—";
        OnPropertyChanged(nameof(HistoryWeekTotalText));
        OnPropertyChanged(nameof(HistoryDailyAverageText));
        OnPropertyChanged(nameof(HistoryMostUsedAppText));
        OnPropertyChanged(nameof(HasHistoryApplications));
        OnPropertyChanged(nameof(HasNoHistoryApplications));
        OnPropertyChanged(nameof(HasHistoryEvents));
        OnPropertyChanged(nameof(HasNoHistoryEvents));
        if (HistoryDays.LastOrDefault() is { } latestDay)
        {
            SelectHistoryDay(latestDay);
        }
    }

    private void BuildRhythm(UsageLedger ledger)
    {
        ControlSettings rhythmSettings = CloneSettings(_settings);
        rhythmSettings.WeeklyReductionGoalPercent = FromDisplayGoal(ReductionGoal);
        RhythmSummary summary = RhythmAnalyzer.Analyze(rhythmSettings, ledger, DateOnly.FromDateTime(DateTime.Today));
        if (_isRhythmBaselineReady != summary.IsBaselineReady)
        {
            _isRhythmBaselineReady = summary.IsBaselineReady;
            OnPropertyChanged(nameof(IsRhythmBaselineReady));
        }

        bool goalMet = FromDisplayGoal(ReductionGoal) > 0 && summary.IsGoalMet;
        if (_isRhythmGoalMet != goalMet)
        {
            _isRhythmGoalMet = goalMet;
            OnPropertyChanged(nameof(IsRhythmGoalMet));
        }

        RhythmBaselineText = summary.IsBaselineReady
            ? UsageHistoryFormatting.FormatDuration(summary.BaselineDailyAverageSeconds)
            : $"{summary.BaselineDays}/7 {L("gün", "days")}";
        RhythmWeekChangeText = summary.WeekChangePercent is { } change
            ? change <= 0
                ? $"↓ %{Math.Abs(change):0}"
                : $"↑ %{change:0}"
            : "—";
        RhythmPlanAlignedText = summary.CurrentObservedDays == 0
            ? "—"
            : IsAwarenessMode
                ? $"{summary.CurrentObservedDays}/7 {L("gün", "days")}"
                : $"{summary.PlanAlignedDays}/{summary.CurrentObservedDays} {L("gün", "days")}";
        RhythmReclaimedText = summary.IsBaselineReady
            ? UsageHistoryFormatting.FormatDuration(summary.ReclaimedSeconds)
            : "—";

        if (!_settings.AwarenessTrackingEnabled)
        {
            RhythmInsightText = L(
                "Ritim farkındalığını açtığında karşılaştırmalar yalnız cihazında oluşur.",
                "Enable rhythm awareness to build private, on-device comparisons.");
        }
        else if (!summary.IsBaselineReady)
        {
            RhythmInsightText = L(
                $"Başlangıç ritmin oluşuyor · {summary.BaselineDays}/7 gün tamamlandı.",
                $"Your starting rhythm is taking shape · {summary.BaselineDays}/7 days complete.");
        }
        else if (summary.WeekChangePercent is { } weeklyChange)
        {
            RhythmInsightText = weeklyChange <= 0
                ? L($"Günlük ortalaman önceki haftaya göre %{Math.Abs(weeklyChange):0} daha sakin.", $"Your daily average is {Math.Abs(weeklyChange):0}% lighter than last week.")
                : L($"Günlük ortalaman önceki haftaya göre %{weeklyChange:0} yükseldi; yalnızca fark etmen yeterli.", $"Your daily average rose {weeklyChange:0}% from last week; noticing it is enough.");
        }
        else
        {
            RhythmInsightText = L("Karşılaştırma için bir önceki haftanın verisi bekleniyor.", "Waiting for a previous week to compare.");
        }

        if (summary.RisingApplication is { } rising && summary.IsBaselineReady)
        {
            RhythmInsightText += L($" En çok yükselen: {rising}.", $" Biggest increase: {rising}.");
        }

        RhythmGoalStatusText = FromDisplayGoal(ReductionGoal) == 0
            ? L("Küçük bir hedef seçmek isteğe bağlıdır.", "Choosing a small goal is optional.")
            : !summary.IsBaselineReady
                ? L("Hedefin başlangıç ritmi tamamlanınca devreye girecek.", "Your goal will begin after the starting rhythm is ready.")
                : summary.IsGoalMet
                    ? L($"Hedef ritminde · günlük {UsageHistoryFormatting.FormatDuration(summary.GoalDailySeconds)}", $"On target · {UsageHistoryFormatting.FormatDuration(summary.GoalDailySeconds)} daily")
                    : L($"Nazik hedef · günlük {UsageHistoryFormatting.FormatDuration(summary.GoalDailySeconds)}", $"Gentle target · {UsageHistoryFormatting.FormatDuration(summary.GoalDailySeconds)} daily");

        RhythmPeakHourText = summary.PeakHour is { } peakHour
            ? $"{peakHour:00}:00–{(peakHour + 1) % 24:00}:00"
            : L("Veri oluşuyor", "Building data");
        RhythmPeakHourDetailText = summary.PeakHour is not null
            ? UsageHistoryFormatting.FormatDuration(summary.PeakHourSeconds)
            : L("Saatlik ölçüm yeni başladı", "Hourly tracking has just started");
        RhythmWeekPatternText = summary.WeekendDifferencePercent is { } weekendDifference
            ? weekendDifference >= 0
                ? L($"Hafta sonu +%{weekendDifference:0}", $"Weekend +{weekendDifference:0}%")
                : L($"Hafta içi +%{Math.Abs(weekendDifference):0}", $"Weekdays +{Math.Abs(weekendDifference):0}%")
            : L("Veri oluşuyor", "Building data");
        RhythmWeekPatternDetailText = summary.WeekendDifferencePercent is not null
            ? L(
                $"Hafta içi {UsageHistoryFormatting.FormatDuration(summary.WeekdayDailyAverageSeconds)} · hafta sonu {UsageHistoryFormatting.FormatDuration(summary.WeekendDailyAverageSeconds)}",
                $"Weekdays {UsageHistoryFormatting.FormatDuration(summary.WeekdayDailyAverageSeconds)} · weekend {UsageHistoryFormatting.FormatDuration(summary.WeekendDailyAverageSeconds)}")
            : L($"{summary.WeekdayObservedDays} hafta içi · {summary.WeekendObservedDays} hafta sonu günü", $"{summary.WeekdayObservedDays} weekday · {summary.WeekendObservedDays} weekend days");

        OnPropertyChanged(nameof(RhythmBaselineText));
        OnPropertyChanged(nameof(RhythmWeekChangeText));
        OnPropertyChanged(nameof(RhythmPlanAlignedText));
        OnPropertyChanged(nameof(RhythmReclaimedText));
        OnPropertyChanged(nameof(RhythmInsightText));
        OnPropertyChanged(nameof(RhythmGoalStatusText));
        OnPropertyChanged(nameof(RhythmPeakHourText));
        OnPropertyChanged(nameof(RhythmPeakHourDetailText));
        OnPropertyChanged(nameof(RhythmWeekPatternText));
        OnPropertyChanged(nameof(RhythmWeekPatternDetailText));
    }

    private async Task RecordPolicyChangeAsync()
    {
        UsageLedger ledger = await _usageStore.LoadAsync();
        ledger.RecentEvents.Add(new UsageEventRecord
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Kind = UsageEventKind.PolicyChanged
        });
        if (ledger.RecentEvents.Count > 200)
        {
            ledger.RecentEvents.RemoveRange(0, ledger.RecentEvents.Count - 200);
        }
        await _usageStore.SaveAsync(ledger);
        _lastUsageLedger = ledger;
        BuildUsageHistory(ledger);
    }

    public void SelectHistoryDay(UsageHistoryDayRow selectedDay)
    {
        foreach (UsageHistoryDayRow day in HistoryDays)
        {
            day.IsSelected = ReferenceEquals(day, selectedDay);
        }

        SelectedHistoryDaySummaryText = selectedDay.SummaryText;
    }

    private static AppUsageHistoryRow CreateAppUsageRow(
        int rank,
        string name,
        long usedSeconds,
        double relativePercent,
        DoubleCollection? trendValues = null) =>
        new()
        {
            Rank = rank,
            Name = name,
            UsedSeconds = usedSeconds,
            RelativePercent = relativePercent,
            Icon = ApplicationIconProvider.GetIcon(name),
            FallbackBrush = ApplicationIconProvider.GetFallbackBrush(name),
            TrendValues = trendValues ?? []
        };

    private static bool SettingsEquivalent(ControlSettings left, ControlSettings right)
    {
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        ControlSettings leftCopy = CloneSettings(left);
        ControlSettings rightCopy = CloneSettings(right);
        leftCopy.PendingChange = null;
        rightCopy.PendingChange = null;
        return JsonSerializer.Serialize(leftCopy, options) == JsonSerializer.Serialize(rightCopy, options);
    }

    private static void MergeImmediateChangesIntoPendingTarget(
        ControlSettings current,
        ControlSettings desired,
        ControlSettings target)
    {
        if (current.Mode != desired.Mode) target.Mode = desired.Mode;
        if (current.DeviceName != desired.DeviceName) target.DeviceName = desired.DeviceName;
        if (current.DefaultDailyLimitMinutes != desired.DefaultDailyLimitMinutes) target.DefaultDailyLimitMinutes = desired.DefaultDailyLimitMinutes;
        if (current.LimitAction != desired.LimitAction) target.LimitAction = desired.LimitAction;
        if (current.Theme != desired.Theme) target.Theme = desired.Theme;
        if (current.Language != desired.Language) target.Language = desired.Language;
        if (current.StartWithWindows != desired.StartWithWindows) target.StartWithWindows = desired.StartWithWindows;
        if (current.AwarenessTrackingEnabled != desired.AwarenessTrackingEnabled) target.AwarenessTrackingEnabled = desired.AwarenessTrackingEnabled;
        if (current.UsageRetentionDays != desired.UsageRetentionDays) target.UsageRetentionDays = desired.UsageRetentionDays;
        if (current.PersonalChangeDelayMinutes != desired.PersonalChangeDelayMinutes) target.PersonalChangeDelayMinutes = desired.PersonalChangeDelayMinutes;
        if (current.StrictPersonalMode != desired.StrictPersonalMode) target.StrictPersonalMode = desired.StrictPersonalMode;
        if (current.WeeklyReductionGoalPercent != desired.WeeklyReductionGoalPercent) target.WeeklyReductionGoalPercent = desired.WeeklyReductionGoalPercent;
        if (!JsonEquivalent(current.AdminPin, desired.AdminPin)) target.AdminPin = desired.AdminPin;
        if (!JsonEquivalent(current.WarningMinutes, desired.WarningMinutes)) target.WarningMinutes = [.. desired.WarningMinutes];

        MergeChangedItems(
            current.Schedule,
            desired.Schedule,
            target.Schedule,
            item => item.Day,
            CloneDaySchedule);
        MergeChangedItems(
            current.TemporaryAllowances,
            desired.TemporaryAllowances,
            target.TemporaryAllowances,
            item => item.Id,
            CloneTemporaryAllowance);
        MergeChangedItems(
            current.AppRules,
            desired.AppRules,
            target.AppRules,
            item => item.Id,
            CloneAppRule);
    }

    private static void MergeChangedItems<T, TKey>(
        IReadOnlyCollection<T> current,
        IReadOnlyCollection<T> desired,
        List<T> target,
        Func<T, TKey> keySelector,
        Func<T, T> clone)
        where TKey : notnull
    {
        Dictionary<TKey, T> currentByKey = current.ToDictionary(keySelector);
        Dictionary<TKey, T> desiredByKey = desired.ToDictionary(keySelector);

        foreach (TKey removedKey in currentByKey.Keys.Except(desiredByKey.Keys))
        {
            target.RemoveAll(item => EqualityComparer<TKey>.Default.Equals(keySelector(item), removedKey));
        }

        foreach ((TKey key, T desiredItem) in desiredByKey)
        {
            if (currentByKey.TryGetValue(key, out T? currentItem) && JsonEquivalent(currentItem, desiredItem))
            {
                continue;
            }

            int targetIndex = target.FindIndex(item => EqualityComparer<TKey>.Default.Equals(keySelector(item), key));
            if (targetIndex >= 0)
            {
                target[targetIndex] = clone(desiredItem);
            }
            else
            {
                target.Add(clone(desiredItem));
            }
        }
    }

    private static bool JsonEquivalent<T>(T left, T right) =>
        JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    public async Task CancelPendingChangeAsync()
    {
        if (_settings.PendingChange is null)
        {
            return;
        }

        _settings.PendingChange = null;
        await _settingsStore.SaveAsync(_settings);
        NotifyPendingChange();
        StatusMessage = L("Bekleyen değişiklik iptal edildi", "Pending change canceled");
    }

    public async Task<bool> ForceApplyPendingForTestingAsync()
    {
        if (_settings.PendingChange is null)
        {
            StatusMessage = L("Atlanacak bekleyen değişiklik yok", "There is no pending change to skip");
            return false;
        }

        _settings.PendingChange.ApplyAfterUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _settingsStore.SaveAsync(_settings);
        await InitializeAsync();
        StatusMessage = L("Test atlaması · Bekleyen değişiklik hemen uygulandı", "Test bypass · Pending change applied now");
        return true;
    }

    public void ChangeLanguage(LanguagePreference language)
    {
        bool usesDefaultDeviceName = DeviceName is "Kardeş Bilgisayarı" or "Oyun Bilgisayarı" or "Bu Bilgisayar" or "This Computer";
        int changeDelayMinutes = FromDisplayDelay(ChangeDelay);
        int reductionGoalPercent = FromDisplayGoal(ReductionGoal);
        int retentionDays = FromDisplayRetention(RetentionPeriod);
        LimitReachedAction currentAction = FromDisplayAction(LimitAction);
        ThemePreference currentTheme = FromDisplayTheme(ThemeMode);
        List<DaySchedule> schedule = ScheduleRows.Select(row =>
        {
            row.TryBuild(out DaySchedule day);
            return day;
        }).ToList();
        List<AppRule> rules = AppRules.Select(row => row.ToModel()).ToList();

        LocalizationService.SetLanguage(System.Windows.Application.Current, language);
        if (usesDefaultDeviceName)
        {
            DeviceName = LocalizationService.Get("DefaultDeviceName");
        }
        RefreshLocalizedCollections(currentAction, currentTheme, reductionGoalPercent);
        RefreshRetentionOptions(retentionDays);
        ChangeDelay = ToDisplayDelay(changeDelayMinutes);

        ScheduleRows.Clear();
        foreach (DaySchedule day in schedule)
        {
            ScheduleRows.Add(new DayScheduleRow(day));
        }

        AppRules.Clear();
        foreach (AppRule rule in rules)
        {
            AppRules.Add(new AppRuleRow(rule));
        }

        OnPropertyChanged(nameof(AdminPinActionText));
        OnPropertyChanged(nameof(ControlModeText));
        OnPropertyChanged(nameof(BuildInformationText));
        OnPropertyChanged(nameof(TodayDescriptionText));
        OnPropertyChanged(nameof(RhythmPlanMetricLabel));
        NotifyPendingChange();
        if (_lastUsageLedger is not null)
        {
            BuildUsageHistory(_lastUsageLedger);
            BuildRhythm(_lastUsageLedger);
        }
        RefreshOverview();
        StatusMessage = language == LanguagePreference.English ? "Language changed" : "Dil değiştirildi";
    }

    private void RefreshLocalizedCollections(LimitReachedAction action, ThemePreference theme, int reductionGoalPercent)
    {
        LimitActions.Clear();
        LimitActions.Add(LocalizationService.Get("BlockScreen"));
        LimitActions.Add(LocalizationService.Get("WindowsLock"));
        LimitActions.Add(LocalizationService.Get("SignOut"));
        ThemeModes.Clear();
        ThemeModes.Add(LocalizationService.Get("System"));
        ThemeModes.Add(LocalizationService.Get("Light"));
        ThemeModes.Add(LocalizationService.Get("Dark"));
        LimitAction = ToDisplayAction(action);
        ThemeMode = ToDisplayTheme(theme);
        int delayMinutes = FromDisplayDelay(ChangeDelay);
        ChangeDelayOptions.Clear();
        ChangeDelayOptions.Add(LocalizationService.Get("Delay15Minutes"));
        ChangeDelayOptions.Add(LocalizationService.Get("Delay1Hour"));
        ChangeDelayOptions.Add(LocalizationService.Get("DelayNextDay"));
        ChangeDelay = ToDisplayDelay(delayMinutes);
        ReductionGoalOptions.Clear();
        ReductionGoalOptions.Add(ToDisplayGoal(0));
        ReductionGoalOptions.Add(ToDisplayGoal(5));
        ReductionGoalOptions.Add(ToDisplayGoal(10));
        ReductionGoalOptions.Add(ToDisplayGoal(15));
        ReductionGoal = ToDisplayGoal(reductionGoalPercent);
    }

    private void RefreshRetentionOptions(int retentionDays)
    {
        RetentionOptions.Clear();
        RetentionOptions.Add(ToDisplayRetention(30));
        RetentionOptions.Add(ToDisplayRetention(90));
        RetentionOptions.Add(ToDisplayRetention(180));
        RetentionPeriod = ToDisplayRetention(retentionDays);
    }

    private void NotifyPendingChange()
    {
        OnPropertyChanged(nameof(HasPendingChange));
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(PendingChangeText));
        OnPropertyChanged(nameof(PendingApplyTimeText));
        OnPropertyChanged(nameof(PendingChangeDetails));
    }

    private string BuildPendingStatusMessage()
    {
        PendingPolicyChange? pending = _settings.PendingChange;
        if (pending is null)
        {
            return string.Empty;
        }

        List<DayOfWeek> changedDays = GetChangedScheduleDays(_settings, pending.TargetSettings);
        string subject = changedDays.Count switch
        {
            1 => $"{GetDayName(changedDays[0])} {L("planı", "plan")}",
            > 1 => $"{changedDays.Count} {L("günün planı", "day plans")}",
            _ => L("Kural değişikliği", "Rule change")
        };
        return $"{subject} {L("bekliyor", "is waiting")} · {pending.ApplyAfterUtc.ToLocalTime():dd.MM HH:mm}";
    }

    private string BuildPendingChangeDetails()
    {
        PendingPolicyChange? pending = _settings.PendingChange;
        if (pending is null)
        {
            return string.Empty;
        }

        List<string> details = [];
        if (_settings.Mode != pending.TargetSettings.Mode)
        {
            string currentMode = ModeDisplayName(_settings.Mode);
            string targetMode = ModeDisplayName(pending.TargetSettings.Mode);
            details.Add($"• {LocalizationService.Get("UsageMode")} · {currentMode} → {targetMode}");
        }

        Dictionary<DayOfWeek, DaySchedule> currentDays = _settings.Schedule.ToDictionary(day => day.Day);
        foreach (DaySchedule targetDay in pending.TargetSettings.Schedule.OrderBy(day => day.Day == DayOfWeek.Sunday ? 7 : (int)day.Day))
        {
            if (!currentDays.TryGetValue(targetDay.Day, out DaySchedule? currentDay))
            {
                continue;
            }

            List<string> changes = [];
            if (currentDay.IsEnabled != targetDay.IsEnabled)
            {
                changes.Add(targetDay.IsEnabled ? L("açılacak", "will be enabled") : L("kapatılacak", "will be disabled"));
            }

            if (currentDay.DailyLimitMinutes != targetDay.DailyLimitMinutes)
            {
                changes.Add($"{L("limit", "limit")} {currentDay.DailyLimitMinutes} → {targetDay.DailyLimitMinutes} {LocalizationService.Get("MinuteShort")}");
            }

            if (currentDay.AllowedFrom != targetDay.AllowedFrom || currentDay.AllowedUntil != targetDay.AllowedUntil)
            {
                changes.Add($"{currentDay.AllowedFrom:HH\\:mm}–{currentDay.AllowedUntil:HH\\:mm} → {targetDay.AllowedFrom:HH\\:mm}–{targetDay.AllowedUntil:HH\\:mm}");
            }

            if (changes.Count > 0)
            {
                DateTimeOffset firstImpact = GetFirstImpact(targetDay, pending.ApplyAfterUtc.ToLocalTime());
                details.Add($"• {GetDayName(targetDay.Day)} · {string.Join(", ", changes)} · {L("ilk etki", "first effect")} {firstImpact:dd.MM HH:mm}");
            }
        }

        Dictionary<Guid, TemporaryAllowance> currentAllowances = _settings.TemporaryAllowances.ToDictionary(item => item.Id);
        foreach (TemporaryAllowance allowance in pending.TargetSettings.TemporaryAllowances.OrderBy(item => item.Date).ThenBy(item => item.AllowedFrom))
        {
            if (!currentAllowances.TryGetValue(allowance.Id, out TemporaryAllowance? current) ||
                current.Date != allowance.Date || current.AllowedFrom != allowance.AllowedFrom ||
                current.AllowedUntil != allowance.AllowedUntil || current.BonusMinutes != allowance.BonusMinutes)
            {
                string note = string.IsNullOrWhiteSpace(allowance.Note) ? string.Empty : $" · {allowance.Note}";
                details.Add($"• {L("Geçici izin", "Temporary allowance")} · {allowance.Date:dd.MM} {allowance.AllowedFrom:HH\\:mm}–{allowance.AllowedUntil:HH\\:mm} · +{allowance.BonusMinutes} {LocalizationService.Get("MinuteShort")}{note}");
            }
        }

        if (_settings.StartWithWindows != pending.TargetSettings.StartWithWindows)
        {
            details.Add($"• {LocalizationService.Get("StartWithWindows")} · {(pending.TargetSettings.StartWithWindows ? LocalizationService.Get("Enabled") : L("Kapalı", "Off"))}");
        }

        if (_settings.StrictPersonalMode != pending.TargetSettings.StrictPersonalMode)
        {
            details.Add($"• {LocalizationService.Get("StrictPersonalMode")} · {(pending.TargetSettings.StrictPersonalMode ? LocalizationService.Get("Enabled") : L("Kapatılacak", "Will be disabled"))}");
        }

        if (_settings.PersonalChangeDelayMinutes != pending.TargetSettings.PersonalChangeDelayMinutes)
        {
            details.Add($"• {LocalizationService.Get("RelaxationDelay")} · {FormatDelay(_settings.PersonalChangeDelayMinutes)} → {FormatDelay(pending.TargetSettings.PersonalChangeDelayMinutes)}");
        }

        Dictionary<string, AppRule> currentRules = _settings.AppRules.ToDictionary(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AppRule> targetRules = pending.TargetSettings.AppRules.ToDictionary(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase);
        foreach (AppRule targetRule in pending.TargetSettings.AppRules)
        {
            if (!currentRules.TryGetValue(targetRule.ExecutablePath, out AppRule? currentRule) ||
                currentRule.Mode != targetRule.Mode ||
                currentRule.DailyLimitMinutes != targetRule.DailyLimitMinutes)
            {
                details.Add($"• {targetRule.Name} · {L("uygulama kuralı değişecek", "application rule will change")}");
            }
        }

        foreach (AppRule currentRule in _settings.AppRules)
        {
            if (!targetRules.ContainsKey(currentRule.ExecutablePath))
            {
                details.Add($"• {currentRule.Name} · {L("uygulama kuralı kaldırılacak", "application rule will be removed")}");
            }
        }

        if (details.Count == 0)
        {
            details.Add($"• {L("Kural ayarları güncellenecek", "Rule settings will be updated")}");
        }

        return string.Join(Environment.NewLine, details);
    }

    private static string FormatDelay(int minutes) => minutes switch
    {
        1440 => LocalizationService.Get("DelayNextDay"),
        60 => LocalizationService.Get("Delay1Hour"),
        15 => LocalizationService.Get("Delay15Minutes"),
        _ => $"{minutes} {LocalizationService.Get("MinuteShort")}"
    };

    private static List<DayOfWeek> GetChangedScheduleDays(ControlSettings current, ControlSettings target)
    {
        Dictionary<DayOfWeek, DaySchedule> currentDays = current.Schedule.ToDictionary(day => day.Day);
        return target.Schedule
            .Where(day => currentDays.TryGetValue(day.Day, out DaySchedule? old) &&
                (old.IsEnabled != day.IsEnabled || old.DailyLimitMinutes != day.DailyLimitMinutes || old.AllowedFrom != day.AllowedFrom || old.AllowedUntil != day.AllowedUntil))
            .Select(day => day.Day)
            .Distinct()
            .ToList();
    }

    private static DateTimeOffset GetFirstImpact(DaySchedule targetDay, DateTimeOffset applyAt)
    {
        DateTime localDate = applyAt.LocalDateTime.Date;
        for (int offset = 0; offset <= 7; offset++)
        {
            DateTime date = localDate.AddDays(offset);
            if (date.DayOfWeek != targetDay.Day)
            {
                continue;
            }

            DateTime candidate = date.Add(targetDay.AllowedFrom.ToTimeSpan());
            if (candidate >= applyAt.LocalDateTime)
            {
                return new DateTimeOffset(candidate, applyAt.Offset);
            }

            if (offset == 0 && targetDay.IsEnabled)
            {
                return applyAt;
            }
        }

        return applyAt;
    }

    private static string GetDayName(DayOfWeek day) => new DayScheduleRow(new DaySchedule { Day = day }).DayName;

    private void LoadPolicyRows(ControlSettings settings)
    {
        ScheduleRows.Clear();
        foreach (DaySchedule schedule in settings.Schedule.OrderBy(item => item.Day == DayOfWeek.Sunday ? 7 : (int)item.Day))
        {
            ScheduleRows.Add(new DayScheduleRow(schedule));
        }

        TemporaryAllowances.Clear();
        foreach (TemporaryAllowance allowance in settings.TemporaryAllowances
                     .Where(item => item.Date >= DateOnly.FromDateTime(DateTime.Today))
                     .OrderBy(item => item.Date)
                     .ThenBy(item => item.AllowedFrom))
        {
            TemporaryAllowances.Add(new TemporaryAllowanceRow(allowance));
        }

        AppRules.Clear();
        foreach (AppRule rule in settings.AppRules)
        {
            AppRules.Add(new AppRuleRow(rule));
        }
    }

    private static int FromDisplayDelay(string delay) => delay switch
    {
        var value when value == LocalizationService.Get("Delay15Minutes") => 15,
        var value when value == LocalizationService.Get("DelayNextDay") => 1440,
        _ => 60
    };

    private static string ToDisplayDelay(int minutes) => minutes switch
    {
        15 => LocalizationService.Get("Delay15Minutes"),
        1440 => LocalizationService.Get("DelayNextDay"),
        _ => LocalizationService.Get("Delay1Hour")
    };

    private static int FromDisplayGoal(string goal) => goal switch
    {
        var value when value == ToDisplayGoal(5) => 5,
        var value when value == ToDisplayGoal(10) => 10,
        var value when value == ToDisplayGoal(15) => 15,
        _ => 0
    };

    private static string ToDisplayGoal(int percent) => percent == 0
        ? LocalizationService.Get("RhythmNoGoal")
        : string.Format(LocalizationService.Get("RhythmGoalLessFormat"), percent);

    private static int FromDisplayRetention(string retention) => retention switch
    {
        var value when value == ToDisplayRetention(30) => 30,
        var value when value == ToDisplayRetention(180) => 180,
        _ => 90
    };

    private static string ToDisplayRetention(int days) => string.Format(LocalizationService.Get("RetentionDaysFormat"), days);

    private static string ModeDisplayName(ControlMode mode) => mode switch
    {
        ControlMode.Awareness => LocalizationService.Get("AwarenessModeShort"),
        ControlMode.Personal => LocalizationService.Get("PersonalModeShort"),
        _ => LocalizationService.Get("ProtectedModeShort")
    };

    private static DaySchedule CloneDaySchedule(DaySchedule day) => new()
    {
        Day = day.Day,
        IsEnabled = day.IsEnabled,
        AllowedFrom = day.AllowedFrom,
        AllowedUntil = day.AllowedUntil,
        DailyLimitMinutes = day.DailyLimitMinutes
    };

    private static List<DaySchedule> CloneSchedule(IEnumerable<DaySchedule> schedule) =>
        schedule.Select(CloneDaySchedule).ToList();

    private static AppRule CloneAppRule(AppRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        ExecutablePath = rule.ExecutablePath,
        OriginalFileName = rule.OriginalFileName,
        ProductName = rule.ProductName,
        PublisherName = rule.PublisherName,
        PublisherThumbprint = rule.PublisherThumbprint,
        Sha256 = rule.Sha256,
        RequireSha256 = rule.RequireSha256,
        PackageFamilyName = rule.PackageFamilyName,
        IncludeChildProcesses = rule.IncludeChildProcesses,
        LauncherExecutablePaths = [.. rule.LauncherExecutablePaths],
        Mode = rule.Mode,
        DailyLimitMinutes = rule.DailyLimitMinutes
    };

    private static List<AppRule> CloneAppRules(IEnumerable<AppRule> rules) =>
        rules.Select(CloneAppRule).ToList();

    private static TemporaryAllowance CloneTemporaryAllowance(TemporaryAllowance item) => new()
    {
        Id = item.Id,
        Date = item.Date,
        AllowedFrom = item.AllowedFrom,
        AllowedUntil = item.AllowedUntil,
        BonusMinutes = item.BonusMinutes,
        Note = item.Note
    };

    private static List<TemporaryAllowance> CloneTemporaryAllowances(IEnumerable<TemporaryAllowance> allowances) =>
        allowances.Select(CloneTemporaryAllowance).ToList();

    private static ControlSettings CloneSettings(ControlSettings settings) => new()
    {
        SchemaVersion = settings.SchemaVersion,
        SetupCompleted = settings.SetupCompleted,
        Mode = settings.Mode,
        DeviceName = settings.DeviceName,
        DefaultDailyLimitMinutes = settings.DefaultDailyLimitMinutes,
        LimitAction = settings.LimitAction,
        Theme = settings.Theme,
        Language = settings.Language,
        StartWithWindows = settings.StartWithWindows,
        AwarenessTrackingEnabled = settings.AwarenessTrackingEnabled,
        UsageRetentionDays = settings.UsageRetentionDays,
        PersonalChangeDelayMinutes = settings.PersonalChangeDelayMinutes,
        StrictPersonalMode = settings.StrictPersonalMode,
        WeeklyReductionGoalPercent = settings.WeeklyReductionGoalPercent,
        AdminPin = new AdminCredential
        {
            Version = settings.AdminPin.Version,
            Iterations = settings.AdminPin.Iterations,
            SaltBase64 = settings.AdminPin.SaltBase64,
            HashBase64 = settings.AdminPin.HashBase64
        },
        RecoveryCodes = CloneRecoveryCodes(settings.RecoveryCodes),
        WarningMinutes = [.. settings.WarningMinutes],
        Schedule = CloneSchedule(settings.Schedule),
        TemporaryAllowances = CloneTemporaryAllowances(settings.TemporaryAllowances),
        AppRules = CloneAppRules(settings.AppRules),
        PendingChange = settings.PendingChange is null
            ? null
            : new PendingPolicyChange
            {
                Id = settings.PendingChange.Id,
                RequestedAtUtc = settings.PendingChange.RequestedAtUtc,
                ApplyAfterUtc = settings.PendingChange.ApplyAfterUtc,
                TargetSettings = CloneSettings(settings.PendingChange.TargetSettings)
            }
    };

    private static List<RecoveryCodeRecord> CloneRecoveryCodes(IEnumerable<RecoveryCodeRecord> codes) => codes.Select(code => new RecoveryCodeRecord
    {
        Id = code.Id,
        Iterations = code.Iterations,
        SaltBase64 = code.SaltBase64,
        HashBase64 = code.HashBase64,
        CreatedAtUtc = code.CreatedAtUtc,
        UsedAtUtc = code.UsedAtUtc
    }).ToList();

    private ControlSettings BuildPreviewSettings()
    {
        List<DaySchedule> schedule = [];
        foreach (DayScheduleRow row in ScheduleRows)
        {
            if (row.TryBuild(out DaySchedule day))
            {
                schedule.Add(day);
            }
        }

        return new ControlSettings
        {
            Mode = SelectedControlMode,
            DeviceName = DeviceName,
            DefaultDailyLimitMinutes = DefaultDailyLimitMinutes,
            Schedule = schedule.Count == 0 ? ControlSettings.CreateDefaultSchedule() : schedule,
            TemporaryAllowances = TemporaryAllowances.Select(row => row.ToModel()).ToList(),
            AppRules = AppRules.Select(row => row.ToModel()).ToList()
        };
    }

    private int GetTodayLimit()
    {
        DayScheduleRow? today = ScheduleRows.FirstOrDefault(row => row.Day == DateTime.Today.DayOfWeek);
        int regular = today is { IsEnabled: true } ? today.DailyLimitMinutes : 0;
        int temporary = TemporaryAllowances
            .Where(item => item.Date == DateOnly.FromDateTime(DateTime.Today))
            .Sum(item => item.BonusMinutes);
        return Math.Clamp(regular + temporary, 0, 1440);
    }

    private static string FormatMinutes(int minutes)
    {
        int hours = minutes / 60;
        int remainder = minutes % 60;
        string hour = LocalizationService.CurrentLanguage == LanguagePreference.English ? "hr" : "sa";
        string minute = LocalizationService.Get("MinuteShort");
        return hours > 0 ? $"{hours} {hour} {remainder} {minute}" : $"{remainder} {minute}";
    }

    private static string ToDisplayAction(LimitReachedAction action) => action switch
    {
        LimitReachedAction.ShowBlockScreen => LocalizationService.Get("BlockScreen"),
        LimitReachedAction.LockWindows => LocalizationService.Get("WindowsLock"),
        LimitReachedAction.SignOut => LocalizationService.Get("SignOut"),
        _ => LocalizationService.Get("SignOut")
    };

    private static LimitReachedAction FromDisplayAction(string action) => action switch
    {
        var value when value == LocalizationService.Get("BlockScreen") => LimitReachedAction.ShowBlockScreen,
        var value when value == LocalizationService.Get("WindowsLock") => LimitReachedAction.LockWindows,
        _ => LimitReachedAction.SignOut
    };

    public static ThemePreference FromDisplayTheme(string theme) => theme switch
    {
        var value when value == LocalizationService.Get("Light") => ThemePreference.Light,
        var value when value == LocalizationService.Get("Dark") => ThemePreference.Dark,
        _ => ThemePreference.System
    };

    private static string ToDisplayTheme(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => LocalizationService.Get("Light"),
        ThemePreference.Dark => LocalizationService.Get("Dark"),
        _ => LocalizationService.Get("System")
    };

    public static LanguagePreference FromDisplayLanguage(string language) => language == "English"
        ? LanguagePreference.English
        : LanguagePreference.Turkish;

    private static string L(string turkish, string english) =>
        LocalizationService.CurrentLanguage == LanguagePreference.English ? english : turkish;
}

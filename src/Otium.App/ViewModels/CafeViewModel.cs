using System.Diagnostics;
using System.IO;
using Otium.Core.Models;
using Otium.Core.Services;
using Otium.App.Services;

namespace Otium.App.ViewModels;

public sealed class CafeViewModel : ObservableObject
{
    private JsonSettingsStore _settingsStore;
    private readonly JsonUsageStore _usageStore;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private readonly ApplicationRuleEnforcer _applicationRuleEnforcer = new();
    private readonly ForegroundApplicationTracker _foregroundApplicationTracker = new();
    private ControlSettings? _settings;
    private SessionEngine? _engine;
    private SessionSnapshot? _snapshot;
    private double _uncommittedSeconds;
    private double _secondsSinceSave;
    private DateTimeOffset? _pauseStartedAt;
    private DateTimeOffset? _pendingApplyAfterUtc;
    private DateTime _settingsLastWriteUtc;
    private string? _persistenceWarning;
    private bool _clockAnomalyAudited;
    private long _flexibleSessionBaselineSeconds;

    public CafeViewModel(JsonSettingsStore? settingsStore = null, JsonUsageStore? usageStore = null)
    {
        _settingsStore = settingsStore ?? new JsonSettingsStore();
        _usageStore = usageStore ?? new JsonUsageStore();
    }

    public event EventHandler? SessionStateChanged;

    public SessionState State => _snapshot?.State ?? SessionState.Ready;
    public bool IsActive => State == SessionState.Active;
    public bool CanStartOrResume => State is SessionState.Ready or SessionState.Paused;
    public bool CanEndSession => State == SessionState.Paused;
    public bool IsBlocked => State is SessionState.TimeExpired or SessionState.OutsideSchedule;
    public bool ShouldShowSessionSurfaces => _settings?.Mode != ControlMode.Awareness;
    public bool IsGuardedPersonalMode =>
        _settings?.Mode == ControlMode.Personal &&
        _settings.PersonalProtectionLevel == PersonalProtectionLevel.Guarded;
    public bool IsFlexiblePersonalMode =>
        _settings?.Mode == ControlMode.Personal &&
        _settings.PersonalProtectionLevel == PersonalProtectionLevel.Flexible;
    public bool CanRequestExtraTime => State == SessionState.TimeExpired &&
        (_settings?.Mode != ControlMode.Personal || _settings.StrictPersonalMode == false);
    public bool IsOutsideSchedule => State == SessionState.OutsideSchedule;
    public bool IsClockRollbackDetected => _engine?.Ledger.ClockRollbackUntilUtc is { } until && until > DateTimeOffset.UtcNow;
    public LimitReachedAction LimitAction => _settings?.LimitAction ?? LimitReachedAction.ShowBlockScreen;
    public string BlockedReasonText => State switch
    {
        SessionState.OutsideSchedule when IsClockRollbackDetected => LocalizationService.Get("ClockRollbackBlocked"),
        SessionState.OutsideSchedule => LocalizationService.Get("CannotStartOutsideSchedule"),
        SessionState.TimeExpired => LocalizationService.Get("CannotStartTimeExpired"),
        _ => string.Empty
    };

    public string StateLabel => State switch
    {
        SessionState.Active => LocalizationService.Get("StateActive"),
        SessionState.Paused => LocalizationService.Get("StatePaused"),
        SessionState.TimeExpired => LocalizationService.Get("StateExpired"),
        SessionState.OutsideSchedule => LocalizationService.Get("StateOutside"),
        _ => LocalizationService.Get("StateReady")
    };

    public string Headline => State switch
    {
        SessionState.Paused => LocalizationService.Get("HeadlinePaused"),
        SessionState.TimeExpired => LocalizationService.Get("HeadlineExpired"),
        SessionState.OutsideSchedule when IsClockRollbackDetected => LocalizationService.Get("HeadlineClockRollback"),
        SessionState.OutsideSchedule => LocalizationService.Get("HeadlineOutside"),
        SessionState.Active => LocalizationService.Get("HeadlineActive"),
        _ => LocalizationService.Get("HeadlineReady")
    };

    public string Description => _persistenceWarning ?? _snapshot?.Reason ?? "Kullanım bilgileri yükleniyor…";
    public bool HasCountdown => !IsFlexiblePersonalMode;
    public string TimeMetricLabel => LocalizationService.Get(
        IsFlexiblePersonalMode ? "ElapsedTimeLong" : "RemainingTimeLong");
    public string TimeMetricLabelShort => LocalizationService.Get(
        IsFlexiblePersonalMode ? "OtiumElapsed" : "OtiumRemaining");
    public string RemainingText => IsFlexiblePersonalMode
        ? FormatClock(Math.Max(0, (_snapshot?.UsedSeconds ?? 0) - _flexibleSessionBaselineSeconds))
        : FormatClock(_snapshot?.RemainingSeconds ?? 0);
    public string UsedText => FormatDuration(_snapshot?.UsedSeconds ?? 0);
    public string LimitText => FormatDuration(_snapshot?.LimitSeconds ?? 0);
    public string UsedDisplayText => $"{LocalizationService.Get("Used")}  {UsedText}";
    public string LimitDisplayText => $"{LocalizationService.Get("Total")}  {LimitText}";
    public string PrimaryActionText => State == SessionState.Paused ? LocalizationService.Get("Resume") : LocalizationService.Get("Start");
    public string PauseDurationText => _pauseStartedAt is null
        ? ""
        : $"{LocalizationService.Get("BreakDuration")} · {FormatClock((long)Math.Max(0, (DateTimeOffset.Now - _pauseStartedAt.Value).TotalSeconds))}";

    public double UsagePercent
    {
        get
        {
            long limit = _snapshot?.LimitSeconds ?? 0;
            return limit == 0 ? 0 : Math.Clamp((double)(_snapshot?.UsedSeconds ?? 0) / limit * 100, 0, 100);
        }
    }

    public async Task InitializeAsync()
    {
        ControlSettings settings;
        try
        {
            settings = await _settingsStore.LoadAsync();
        }
        catch
        {
            settings = CreateFailClosedSettings();
            _persistenceWarning = LocalizationService.Get("SettingsRecoveryRequired");
        }
        _settings = settings;
        OnPropertyChanged(nameof(ShouldShowSessionSurfaces));
        OnPropertyChanged(nameof(IsGuardedPersonalMode));
        _settingsLastWriteUtc = GetSettingsLastWriteUtc();
        _pendingApplyAfterUtc = settings.PendingChange?.ApplyAfterUtc;
        UsageLedger ledger = await _usageStore.LoadAsync();
        _engine = new SessionEngine(settings, ledger, DateTimeOffset.Now);
        _flexibleSessionBaselineSeconds = ledger.UsedSeconds;
        _engine.ObserveClock(DateTimeOffset.Now, WindowsMonotonicClock.Uptime, WindowsMonotonicClock.GetBootId());
        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: false);
        await SaveAsync();
    }

    public async Task TickAsync()
    {
        if (_engine is null)
        {
            return;
        }

        if (_pendingApplyAfterUtc is not null && _pendingApplyAfterUtc <= DateTimeOffset.UtcNow)
        {
            await ReloadSettingsAsync();
        }
        else if (GetSettingsLastWriteUtc() > _settingsLastWriteUtc)
        {
            await ReloadSettingsAsync();
        }

        TimeSpan elapsed = _tickWatch.Elapsed;
        _tickWatch.Restart();
        ClockChangeKind clockChange = _engine.ObserveClock(
            DateTimeOffset.Now,
            WindowsMonotonicClock.Uptime,
            WindowsMonotonicClock.GetBootId());
        if (clockChange is ClockChangeKind.Rollback or ClockChangeKind.ForwardJump)
        {
            _secondsSinceSave = Math.Max(_secondsSinceSave, 5);
            if (!_clockAnomalyAudited)
            {
                _clockAnomalyAudited = true;
                string auditPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Otium",
                    "security-audit.jsonl");
                await new SecurityAuditLog(auditPath).AppendAsync(
                    "clock.anomaly",
                    clockChange == ClockChangeKind.Rollback ? "rollback" : "forward-jump");
            }
        }
        ControlSettings? activeSettings = _settings;
        if (activeSettings is not null &&
            ShouldEnforceApplicationRules(activeSettings, _engine.Ledger.State) &&
            _applicationRuleEnforcer.Enforce(activeSettings, _engine.Ledger, elapsed))
        {
            _secondsSinceSave = Math.Max(_secondsSinceSave, 5);
        }
        if ((_settings?.AwarenessTrackingEnabled == true || _settings?.Mode == ControlMode.Awareness) &&
            _foregroundApplicationTracker.Sample(_engine.Ledger, elapsed))
        {
            _secondsSinceSave = Math.Max(_secondsSinceSave, 5);
        }
        _uncommittedSeconds += elapsed.TotalSeconds;
        _secondsSinceSave += elapsed.TotalSeconds;

        if (_uncommittedSeconds >= 1)
        {
            long wholeSeconds = (long)Math.Floor(_uncommittedSeconds);
            _uncommittedSeconds -= wholeSeconds;
            _engine.Accrue(TimeSpan.FromSeconds(wholeSeconds), DateTimeOffset.Now);
        }

        RefreshSnapshot(notifyStateChange: true);

        if (_secondsSinceSave >= 5)
        {
            _secondsSinceSave = 0;
            await SaveAsync();
        }
    }

    public async Task<bool> StartOrResumeAsync()
    {
        if (_engine is null)
        {
            return false;
        }

        bool startingNewFlexibleSession = IsFlexiblePersonalMode && State == SessionState.Ready;
        bool started = _engine.StartOrResume(DateTimeOffset.Now);
        if (started)
        {
            if (startingNewFlexibleSession)
            {
                _flexibleSessionBaselineSeconds = _engine.Ledger.UsedSeconds;
            }
            _pauseStartedAt = null;
            _uncommittedSeconds = 0;
            _tickWatch.Restart();
            RefreshSnapshot(notifyStateChange: true);
            await SaveAsync();
        }

        return started;
    }

    public static bool ShouldEnforceApplicationRules(ControlSettings settings, SessionState state) =>
        settings.Mode != ControlMode.Awareness &&
        (settings.Mode != ControlMode.Personal ||
         settings.PersonalProtectionLevel != PersonalProtectionLevel.Flexible ||
         state == SessionState.Active);

    public async Task<bool> PauseAsync()
    {
        bool paused = PauseForSystemInterruption();
        if (paused)
        {
            SystemMediaController.StopPlayback();
            await SaveAsync();
        }

        return paused;
    }

    public bool PauseForSystemInterruption()
    {
        if (_engine is null)
        {
            return false;
        }

        CommitPendingActiveTime();
        bool paused = _engine.Pause(DateTimeOffset.Now);
        if (paused)
        {
            _pauseStartedAt = DateTimeOffset.Now;
            RefreshSnapshot(notifyStateChange: true);
        }

        return paused;
    }

    public async Task EndSessionAsync()
    {
        if (_engine is null)
        {
            return;
        }

        CommitPendingActiveTime();
        _engine.EndSession(DateTimeOffset.Now);
        if (IsFlexiblePersonalMode)
        {
            _flexibleSessionBaselineSeconds = _engine.Ledger.UsedSeconds;
        }
        _pauseStartedAt = null;
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }

    public async Task AddBonusMinutesAsync(int minutes)
    {
        if (_engine is null || (_settings?.Mode == ControlMode.Personal && _settings.StrictPersonalMode))
        {
            return;
        }

        _engine.AddBonusMinutes(minutes, DateTimeOffset.Now);
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }

#if OTIUM_DEVELOPMENT_BUILD
    public async Task ForceUnlockForTestingAsync()
    {
        if (_engine is null)
        {
            return;
        }

        CommitPendingActiveTime();
        if (_settings?.PendingChange is { } pending)
        {
            ControlSettings target = pending.TargetSettings;
            target.PendingChange = null;
            target.SchemaVersion = 9;
            target.SetupCompleted = true;
            await _settingsStore.SaveAsync(target);
            _settings = target;
            _pendingApplyAfterUtc = null;
            _settingsLastWriteUtc = GetSettingsLastWriteUtc();
            _engine = new SessionEngine(target, _engine.Ledger, DateTimeOffset.Now);
        }

        _engine.ForceStartForTesting(DateTimeOffset.Now);
        _pauseStartedAt = null;
        _uncommittedSeconds = 0;
        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }
#endif

    public async Task SaveAsync()
    {
        if (_engine is null)
        {
            return;
        }

        try
        {
            await _usageStore.SaveAsync(_engine.Ledger);
            if (_persistenceWarning is not null)
            {
                _persistenceWarning = null;
                OnPropertyChanged(nameof(Description));
            }
        }
        catch (Exception exception)
        {
            ReportRuntimeError(exception);
        }
    }

    public void ReportRuntimeError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _persistenceWarning = LocalizationService.CurrentLanguage == LanguagePreference.English
            ? "Usage data could not be saved. Otium will retry automatically."
            : "Kullanım verisi kaydedilemedi. Otium otomatik olarak yeniden deneyecek.";
        OnPropertyChanged(nameof(Description));
    }

    public async Task ReloadSettingsAsync()
    {
        if (_engine is null)
        {
            return;
        }

        bool wasActive = _engine.Ledger.State == SessionState.Active;
        bool wasFlexiblePersonal = IsFlexiblePersonalMode;
        CommitPendingActiveTime();
        await SaveAsync();
        ControlSettings settings;
        try
        {
            settings = await _settingsStore.LoadAsync();
        }
        catch
        {
            settings = _settings ?? CreateFailClosedSettings();
            _persistenceWarning = LocalizationService.Get("SettingsRecoveryRequired");
        }
        _settings = settings;
        OnPropertyChanged(nameof(ShouldShowSessionSurfaces));
        OnPropertyChanged(nameof(IsGuardedPersonalMode));
        OnPropertyChanged(nameof(IsFlexiblePersonalMode));
        _settingsLastWriteUtc = GetSettingsLastWriteUtc();
        _pendingApplyAfterUtc = settings.PendingChange?.ApplyAfterUtc;
        UsageLedger latestLedger = await _usageStore.LoadAsync();
        _engine = new SessionEngine(settings, latestLedger, DateTimeOffset.Now);
        bool enteredFlexiblePersonal = IsFlexiblePersonalMode && !wasFlexiblePersonal;
        if (enteredFlexiblePersonal)
        {
            _flexibleSessionBaselineSeconds = latestLedger.UsedSeconds;
        }
        if (wasActive && !enteredFlexiblePersonal)
        {
            _engine.StartOrResume(DateTimeOffset.Now);
        }

        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }

    public async Task SwitchToUserSettingsStoreAsync()
    {
        _settingsStore = new JsonSettingsStore();
        await ReloadSettingsAsync();
    }

    public async Task ReloadUsageAfterClearAsync()
    {
        if (_settings is null)
        {
            return;
        }

        bool wasActive = IsActive;
        UsageLedger ledger = await _usageStore.LoadAsync();
        _engine = new SessionEngine(_settings, ledger, DateTimeOffset.Now);
        if (wasActive || _settings.Mode == ControlMode.Awareness)
        {
            _engine.StartOrResume(DateTimeOffset.Now);
        }

        _uncommittedSeconds = 0;
        _secondsSinceSave = 0;
        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: true);
    }

    private void CommitPendingActiveTime()
    {
        if (_engine is null)
        {
            return;
        }

        TimeSpan elapsed = _tickWatch.Elapsed;
        _tickWatch.Restart();
        _uncommittedSeconds += elapsed.TotalSeconds;
        long wholeSeconds = (long)Math.Floor(_uncommittedSeconds);
        _uncommittedSeconds -= wholeSeconds;
        if (wholeSeconds > 0)
        {
            _engine.Accrue(TimeSpan.FromSeconds(wholeSeconds), DateTimeOffset.Now);
        }
    }

    private void RefreshSnapshot(bool notifyStateChange)
    {
        if (_engine is null)
        {
            return;
        }

        SessionState previousState = _snapshot?.State ?? _engine.Ledger.State;
        _snapshot = _engine.GetSnapshot(DateTimeOffset.Now);

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanStartOrResume));
        OnPropertyChanged(nameof(CanEndSession));
        OnPropertyChanged(nameof(IsBlocked));
        OnPropertyChanged(nameof(CanRequestExtraTime));
        OnPropertyChanged(nameof(IsOutsideSchedule));
        OnPropertyChanged(nameof(IsClockRollbackDetected));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(TimeMetricLabel));
        OnPropertyChanged(nameof(TimeMetricLabelShort));
        OnPropertyChanged(nameof(BlockedReasonText));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(UsedText));
        OnPropertyChanged(nameof(LimitText));
        OnPropertyChanged(nameof(UsedDisplayText));
        OnPropertyChanged(nameof(LimitDisplayText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(PauseDurationText));
        OnPropertyChanged(nameof(UsagePercent));

        if (notifyStateChange && previousState != _snapshot.State)
        {
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ControlSettings CreateFailClosedSettings()
    {
        ControlSettings settings = new()
        {
            SetupCompleted = true,
            Mode = ControlMode.Protected,
            DefaultDailyLimitMinutes = 0
        };
        foreach (DaySchedule day in settings.Schedule)
        {
            day.IsEnabled = false;
            day.DailyLimitMinutes = 0;
        }
        return settings;
    }

    private static string FormatClock(long seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string FormatDuration(long seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        int hours = (int)time.TotalHours;
        string hour = LocalizationService.CurrentLanguage == LanguagePreference.English ? "hr" : "sa";
        string minute = LocalizationService.Get("MinuteShort");
        return hours > 0 ? $"{hours} {hour} {time.Minutes} {minute}" : $"{time.Minutes} {minute}";
    }

    private DateTime GetSettingsLastWriteUtc()
    {
        try
        {
            return File.GetLastWriteTimeUtc(_settingsStore.FilePath);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}

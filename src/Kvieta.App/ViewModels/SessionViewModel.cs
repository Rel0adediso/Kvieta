using System.Diagnostics;
using System.IO;
using Kvieta.Core.Models;
using Kvieta.Core.Services;
using Kvieta.App.Services;

namespace Kvieta.App.ViewModels;

public sealed record FocusSessionClosure(bool Completed, long ActiveSeconds, long TargetSeconds, string Intention);

public sealed class SessionViewModel : ObservableObject, IDisposable
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
    private bool _usageSuspendedForAdministration;
    private long _flexibleSessionBaselineSeconds;
    private FocusSessionGoal? _focusGoal;
    private readonly int? _requestedFocusDurationMinutes;
    private string _focusIntention = string.Empty;
    private FocusSessionClosure? _focusClosure;

    public SessionViewModel(
        JsonSettingsStore? settingsStore = null,
        JsonUsageStore? usageStore = null,
        int? focusDurationMinutes = null)
    {
        _settingsStore = settingsStore ?? new JsonSettingsStore();
        _usageStore = usageStore ?? new JsonUsageStore();
        _requestedFocusDurationMinutes = focusDurationMinutes is > 0 ? focusDurationMinutes : null;
    }

    public void Dispose() => _applicationRuleEnforcer.Dispose();

    public event EventHandler? SessionStateChanged;

    public SessionState State => _snapshot?.State ?? SessionState.Ready;
    public bool IsActive => State == SessionState.Active;
    public bool CanStartOrResume =>
        _focusGoal?.IsCompleted != true && State is (SessionState.Ready or SessionState.Paused);
    public bool CanEndSession => State == SessionState.Paused;
    public bool HasFocusSession => _focusGoal is not null && _focusClosure is null;
    public bool HasFocusClosure => _focusClosure is not null;
    public bool CanContinueAfterFocus => HasFocusClosure && IsFlexiblePersonalMode &&
        State is SessionState.Ready or SessionState.Paused;
    public string FocusIntention
    {
        get => _focusIntention;
        set
        {
            string clean = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            SetProperty(ref _focusIntention, clean[..Math.Min(80, clean.Length)]);
        }
    }
    public string FocusClosureTitle => _focusClosure?.Completed == true
        ? LocalizationService.Get("FocusCompletedHeadline")
        : LocalizationService.Get("FocusEndedEarlyHeadline");
    public string FocusClosureSummary => _focusClosure is null ? string.Empty :
        string.Format(LocalizationService.Get(_focusClosure.Completed ? "FocusCompletedSummary" : "FocusEndedEarlySummary"),
            FormatDuration(_focusClosure.ActiveSeconds));
    public string FocusClosureGoalProgress
    {
        get
        {
            if (_focusClosure is null || _engine is null) return string.Empty;
            long progress = _engine.Ledger.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes
                ? _engine.Ledger.FocusCompletedSeconds / 60
                : _engine.Ledger.FocusSessionCount;
            string unit = _engine.Ledger.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes
                ? LocalizationService.Get("MinuteShort")
                : LocalizationService.Get("FocusSessionsShort");
            return string.Format(LocalizationService.Get("DailyGoalProgressFormat"), progress, _engine.Ledger.RhythmGoalTarget, unit);
        }
    }
    public bool IsBlocked => State is SessionState.TimeExpired or SessionState.OutsideSchedule;
    public bool ShouldShowSessionSurfaces => _settings?.Mode != UsageMode.Insights;
    public bool IsProtectedPersonalMode =>
        _settings?.Mode == UsageMode.Personal &&
        _settings.PersonalProtectionLevel == PersonalProtectionLevel.Protected;
    public bool IsFlexiblePersonalMode =>
        _settings?.Mode == UsageMode.Personal &&
        _settings.PersonalProtectionLevel == PersonalProtectionLevel.Flexible;
    public bool CanRequestExtraTime => _settings is not null &&
        ShouldAllowExtraTimeRequest(_settings, State);
    public bool CanPlanTomorrow => _settings is not null &&
        (_settings.Mode == UsageMode.Family ||
         _settings.Mode == UsageMode.Personal &&
         _settings.PersonalProtectionLevel != PersonalProtectionLevel.Flexible);
    public bool IsOutsideSchedule => State == SessionState.OutsideSchedule;
    public bool IsClockRollbackDetected => _engine?.Ledger.ClockRollbackUntilUtc is { } until && until > DateTimeOffset.UtcNow;
    public bool IsRegularOutsideSchedule => IsOutsideSchedule && !IsClockRollbackDetected;
    public LimitReachedAction LimitAction => _settings?.LimitAction ?? LimitReachedAction.ShowBlockScreen;
    public long RemainingSeconds => _focusGoal is not null
        ? FocusRemainingSeconds()
        : Math.Max(0, _snapshot?.RemainingSeconds ?? 0);
    public IReadOnlyList<int> WarningMinutes => _settings?.WarningMinutes ?? [15, 5, 1];
    public string BlockedReasonText => IsBlocked
        ? CurrentStatusExplanation?.WhatHappened ?? string.Empty
        : string.Empty;

    public string StateLabel => _focusGoal?.IsCompleted == true
        ? LocalizationService.Get("FocusCompletedState")
        : State switch
        {
            SessionState.Active => LocalizationService.Get("StateActive"),
            SessionState.Paused => LocalizationService.Get("StatePaused"),
            SessionState.TimeExpired => LocalizationService.Get("StateExpired"),
            SessionState.OutsideSchedule when IsClockRollbackDetected => LocalizationService.Get("StateClockProtection"),
            SessionState.OutsideSchedule => LocalizationService.Get("StateOutside"),
            _ => LocalizationService.Get("StateReady")
        };

    public string Headline => _focusGoal?.IsCompleted == true
        ? LocalizationService.Get("FocusCompletedHeadline")
        : State switch
        {
            SessionState.Paused => LocalizationService.Get("HeadlinePaused"),
            SessionState.TimeExpired => LocalizationService.Get("HeadlineExpired"),
            SessionState.OutsideSchedule when IsClockRollbackDetected => LocalizationService.Get("HeadlineClockRollback"),
            SessionState.OutsideSchedule => LocalizationService.Get("HeadlineOutside"),
            SessionState.Active => LocalizationService.Get("HeadlineActive"),
            _ => LocalizationService.Get("HeadlineReady")
        };

    public string Description => _focusGoal?.IsCompleted == true
        ? LocalizationService.Get("FocusCompletedDescription")
        : _persistenceWarning ?? _snapshot?.Reason ?? "Kullanım bilgileri yükleniyor…";
    public string StatusExplanationText => CurrentStatusExplanation?.AccessibleText ?? string.Empty;
    private SessionStatusExplanation? CurrentStatusExplanation => _settings is null || _engine is null
        ? null
        : SessionStatusExplainer.Explain(
            _settings, _engine.Ledger, State, DateTimeOffset.Now,
            _settings.RequiresGuardian && ProtectionServiceManager.GetState() != ProtectionServiceState.Running);
    public bool HasCountdown => _focusGoal is not null || !IsFlexiblePersonalMode;
    public string TimeMetricLabel => LocalizationService.Get(
        _focusGoal is null && IsFlexiblePersonalMode ? "ElapsedTimeLong" : "RemainingTimeLong");
    public string TimeMetricLabelShort => LocalizationService.Get(
        _focusGoal is null && IsFlexiblePersonalMode ? "KvietaElapsed" : "KvietaRemaining");
    public string RemainingText => _focusGoal is not null
        ? FormatClock(FocusRemainingSeconds())
        : IsFlexiblePersonalMode
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
            if (_focusGoal is not null)
            {
                return _focusGoal.ProgressPercent();
            }

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
        OnPropertyChanged(nameof(IsProtectedPersonalMode));
        _settingsLastWriteUtc = GetSettingsLastWriteUtc();
        _pendingApplyAfterUtc = settings.PendingChange?.ApplyAfterUtc;
        UsageLedger ledger = await _usageStore.LoadAsync();
        if (_requestedFocusDurationMinutes is { } requestedMinutes)
        {
            _focusGoal = new FocusSessionGoal(requestedMinutes);
            _focusGoal.Start();
            ledger.ActiveFocusSessionId = Guid.NewGuid();
            ledger.ActiveFocusTargetSeconds = _focusGoal.DurationSeconds;
            ledger.ActiveFocusElapsedSeconds = 0;
        }
        else if (ledger.ActiveFocusSessionId is not null && ledger.ActiveFocusTargetSeconds > 0)
        {
            _focusGoal = FocusSessionGoal.Restore(
                ledger.ActiveFocusTargetSeconds,
                ledger.ActiveFocusElapsedSeconds);
        }
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
                    "Kvieta",
                    "security-audit.jsonl");
                await new SecurityAuditLog(auditPath).AppendAsync(
                    "clock.anomaly",
                    clockChange == ClockChangeKind.Rollback ? "rollback" : "forward-jump");
            }
        }

        if (_usageSuspendedForAdministration)
        {
            _uncommittedSeconds = 0;
            RefreshSnapshot(notifyStateChange: true);
            if (_secondsSinceSave >= 5)
            {
                _secondsSinceSave = 0;
                await SaveAsync();
            }

            return;
        }

        ControlSettings? activeSettings = _settings;
        if (activeSettings is not null &&
            ShouldEnforceApplicationRules(activeSettings, _engine.Ledger.State) &&
            _applicationRuleEnforcer.Enforce(activeSettings, _engine.Ledger, elapsed, _engine.Ledger.State))
        {
            _secondsSinceSave = Math.Max(_secondsSinceSave, 5);
        }
        if ((_settings?.AwarenessTrackingEnabled == true || _settings?.Mode == UsageMode.Insights) &&
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
            long activeSeconds = _engine.Accrue(TimeSpan.FromSeconds(wholeSeconds), DateTimeOffset.Now);
            AccrueFocus(activeSeconds);
        }

        RefreshSnapshot(notifyStateChange: true);

        if (TryCompleteFocus())
        {
            RefreshSnapshot(notifyStateChange: true);
            await SaveAsync();
            return;
        }

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
            settings.Mode != UsageMode.Insights &&
            (settings.Mode != UsageMode.Personal ||
         settings.PersonalProtectionLevel != PersonalProtectionLevel.Flexible ||
         state == SessionState.Active);

    public static bool ShouldAllowExtraTimeRequest(ControlSettings settings, SessionState state) =>
        settings.Mode == UsageMode.Family && state is SessionState.Active or SessionState.Paused or SessionState.TimeExpired ||
        state == SessionState.TimeExpired &&
        (settings.Mode != UsageMode.Personal || settings.StrictPersonalMode == false);

    public void SuspendUsageForAdministration()
    {
        if (_engine is null || _usageSuspendedForAdministration)
        {
            return;
        }

        CommitPendingActiveTime();
        _usageSuspendedForAdministration = true;
        _secondsSinceSave = Math.Max(_secondsSinceSave, 5);
    }

    public void ResumeUsageAfterAdministration()
    {
        if (!_usageSuspendedForAdministration)
        {
            return;
        }

        _usageSuspendedForAdministration = false;
        _uncommittedSeconds = 0;
        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: true);
    }

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
        if (TryCompleteFocus())
        {
            return true;
        }
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
        if (!TryCompleteFocus() && _focusGoal is not null)
        {
            _focusClosure = new FocusSessionClosure(false, _focusGoal.ElapsedSeconds, _focusGoal.DurationSeconds, FocusIntention);
        }
        _engine.EndSession(DateTimeOffset.Now);
        ClearPersistedFocus();
        _focusGoal = null;
        if (IsFlexiblePersonalMode)
        {
            _flexibleSessionBaselineSeconds = _engine.Ledger.UsedSeconds;
        }
        _pauseStartedAt = null;
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }

    public async Task<bool> ContinueAfterFocusAsync()
    {
        if (_engine is null || _focusClosure is null || !CanContinueAfterFocus) return false;
        long targetSeconds = _focusClosure.TargetSeconds;
        _focusClosure = null;
        FocusIntention = string.Empty;
        _focusGoal = FocusSessionGoal.Restore(targetSeconds, 0);
        _focusGoal.Start();
        _engine.Ledger.ActiveFocusSessionId = Guid.NewGuid();
        _engine.Ledger.ActiveFocusTargetSeconds = targetSeconds;
        _engine.Ledger.ActiveFocusElapsedSeconds = 0;
        bool started = _engine.StartOrResume(DateTimeOffset.Now);
        if (!started)
        {
            ClearPersistedFocus();
            _focusGoal = null;
        }
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
        return started;
    }

    public void DismissFocusClosure()
    {
        _focusClosure = null;
        FocusIntention = string.Empty;
        RefreshSnapshot(notifyStateChange: true);
    }

    public async Task AddBonusMinutesAsync(int minutes)
    {
        if (_engine is null || (_settings?.Mode == UsageMode.Personal && _settings.StrictPersonalMode))
        {
            return;
        }

        _engine.AddBonusMinutes(minutes, DateTimeOffset.Now);
        RefreshSnapshot(notifyStateChange: true);
        await SaveAsync();
    }

#if KVIETA_DEVELOPMENT_BUILD
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
            target.SchemaVersion = 10;
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
            ? "Usage data could not be saved. Kvieta will retry automatically."
            : "Kullanım verisi kaydedilemedi. Kvieta otomatik olarak yeniden deneyecek.";
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
        OnPropertyChanged(nameof(IsProtectedPersonalMode));
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
        if (ledger.ActiveFocusSessionId is null)
        {
            _focusGoal = null;
        }
        _engine = new SessionEngine(_settings, ledger, DateTimeOffset.Now);
        if (wasActive || _settings.Mode == UsageMode.Insights)
        {
            _engine.StartOrResume(DateTimeOffset.Now);
        }

        _uncommittedSeconds = 0;
        _secondsSinceSave = 0;
        _tickWatch.Restart();
        RefreshSnapshot(notifyStateChange: true);
    }

    public async Task ClearClockAnomalyAsync()
    {
        await _usageStore.ClearClockAnomalyAsync(
            DateTimeOffset.Now,
            WindowsMonotonicClock.Uptime,
            WindowsMonotonicClock.GetBootId());
        await ReloadUsageAfterClearAsync();
    }

    private void CommitPendingActiveTime()
    {
        if (_engine is null)
        {
            return;
        }

        TimeSpan elapsed = _tickWatch.Elapsed;
        _tickWatch.Restart();
        if (_usageSuspendedForAdministration)
        {
            _uncommittedSeconds = 0;
            return;
        }

        _uncommittedSeconds += elapsed.TotalSeconds;
        long wholeSeconds = (long)Math.Floor(_uncommittedSeconds);
        _uncommittedSeconds -= wholeSeconds;
        if (wholeSeconds > 0)
        {
            long activeSeconds = _engine.Accrue(TimeSpan.FromSeconds(wholeSeconds), DateTimeOffset.Now);
            AccrueFocus(activeSeconds);
        }
    }

    private void AccrueFocus(long activeSeconds)
    {
        if (_engine is null || _focusGoal is null || activeSeconds <= 0) return;
        _focusGoal.Accrue(activeSeconds);
        if (_engine.Ledger.ActiveFocusSessionId is not null)
        {
            _engine.Ledger.ActiveFocusElapsedSeconds = _focusGoal.ElapsedSeconds;
        }
    }

    private void ClearPersistedFocus()
    {
        if (_engine is null) return;
        _engine.Ledger.ActiveFocusSessionId = null;
        _engine.Ledger.ActiveFocusTargetSeconds = 0;
        _engine.Ledger.ActiveFocusElapsedSeconds = 0;
    }

    private bool TryCompleteFocus()
    {
        if (_engine is null || _focusGoal?.CompleteIfReached() != true) return false;
        _engine.Ledger.FocusSessionCount++;
        _engine.Ledger.FocusCompletedSeconds += _focusGoal.DurationSeconds;
        _focusClosure = new FocusSessionClosure(true, _focusGoal.ElapsedSeconds, _focusGoal.DurationSeconds, FocusIntention);
        ClearPersistedFocus();
        _engine.EndSession(DateTimeOffset.Now);
        return true;
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
        OnPropertyChanged(nameof(HasFocusSession));
        OnPropertyChanged(nameof(HasFocusClosure));
        OnPropertyChanged(nameof(CanContinueAfterFocus));
        OnPropertyChanged(nameof(IsBlocked));
        OnPropertyChanged(nameof(CanRequestExtraTime));
        OnPropertyChanged(nameof(CanPlanTomorrow));
        OnPropertyChanged(nameof(IsOutsideSchedule));
        OnPropertyChanged(nameof(IsClockRollbackDetected));
        OnPropertyChanged(nameof(IsRegularOutsideSchedule));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(TimeMetricLabel));
        OnPropertyChanged(nameof(TimeMetricLabelShort));
        OnPropertyChanged(nameof(BlockedReasonText));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(StatusExplanationText));
        OnPropertyChanged(nameof(FocusClosureTitle));
        OnPropertyChanged(nameof(FocusClosureSummary));
        OnPropertyChanged(nameof(FocusClosureGoalProgress));
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
            Mode = UsageMode.Family,
            DefaultDailyLimitMinutes = 0
        };
        foreach (DaySchedule day in settings.Schedule)
        {
            day.IsEnabled = false;
            day.DailyLimitMinutes = 0;
        }
        return settings;
    }

    private long FocusRemainingSeconds()
    {
        if (_focusGoal is null)
        {
            return 0;
        }

        long focusRemaining = _focusGoal.RemainingSeconds();
        if (IsFlexiblePersonalMode || _snapshot is null)
        {
            return focusRemaining;
        }

        return Math.Min(focusRemaining, Math.Max(0, _snapshot.RemainingSeconds));
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

using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App.Services;

public enum SessionStatusReason
{
    Ready,
    Active,
    Paused,
    OutsideSchedule,
    DailyLimit,
    ApplicationLimit,
    FocusBlock,
    PermanentBlock,
    PendingPolicy,
    ClockUntrusted,
    GuardianUnavailable
}

public sealed record SessionStatusExplanation(
    SessionStatusReason Reason,
    string WhatHappened,
    string RuleSource,
    DateTimeOffset? ChangesAt,
    string AvailableAction,
    bool ActionAvailable)
{
    public string AccessibleText =>
        $"{WhatHappened} · {RuleSource} · " +
        (ChangesAt is { } at
            ? $"{L("Değişim", "Changes")}: {at.ToLocalTime():dd.MM HH:mm}"
            : L("Bitiş zamanı bilinmiyor", "End time is unknown")) +
        $" · {AvailableAction}";

    private static string L(string tr, string en) =>
        LocalizationService.CurrentLanguage == LanguagePreference.English ? en : tr;
}

public static class SessionStatusExplainer
{
    public static SessionStatusExplanation Explain(
        ControlSettings settings,
        UsageLedger ledger,
        SessionState state,
        DateTimeOffset now,
        bool guardianUnavailable = false)
    {
        bool english = settings.Language == LanguagePreference.English;
        string L(string tr, string en) => english ? en : tr;
        ScheduleStatus schedule = ScheduleEvaluator.Evaluate(settings, now);

        if (ledger.ClockAnomalyRequiresRecovery || ledger.ClockRollbackUntilUtc is { } until && until > now)
        {
            return new(SessionStatusReason.ClockUntrusted,
                L("Saat değişikliği nedeniyle kullanım güvenli biçimde durduruldu.", "Usage was safely paused because the clock changed."),
                L("Güvenilir saat koruması", "Trusted-clock protection"),
                ledger.ClockRollbackUntilUtc,
                L("Yönetici mevcut saati doğrulayabilir.", "An administrator can verify the current clock."), false);
        }
        if (guardianUnavailable && settings.RequiresGuardian)
        {
            return new(SessionStatusReason.GuardianUnavailable,
                L("Koruma hizmetine ulaşılamadığı için gevşetici işlem yapılamıyor.", "Relaxing actions are unavailable because the protection service cannot be reached."),
                "Guardian",
                null,
                L("Bu cihazdaki Kontrol Merkezi'nden Guardian durumunu düzelt.", "Repair Guardian from Control Center on this device."), false);
        }
        if (state == SessionState.OutsideSchedule)
        {
            return new(SessionStatusReason.OutsideSchedule,
                L("Bugünkü izin verilen zaman aralığının dışındasın.", "You are outside today's allowed time window."),
                schedule.Reason,
                schedule.AllowedUntil,
                L("Planı görüntüleyebilirsin; değişiklik mevcut yetkilendirmeyi kullanır.", "You can view the plan; changes use the existing authorization flow."), false);
        }
        if (state == SessionState.TimeExpired)
        {
            return new(SessionStatusReason.DailyLimit,
                L("Bugünkü onaylı süre tamamlandı.", "Today's approved time is complete."),
                L($"Günlük limit: {schedule.DailyLimitMinutes + ledger.BonusMinutes} dk", $"Daily limit: {schedule.DailyLimitMinutes + ledger.BonusMinutes} min"),
                schedule.AllowedUntil,
                settings.Mode == UsageMode.Family
                    ? L("Ek süre yalnız bu cihazda PIN ile onaylanabilir.", "Extra time can only be approved with the PIN on this device.")
                    : L("Kullanılabilir bir ek süre eylemi yok.", "No extra-time action is available."),
                settings.Mode == UsageMode.Family);
        }
        if (settings.PendingChange is { } pending)
        {
            return new(SessionStatusReason.PendingPolicy,
                L("Bir ayar değişikliği bekliyor; mevcut kural uygulanmaya devam ediyor.", "A settings change is pending; the current rule remains active."),
                L("Bekleyen ayar", "Pending setting"), pending.ApplyAfterUtc,
                L("Ayrıntıları Kontrol Merkezi'nde inceleyebilirsin.", "You can review the details in Control Center."), true);
        }
        if (schedule.IsTemporaryAllowanceActive)
        {
            return new(state == SessionState.Paused ? SessionStatusReason.Paused : SessionStatusReason.Active,
                L("Geçici izin kapsamında kullanım açık.", "Usage is open under a temporary allowance."),
                L($"Onaylı ek süre: {ledger.RhythmApprovedMinutes + ledger.BonusMinutes} dk", $"Approved extra time: {ledger.RhythmApprovedMinutes + ledger.BonusMinutes} min"),
                schedule.AllowedUntil,
                L("İzin yalnız gösterilen zaman ve bu cihaz için geçerli.", "The allowance applies only to the shown time and this device."), true);
        }

        return new(state switch
        {
            SessionState.Active => SessionStatusReason.Active,
            SessionState.Paused => SessionStatusReason.Paused,
            _ => SessionStatusReason.Ready
        },
            state switch
            {
                SessionState.Active => L("Oturum etkin; yalnız aktif süre sayılıyor.", "The session is active; only active time is counted."),
                SessionState.Paused => L("Mola etkin; süre sayılmıyor.", "Break is active; time is not counted."),
                _ => L("Oturum başlatılmaya hazır.", "The session is ready to start.")
            },
            schedule.Reason,
            schedule.AllowedUntil,
            state == SessionState.Paused
                ? L("Devam edebilir veya oturumu bitirebilirsin.", "You can resume or end the session.")
                : L("Kullanılabilir eylemler mevcut policy ile sınırlıdır.", "Available actions are limited by the current policy."), true);
    }

    public static SessionStatusExplanation PreviewApplication(
        ControlSettings settings,
        UsageLedger ledger,
        AppRule rule,
        SessionState state,
        DateTimeOffset now)
    {
        bool english = settings.Language == LanguagePreference.English;
        string L(string tr, string en) => english ? en : tr;
        long used = ledger.AppUsedSeconds.GetValueOrDefault(rule.Id);
        bool blocked = ApplicationRuleEnforcer.ShouldBlock(rule, used, state);
        SessionStatusReason reason = rule.Mode switch
        {
            AppRuleMode.Blocked => SessionStatusReason.PermanentBlock,
            AppRuleMode.Limited => SessionStatusReason.ApplicationLimit,
            AppRuleMode.FocusBlocked => SessionStatusReason.FocusBlock,
            _ => SessionStatusReason.OutsideSchedule
        };
        string source = rule.Mode switch
        {
            AppRuleMode.Blocked => L("Kalıcı engel", "Permanent block"),
            AppRuleMode.Limited => L($"Uygulama limiti {rule.DailyLimitMinutes} dk; kullanılan {used / 60} dk", $"App limit {rule.DailyLimitMinutes} min; used {used / 60} min"),
            AppRuleMode.FocusBlocked => L("Odak sırasında engel", "Blocked during focus"),
            _ => L("Yalnız plan içinde", "Schedule only")
        };
        return new(reason,
            blocked ? L("Şu an engellenir.", "Would be blocked now.") : L("Şu an kullanılabilir.", "Would be available now."),
            source,
            ScheduleEvaluator.Evaluate(settings, now).AllowedUntil,
            L("Bu yalnız önizlemedir; Kaydetmeden koruma değişmez.", "This is only a preview; protection does not change until Save."), false);
    }
}

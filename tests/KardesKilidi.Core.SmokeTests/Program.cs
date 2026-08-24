using KardesKilidi.Core.Models;
using KardesKilidi.Core.Services;
using KardesKilidi.App.ViewModels;
using KardesKilidi.App.Services;
using System.Diagnostics;

ControlSettings settings = new();
Assert(settings.DeviceName == "Bu Bilgisayar", "Yeni kurulumun varsayılan cihaz adı yanlış.");
Assert(!settings.SetupCompleted, "Yeni kurulum, kullanım biçimi seçilmeden tamamlanmış görünmemeli.");
Assert(!AdminPinService.IsValidFormat("12ab"), "Harf içeren PIN kabul edilmemeliydi.");
AdminCredential credential = AdminPinService.Create("4826");
Assert(AdminPinService.Verify("4826", credential), "Doğru yönetici PIN'i doğrulanamadı.");
Assert(!AdminPinService.Verify("4827", credential), "Yanlış yönetici PIN'i kabul edildi.");
settings.AdminPin = credential;
DaySchedule monday = settings.Schedule.Single(item => item.Day == DayOfWeek.Monday);
monday.AllowedFrom = new TimeOnly(9, 0);
monday.AllowedUntil = new TimeOnly(21, 0);
monday.DailyLimitMinutes = 180;

DateTimeOffset allowedTime = new(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(3));
ScheduleStatus allowed = ScheduleEvaluator.Evaluate(settings, allowedTime);
Assert(allowed.IsAllowed, "Öğlen kullanıma izin verilmeliydi.");
Assert(allowed.DailyLimitMinutes == 180, "Günlük limit yanlış okundu.");

DateTimeOffset blockedTime = new(2026, 8, 24, 22, 0, 0, TimeSpan.FromHours(3));
ScheduleStatus blocked = ScheduleEvaluator.Evaluate(settings, blockedTime);
Assert(!blocked.IsAllowed, "Gece kullanımı engellenmeliydi.");

ControlSettings allowanceSettings = new();
DaySchedule allowanceMonday = allowanceSettings.Schedule.Single(item => item.Day == DayOfWeek.Monday);
allowanceMonday.IsEnabled = false;
allowanceSettings.TemporaryAllowances.Add(new TemporaryAllowance
{
    Date = new DateOnly(2026, 8, 24),
    AllowedFrom = new TimeOnly(18, 0),
    AllowedUntil = new TimeOnly(20, 0),
    BonusMinutes = 75,
    Note = "Özel gün"
});
ScheduleStatus temporaryAllowed = ScheduleEvaluator.Evaluate(allowanceSettings, new DateTimeOffset(2026, 8, 24, 19, 0, 0, TimeSpan.FromHours(3)));
Assert(temporaryAllowed.IsAllowed, "Geçici izin, kapalı bir günde kullanım açmadı.");
Assert(temporaryAllowed.DailyLimitMinutes == 75, "Geçici izin ek süresi günlük limite yansımadı.");
Assert(!ScheduleEvaluator.Evaluate(allowanceSettings, new DateTimeOffset(2026, 8, 24, 20, 30, 0, TimeSpan.FromHours(3))).IsAllowed, "Geçici izin saatinden sonra kullanım açık kaldı.");
SessionEngine allowanceEngine = new(allowanceSettings, new UsageLedger { LocalDay = new DateOnly(2026, 8, 24) }, new DateTimeOffset(2026, 8, 24, 19, 0, 0, TimeSpan.FromHours(3)));
Assert(allowanceEngine.GetSnapshot(new DateTimeOffset(2026, 8, 24, 19, 0, 0, TimeSpan.FromHours(3))).LimitSeconds == 75 * 60, "Geçici izin oturum limitine eklenmedi.");

string testDirectory = Path.Combine(Path.GetTempPath(), "KardesKilidi-SmokeTests", Guid.NewGuid().ToString("N"));
string testPath = Path.Combine(testDirectory, "settings.json");
JsonSettingsStore store = new(testPath);
settings.DeviceName = "Test Bilgisayarı";
settings.Theme = ThemePreference.Light;
settings.Language = LanguagePreference.English;
settings.SetupCompleted = true;
settings.Mode = ControlMode.Personal;
settings.StartWithWindows = true;
settings.AppRules.Add(new AppRule { Name = "Test", ExecutablePath = "C:\\Test.exe" });
await store.SaveAsync(settings);
ControlSettings loaded = await store.LoadAsync();
Assert(loaded.DeviceName == settings.DeviceName, "Ayarlar geri yüklenemedi.");
Assert(loaded.Theme == ThemePreference.Light, "Tema tercihi geri yüklenemedi.");
Assert(loaded.Language == LanguagePreference.English, "Dil tercihi geri yüklenemedi.");
Assert(loaded.SetupCompleted && loaded.Mode == ControlMode.Personal, "Kullanım biçimi geri yüklenemedi.");
Assert(loaded.StartWithWindows, "Windows başlangıç tercihi geri yüklenemedi.");
Assert(AdminPinService.Verify("4826", loaded.AdminPin), "Yönetici PIN'i güvenli biçimde geri yüklenemedi.");
Assert(loaded.AppRules.Count == 1, "Uygulama kuralları geri yüklenemedi.");

string recoverySettingsPath = Path.Combine(testDirectory, "recovery-settings.json");
JsonSettingsStore recoveryStore = new(recoverySettingsPath);
await recoveryStore.SaveAsync(new ControlSettings { DeviceName = "İlk sağlam kayıt" });
await recoveryStore.SaveAsync(new ControlSettings { DeviceName = "Son sağlam kayıt" });
Assert(File.Exists(recoveryStore.BackupPath), "Ayarların son sağlam yedeği oluşturulmadı.");
await File.WriteAllTextAsync(recoverySettingsPath, "{ bozuk json");
ControlSettings recoveredSettings = await recoveryStore.LoadAsync();
Assert(recoveredSettings.DeviceName == "Son sağlam kayıt", "Bozuk ayar dosyası son sağlam kayıttan kurtarılamadı.");
Assert(recoveryStore.LastLoadRecoveredFromBackup, "Yedekten kurtarma durumu bildirilmedi.");
Assert((await new JsonSettingsStore(recoverySettingsPath).LoadAsync()).DeviceName == "Son sağlam kayıt", "Kurtarılan ayar ana dosyaya geri yazılmadı.");

await File.WriteAllTextAsync(recoverySettingsPath, "{ yine bozuk");
await File.WriteAllTextAsync(recoveryStore.BackupPath, "{ yedek de bozuk");
bool corruptPairRejected = false;
try
{
    await recoveryStore.LoadAsync();
}
catch (InvalidDataException)
{
    corruptPairRejected = true;
}
Assert(corruptPairRejected, "Ana dosya ve yedek bozukken sessizce varsayılan ayarlara geçildi.");

string migrationUsagePath = Path.Combine(testDirectory, "migration-usage.json");
await File.WriteAllTextAsync(migrationUsagePath, """
{
  "SchemaVersion": 1,
  "LocalDay": "2026-08-24",
  "UsedSeconds": 120
}
""");
JsonUsageStore migrationUsageStore = new(migrationUsagePath);
UsageLedger migratedUsage = await migrationUsageStore.LoadAsync();
Assert(migratedUsage.SchemaVersion == 2 && migrationUsageStore.LastLoadMigrated, "Kullanım verisi şema 2'ye taşınmadı.");
Assert(File.Exists(migrationUsageStore.BackupPath), "Migration sonrasında sağlam kullanım yedeği oluşturulmadı.");

string concurrentUsagePath = Path.Combine(testDirectory, "concurrent-usage.json");
JsonUsageStore concurrentStoreA = new(concurrentUsagePath);
JsonUsageStore concurrentStoreB = new(concurrentUsagePath);
Guid concurrentRuleA = Guid.NewGuid();
Guid concurrentRuleB = Guid.NewGuid();
DateOnly concurrentDay = DateOnly.FromDateTime(DateTime.Today);
UsageLedger concurrentA = new()
{
    LocalDay = concurrentDay,
    UsedSeconds = 300,
    LastUpdatedUtc = DateTimeOffset.UtcNow,
    AppUsedSeconds = new Dictionary<Guid, long> { [concurrentRuleA] = 90 },
    RecentEvents = [new UsageEventRecord { Kind = UsageEventKind.BreakStarted, OccurredAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) }]
};
UsageLedger concurrentB = new()
{
    LocalDay = concurrentDay,
    UsedSeconds = 420,
    LastUpdatedUtc = DateTimeOffset.UtcNow.AddMilliseconds(1),
    AppUsedSeconds = new Dictionary<Guid, long> { [concurrentRuleB] = 120 },
    RecentEvents = [new UsageEventRecord { Kind = UsageEventKind.PolicyChanged, OccurredAtUtc = DateTimeOffset.UtcNow }]
};
await Task.WhenAll(concurrentStoreA.SaveAsync(concurrentA), concurrentStoreB.SaveAsync(concurrentB));
UsageLedger concurrentMerged = await concurrentStoreA.LoadAsync();
Assert(concurrentMerged.UsedSeconds == 420, "Eşzamanlı kullanım kayıtlarında güncel sayaç korunmadı.");
Assert(concurrentMerged.AppUsedSeconds.ContainsKey(concurrentRuleA) && concurrentMerged.AppUsedSeconds.ContainsKey(concurrentRuleB), "Eşzamanlı uygulama sayaçlarından biri kayboldu.");
Assert(concurrentMerged.RecentEvents.Count == 2, "Eşzamanlı geçmiş olaylarından biri kayboldu.");

string lockWaitPath = Path.Combine(testDirectory, "lock-wait-usage.json");
JsonUsageStore lockWaitStore = new(lockWaitPath);
Directory.CreateDirectory(Path.GetDirectoryName(lockWaitPath)!);
await using (FileStream heldLock = new(lockWaitPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
{
    Task delayedSave = lockWaitStore.SaveAsync(new UsageLedger { LocalDay = concurrentDay, UsedSeconds = 60 });
    await Task.Delay(150);
    Assert(!delayedSave.IsCompleted, "Kilitli veri dosyasına yazma beklemeden ilerledi.");
    await heldLock.DisposeAsync();
    await delayedSave;
}
Assert((await lockWaitStore.LoadAsync()).UsedSeconds == 60, "Dosya kilidi kalktıktan sonra kullanım kaydedilemedi.");

foreach (DaySchedule day in settings.Schedule)
{
    day.AllowedFrom = new TimeOnly(0, 0);
    day.AllowedUntil = new TimeOnly(0, 0);
    day.DailyLimitMinutes = 60;
}

UsageLedger ledger = new() { LocalDay = DateOnly.FromDateTime(allowedTime.LocalDateTime) };
SessionEngine engine = new(settings, ledger, allowedTime);
Assert(engine.StartOrResume(allowedTime), "Oturum başlatılamadı.");
engine.Accrue(TimeSpan.FromMinutes(20), allowedTime.AddMinutes(20));
Assert(engine.GetSnapshot(allowedTime.AddMinutes(20)).RemainingSeconds == 40 * 60, "Aktif süre doğru düşülmedi.");
Assert(engine.Pause(allowedTime.AddMinutes(20)), "Oturum mola moduna alınamadı.");
engine.Accrue(TimeSpan.FromMinutes(15), allowedTime.AddMinutes(35));
Assert(engine.GetSnapshot(allowedTime.AddMinutes(35)).RemainingSeconds == 40 * 60, "Mola sırasında süre düşmemeliydi.");
Assert(engine.StartOrResume(allowedTime.AddMinutes(35)), "Moladan devam edilemedi.");
engine.Accrue(TimeSpan.FromMinutes(40), allowedTime.AddMinutes(75));
Assert(engine.GetSnapshot(allowedTime.AddMinutes(75)).State == SessionState.TimeExpired, "Süre dolunca oturum kapanmadı.");

UsageLedger rollbackLedger = new()
{
    LocalDay = DateOnly.FromDateTime(allowedTime.LocalDateTime),
    LastUpdatedUtc = allowedTime.ToUniversalTime()
};
SessionEngine rollbackEngine = new(settings, rollbackLedger, allowedTime.AddHours(-1));
SessionSnapshot rollbackSnapshot = rollbackEngine.GetSnapshot(allowedTime.AddHours(-1));
Assert(rollbackSnapshot.State == SessionState.OutsideSchedule, "Sistem saati geri alındığında kullanım engellenmedi.");
Assert(rollbackLedger.ClockRollbackUntilUtc == allowedTime.ToUniversalTime(), "Saat geri alma güvenli zamana kadar kaydedilmedi.");
rollbackEngine.ForceStartForTesting(allowedTime.AddHours(-1));
Assert(rollbackEngine.GetSnapshot(allowedTime.AddHours(-1)).State == SessionState.Active, "Gizli test çıkışı saat geri alma kilidini kaldıramadı.");

ControlSettings expandedLimitSettings = new();
foreach (DaySchedule day in expandedLimitSettings.Schedule)
{
    day.AllowedFrom = new TimeOnly(0, 0);
    day.AllowedUntil = new TimeOnly(0, 0);
    day.DailyLimitMinutes = 90;
}

UsageLedger expiredLedger = new()
{
    LocalDay = DateOnly.FromDateTime(allowedTime.LocalDateTime),
    UsedSeconds = 60 * 60,
    State = SessionState.TimeExpired
};
SessionEngine expandedLimitEngine = new(expandedLimitSettings, expiredLedger, allowedTime.AddMinutes(75));
SessionSnapshot expandedSnapshot = expandedLimitEngine.GetSnapshot(allowedTime.AddMinutes(75));
Assert(expandedSnapshot.State == SessionState.Ready, "Günlük limit artırılınca süre sonu kilidi kalkmadı.");
Assert(expandedSnapshot.RemainingSeconds == 30 * 60, "Artırılan günlük süre kalan zamana yansımadı.");

engine.AddBonusMinutes(30, allowedTime.AddMinutes(75));
Assert(engine.GetSnapshot(allowedTime.AddMinutes(75)).State == SessionState.Ready, "Ek süre oturumu tekrar hazır hâle getirmedi.");

UsageLedger testUnlockLedger = new()
{
    LocalDay = DateOnly.FromDateTime(allowedTime.LocalDateTime),
    UsedSeconds = 60 * 60,
    State = SessionState.TimeExpired
};
SessionEngine testUnlockEngine = new(settings, testUnlockLedger, allowedTime.AddMinutes(75));
testUnlockEngine.ForceStartForTesting(allowedTime.AddMinutes(75));
SessionSnapshot testUnlockSnapshot = testUnlockEngine.GetSnapshot(allowedTime.AddMinutes(75));
Assert(testUnlockSnapshot.State == SessionState.Active, "Gizli test kilidi kaldırma oturumu başlatmadı.");
Assert(testUnlockSnapshot.RemainingSeconds == 60 * 60, "Gizli test kilidi kaldırma bir saatlik pencere açmadı.");
testUnlockEngine.Accrue(TimeSpan.FromHours(1), allowedTime.AddMinutes(135));
Assert(testUnlockEngine.GetSnapshot(allowedTime.AddMinutes(135)).State == SessionState.TimeExpired, "Test kilidi kaldırma süresiz açık kaldı.");

string usagePath = Path.Combine(testDirectory, "usage.json");
JsonUsageStore usageStore = new(usagePath);
await usageStore.SaveAsync(ledger);
UsageLedger loadedLedger = await usageStore.LoadAsync();
Assert(loadedLedger.UsedSeconds == ledger.UsedSeconds, "Kullanım kaydı geri yüklenemedi.");

string legacyPath = Path.Combine(testDirectory, "legacy-settings.json");
await File.WriteAllTextAsync(legacyPath, "{\"SchemaVersion\":1,\"DeviceName\":\"Eski Kurulum\"}");
ControlSettings migratedSettings = await new JsonSettingsStore(legacyPath).LoadAsync();
Assert(migratedSettings.SchemaVersion == 2, "Eski ayar şeması yükseltilemedi.");
Assert(migratedSettings.SetupCompleted, "Mevcut kullanıcıya ilk kurulum ekranı yeniden gösterilmemeli.");
Assert(migratedSettings.Mode == ControlMode.Protected, "Mevcut kullanıcı korumalı kullanıma taşınmalı.");

ControlSettings strictPolicy = new();
strictPolicy.StartWithWindows = true;
DaySchedule strictMonday = strictPolicy.Schedule.Single(day => day.Day == DayOfWeek.Monday);
strictMonday.AllowedFrom = new TimeOnly(10, 0);
strictMonday.AllowedUntil = new TimeOnly(20, 0);
strictMonday.DailyLimitMinutes = 60;

ControlSettings relaxedPolicy = new();
relaxedPolicy.StartWithWindows = false;
DaySchedule relaxedMonday = relaxedPolicy.Schedule.Single(day => day.Day == DayOfWeek.Monday);
relaxedMonday.AllowedFrom = new TimeOnly(9, 0);
relaxedMonday.AllowedUntil = new TimeOnly(21, 0);
relaxedMonday.DailyLimitMinutes = 90;
Assert(SettingsPolicyComparer.HasRelaxation(strictPolicy, relaxedPolicy), "Süre/saat genişletme gevşetme olarak algılanmadı.");
Assert(!SettingsPolicyComparer.HasRelaxation(relaxedPolicy, strictPolicy), "Kuralları sıkılaştırma yanlışlıkla gecikmeli sayıldı.");
ControlSettings datedRelaxation = CloneForTest(strictPolicy);
datedRelaxation.TemporaryAllowances.Add(new TemporaryAllowance { Date = new DateOnly(2026, 8, 26), BonusMinutes = 45 });
Assert(SettingsPolicyComparer.HasRelaxation(strictPolicy, datedRelaxation), "Yeni geçici izin gevşetme olarak algılanmadı.");

string pendingPath = Path.Combine(testDirectory, "pending-settings.json");
ControlSettings pendingBase = new()
{
    SetupCompleted = true,
    Mode = ControlMode.Personal,
    DeviceName = "Şimdiki Ad"
};
ControlSettings pendingTarget = new()
{
    SetupCompleted = true,
    Mode = ControlMode.Personal,
    DeviceName = "Yeni Ad"
};
pendingBase.PendingChange = new PendingPolicyChange
{
    ApplyAfterUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
    TargetSettings = pendingTarget
};
JsonSettingsStore pendingStore = new(pendingPath);
await pendingStore.SaveAsync(pendingBase);
ControlSettings appliedPending = await pendingStore.LoadAsync();
Assert(appliedPending.DeviceName == "Yeni Ad" && appliedPending.PendingChange is null, "Süresi dolan bekleyen değişiklik uygulanmadı.");

string shortcutSettingsPath = Path.Combine(testDirectory, "shortcut-settings.json");
string shortcutUsagePath = Path.Combine(testDirectory, "shortcut-usage.json");
ControlSettings shortcutSettings = new() { SetupCompleted = true, Mode = ControlMode.Personal };
shortcutSettings.PendingChange = new PendingPolicyChange
{
    ApplyAfterUtc = DateTimeOffset.UtcNow.AddHours(1),
    TargetSettings = new ControlSettings
    {
        SetupCompleted = true,
        Mode = ControlMode.Protected,
        AdminPin = credential
    }
};
JsonSettingsStore shortcutStore = new(shortcutSettingsPath);
await shortcutStore.SaveAsync(shortcutSettings);
CafeViewModel shortcutViewModel = new(shortcutStore, new JsonUsageStore(shortcutUsagePath));
await shortcutViewModel.InitializeAsync();
await shortcutViewModel.ForceUnlockForTestingAsync();
ControlSettings shortcutApplied = await shortcutStore.LoadAsync();
Assert(shortcutApplied.Mode == ControlMode.Protected && shortcutApplied.PendingChange is null, "Gizli yönetici kısayolu bekleyen değişikliği hemen uygulamadı.");

string personalSettingsPath = Path.Combine(testDirectory, "personal-settings.json");
string personalUsagePath = Path.Combine(testDirectory, "personal-usage.json");
ControlSettings personalSettings = new()
{
    SetupCompleted = true,
    Mode = ControlMode.Personal,
    PersonalChangeDelayMinutes = 60
};
personalSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes = 60;
JsonSettingsStore personalSettingsStore = new(personalSettingsPath);
await personalSettingsStore.SaveAsync(personalSettings);
MainViewModel personalViewModel = new(personalSettingsStore, new JsonUsageStore(personalUsagePath));
await personalViewModel.InitializeAsync();
personalViewModel.ScheduleRows.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes = 90;
Assert(await personalViewModel.SaveAsync(), $"Kişisel mod değişikliği kaydedilemedi: {personalViewModel.StatusMessage}");
ControlSettings queuedPersonalSettings = await personalSettingsStore.LoadAsync();
Assert(queuedPersonalSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 60, "Gevşeten değişiklik hemen uygulandı.");
Assert(queuedPersonalSettings.PendingChange?.TargetSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 90, "Gevşeten değişiklik beklemeye alınmadı.");

ControlSettings shorterDelay = CloneForTest(queuedPersonalSettings);
shorterDelay.PersonalChangeDelayMinutes = 15;
Assert(SettingsPolicyComparer.HasRelaxation(queuedPersonalSettings, shorterDelay), "Bekleme süresini azaltma gevşetme olarak algılanmadı.");

await personalViewModel.SetControlModeAsync(ControlMode.Protected, "4826");
ControlSettings queuedModeSettings = await personalSettingsStore.LoadAsync();
Assert(queuedModeSettings.Mode == ControlMode.Personal, "Korumalı moda geçiş beklemeden uygulandı.");
PendingPolicyChange queuedMode = queuedModeSettings.PendingChange ?? throw new InvalidOperationException("Mod değişikliği beklemeye alınmadı.");
Assert(queuedMode.TargetSettings.Mode == ControlMode.Protected, "Bekleyen mod hedefi yanlış.");
Assert(queuedMode.TargetSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 90, "Mod değişikliği mevcut bekleyen ayarı kaybetti.");
Assert(AdminPinService.Verify("4826", queuedMode.TargetSettings.AdminPin), "Bekleyen korumalı mod PIN'i güvenli biçimde saklanmadı.");

string blockedExecutable = Path.Combine(testDirectory, "otium-rule-test.exe");
File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), blockedExecutable);
ControlSettings enforcementSettings = new();
enforcementSettings.AppRules.Add(new AppRule
{
    Name = "Rule test",
    ExecutablePath = blockedExecutable,
    Mode = AppRuleMode.Blocked
});
using (Process blockedProcess = Process.Start(new ProcessStartInfo
{
    FileName = blockedExecutable,
    Arguments = "/c ping 127.0.0.1 -n 30 >nul",
    UseShellExecute = false,
    CreateNoWindow = true
}) ?? throw new InvalidOperationException("Uygulama kuralı test süreci başlatılamadı."))
{
    await Task.Delay(200);
    new ApplicationRuleEnforcer().Enforce(enforcementSettings, new UsageLedger(), TimeSpan.FromSeconds(1));
    Assert(blockedProcess.WaitForExit(3000), "Engelli uygulama çalışan süreç olarak bırakıldı.");
}

AppRule limitedRule = enforcementSettings.AppRules[0];
limitedRule.Mode = AppRuleMode.Limited;
limitedRule.DailyLimitMinutes = 1;
UsageLedger applicationLedger = new();
ApplicationRuleEnforcer limitedEnforcer = new();
using (Process limitedProcess = Process.Start(new ProcessStartInfo
{
    FileName = blockedExecutable,
    Arguments = "/c ping 127.0.0.1 -n 30 >nul",
    UseShellExecute = false,
    CreateNoWindow = true
}) ?? throw new InvalidOperationException("Süreli uygulama test süreci başlatılamadı."))
{
    await Task.Delay(200);
    limitedEnforcer.Enforce(enforcementSettings, applicationLedger, TimeSpan.FromSeconds(2));
    Assert(applicationLedger.AppUsedSeconds.GetValueOrDefault(limitedRule.Id) == 2, "Uygulama kullanım süresi kaydedilmedi.");
    limitedRule.DailyLimitMinutes = 0;
    limitedEnforcer.Enforce(enforcementSettings, applicationLedger, TimeSpan.Zero);
    Assert(limitedProcess.WaitForExit(3000), "Uygulama limiti dolunca süreç sonlandırılmadı.");
}

limitedRule.Mode = AppRuleMode.Unlimited;
limitedRule.DailyLimitMinutes = 0;
UsageLedger unlimitedLedger = new();
using (Process unlimitedProcess = Process.Start(new ProcessStartInfo
{
    FileName = blockedExecutable,
    Arguments = "/c ping 127.0.0.1 -n 3 >nul",
    UseShellExecute = false,
    CreateNoWindow = true
}) ?? throw new InvalidOperationException("Sınırsız uygulama test süreci başlatılamadı."))
{
    await Task.Delay(200);
    new ApplicationRuleEnforcer().Enforce(enforcementSettings, unlimitedLedger, TimeSpan.FromSeconds(2));
    Assert(unlimitedLedger.AppUsedSeconds.GetValueOrDefault(limitedRule.Id) == 2, "Sınırsız uygulama kullanım geçmişine kaydedilmedi.");
    unlimitedProcess.Kill(entireProcessTree: true);
    unlimitedProcess.WaitForExit(3000);
}

ControlSettings historySettings = new();
DaySchedule historyMonday = historySettings.Schedule.Single(item => item.Day == DayOfWeek.Monday);
historyMonday.AllowedFrom = new TimeOnly(9, 0);
historyMonday.AllowedUntil = new TimeOnly(21, 0);
historyMonday.DailyLimitMinutes = 60;
AppRule historyRule = new() { Name = "Geçmiş uygulaması", ExecutablePath = "C:\\History.exe", Mode = AppRuleMode.Limited };
historySettings.AppRules.Add(historyRule);
UsageLedger historyLedger = new()
{
    LocalDay = new DateOnly(2026, 8, 23),
    UsedSeconds = 3600,
    BonusMinutes = 15,
    BreakCount = 2,
    LimitReachedCount = 1,
    ExtraTimeGrantCount = 1,
    AppUsedSeconds = new Dictionary<Guid, long> { [historyRule.Id] = 900 }
};
DateTimeOffset historyNow = new(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(3));
SessionEngine historyEngine = new(historySettings, historyLedger, historyNow);
DailyUsageRecord archivedDay = historyLedger.History.Single(item => item.LocalDay == new DateOnly(2026, 8, 23));
Assert(archivedDay.UsedSeconds == 3600 && archivedDay.BreakCount == 2, "Önceki gün kullanım geçmişine arşivlenmedi.");
Assert(archivedDay.Applications.Single().Name == "Geçmiş uygulaması", "Uygulama geçmişi adıyla arşivlenmedi.");
Assert(historyLedger.UsedSeconds == 0 && historyLedger.BreakCount == 0, "Yeni günde aktif sayaçlar sıfırlanmadı.");
Assert(historyEngine.StartOrResume(historyNow), "Geçmiş testi oturumu başlatılamadı.");
Assert(historyEngine.Pause(historyNow.AddMinutes(1)), "Geçmiş testi molası başlatılamadı.");
historyEngine.AddBonusMinutes(15, historyNow.AddMinutes(2));
Assert(historyLedger.BreakCount == 1 && historyLedger.ExtraTimeGrantCount == 1, "Mola veya ek süre sayaçları kaydedilmedi.");
Assert(historyLedger.RecentEvents.Any(item => item.Kind == UsageEventKind.BreakStarted) &&
       historyLedger.RecentEvents.Any(item => item.Kind == UsageEventKind.ExtraTimeGranted && item.Value == 15),
    "Kullanım geçmişi olayları kaydedilmedi.");

ControlSettings limitHistorySettings = CloneForTest(historySettings);
limitHistorySettings.Schedule.Single(item => item.Day == DayOfWeek.Monday).DailyLimitMinutes = 1;
UsageLedger limitHistoryLedger = new() { LocalDay = new DateOnly(2026, 8, 24) };
SessionEngine limitHistoryEngine = new(limitHistorySettings, limitHistoryLedger, historyNow);
Assert(limitHistoryEngine.StartOrResume(historyNow), "Limit geçmişi oturumu başlatılamadı.");
limitHistoryEngine.Accrue(TimeSpan.FromMinutes(1), historyNow.AddMinutes(1));
Assert(limitHistoryLedger.LimitReachedCount == 1 && limitHistoryLedger.RecentEvents.Single().Kind == UsageEventKind.LimitReached,
    "Limit dolma olayı geçmişe kaydedilmedi.");

Directory.Delete(testDirectory, true);
Console.WriteLine("Otium çekirdek kontrolleri başarılı.");

static ControlSettings CloneForTest(ControlSettings settings) => new()
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
    PersonalChangeDelayMinutes = settings.PersonalChangeDelayMinutes,
    AdminPin = settings.AdminPin,
    WarningMinutes = [.. settings.WarningMinutes],
    Schedule = settings.Schedule.Select(day => new DaySchedule
    {
        Day = day.Day,
        IsEnabled = day.IsEnabled,
        AllowedFrom = day.AllowedFrom,
        AllowedUntil = day.AllowedUntil,
        DailyLimitMinutes = day.DailyLimitMinutes
    }).ToList(),
    TemporaryAllowances = settings.TemporaryAllowances.Select(item => new TemporaryAllowance
    {
        Id = item.Id,
        Date = item.Date,
        AllowedFrom = item.AllowedFrom,
        AllowedUntil = item.AllowedUntil,
        BonusMinutes = item.BonusMinutes,
        Note = item.Note
    }).ToList(),
    AppRules = settings.AppRules.Select(rule => new AppRule
    {
        Id = rule.Id,
        Name = rule.Name,
        ExecutablePath = rule.ExecutablePath,
        Mode = rule.Mode,
        DailyLimitMinutes = rule.DailyLimitMinutes
    }).ToList()
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

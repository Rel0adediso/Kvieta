using Kvieta.Core.Models;
using Kvieta.Core.Services;
using Kvieta.App;
using Kvieta.App.ViewModels;
using Kvieta.App.Services;
using Kvieta.SetupApp;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

if (args.Contains("--companion-preview", StringComparer.Ordinal))
{
    await using LocalManagerDeviceEnrollmentEndpoint previewEndpoint =
        LocalManagerDeviceEnrollmentEndpoint.Start(_ => Task.FromResult(false), DateTimeOffset.UtcNow);
    Console.WriteLine(previewEndpoint.PairingUri.AbsoluteUri);
    await Task.Delay(TimeSpan.FromMinutes(2));
    return;
}

ControlSettings settings = new();
#if KVIETA_DEVELOPMENT_BUILD
Assert(BuildInfo.IsDevelopmentBuild && BuildInfo.Flavor == "development", "Debug paketi Development/Test olarak işaretlenmedi.");
#else
Assert(!BuildInfo.IsDevelopmentBuild && BuildInfo.Flavor == "public", "Release paketi Public olarak işaretlenmedi.");
#endif
Assert(!string.IsNullOrWhiteSpace(BuildInfo.RepositoryCommit) &&
       !string.IsNullOrWhiteSpace(BuildInfo.InformationalVersion) &&
       (BuildInfo.RepositoryCommit == "unknown" ||
        BuildInfo.RepositoryCommit.Length == 40 && BuildInfo.RepositoryCommit.All(Uri.IsHexDigit)) &&
       BuildInfo.DisplayRevision.EndsWith("-dirty", StringComparison.Ordinal) == BuildInfo.IsRepositoryDirty,
    "Build commit veya çalışma ağacı kimliği assembly metadata'sına doğru gömülmedi.");
Assert(settings.DeviceName == "Bu Bilgisayar", "Yeni kurulumun varsayılan cihaz adı yanlış.");
string singleWindowChannel = $"Smoke{Guid.NewGuid():N}";
using (SingleInstanceCoordinator primaryWindow = new(singleWindowChannel))
using (SingleInstanceCoordinator duplicateWindow = new(singleWindowChannel))
{
    Assert(primaryWindow.IsPrimary && !duplicateWindow.IsPrimary,
        "Aynı yönetim penceresi kanalı ikinci bir örneğe izin veriyor.");
}
Exception? adminPinWindowInitializationError = null;
Thread adminPinWindowThread = new(() =>
{
    try
    {
        Kvieta.App.App application = new();
        application.InitializeComponent();
        _ = Kvieta.App.AdminPinWindow.CreateSetup();
        _ = new Kvieta.App.RecoveryResetWindow(
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult(false),
            recoveryCodeAvailable: false,
            managerDeviceName: "Test phone");
        _ = new Kvieta.App.BonusTimeWindow();
        application.Shutdown();
    }
    catch (Exception exception)
    {
        adminPinWindowInitializationError = exception;
    }
});
adminPinWindowThread.SetApartmentState(ApartmentState.STA);
adminPinWindowThread.Start();
adminPinWindowThread.Join();
Assert(adminPinWindowInitializationError is null,
    $"PIN, QR ve yönetici çıkışının ortak penceresi oluşturulamadı: {adminPinWindowInitializationError?.Message}");
RecoveryChallenge protocolVector = new(
    "ABC",
    "device-1",
    DateTimeOffset.FromUnixTimeSeconds(1_700_000_123),
    "AQIDBA==",
    "pin-reset",
    "YWJjZA==");
Assert(ManagerDeviceAuthorizationService.CreateSignedContent(protocolVector) ==
       "kvieta-manager-recovery-v1.QUJD.ZGV2aWNlLTE=.MTcwMDAwMDEyMw==.QVFJREJBPT0=.cGluLXJlc2V0.WVdKalpBPT0=" &&
       ManagerDeviceVerificationCode.ForRecoveryChallenge(protocolVector) == "263607",
    "Yerel companion sitesiyle paylaşılan recovery protokol vektörü değişti.");
Assert(QrCodeImageService.Create("http://192.168.1.2:24873/token/").PixelWidth > 0,
    "Companion eşleştirme QR görseli üretilemedi.");
await using (LocalManagerDeviceEnrollmentEndpoint endpoint =
    LocalManagerDeviceEnrollmentEndpoint.Start(_ => Task.FromResult(false), DateTimeOffset.UtcNow))
{
    using HttpClient localClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    using HttpResponseMessage siteResponse = await localClient.GetAsync(endpoint.PairingUri);
    string siteHtml = await siteResponse.Content.ReadAsStringAsync();
    Assert(siteResponse.IsSuccessStatusCode &&
        siteResponse.Content.Headers.ContentType?.MediaType == "text/html" &&
        siteHtml.Contains("Kvieta Companion", StringComparison.Ordinal) &&
        siteResponse.Headers.Contains("Content-Security-Policy"),
        "Telefon için gömülü yerel companion sitesi sunulamadı.");
    string endpointDescription = await localClient.GetStringAsync(new Uri(endpoint.PairingUri, "api"));
    Assert(endpointDescription.Contains("kvieta-enrollment", StringComparison.Ordinal) &&
        !endpointDescription.Contains("verificationCode", StringComparison.Ordinal),
        "Standart kullanıcı yerel eşleştirme endpoint'ini açamadı.");
}
await using (LocalManagerDeviceEnrollmentEndpoint firstEndpoint =
    LocalManagerDeviceEnrollmentEndpoint.Start(_ => Task.FromResult(false), DateTimeOffset.UtcNow))
await using (LocalManagerDeviceEnrollmentEndpoint secondEndpoint =
    LocalManagerDeviceEnrollmentEndpoint.Start(_ => Task.FromResult(false), DateTimeOffset.UtcNow))
{
    Assert(firstEndpoint.PairingUri.Port != secondEndpoint.PairingUri.Port,
        "Açık kalmış eşleştirme oturumu yeni QR oturumunun başlamasını engelledi.");
    using HttpClient localClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    using HttpResponseMessage firstResponse = await localClient.GetAsync(firstEndpoint.PairingUri);
    using HttpResponseMessage secondResponse = await localClient.GetAsync(secondEndpoint.PairingUri);
    Assert(firstResponse.IsSuccessStatusCode && secondResponse.IsSuccessStatusCode,
        "Yedek eşleştirme portu yerel companion sitesini sunamadı.");
}
DateTimeOffset challengeNow = DateTimeOffset.UtcNow;
byte[] challengeKey = RandomNumberGenerator.GetBytes(32);
RecoveryChallengeService challengeService = new();
RecoveryChallenge challenge = challengeService.Issue("manager-phone", challengeNow);
byte[] challengeSignature = HMACSHA256.HashData(
    challengeKey,
    System.Text.Encoding.UTF8.GetBytes(RecoveryChallengeService.CreateSignedContent(challenge)));
RecoveryChallengeResponse challengeResponse = new(
    challenge.ChallengeId,
    challenge.DeviceId,
    challenge.NonceBase64,
    Convert.ToBase64String(challengeSignature));
Assert(!challengeService.TryConsume(
        challengeResponse with { SignatureBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        "manager-phone",
        challengeKey,
        challengeNow) &&
       challengeService.TryConsume(challengeResponse, "manager-phone", challengeKey, challengeNow) &&
       !challengeService.TryConsume(challengeResponse, "manager-phone", challengeKey, challengeNow),
    "Hatalı imza challenge'ı tüketti veya recovery challenge tek kullanımlık uygulanmadı.");
RecoveryChallenge expiredChallenge = challengeService.Issue("manager-phone", challengeNow);
byte[] expiredSignature = HMACSHA256.HashData(
    challengeKey,
    System.Text.Encoding.UTF8.GetBytes(RecoveryChallengeService.CreateSignedContent(expiredChallenge)));
Assert(!challengeService.TryConsume(
        new RecoveryChallengeResponse(
            expiredChallenge.ChallengeId,
            expiredChallenge.DeviceId,
            expiredChallenge.NonceBase64,
            Convert.ToBase64String(expiredSignature)),
        "manager-phone",
        challengeKey,
        challengeNow.AddMinutes(3)) &&
    !challengeService.TryConsume(
        challengeResponse with
        {
            ChallengeId = expiredChallenge.ChallengeId,
            NonceBase64 = expiredChallenge.NonceBase64
        },
        "other-phone",
        challengeKey,
        challengeNow),
    "Recovery challenge süresi veya cihaz bağlamı doğrulanmıyor.");
using ECDsa managerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
RecoveryChallenge managerChallenge = challengeService.Issue("manager-phone", challengeNow);
ManagerDeviceEnrollment managerEnrollment = new(
    "manager-phone",
    "Manager phone",
    managerKey.ExportSubjectPublicKeyInfoPem(),
    challengeNow);
byte[] enrollmentProof = managerKey.SignData(
    System.Text.Encoding.UTF8.GetBytes(ManagerDeviceEnrollmentService.CreateProofContent(managerEnrollment)),
    HashAlgorithmName.SHA256,
    DSASignatureFormat.Rfc3279DerSequence);
ManagerDeviceEnrollmentRequest enrollmentRequest = new(
    managerEnrollment,
    Convert.ToBase64String(enrollmentProof));
Assert(ManagerDeviceEnrollmentService.VerifyRequest(enrollmentRequest, challengeNow) &&
    !ManagerDeviceEnrollmentService.VerifyRequest(enrollmentRequest, challengeNow.AddMinutes(11)) &&
    !ManagerDeviceEnrollmentService.VerifyRequest(
        enrollmentRequest with { ProofSignatureBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)) },
        challengeNow),
    "Yönetici cihazı enrollment anahtar sahipliği veya zaman sınırı doğrulanmadı.");
string enrollmentVerificationCode = ManagerDeviceVerificationCode.ForEnrollmentRequest(enrollmentRequest);
Assert(enrollmentVerificationCode.Length == 8 && enrollmentVerificationCode.All(char.IsAsciiDigit) &&
       enrollmentVerificationCode != ManagerDeviceVerificationCode.ForEnrollmentRequest(
           enrollmentRequest with { Enrollment = managerEnrollment with { DeviceName = "Other phone" } }),
    "Enrollment doğrulama kodu teklif edilen cihaz kimliğine bağlı değil.");
bool localEnrollmentAccepted = false;
await using (LocalManagerDeviceEnrollmentEndpoint endpoint =
    LocalManagerDeviceEnrollmentEndpoint.Start(request =>
    {
        localEnrollmentAccepted = ManagerDeviceEnrollmentService.VerifyRequest(request, DateTimeOffset.UtcNow);
        return Task.FromResult(localEnrollmentAccepted);
    }, DateTimeOffset.UtcNow))
{
    using HttpClient localClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    using StringContent content = new(
        JsonSerializer.Serialize(enrollmentRequest),
        System.Text.Encoding.UTF8,
        "application/json");
    using HttpResponseMessage response = await localClient.PostAsync(new Uri(endpoint.PairingUri, "api"), content);
    string pendingResponse = await response.Content.ReadAsStringAsync();
    Assert(response.IsSuccessStatusCode && !localEnrollmentAccepted &&
           pendingResponse.Contains(enrollmentVerificationCode, StringComparison.Ordinal),
        "Companion enrollment teklifi bilgisayar onayı beklemeden kaydedildi.");
    bool enrollmentConfirmed = await endpoint.ConfirmPendingAsync();
    Assert(enrollmentConfirmed && localEnrollmentAccepted,
        "Bilgisayarda kod karşılaştırması onaylanan enrollment Guardian callback'ine ulaşmadı.");
}
byte[] managerSignature = managerKey.SignData(
    System.Text.Encoding.UTF8.GetBytes(ManagerDeviceAuthorizationService.CreateSignedContent(managerChallenge)),
    HashAlgorithmName.SHA256,
    DSASignatureFormat.Rfc3279DerSequence);
RecoveryChallengeResponse managerResponse = new(
    managerChallenge.ChallengeId,
    managerChallenge.DeviceId,
    managerChallenge.NonceBase64,
    Convert.ToBase64String(managerSignature));
Assert(ManagerDeviceAuthorizationService.VerifyResponse(
        managerEnrollment,
        managerChallenge,
        managerResponse,
        challengeNow) &&
    !ManagerDeviceAuthorizationService.VerifyResponse(
        managerEnrollment with { RevokedAtUtc = challengeNow },
        managerChallenge,
        managerResponse,
        challengeNow) &&
    !ManagerDeviceAuthorizationService.VerifyResponse(
        managerEnrollment,
        managerChallenge,
        managerResponse,
        challengeNow.AddMinutes(3)),
    "Yönetici telefonunun imzası, iptal durumu veya challenge süresi doğrulanmadı.");
bool localRecoveryAccepted = false;
await using (LocalRecoveryEndpoint endpoint = LocalRecoveryEndpoint.Start(
    managerEnrollment,
    managerChallenge,
    _ =>
    {
        localRecoveryAccepted = true;
        return Task.FromResult(true);
    },
    challengeNow))
{
    using HttpClient localClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    using StringContent content = new(
        JsonSerializer.Serialize(managerResponse),
        System.Text.Encoding.UTF8,
        "application/json");
    using HttpResponseMessage response = await localClient.PostAsync(new Uri(endpoint.RecoveryUri, "api"), content);
    Assert(response.IsSuccessStatusCode && localRecoveryAccepted,
        "Companion recovery imzası yerel endpoint üzerinden Guardian callback'ine ulaşmadı.");
}
AdminCredential managerResetCredential = AdminPinService.Create("7351");
RecoveryChallenge managerResetChallenge = ManagerDeviceRecoveryService.CreatePinResetChallenge(
    managerEnrollment.DeviceId,
    managerResetCredential,
    challengeNow);
byte[] managerResetSignature = managerKey.SignData(
    System.Text.Encoding.UTF8.GetBytes(
        ManagerDeviceAuthorizationService.CreateSignedContent(managerResetChallenge)),
    HashAlgorithmName.SHA256,
    DSASignatureFormat.Rfc3279DerSequence);
RecoveryChallengeResponse managerResetResponse = new(
    managerResetChallenge.ChallengeId,
    managerResetChallenge.DeviceId,
    managerResetChallenge.NonceBase64,
    Convert.ToBase64String(managerResetSignature));
Assert(ManagerDeviceRecoveryService.MatchesPinReset(managerResetChallenge, managerResetCredential) &&
    !ManagerDeviceRecoveryService.MatchesPinReset(
        managerResetChallenge,
        AdminPinService.Create("7352")) &&
    ManagerDeviceAuthorizationService.VerifyResponse(
        managerEnrollment,
        managerResetChallenge,
        managerResetResponse,
        challengeNow),
    "Yönetici cihazı onayı yeni PIN credential'ına bağlanmadı.");
using ECDsa replacementKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
ManagerDeviceEnrollment replacementEnrollment = new(
    "replacement-phone",
    "Replacement phone",
    replacementKey.ExportSubjectPublicKeyInfoPem(),
    challengeNow);
ManagerDeviceTransfer deviceTransfer = new(
    managerEnrollment.DeviceId,
    replacementEnrollment.DeviceId,
    ManagerDeviceTransferService.CreatePublicKeyHash(replacementEnrollment.PublicKeyPem),
    challengeNow.AddMinutes(5),
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
    string.Empty,
    string.Empty);
byte[] transferContent = System.Text.Encoding.UTF8.GetBytes(
    ManagerDeviceTransferService.CreateSignedContent(deviceTransfer));
deviceTransfer = deviceTransfer with
{
    CurrentDeviceSignatureBase64 = Convert.ToBase64String(managerKey.SignData(
        transferContent,
        HashAlgorithmName.SHA256,
        DSASignatureFormat.Rfc3279DerSequence)),
    NewDeviceSignatureBase64 = Convert.ToBase64String(replacementKey.SignData(
        transferContent,
        HashAlgorithmName.SHA256,
        DSASignatureFormat.Rfc3279DerSequence))
};
Assert(ManagerDeviceTransferService.CompleteTransfer(
        managerEnrollment,
        replacementEnrollment,
        deviceTransfer,
        challengeNow)?.DeviceId == replacementEnrollment.DeviceId &&
    ManagerDeviceTransferService.CompleteTransfer(
        managerEnrollment,
        replacementEnrollment,
        deviceTransfer,
        challengeNow.AddMinutes(6)) is null &&
    ManagerDeviceTransferService.CompleteTransfer(
        managerEnrollment,
        replacementEnrollment,
        deviceTransfer with { NewDeviceSignatureBase64 = deviceTransfer.CurrentDeviceSignatureBase64 },
        challengeNow) is null &&
    !ManagerDeviceTransferService.Revoke(managerEnrollment, challengeNow).IsActive,
    "Yönetici telefonu aktarımı imzaları, süresi veya eski cihaz iptali doğru uygulanmadı.");
bool localTransferAccepted = false;
await using (LocalManagerDeviceTransferEndpoint endpoint = LocalManagerDeviceTransferEndpoint.Start(
    managerEnrollment,
    request =>
    {
        localTransferAccepted = ManagerDeviceTransferService.CompleteTransfer(
            managerEnrollment, request.Replacement, request.Transfer, DateTimeOffset.UtcNow) is not null;
        return Task.FromResult(localTransferAccepted);
    },
    DateTimeOffset.UtcNow))
{
    using HttpClient localClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    Uri apiUri = new(endpoint.TransferUri, "api");
    ManagerDeviceTransferRequest proposal = new(
        replacementEnrollment,
        deviceTransfer with { CurrentDeviceSignatureBase64 = string.Empty });
    using StringContent proposalContent = new(
        JsonSerializer.Serialize(proposal),
        System.Text.Encoding.UTF8,
        "application/json");
    using HttpResponseMessage proposalResponse = await localClient.PostAsync(apiUri, proposalContent);
    string currentPhase = await localClient.GetStringAsync(apiUri);
    using StringContent invalidApprovalContent = new(
        JsonSerializer.Serialize(new
        {
            CurrentDeviceSignatureBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
        }),
        System.Text.Encoding.UTF8,
        "application/json");
    using HttpResponseMessage invalidApprovalResponse = await localClient.PostAsync(apiUri, invalidApprovalContent);
    using StringContent approvalContent = new(
        JsonSerializer.Serialize(new { deviceTransfer.CurrentDeviceSignatureBase64 }),
        System.Text.Encoding.UTF8,
        "application/json");
    using HttpResponseMessage approvalResponse = await localClient.PostAsync(apiUri, approvalContent);
    Assert(proposalResponse.IsSuccessStatusCode &&
        currentPhase.Contains("kvieta-transfer-current", StringComparison.Ordinal) &&
        invalidApprovalResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
        approvalResponse.IsSuccessStatusCode && localTransferAccepted,
        "Geçersiz imza transferi tüketti veya geçerli çift cihaz imzalı aktarım tamamlanmadı.");
}
Assert(SetupPlan.DeterminePackageAction(null, new Version(1, 0, 0)) == SetupPackageAction.FreshInstall &&
       SetupPlan.DeterminePackageAction(new Version(0, 19, 0), new Version(1, 0, 0)) == SetupPackageAction.Update &&
       SetupPlan.DeterminePackageAction(new Version(1, 0, 0), new Version(1, 0, 0)) == SetupPackageAction.Repair &&
       SetupPlan.DeterminePackageAction(new Version(1, 1, 0), new Version(1, 0, 0)) == SetupPackageAction.DowngradeBlocked,
    "Kurucu yüklü sürüm için güncelleme, onarım veya downgrade kararını doğru vermedi.");
Assert(WindowsAdministratorVerificationService.IsAllowedAuditEvent("recovery.code.consume") &&
    !WindowsAdministratorVerificationService.IsAllowedAuditEvent("arbitrary.command"),
    "Windows yönetici doğrulama yardımcısı audit olaylarını allowlist ile sınırlamıyor.");
Assert(!settings.SetupCompleted, "Yeni kurulum, kullanım biçimi seçilmeden tamamlanmış görünmemeli.");
IReadOnlyDictionary<string, string> turkishProductTerms = ProductTerminology
    .GetResources(LanguagePreference.Turkish)
    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
IReadOnlyDictionary<string, string> englishProductTerms = ProductTerminology
    .GetResources(LanguagePreference.English)
    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
Assert(turkishProductTerms.Keys.Order().SequenceEqual(englishProductTerms.Keys.Order()) &&
       turkishProductTerms["InsightsModeShort"] == "Farkındalık" &&
       englishProductTerms["InsightsModeShort"] == "Insights" &&
       turkishProductTerms["FamilyModeShort"] == "Aile" &&
       englishProductTerms["FamilyModeShort"] == "Family",
    "Ortak ürün sözlüğünde Türkçe/English anahtar veya anlam eşitliği bozuldu.");
SetupPlan awarenessSetup = new()
{
    Language = SetupLanguage.English,
    Mode = UsageMode.Insights,
    DeviceName = "Test PC",
    DailyLimitMinutes = 120,
    StartWithWindows = true,
    AwarenessTracking = false
};
ControlSettings awarenessSetupSettings = awarenessSetup.ComposeSettings(null);
Assert(awarenessSetupSettings.SetupCompleted &&
       awarenessSetupSettings.Mode == UsageMode.Insights &&
       awarenessSetupSettings.Language == LanguagePreference.English &&
       awarenessSetupSettings.AwarenessTrackingEnabled &&
       awarenessSetupSettings.RecoveryCodes.Count == 0 &&
       awarenessSetupSettings.Schedule.All(day => day.DailyLimitMinutes == 120) &&
       awarenessSetup.LaunchArguments == string.Empty,
    "Kurucu Farkındalık ayarlarını doğru oluşturmadı.");
SetupPlan scheduledSetup = new() { Mode = UsageMode.Family, AdminPin = "2468", HasCustomSchedule = true };
SetupScheduleDayRow scheduledMonday = scheduledSetup.Schedule.Single(day => day.Day == DayOfWeek.Monday);
scheduledMonday.AllowedFromText = "08:30";
scheduledMonday.AllowedUntilText = "20:15";
scheduledMonday.DailyLimitText = "95";
SetupScheduleDayRow scheduledSunday = scheduledSetup.Schedule.Single(day => day.Day == DayOfWeek.Sunday);
scheduledSunday.IsEnabled = false;
ControlSettings scheduledSetupSettings = scheduledSetup.ComposeSettings(null);
DaySchedule composedMonday = scheduledSetupSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday);
Assert(composedMonday.AllowedFrom == new TimeOnly(8, 30) &&
       composedMonday.AllowedUntil == new TimeOnly(20, 15) &&
       composedMonday.DailyLimitMinutes == 95 &&
       !scheduledSetupSettings.Schedule.Single(day => day.Day == DayOfWeek.Sunday).IsEnabled &&
       scheduledSetupSettings.DefaultDailyLimitMinutes == 95,
    "Kurucu ayrıntılı haftalık planı ayarlara doğru aktarmadı.");
SetupPlan flexibleSetup = new()
{
    Mode = UsageMode.Personal,
    PersonalLevel = PersonalProtectionLevel.Flexible
};
Assert(!flexibleSetup.UsesScheduledPlan && scheduledSetup.UsesScheduledPlan,
    "Kurulum plan adımı, plan kullanmayan Esnek moddan ayrıştırılamadı.");
SetupPlan understandUsageTemplate = new();
understandUsageTemplate.ApplyTemplate(SetupTemplate.UnderstandUsage);
SetupPlan focusTemplate = new();
focusTemplate.ApplyTemplate(SetupTemplate.Focus);
SetupPlan gamingTemplate = new();
gamingTemplate.ApplyTemplate(SetupTemplate.GamingRoutine);
SetupPlan eveningTemplate = new();
eveningTemplate.ApplyTemplate(SetupTemplate.EveningWindDown);
SetupPlan familyTemplate = new();
familyTemplate.ApplyTemplate(SetupTemplate.FamilyRoutine);
SetupScheduleDayRow gamingMonday = gamingTemplate.Schedule.Single(day => day.Day == DayOfWeek.Monday);
SetupScheduleDayRow gamingSaturday = gamingTemplate.Schedule.Single(day => day.Day == DayOfWeek.Saturday);
SetupScheduleDayRow eveningSunday = eveningTemplate.Schedule.Single(day => day.Day == DayOfWeek.Sunday);
SetupScheduleDayRow familyMonday = familyTemplate.Schedule.Single(day => day.Day == DayOfWeek.Monday);
Assert(understandUsageTemplate.Mode == UsageMode.Insights && !understandUsageTemplate.UsesScheduledPlan &&
       focusTemplate.Mode == UsageMode.Personal && focusTemplate.PersonalLevel == PersonalProtectionLevel.Flexible &&
       gamingTemplate.SelectedTemplate == SetupTemplate.GamingRoutine && gamingTemplate.UsesScheduledPlan &&
       gamingMonday.AllowedFromText == "17:00" && gamingMonday.AllowedUntilText == "22:00" && gamingMonday.DailyLimitText == "120" &&
       gamingSaturday.AllowedFromText == "10:00" && gamingSaturday.AllowedUntilText == "23:00" && gamingSaturday.DailyLimitText == "180" &&
       eveningSunday.AllowedUntilText == "21:30" && eveningSunday.DailyLimitText == "180" &&
       familyTemplate.Mode == UsageMode.Family && familyTemplate.RequiresUserPin && familyTemplate.RequiresGuardian &&
       familyMonday.AllowedUntilText == "20:00" && familyMonday.DailyLimitText == "120",
    "Kurulum niyet şablonları kullanım biçimi, koruma düzeyi veya haftalık planı doğru hazırlamadı.");
gamingMonday.DailyLimitText = "90";
Assert(gamingTemplate.ComposeSettings(null).Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 90,
    "Kurulum şablonuyla başlayan haftalık plan kullanıcı tarafından düzenlenemedi.");
SetupPlan guardedSetup = new()
{
    Mode = UsageMode.Personal,
    PersonalLevel = PersonalProtectionLevel.Protected
};
ControlSettings guardedSetupSettings = guardedSetup.ComposeSettings(null);
Assert(guardedSetupSettings.RequiresGuardian && guardedSetupSettings.AdminPin.IsConfigured &&
       guardedSetup.LaunchArguments == string.Empty,
    "Kurucu Korumalı kişisel mod kimliğini veya başlangıç yüzeyini hazırlamadı.");
SetupPlan protectedSetup = new() { Mode = UsageMode.Family, AdminPin = "2468" };
IReadOnlyList<string> setupRecoveryCodes = protectedSetup.EnsureRecoveryCodes();
Assert(setupRecoveryCodes.SequenceEqual(protectedSetup.EnsureRecoveryCodes()),
    "Kurucu geri dönüşte kurtarma kodlarını sessizce yeniledi.");
ControlSettings protectedSetupSettings = protectedSetup.ComposeSettings(null);
Assert(protectedSetupSettings.AdminPin.IsConfigured &&
       AdminPinService.Verify("2468", protectedSetupSettings.AdminPin) &&
       setupRecoveryCodes.Count == 8 &&
       protectedSetupSettings.RecoveryCodes.Count == 8 &&
       setupRecoveryCodes.All(code => !JsonSerializer.Serialize(protectedSetupSettings).Contains(code, StringComparison.Ordinal)) &&
       RecoveryCodeService.TryConsume(protectedSetupSettings, setupRecoveryCodes[0]) &&
       !RecoveryCodeService.TryConsume(protectedSetupSettings, setupRecoveryCodes[0]) &&
       protectedSetup.LaunchArguments == string.Empty,
    "Kurucu Korumalı mod PIN'ini, tek kullanımlık kurtarmayı veya başlangıç yüzeyini hazırlamadı.");
string protectedCredentialHash = protectedSetupSettings.AdminPin.HashBase64;
string protectedCredentialSalt = protectedSetupSettings.AdminPin.SaltBase64;
ControlSettings publicProtectedPolicy = ProtectionPolicyChannel.CreatePublicPolicy(protectedSetupSettings);
Assert(publicProtectedPolicy.AdminPin.IsPublicMarker &&
       publicProtectedPolicy.AdminPin.IsConfigured &&
       !AdminPinService.Verify("2468", publicProtectedPolicy.AdminPin) &&
       publicProtectedPolicy.AdminPin.HashBase64 != protectedCredentialHash &&
       publicProtectedPolicy.AdminPin.SaltBase64 != protectedCredentialSalt &&
       publicProtectedPolicy.RecoveryCodes.Count == 8 &&
       setupRecoveryCodes.All(code => !JsonSerializer.Serialize(publicProtectedPolicy).Contains(code, StringComparison.Ordinal)) &&
       !protectedSetup.PairManagerDeviceAfterInstall,
    "Kullanıcı politikası PIN doğrulayıcısını sızdırıyor veya kurtarma/onboarding verisini kaybediyor.");
ControlSettings storedProtectedPolicy = JsonSerializer.Deserialize<ControlSettings>(
    ProtectionPolicyChannel.CreateProtectedPolicyBytes(protectedSetupSettings),
    new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })
    ?? throw new InvalidOperationException("Guardian politikası ayrıştırılamadı.");
Assert(storedProtectedPolicy.AdminPin.IsPublicMarker &&
       storedProtectedPolicy.RecoveryCodes.Count == protectedSetupSettings.RecoveryCodes.Count &&
       RecoveryCodeService.TryConsume(storedProtectedPolicy, setupRecoveryCodes[1]),
    "Guardian'a yazılan politika geçerli kurtarma kayıtlarını kaybetti.");
JsonSerializerOptions settingsJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
string renamedSettingsJson = JsonSerializer.Serialize(new ControlSettings
{
    Mode = UsageMode.Family,
    PersonalProtectionLevel = PersonalProtectionLevel.Protected
}, settingsJsonOptions);
ControlSettings legacyNamedSettings = JsonSerializer.Deserialize<ControlSettings>(
    """{"Mode":"Awareness","PersonalProtectionLevel":"Guarded"}""",
    settingsJsonOptions) ?? throw new InvalidOperationException("Eski ayar adları ayrıştırılamadı.");
Assert(renamedSettingsJson.Contains("\"Mode\":\"Protected\"", StringComparison.Ordinal) &&
       renamedSettingsJson.Contains("\"PersonalProtectionLevel\":\"Guarded\"", StringComparison.Ordinal) &&
       legacyNamedSettings.Mode == UsageMode.Insights &&
       legacyNamedSettings.PersonalProtectionLevel == PersonalProtectionLevel.Protected,
    "Yeni ürün adları eski ayar dosyalarının JSON sözleşmesini bozdu.");
Assert(ProtectionPolicyChannel.CanAttemptDuringPinCooldown("recovery-pin-reset") &&
       ProtectionPolicyChannel.CanAttemptDuringPinCooldown("manager-device-pin-reset") &&
       !ProtectionPolicyChannel.CanAttemptDuringPinCooldown("verify-pin") &&
       !ProtectionPolicyChannel.CanAttemptDuringPinCooldown("sync"),
    "PIN bekleme politikası kurtarma yolunu da kilitliyor.");
GuardianInstallRequest repairRequest = new("S-1-5-21-test", "C:\\Users\\test\\settings.json");
GuardianEnrollment existingGuardianEnrollment = new(
    repairRequest.UserSid,
    repairRequest.SettingsPath,
    protectedSetupSettings.AdminPin);
GuardianEnrollment repairedGuardianEnrollment = ProtectionServiceManager.ResolveEnrollmentForProvisioning(
    repairRequest,
    publicProtectedPolicy.AdminPin,
    existingGuardianEnrollment);
Assert(ReferenceEquals(repairedGuardianEnrollment.AdminPin, protectedSetupSettings.AdminPin) &&
       AdminPinService.Verify("2468", repairedGuardianEnrollment.AdminPin) &&
       !repairedGuardianEnrollment.AdminPin.IsPublicMarker,
    "KeepExisting repair gerçek Guardian PIN'ini public marker ile değiştirdi.");
bool missingRepairCredentialRejected = false;
try
{
    _ = ProtectionServiceManager.ResolveEnrollmentForProvisioning(
        repairRequest,
        publicProtectedPolicy.AdminPin,
        existingEnrollment: null);
}
catch (InvalidOperationException)
{
    missingRepairCredentialRejected = true;
}
Assert(missingRepairCredentialRejected,
    "Gerçek enrollment credential'ı olmadan public marker provisioning kabul edildi.");
ControlSettings existingSetupSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible,
    DeviceName = "Korunan ad"
};
IReadOnlyList<string> existingRecoveryCodes = RecoveryCodeService.Generate(existingSetupSettings, 1);
SetupPlan keepSetup = new()
{
    ExistingChoice = SetupChoice.KeepExisting,
    Language = SetupLanguage.English,
    Mode = UsageMode.Family,
    DeviceName = "Değişmemeli"
};
ControlSettings keptSettings = keepSetup.ComposeSettings(existingSetupSettings);
Assert(ReferenceEquals(keptSettings, existingSetupSettings) &&
       keptSettings.Mode == UsageMode.Personal &&
       keptSettings.DeviceName == "Korunan ad" &&
       keptSettings.Language == LanguagePreference.English &&
       keptSettings.RecoveryCodes.Count == 1 &&
       RecoveryCodeService.TryConsume(keptSettings, existingRecoveryCodes[0]) &&
       keepSetup.LaunchArguments == string.Empty,
    "Kurucu mevcut ayarları koruma seçiminde politika alanlarını değiştirdi.");
Assert(new ProtectionHealthReport(ProtectionServiceState.Running, []).IsHealthy,
    "Eksiksiz çalışan Guardian sağlık raporu sağlıklı sayılmadı.");
Assert(!new ProtectionHealthReport(
        ProtectionServiceState.Running,
        [ProtectionHealthIssue.GuardianSessionMissing]).IsHealthy &&
    !new ProtectionHealthReport(ProtectionServiceState.Stopped, []).IsHealthy,
    "Eksik veya durmuş Guardian sağlık raporu yanlışlıkla sağlıklı sayıldı.");
Assert(ProtectionServiceManager.IsCommunityClientIdentityValid(
        "community",
        "AA BB",
        "aabb",
        new Version(1, 0, 0),
        new Version(1, 0, 0, 0)) &&
    !ProtectionServiceManager.IsCommunityClientIdentityValid(
        "community",
        "aabb",
        "aabc",
        new Version(1, 0, 0),
        new Version(1, 0, 0)) &&
    !ProtectionServiceManager.IsCommunityClientIdentityValid(
        "test",
        "aabb",
        "aabb",
        new Version(1, 0, 0),
        new Version(1, 0, 0)),
    "Community Guardian istemci kimliği hash ve paket türüyle doğrulanmadı.");
Version? actualAppBinaryVersion = ProtectionServiceManager.ReadProductVersionForIdentity(
    typeof(ProtectionServiceManager).Assembly.Location);
Assert(actualAppBinaryVersion == new Version(1, 0, 0),
    "Alpha release etiketi sayısal EXE/DLL dosya sürümünün okunmasını engelledi.");
Assert(SessionSurfaceRecoveryPolicy.ShouldRecover(
        shouldShowSessionSurfaces: true,
        isSurfaceVisible: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: false,
        isModalDialogOpen: false,
        isTransitionInProgress: false),
    "Zorunlu tam ekran oturum yüzeyi kaçış sonrasında geri getirilmiyor.");
SystemInterruptionState lifecycleState = new();
SystemInterruptionDecision suspended = SystemInterruptionPolicy.Evaluate(
    lifecycleState, SystemInterruptionKind.PowerSuspend, sessionIsActive: true);
SystemInterruptionDecision resumed = SystemInterruptionPolicy.Evaluate(
    suspended.State, SystemInterruptionKind.PowerResume, sessionIsActive: false);
Assert(suspended.ShouldPause && suspended.State.ResumeAfterPower &&
       resumed.ShouldResume && !resumed.State.PowerSuspended && !resumed.State.ResumeAfterPower,
    "Uyku öncesi aktif oturum güvenli duraklatılıp uyanınca devam ettirilmedi.");
SystemInterruptionDecision lockedDuringSleep = SystemInterruptionPolicy.Evaluate(
    suspended.State, SystemInterruptionKind.SessionLock, sessionIsActive: false);
SystemInterruptionDecision powerReturnedLocked = SystemInterruptionPolicy.Evaluate(
    lockedDuringSleep.State, SystemInterruptionKind.PowerResume, sessionIsActive: false);
SystemInterruptionDecision unlocked = SystemInterruptionPolicy.Evaluate(
    powerReturnedLocked.State, SystemInterruptionKind.SessionUnlock, sessionIsActive: false);
Assert(!powerReturnedLocked.ShouldResume && !unlocked.ShouldResume && unlocked.ShouldRefreshSurfaces &&
       !unlocked.State.ResumeAfterPower,
    "Win+L ile kilitlenen oturum kullanıcı onayı olmadan otomatik devam etti.");
Assert(!SessionSurfaceRecoveryPolicy.ShouldRecover(
        shouldShowSessionSurfaces: true,
        isSurfaceVisible: true,
        isFullSurfaceRequired: false,
        isControlCenterOpen: false,
        isModalDialogOpen: false,
        isTransitionInProgress: false),
    "Aktif oturum widget'ı yanlışlıkla tam ekran yüzeye zorlanıyor.");
Assert(!SessionSurfaceRecoveryPolicy.ShouldRecover(
        shouldShowSessionSurfaces: true,
        isSurfaceVisible: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: true,
        isModalDialogOpen: false,
        isTransitionInProgress: false) &&
    !SessionSurfaceRecoveryPolicy.ShouldRecover(
        shouldShowSessionSurfaces: true,
        isSurfaceVisible: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: false,
        isModalDialogOpen: true,
        isTransitionInProgress: false),
    "Kontrol Merkezi veya modal doğrulama penceresi oturum yüzeyi tarafından örtülüyor.");
Assert(!SessionSurfaceRecoveryPolicy.ShouldRecover(
        shouldShowSessionSurfaces: false,
        isSurfaceVisible: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: false,
        isModalDialogOpen: false,
        isTransitionInProgress: false),
    "Farkındalık modu yanlışlıkla tam ekran yüzey korumasını etkinleştiriyor.");
FocusSessionGoal focusGoal = new(25);
HashSet<int> shownWarnings = [];
Assert(SessionWarningPolicy.GetDueWarningMinutes(14 * 60, [15, 5, 1], shownWarnings) == 15,
    "Nazik süre uyarısı 15 dakika eşiğinde gösterilmedi.");
shownWarnings.Add(15);
Assert(SessionWarningPolicy.GetDueWarningMinutes(4 * 60, [15, 5, 1], shownWarnings) == 5,
    "Nazik süre uyarısı atlanan eşikler arasında en yakın eşiği seçmedi.");
shownWarnings.Add(5);
Assert(SessionWarningPolicy.GetDueWarningMinutes(50, [15, 5, 1], shownWarnings) == 1 &&
       SessionWarningPolicy.GetDueWarningMinutes(0, [15, 5, 1], shownWarnings) is null,
    "Nazik süre uyarısı son dakika veya süre bitişini yanlış değerlendirdi.");
ControlCenterRequestEventArgs planRequest = new(openPlan: true);
Assert(planRequest.OpenPlan, "Süre uyarısından Plan ekranına yönlendirme isteği taşınmadı.");
focusGoal.Start();
Assert(focusGoal.RemainingSeconds() == 1_500 &&
       focusGoal.Accrue(750) == 750 && focusGoal.RemainingSeconds() == 750 &&
       Math.Abs(focusGoal.ProgressPercent() - 50) < 0.001 &&
       !focusGoal.CompleteIfReached() && focusGoal.Accrue(750) == 750 &&
       focusGoal.CompleteIfReached() &&
       focusGoal.IsCompleted,
    "Hızlı odak hedefi yalnız kendisine eklenen aktif süreyle deterministik ilerlemiyor.");
FocusSessionGoal restoredFocusGoal = FocusSessionGoal.Restore(1_500, 600);
Assert(restoredFocusGoal.DurationSeconds == 1_500 && restoredFocusGoal.ElapsedSeconds == 600 &&
       restoredFocusGoal.RemainingSeconds() == 900 && !restoredFocusGoal.IsCompleted,
    "Yarım kalan odak hedefi günlük sayaçtan bağımsız ilerlemesiyle geri yüklenemedi.");
ControlSettings midnightFocusSettings = new()
{
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
};
DateTimeOffset beforeFocusMidnight = new(2026, 8, 24, 23, 59, 59, TimeSpan.FromHours(3));
UsageLedger midnightFocusLedger = new() { LocalDay = new DateOnly(2026, 8, 24), UsedSeconds = 100 };
SessionEngine midnightFocusEngine = new(midnightFocusSettings, midnightFocusLedger, beforeFocusMidnight);
Assert(midnightFocusEngine.StartOrResume(beforeFocusMidnight), "Gece yarısı odak testi başlatılamadı.");
long midnightFocusAccrued = midnightFocusEngine.Accrue(TimeSpan.FromSeconds(2), beforeFocusMidnight.AddSeconds(2));
Assert(midnightFocusAccrued == 2 && midnightFocusEngine.Ledger.LocalDay == new DateOnly(2026, 8, 25) &&
       midnightFocusEngine.Ledger.UsedSeconds == 1 &&
       midnightFocusEngine.Ledger.History.Single(item => item.LocalDay == new DateOnly(2026, 8, 24)).UsedSeconds == 101,
    "Gece yarısını geçen aktif süre önceki ve yeni güne doğru bölünmedi.");
Assert(!SessionSurfaceRecoveryPolicy.ShouldResumeAfterControlCenterDismissal(isFamilyMode: true) &&
       SessionSurfaceRecoveryPolicy.ShouldResumeAfterControlCenterDismissal(isFamilyMode: false),
    "Korumalı Kontrol Merkezi kapatılınca oturum yüzeyi kendiliğinden geri geliyor.");
Assert(SessionSurfaceRecoveryPolicy.ShouldCoverAllDisplays(
        shouldShowSessionSurfaces: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: false),
    "Zorunlu oturum yüzeyi bütün bağlı ekranları kapsamıyor.");
Assert(!SessionSurfaceRecoveryPolicy.ShouldCoverAllDisplays(
        shouldShowSessionSurfaces: true,
        isFullSurfaceRequired: false,
        isControlCenterOpen: false) &&
    !SessionSurfaceRecoveryPolicy.ShouldCoverAllDisplays(
        shouldShowSessionSurfaces: false,
        isFullSurfaceRequired: true,
        isControlCenterOpen: false) &&
    !SessionSurfaceRecoveryPolicy.ShouldCoverAllDisplays(
        shouldShowSessionSurfaces: true,
        isFullSurfaceRequired: true,
        isControlCenterOpen: true),
    "Kontrol Merkezi, aktif widget veya Farkındalık modu ikincil ekranları yanlışlıkla kapatıyor.");
Assert(SessionShortcutGuard.ShouldBlockShortcut(
        SessionShortcutGuard.VirtualKeyLeftWindows,
        controlPressed: false,
        altPressed: false,
        shiftPressed: false) &&
    SessionShortcutGuard.ShouldBlockShortcut(
        SessionShortcutGuard.VirtualKeyRightWindows,
        controlPressed: false,
        altPressed: false,
        shiftPressed: false) &&
    SessionShortcutGuard.ShouldBlockShortcut(
        SessionShortcutGuard.VirtualKeyEscape,
        controlPressed: true,
        altPressed: false,
        shiftPressed: false) &&
    SessionShortcutGuard.ShouldBlockShortcut(
        SessionShortcutGuard.VirtualKeyTab,
        controlPressed: false,
        altPressed: true,
        shiftPressed: false),
    "Zorunlu oturum yüzeyinde shell kaçış kısayolları engellenmiyor.");
Assert(!SessionShortcutGuard.ShouldBlockShortcut(
        virtualKey: 0x4C,
        controlPressed: true,
        altPressed: false,
        shiftPressed: false) &&
    !SessionShortcutGuard.ShouldBlockShortcut(
        SessionShortcutGuard.VirtualKeyEscape,
        controlPressed: false,
        altPressed: false,
        shiftPressed: false),
    "Normal klavye girdileri zorunlu yüzey kısayol filtresinde yanlışlıkla engelleniyor.");
Assert(!AdminPinService.IsValidFormat("12ab"), "Harf içeren PIN kabul edilmemeliydi.");
AdminCredential credential = AdminPinService.Create("4826");
Assert(AdminPinService.Verify("4826", credential), "Doğru yönetici PIN'i doğrulanamadı.");
Assert(!AdminPinService.Verify("4827", credential), "Yanlış yönetici PIN'i kabul edildi.");
settings.AdminPin = credential;
ControlSettings recoveryCodeSettings = new();
IReadOnlyList<string> recoveryCodes = RecoveryCodeService.Generate(recoveryCodeSettings, 2);
Assert(recoveryCodes.Count == 2 && recoveryCodeSettings.RecoveryCodes.Count == 2 &&
    recoveryCodes.All(code => !JsonSerializer.Serialize(recoveryCodeSettings).Contains(code, StringComparison.Ordinal)),
    "Recovery kodları tek yönlü saklanmadı.");
string incorrectRecoveryCode = recoveryCodes[0][..7] + "AAAAAA-AAAAAA-AAAAAA";
Assert(!RecoveryCodeService.TryConsume(recoveryCodeSettings, incorrectRecoveryCode), "Yanlış recovery kodu kabul edildi.");
Assert(RecoveryCodeService.TryConsume(recoveryCodeSettings, recoveryCodes[0]), "Geçerli recovery kodu kabul edilmedi.");
Assert(!RecoveryCodeService.TryConsume(recoveryCodeSettings, recoveryCodes[0]), "Recovery kodu ikinci kez kullanılabildi.");
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
Assert(blocked.Reason.StartsWith("İzin verilen saatler", StringComparison.Ordinal), "Plan durumu ayarlardaki Türkçe tercihine uymadı.");
settings.Language = LanguagePreference.English;
Assert(ScheduleEvaluator.Evaluate(settings, blockedTime).Reason.StartsWith("Allowed hours", StringComparison.Ordinal),
    "Plan durumu ayarlardaki İngilizce tercihine uymadı.");
Assert(new SessionEngine(settings, new UsageLedger { LocalDay = new DateOnly(2026, 8, 24) }, blockedTime)
        .GetSnapshot(blockedTime).Reason.StartsWith("Allowed hours", StringComparison.Ordinal),
    "Oturum durumu ayarlardaki dil tercihine uymadı.");
settings.Language = LanguagePreference.Turkish;

ControlSettings flexibleSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
};
DaySchedule flexibleMonday = flexibleSettings.Schedule.Single(item => item.Day == DayOfWeek.Monday);
flexibleMonday.IsEnabled = false;
flexibleMonday.DailyLimitMinutes = 1;
ScheduleStatus flexibleSchedule = ScheduleEvaluator.Evaluate(flexibleSettings, blockedTime);
Assert(flexibleSchedule.IsAllowed && flexibleSchedule.DailyLimitMinutes == 1440 && flexibleSchedule.AllowedUntil is null,
    "Esnek kişisel mod haftalık planı veya günlük limiti uygulamaya devam etti.");
UsageLedger flexibleLedger = new() { LocalDay = new DateOnly(2026, 8, 24) };
SessionEngine flexibleEngine = new(flexibleSettings, flexibleLedger, blockedTime);
Assert(flexibleEngine.StartOrResume(blockedTime), "Esnek kişisel oturum plan dışında manuel başlatılamadı.");
flexibleEngine.Accrue(TimeSpan.FromMinutes(2), blockedTime.AddMinutes(2));
Assert(flexibleEngine.Pause(blockedTime.AddMinutes(2)) && flexibleEngine.GetSnapshot(blockedTime.AddMinutes(2)).State == SessionState.Paused,
    "Esnek kişisel oturum kullanıcı tarafından duraklatılamadı.");
flexibleEngine.EndSession(blockedTime.AddMinutes(3));
Assert(flexibleEngine.GetSnapshot(blockedTime.AddMinutes(3)).State == SessionState.Ready,
    "Esnek kişisel oturum kullanıcı tarafından bitirilemedi.");
Assert(!SessionViewModel.ShouldEnforceApplicationRules(flexibleSettings, SessionState.Ready) &&
       !SessionViewModel.ShouldEnforceApplicationRules(flexibleSettings, SessionState.Paused) &&
       SessionViewModel.ShouldEnforceApplicationRules(flexibleSettings, SessionState.Active),
"Esnek kişisel uygulama kuralları manuel oturum durumuna bağlanmadı.");

Assert(ProtectionServiceManager.EvaluateVersionCompatibility(
        new Version(0, 17, 0, 0), new Version(0, 17, 0), new Version(0, 17, 0)) ==
    ProtectionVersionCompatibility.Compatible,
    "Üç ve dört parçalı eşleşen uygulama, Guardian ve installer sürümleri uyumlu sayılmadı.");
Assert(ProtectionServiceManager.EvaluateVersionCompatibility(
        new Version(0, 17, 1), new Version(0, 17, 0), new Version(0, 17, 0)) ==
    ProtectionVersionCompatibility.Mismatch,
    "Uygulama ve Guardian sürüm uyumsuzluğu algılanmadı.");
Assert(ProtectionServiceManager.EvaluateVersionCompatibility(
        new Version(0, 17, 0), null, new Version(0, 17, 0)) ==
    ProtectionVersionCompatibility.Unknown,
    "Okunamayan Guardian sürümü güvenli olmayan biçimde uyumlu sayıldı.");
ControlSettings uninstallPolicy = new()
{
    Mode = UsageMode.Family,
    AdminPin = AdminPinService.Create("2468")
};
Assert(ProtectionServiceManager.RequiresPinForUninstall(uninstallPolicy),
    "Korumalı mod kaldırma akışı PIN istemiyor.");
uninstallPolicy.Mode = UsageMode.Personal;
uninstallPolicy.PersonalProtectionLevel = PersonalProtectionLevel.Protected;
Assert(!ProtectionServiceManager.RequiresPinForUninstall(uninstallPolicy),
    "Sıkı kişisel mod kendi belirlediği PIN'i çıkış anahtarına dönüştürdü.");
string expectedLocalKvietaPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Kvieta");
string currentUserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
Assert(UninstallDataCleaner.IsSafeLocalDataDirectory(expectedLocalKvietaPath) &&
       UninstallDataCleaner.IsSafeLocalDataDirectory(expectedLocalKvietaPath, currentUserSid) &&
       !UninstallDataCleaner.IsSafeLocalDataDirectory(expectedLocalKvietaPath, "S-1-invalid") &&
       !UninstallDataCleaner.IsSafeLocalDataDirectory(Path.GetTempPath()) &&
       UninstallDataCleaner.IsSafeProtectionDataDirectory(ProtectionServiceManager.ProtectionDataDirectory) &&
       !UninstallDataCleaner.IsSafeProtectionDataDirectory(expectedLocalKvietaPath),
    "Kaldırma veri temizliği güvenli Kvieta dizin sınırlarını doğrulamadı.");
Assert(ProtectionServiceManager.RunProductUninstall("not-a-product-code") == 87,
    "Kaldırma çalışanı geçersiz ürün kodunu Windows Installer'a gönderdi.");
ProtectionHealthReport missingProtection = new(
    ProtectionServiceState.NotInstalled,
    [ProtectionHealthIssue.ServiceNotInstalled]);
Assert(ProtectionServiceManager.RequiresProductRepair(missingProtection, installerManaged: true),
    "Installer yönetimli eksik Guardian ürün onarımına yönlendirilmedi.");
Assert(!ProtectionServiceManager.RequiresProductRepair(missingProtection, installerManaged: false),
    "Installer dışı Guardian gereksiz MSI onarımına yönlendirildi.");
ProtectionHealthReport stoppedProtection = new(
    ProtectionServiceState.Stopped,
    [ProtectionHealthIssue.ServiceStopped]);
Assert(!ProtectionServiceManager.RequiresProductRepair(stoppedProtection, installerManaged: true),
    "Yalnız durmuş Guardian gereksiz MSI onarımına yönlendirildi.");
Assert(ManagerDeviceWindow.GetFriendlyDeviceName("Linux armv81", english: false) == "Android telefon" &&
       ManagerDeviceWindow.GetFriendlyDeviceName("Linux aarch64", english: true) == "Android phone",
    "Telefon platform bilgisi kullanıcı dostu cihaz adına dönüştürülmedi.");

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
Assert(temporaryAllowed.IsTemporaryAllowanceActive, "Geçici izin ritim adilliği için işaretlenmedi.");
Assert(!ScheduleEvaluator.Evaluate(allowanceSettings, new DateTimeOffset(2026, 8, 24, 20, 30, 0, TimeSpan.FromHours(3))).IsAllowed, "Geçici izin saatinden sonra kullanım açık kaldı.");
allowanceMonday.IsEnabled = true;
allowanceMonday.AllowedFrom = new TimeOnly(8, 0);
allowanceMonday.AllowedUntil = new TimeOnly(17, 0);
allowanceMonday.DailyLimitMinutes = 120;
ScheduleStatus beforeTemporaryAllowance = ScheduleEvaluator.Evaluate(
    allowanceSettings,
    new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(3)));
Assert(beforeTemporaryAllowance.IsAllowed && beforeTemporaryAllowance.DailyLimitMinutes == 120,
    "Geçici izin bonusu tanımlanan saat aralığından önce günlük limite eklendi.");
allowanceMonday.IsEnabled = false;
SessionEngine allowanceEngine = new(allowanceSettings, new UsageLedger { LocalDay = new DateOnly(2026, 8, 24) }, new DateTimeOffset(2026, 8, 24, 19, 0, 0, TimeSpan.FromHours(3)));
Assert(allowanceEngine.GetSnapshot(new DateTimeOffset(2026, 8, 24, 19, 0, 0, TimeSpan.FromHours(3))).LimitSeconds == 75 * 60, "Geçici izin oturum limitine eklenmedi.");
Assert(!allowanceEngine.Ledger.RhythmExcused && allowanceEngine.Ledger.RhythmApprovedMinutes == 75,
    "Geçici izin bütün günü muaf tutmak yerine onaylanan süre olarak kaydedilmedi.");
DailyUsageRecord allowanceRhythmDay = new()
{
    LocalDay = new DateOnly(2026, 8, 24),
    UsedSeconds = 150 * 60,
    RhythmGoal = RhythmGoalKind.KeepBalance,
    RhythmDailyLimitMinutes = 120,
    RhythmApprovedMinutes = 75,
    RhythmMeasurementAvailable = true
};
RhythmStreakAnalyzer.FinalizeDay(allowanceRhythmDay);
Assert(allowanceRhythmDay.RhythmOutcome == RhythmDayOutcome.Success && !allowanceRhythmDay.RhythmExcused,
    "Onaylı geçici izin yalnız verilen süreyi adil değerlendirmeye katmadı.");

ControlSettings overnightSettings = new();
DaySchedule overnightMonday = overnightSettings.Schedule.Single(item => item.Day == DayOfWeek.Monday);
overnightMonday.AllowedFrom = new TimeOnly(22, 0);
overnightMonday.AllowedUntil = new TimeOnly(2, 0);
overnightMonday.DailyLimitMinutes = 180;
overnightSettings.Schedule.Single(item => item.Day == DayOfWeek.Tuesday).IsEnabled = false;
DateTimeOffset overnightTuesday = new(2026, 8, 25, 1, 0, 0, TimeSpan.FromHours(3));
ScheduleStatus overnightAllowed = ScheduleEvaluator.Evaluate(overnightSettings, overnightTuesday);
Assert(overnightAllowed.IsAllowed && overnightAllowed.DailyLimitMinutes == 180,
    "Önceki gün başlayan gece planı gece yarısından sonra devam etmedi.");
Assert(overnightAllowed.AllowedUntil?.Hour == 2 && overnightAllowed.AllowedUntil?.Day == 25,
    "Gece planının bitiş zamanı ertesi güne taşınmadı.");
SessionEngine overnightEngine = new(
    overnightSettings,
    new UsageLedger { LocalDay = new DateOnly(2026, 8, 25) },
    overnightTuesday);
Assert(overnightEngine.StartOrResume(overnightTuesday) && overnightEngine.GetSnapshot(overnightTuesday).LimitSeconds == 180 * 60,
    "Gece planı oturum motorunda başlatılamadı.");

ControlSettings overnightAllowanceSettings = new();
overnightAllowanceSettings.Schedule.ForEach(day => day.IsEnabled = false);
overnightAllowanceSettings.TemporaryAllowances.Add(new TemporaryAllowance
{
    Date = new DateOnly(2026, 8, 24),
    AllowedFrom = new TimeOnly(23, 0),
    AllowedUntil = new TimeOnly(2, 0),
    BonusMinutes = 45
});
ScheduleStatus overnightAllowance = ScheduleEvaluator.Evaluate(overnightAllowanceSettings, overnightTuesday);
Assert(overnightAllowance.IsAllowed && overnightAllowance.DailyLimitMinutes == 45,
    "Gece yarısını aşan geçici izin ertesi gün devam etmedi.");

string testDirectory = Path.Combine(Path.GetTempPath(), "Kvieta-SmokeTests", Guid.NewGuid().ToString("N"));
string intentionSettingsPath = Path.Combine(testDirectory, "intention-settings.json");
string intentionUsagePath = Path.Combine(testDirectory, "intention-usage.json");
ControlSettings intentionSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
};
await new JsonSettingsStore(intentionSettingsPath).SaveAsync(intentionSettings);
SessionViewModel intentionSession = new(
    new JsonSettingsStore(intentionSettingsPath),
    new JsonUsageStore(intentionUsagePath),
    focusDurationMinutes: 25);
await intentionSession.InitializeAsync();
intentionSession.FocusIntention = "Matematik çalış — özel";
Assert(await intentionSession.StartOrResumeAsync(), "Niyetli odak mevcut hızlı başlangıç yolundan başlatılamadı.");
await intentionSession.EndSessionAsync();
Assert(intentionSession.HasFocusClosure &&
       (intentionSession.FocusClosureSummary.Contains("Yarım", StringComparison.Ordinal) ||
        intentionSession.FocusClosureSummary.Contains("Ended early", StringComparison.Ordinal)),
    "Yarım bırakılan odak tamamlanmış oturumdan ayrılmadı.");
Assert(!File.ReadAllText(intentionUsagePath).Contains("Matematik", StringComparison.Ordinal),
    "Özel odak niyeti kullanım geçmişine yazıldı.");
Assert(await intentionSession.ContinueAfterFocusAsync(),
    "Aynı süreyle devam eylemi mevcut oturum policy hesabından geçemedi.");
intentionSession.Dispose();
string resumedFocusSettingsPath = Path.Combine(testDirectory, "resumed-focus-settings.json");
string resumedFocusUsagePath = Path.Combine(testDirectory, "resumed-focus-usage.json");
JsonSettingsStore resumedFocusSettingsStore = new(resumedFocusSettingsPath);
await resumedFocusSettingsStore.SaveAsync(new ControlSettings
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
});
Guid resumedFocusId = Guid.NewGuid();
await new JsonUsageStore(resumedFocusUsagePath).ReplaceAsync(new UsageLedger
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    ActiveFocusSessionId = resumedFocusId,
    ActiveFocusTargetSeconds = 1500,
    ActiveFocusElapsedSeconds = 600,
    LastUpdatedUtc = DateTimeOffset.UtcNow
});
using (SessionViewModel resumedFocusViewModel = new(
    resumedFocusSettingsStore,
    new JsonUsageStore(resumedFocusUsagePath)))
{
    await resumedFocusViewModel.InitializeAsync();
    Assert(resumedFocusViewModel.HasCountdown && resumedFocusViewModel.RemainingSeconds == 900,
        "Yarım kalan aktif odak oturumu yeniden açılışta ilerlemesiyle yüklenmedi.");
}
JsonUsageStore completedFocusStore = new(resumedFocusUsagePath);
UsageLedger staleActiveFocus = await completedFocusStore.LoadAsync();
UsageLedger completedFocus = await completedFocusStore.LoadAsync();
completedFocus.ActiveFocusSessionId = null;
completedFocus.ActiveFocusTargetSeconds = 0;
completedFocus.ActiveFocusElapsedSeconds = 0;
completedFocus.FocusSessionCount = 1;
completedFocus.FocusCompletedSeconds = 1500;
completedFocus.LastUpdatedUtc = DateTimeOffset.UtcNow.AddSeconds(1);
await completedFocusStore.SaveAsync(completedFocus);
await completedFocusStore.SaveAsync(staleActiveFocus);
UsageLedger focusAfterStaleSave = await completedFocusStore.LoadAsync();
Assert(focusAfterStaleSave.ActiveFocusSessionId is null && focusAfterStaleSave.FocusSessionCount == 1 &&
       focusAfterStaleSave.FocusCompletedSeconds == 1500,
    "Tamamlanan odak oturumu eski eşzamanlı kayıtla dirildi veya iki kez sayıldı.");
string focusPreferencesPath = Path.Combine(testDirectory, "focus-preferences.json");
JsonFocusPreferencesStore focusPreferencesStore = new(focusPreferencesPath);
await focusPreferencesStore.SaveLastDurationAsync(50);
Assert((await focusPreferencesStore.LoadAsync()).LastDurationMinutes == 50,
    "Son odak süresi ayrı yerel tercih kaydında korunmadı.");
await focusPreferencesStore.SaveLastDurationAsync(2_000);
Assert((await focusPreferencesStore.LoadAsync()).LastDurationMinutes == 1_440,
    "Özel odak süresi güvenli aralığa sınırlandırılmadı.");
string rhythmPreferencesPath = Path.Combine(testDirectory, "rhythm-preferences.json");
JsonRhythmPreferencesStore rhythmPreferencesStore = new(rhythmPreferencesPath);
DateTimeOffset reminderNow = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
await rhythmPreferencesStore.SaveAsync(RhythmSuggestionPreference.RemindLater, reminderNow.AddDays(1));
RhythmPreferences rhythmPreferences = await rhythmPreferencesStore.LoadAsync();
Assert(!rhythmPreferences.ShouldShowSuggestion(reminderNow) &&
       rhythmPreferences.ShouldShowSuggestion(reminderNow.AddDays(2)),
    "Ritim önerisinin sonra hatırlat tercihi kalıcı veya zamanlı değil.");
await rhythmPreferencesStore.SaveAsync(RhythmSuggestionPreference.Hidden);
Assert(!(await rhythmPreferencesStore.LoadAsync()).ShouldShowSuggestion(reminderNow.AddYears(1)),
    "Ritim önerisinin gizle tercihi kalıcı değil.");
await rhythmPreferencesStore.MarkMilestoneCelebratedAsync(7);
await rhythmPreferencesStore.SaveAsync(RhythmSuggestionPreference.Visible);
RhythmPreferences celebratedPreferences = await rhythmPreferencesStore.LoadAsync();
Assert(celebratedPreferences.LastCelebratedStreakMilestone == 7 &&
       celebratedPreferences.ShouldShowSuggestion(reminderNow),
    "Tek seferlik seri kutlaması öneri tercihi değişirken kayboldu.");
System.Windows.Media.Imaging.BitmapSource rhythmCard = RhythmShareCardRenderer.Create(
    "Haftalık Ritimim", "7 gün", "14 gün", "↓ %12", "2 sa", english: false);
Assert(rhythmCard.PixelWidth == 1200 && rhythmCard.PixelHeight == 630,
    "Haftalık ritim paylaşımı gerçek görsel kart üretmedi.");
string rhythmUsagePath = Path.Combine(testDirectory, "rhythm-usage.json");
JsonUsageStore rhythmUsageStore = new(rhythmUsagePath);
UsageLedger persistedRhythmLedger = new()
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    FocusSessionCount = 2,
    FocusCompletedSeconds = 3000
};
await rhythmUsageStore.ReplaceAsync(persistedRhythmLedger);
await rhythmUsageStore.MarkSummaryReviewedAsync(persistedRhythmLedger.LocalDay);
await rhythmUsageStore.MarkRhythmExcusedAsync(persistedRhythmLedger.LocalDay);
UsageLedger loadedRhythmLedger = await rhythmUsageStore.LoadAsync();
Assert(loadedRhythmLedger.SummaryReviewed && loadedRhythmLedger.FocusSessionCount == 2 &&
       loadedRhythmLedger.FocusCompletedSeconds == 3000 && loadedRhythmLedger.RhythmExcused,
    "Ritim özeti inceleme veya tamamlanan odak verisi yerel günlükte korunmadı.");
string todayOverviewSettingsPath = Path.Combine(testDirectory, "today-overview-settings.json");
string todayOverviewUsagePath = Path.Combine(testDirectory, "today-overview-usage.json");
ControlSettings todayOverviewSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Balanced
};
DaySchedule todayOverviewSchedule = todayOverviewSettings.Schedule.Single(day => day.Day == DateTime.Today.DayOfWeek);
todayOverviewSchedule.AllowedFrom = TimeOnly.MinValue;
todayOverviewSchedule.AllowedUntil = TimeOnly.MinValue;
todayOverviewSchedule.DailyLimitMinutes = 240;
await new JsonSettingsStore(todayOverviewSettingsPath).SaveAsync(todayOverviewSettings);
await new JsonUsageStore(todayOverviewUsagePath).SaveAsync(new UsageLedger
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    UsedSeconds = 3_600,
    ForegroundAppUsedSeconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
    {
        ["C:\\Apps\\Browser.exe"] = 1_800
    },
    History =
    [
        new DailyUsageRecord
        {
            LocalDay = DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            UsedSeconds = 7_200
        }
    ]
});
MainViewModel todayOverviewViewModel = new(
    new JsonSettingsStore(todayOverviewSettingsPath),
    new JsonUsageStore(todayOverviewUsagePath));
await todayOverviewViewModel.InitializeAsync();
Assert(todayOverviewViewModel.TodayApplications.Count == 1 &&
       todayOverviewViewModel.TodayApplications[0].Name == "Browser" &&
       todayOverviewViewModel.TodayChangeText.Contains("%50", StringComparison.Ordinal) &&
       todayOverviewViewModel.NextPlanText != "—",
    "Bugün özeti en çok kullanılan uygulamayı, günlük değişimi veya plan durumunu üretmedi.");
string testPath = Path.Combine(testDirectory, "settings.json");
JsonSettingsStore store = new(testPath);
settings.DeviceName = "Test Bilgisayarı";
settings.Theme = ThemePreference.Light;
settings.Language = LanguagePreference.English;
settings.SetupCompleted = true;
settings.Mode = UsageMode.Personal;
settings.StartWithWindows = true;
settings.AwarenessTrackingEnabled = true;
settings.UsageRetentionDays = 180;
settings.WeeklyReductionGoalPercent = 10;
settings.AppRules.Add(new AppRule { Name = "Test", ExecutablePath = "C:\\Test.exe" });
await store.SaveAsync(settings);
ControlSettings loaded = await store.LoadAsync();
Assert(loaded.DeviceName == settings.DeviceName, "Ayarlar geri yüklenemedi.");
Assert(loaded.Theme == ThemePreference.Light, "Tema tercihi geri yüklenemedi.");
Assert(loaded.Language == LanguagePreference.English, "Dil tercihi geri yüklenemedi.");
Assert(loaded.SetupCompleted && loaded.Mode == UsageMode.Personal, "Kullanım biçimi geri yüklenemedi.");
Assert(loaded.StartWithWindows, "Windows başlangıç tercihi geri yüklenemedi.");
Assert(loaded.AwarenessTrackingEnabled && loaded.UsageRetentionDays == 180, "Ritim gizlilik tercihleri geri yüklenemedi.");
Assert(loaded.WeeklyReductionGoalPercent == 10, "Ritim azaltma hedefi geri yüklenemedi.");
Assert(AdminPinService.Verify("4826", loaded.AdminPin), "Yönetici PIN'i güvenli biçimde geri yüklenemedi.");
Assert(loaded.AppRules.Count == 1, "Uygulama kuralları geri yüklenemedi.");
await using (FileStream heldWriterLock = new(
                 testPath + ".lock",
                 FileMode.OpenOrCreate,
                 FileAccess.ReadWrite,
                 FileShare.None))
{
    JsonSettingsStore readOnlyStore = new(testPath, readOnly: true);
    ControlSettings readOnlyLoaded = await readOnlyStore.LoadAsync();
    Assert(readOnlyLoaded.DeviceName == settings.DeviceName,
        "Salt okunur korunan ayar yüklemesi yazar lock'una gereksiz yere bağımlı kaldı.");
    bool readOnlyWriteRejected = false;
    try
    {
        await readOnlyStore.SaveAsync(readOnlyLoaded);
    }
    catch (InvalidOperationException)
    {
        readOnlyWriteRejected = true;
    }

    Assert(readOnlyWriteRejected, "Salt okunur korunan ayar deposu yazmayı reddetmedi.");
}

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

await File.WriteAllTextAsync(recoverySettingsPath, "{\"SchemaVersion\":7,\"DeviceName\":\"İstenmeyen değişiklik\"}");
ControlSettings explicitlyRestoredSettings = await recoveryStore.RestoreBackupAsync();
Assert(explicitlyRestoredSettings.DeviceName == "Son sağlam kayıt" &&
    (await recoveryStore.LoadAsync()).DeviceName == "Son sağlam kayıt",
    "Açık son-sağlam-kopya geri yüklemesi doğrulanmış ayarı döndürmedi.");

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

string recoveryManagerPath = Path.Combine(testDirectory, "recovery-manager-settings.json");
string recoveryAuditPath = Path.Combine(testDirectory, "security-audit.jsonl");
JsonSettingsStore recoveryManagerStore = new(recoveryManagerPath);
ControlSettings managedRecoverySettings = new();
string managedRecoveryCode = RecoveryCodeService.Generate(managedRecoverySettings, 1).Single();
await recoveryManagerStore.SaveAsync(managedRecoverySettings);
RecoveryManager recoveryManager = new(recoveryManagerStore, new SecurityAuditLog(recoveryAuditPath));
Assert(await recoveryManager.TryConsumeCodeAsync(managedRecoveryCode), "Recovery manager geçerli kodu tüketemedi.");
Assert(!await recoveryManager.TryConsumeCodeAsync(managedRecoveryCode), "Recovery manager aynı kodu ikinci kez kabul etti.");
ControlSettings persistedRecoverySettings = await recoveryManagerStore.LoadAsync();
Assert(persistedRecoverySettings.RecoveryCodes.Single().UsedAtUtc is not null, "Kullanılan recovery kodu atomik olarak kaydedilmedi.");
string[] recoveryAuditLines = await File.ReadAllLinesAsync(recoveryAuditPath);
Assert(recoveryAuditLines.Length == 2 && recoveryAuditLines[0].Contains("accepted", StringComparison.Ordinal) &&
    recoveryAuditLines[1].Contains("rejected", StringComparison.Ordinal) &&
    recoveryAuditLines.All(line => !line.Contains(managedRecoveryCode, StringComparison.Ordinal)),
    "Recovery audit kaydı eksik veya hassas kod içeriyor.");
string concurrentAuditPath = Path.Combine(testDirectory, "concurrent-security-audit.jsonl");
await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
    new SecurityAuditLog(concurrentAuditPath).AppendAsync("audit.concurrent", $"entry-{index}")));
Assert((await File.ReadAllLinesAsync(concurrentAuditPath)).Length == 20,
    "Süreçler arası audit kilidi eşzamanlı güvenlik olaylarını kaybetti.");

string retentionAuditPath = Path.Combine(testDirectory, "retention-security-audit.jsonl");
JsonSerializerOptions auditJsonOptions = new(JsonSerializerDefaults.Web);
List<string> seededAuditLines =
[
    JsonSerializer.Serialize(new SecurityAuditEntry(DateTimeOffset.UtcNow.AddDays(-31), "audit.expired", "ignored"), auditJsonOptions),
    "{ malformed",
    .. Enumerable.Range(0, 520).Select(index => JsonSerializer.Serialize(
        new SecurityAuditEntry(DateTimeOffset.UtcNow.AddMinutes(-1), "audit.seeded", $"entry-{index}"),
        auditJsonOptions))
];
await File.WriteAllLinesAsync(retentionAuditPath, seededAuditLines);
SecurityAuditLog retentionAudit = new(retentionAuditPath);
await retentionAudit.AppendAsync("audit.current", "accepted");
string[] retainedAuditLines = await File.ReadAllLinesAsync(retentionAuditPath);
IReadOnlyList<SecurityAuditEntry> recentAuditEntries = await retentionAudit.ReadRecentAsync(10);
Assert(retainedAuditLines.Length == 500 &&
       retainedAuditLines.All(line => !line.Contains("expired", StringComparison.Ordinal) &&
                                      !line.Contains("malformed", StringComparison.Ordinal)) &&
       recentAuditEntries.Count == 10 && recentAuditEntries[^1].Event == "audit.current" &&
       new FileInfo(retentionAuditPath).Length <= 256 * 1024,
    "Güvenlik audit saklama süresi, olay sayısı veya dosya boyutu sınırı uygulanmadı.");
bool oversizedAuditTokenRejected = false;
try
{
    await retentionAudit.AppendAsync("audit.oversized", new string('a', 97));
}
catch (ArgumentException)
{
    oversizedAuditTokenRejected = true;
}
Assert(oversizedAuditTokenRejected, "Aşırı uzun audit alanı dosya büyüme sınırını aşabildi.");

string pinResetPath = Path.Combine(testDirectory, "recovery-pin-reset-settings.json");
string pinResetAuditPath = Path.Combine(testDirectory, "recovery-pin-reset-audit.jsonl");
JsonSettingsStore pinResetStore = new(pinResetPath);
ControlSettings pinResetSettings = new() { AdminPin = AdminPinService.Create("1111") };
string pinResetCode = RecoveryCodeService.Generate(pinResetSettings, 1).Single();
await pinResetStore.SaveAsync(pinResetSettings);
RecoveryManager pinResetManager = new(pinResetStore, new SecurityAuditLog(pinResetAuditPath));
AdminCredential replacementCredential = AdminPinService.Create("7391");
Assert(!await pinResetManager.TryResetPinAsync("WRONG-CODE", replacementCredential),
    "Yanlış recovery kodu PIN sıfırlamasında kabul edildi.");
Assert(AdminPinService.Verify("1111", (await pinResetStore.LoadAsync()).AdminPin),
    "Reddedilen recovery isteği eski PIN'i değiştirdi.");
Assert(await pinResetManager.TryResetPinAsync(pinResetCode, replacementCredential),
    "Geçerli recovery kodu PIN'i sıfırlayamadı.");
ControlSettings resetSettings = await pinResetStore.LoadAsync();
Assert(AdminPinService.Verify("7391", resetSettings.AdminPin) &&
    !AdminPinService.Verify("1111", resetSettings.AdminPin),
    "Recovery PIN sıfırlaması atomik olarak kaydedilmedi.");
Assert(!await pinResetManager.TryResetPinAsync(pinResetCode, AdminPinService.Create("8520")),
    "Kullanılmış recovery kodu PIN'i ikinci kez değiştirdi.");

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
Assert(migratedUsage.SchemaVersion == 8 && migrationUsageStore.LastLoadMigrated, "Kullanım verisi şema 8'e taşınmadı.");
Assert(migratedUsage.AwarenessHourlyUsedSeconds.Count == 0, "Eski kullanım verisine sahte saatlik dağılım eklendi.");
Assert(File.Exists(migrationUsageStore.BackupPath), "Migration sonrasında sağlam kullanım yedeği oluşturulmadı.");

UsageLedger clockLedger = new();
DateTimeOffset clockStart = new(2026, 8, 25, 10, 0, 0, TimeSpan.FromHours(2));
Assert(ClockIntegrityMonitor.Observe(clockLedger, clockStart, TimeSpan.FromHours(1), "boot-a") == ClockChangeKind.None,
    "İlk güvenilir saat gözlemi anomali sayıldı.");
Assert(ClockIntegrityMonitor.Observe(clockLedger, clockStart.AddMinutes(1), TimeSpan.FromMinutes(61), "boot-a") == ClockChangeKind.None,
    "Monotonic ilerleyen saat anomali sayıldı.");
DateTimeOffset timeZoneChange = clockStart.AddMinutes(2).ToUniversalTime().ToOffset(TimeSpan.FromHours(3));
Assert(ClockIntegrityMonitor.Observe(clockLedger, timeZoneChange, TimeSpan.FromMinutes(62), "boot-a") == ClockChangeKind.TimeZoneChanged &&
    !clockLedger.ClockAnomalyRequiresRecovery,
    "Saat dilimi değişikliği güvenli biçimde ayrıştırılamadı.");
DateTimeOffset afterReboot = timeZoneChange.AddMinutes(10);
Assert(ClockIntegrityMonitor.Observe(clockLedger, afterReboot, TimeSpan.FromSeconds(30), "boot-b") == ClockChangeKind.Reboot &&
    !clockLedger.ClockAnomalyRequiresRecovery,
    "Windows yeniden başlatması saat manipülasyonu sayıldı.");
DateTimeOffset forwardJump = afterReboot.AddHours(2);
Assert(ClockIntegrityMonitor.Observe(clockLedger, forwardJump, TimeSpan.FromMinutes(1), "boot-b") == ClockChangeKind.ForwardJump &&
    clockLedger.ClockAnomalyRequiresRecovery,
    "İleri saat sıçraması monotonic kaynağa rağmen yakalanmadı.");
ClockIntegrityMonitor.ClearAnomaly(clockLedger, afterReboot.AddMinutes(1), TimeSpan.FromMinutes(1), "boot-b");
Assert(!clockLedger.ClockAnomalyRequiresRecovery,
    "Yönetici saat kurtarma yolu anomalinin güvenli durumunu temizlemedi.");

UsageLedger caughtUpRollbackLedger = new();
DateTimeOffset caughtUpStart = new(2026, 8, 25, 14, 0, 0, TimeSpan.FromHours(3));
ClockIntegrityMonitor.Observe(caughtUpRollbackLedger, caughtUpStart, TimeSpan.FromHours(2), "boot-c");
Assert(ClockIntegrityMonitor.Observe(caughtUpRollbackLedger, caughtUpStart.AddMinutes(-10), TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(10)), "boot-c") == ClockChangeKind.Rollback &&
    caughtUpRollbackLedger.ClockAnomalyRequiresRecovery,
    "Geri alınan saat güvenli zamana ulaşmadan engellenmedi.");
Assert(ClockIntegrityMonitor.Observe(caughtUpRollbackLedger, caughtUpStart, TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(20)), "boot-c") == ClockChangeKind.None &&
    !caughtUpRollbackLedger.ClockAnomalyRequiresRecovery,
    "Saat son güvenilir ana ulaştığında geri alma kilidi otomatik temizlenmedi.");

string retentionUsagePath = Path.Combine(testDirectory, "retention-usage.json");
JsonUsageStore retentionUsageStore = new(retentionUsagePath);
DateOnly retentionToday = DateOnly.FromDateTime(DateTime.Today);
await retentionUsageStore.ReplaceAsync(new UsageLedger
{
    LocalDay = retentionToday,
    History =
    [
        new DailyUsageRecord { LocalDay = retentionToday.AddDays(-31), AwarenessUsedSeconds = 60 },
        new DailyUsageRecord { LocalDay = retentionToday.AddDays(-1), AwarenessUsedSeconds = 60 }
    ],
    RecentEvents =
    [
        new UsageEventRecord { OccurredAtUtc = DateTimeOffset.Now.AddDays(-31), Kind = UsageEventKind.BreakStarted },
        new UsageEventRecord { OccurredAtUtc = DateTimeOffset.Now.AddDays(-1), Kind = UsageEventKind.BreakStarted }
    ]
});
UsageLedger staleBeforeTrim = await retentionUsageStore.LoadAsync();
await retentionUsageStore.TrimHistoryAsync(30);
await retentionUsageStore.SaveAsync(staleBeforeTrim);
UsageLedger retainedUsage = await retentionUsageStore.LoadAsync();
Assert(retainedUsage.History.Select(day => day.LocalDay).SequenceEqual([retentionToday.AddDays(-1)]),
    "30 günlük saklama süresi eski geçmişi temizlemedi.");
Assert(retainedUsage.RecentEvents.Count == 1 && retainedUsage.RecentEvents[0].OccurredAtUtc > DateTimeOffset.Now.AddDays(-30),
    "30 günlük saklama süresi eski hareketleri temizlemedi.");
UsageLedger staleBeforeClear = retainedUsage;
UsageLedger clearedGeneration = await retentionUsageStore.ClearAsync();
await retentionUsageStore.SaveAsync(staleBeforeClear);
UsageLedger afterStaleSave = await retentionUsageStore.LoadAsync();
Assert(afterStaleSave.DataGeneration == clearedGeneration.DataGeneration && afterStaleSave.History.Count == 0 &&
       afterStaleSave.RecentEvents.Count == 0 && afterStaleSave.RhythmCheckpoint.ProcessedThroughDay is null &&
       afterStaleSave.RhythmCheckpoint.BestStreak == 0,
    "Silme sonrasında gelen eski arka plan kaydı geçmişi geri oluşturdu.");

UsageLedger awarenessLedger = new();
Assert(AwarenessUsageCounter.Accrue(awarenessLedger, @"C:\Program Files\Browser\browser.exe", TimeSpan.FromSeconds(3.8), allowedTime), "Ön plan farkındalık süresi eklenemedi.");
Assert(awarenessLedger.AwarenessUsedSeconds == 3, "Farkındalık toplamı yanlış hesaplandı.");
Assert(awarenessLedger.ForegroundAppUsedSeconds.GetValueOrDefault("browser.exe") == 3, "Farkındalık kaydında yalnız güvenli uygulama kimliği tutulmadı.");
Assert(awarenessLedger.AwarenessHourlyUsedSeconds.GetValueOrDefault(12) == 3, "Saatlik farkındalık dilimi kaydedilmedi.");
Assert(awarenessLedger.AppUsedSeconds.Count == 0 && awarenessLedger.UsedSeconds == 0, "Farkındalık sayacı kural veya oturum sayacına karıştı.");

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

UsageLedger ledger = new() { LocalDay = DateOnly.FromDateTime(allowedTime.DateTime) };
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
    LocalDay = DateOnly.FromDateTime(allowedTime.DateTime),
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
    LocalDay = DateOnly.FromDateTime(allowedTime.DateTime),
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
    LocalDay = DateOnly.FromDateTime(allowedTime.DateTime),
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
await usageStore.ReplaceAsync(ledger);
UsageLedger loadedLedger = await usageStore.LoadAsync();
Assert(loadedLedger.UsedSeconds == ledger.UsedSeconds, "Kullanım kaydı geri yüklenemedi.");

string legacyPath = Path.Combine(testDirectory, "legacy-settings.json");
await File.WriteAllTextAsync(legacyPath, "{\"SchemaVersion\":1,\"DeviceName\":\"Eski Kurulum\"}");
ControlSettings migratedSettings = await new JsonSettingsStore(legacyPath).LoadAsync();
Assert(migratedSettings.SchemaVersion == 10, "Eski ayar şeması yükseltilemedi.");
Assert(migratedSettings.Mode == UsageMode.Family &&
       migratedSettings.PersonalProtectionLevel == PersonalProtectionLevel.Balanced &&
       !migratedSettings.StrictPersonalMode,
    "Eski korumalı ayardaki ilgisiz kişisel seviye temizlenmedi.");
Assert(!migratedSettings.AwarenessTrackingEnabled && migratedSettings.UsageRetentionDays == 90, "Migration açık rıza gerektiren ölçümü kendiliğinden etkinleştirdi.");
Assert(migratedSettings.WeeklyReductionGoalPercent == 0, "Migration kullanıcı onayı olmadan azaltma hedefi oluşturdu.");

string signOutSettingsPath = Path.Combine(testDirectory, "sign-out-settings.json");
await File.WriteAllTextAsync(
    signOutSettingsPath,
    "{\"SchemaVersion\":9,\"SetupCompleted\":true,\"LimitAction\":\"SignOut\"}");
ControlSettings migratedSignOutSettings = await new JsonSettingsStore(signOutSettingsPath).LoadAsync();
Assert(migratedSignOutSettings.LimitAction == LimitReachedAction.LockWindows,
    "Eski oturum kapatma eylemi güvenli Windows kilidine taşınmadı.");

string legacyPersonalPath = Path.Combine(testDirectory, "legacy-personal-settings.json");
await File.WriteAllTextAsync(
    legacyPersonalPath,
    "{\"SchemaVersion\":8,\"SetupCompleted\":true,\"Mode\":\"Personal\",\"StrictPersonalMode\":false}");
ControlSettings migratedPersonalSettings = await new JsonSettingsStore(legacyPersonalPath).LoadAsync();
Assert(migratedPersonalSettings.PersonalProtectionLevel == PersonalProtectionLevel.Flexible &&
       !migratedPersonalSettings.StrictPersonalMode,
    "Eski kişisel ayar koruma seviyesine güvenli biçimde taşınmadı.");

DateOnly rhythmToday = new(2026, 8, 24);
ControlSettings rhythmSettings = new() { WeeklyReductionGoalPercent = 10 };
UsageLedger rhythmLedger = new()
{
    LocalDay = rhythmToday,
    AwarenessUsedSeconds = 3600,
    UsedSeconds = 1800,
    FocusSessionCount = 1,
    FocusCompletedSeconds = 1200
};
rhythmLedger.ForegroundAppUsedSeconds["editor.exe"] = 3600;
rhythmLedger.AwarenessHourlyUsedSeconds[20] = 3600;
for (int offset = 13; offset >= 1; offset--)
{
    bool previousWeek = offset >= 7;
    rhythmLedger.History.Add(new DailyUsageRecord
    {
        LocalDay = rhythmToday.AddDays(-offset),
        AwarenessUsedSeconds = previousWeek ? 7200 : 3600,
        UsedSeconds = 1800,
        AwarenessHourlyUsedSeconds = new Dictionary<int, long> { [previousWeek ? 21 : 20] = previousWeek ? 7200 : 3600 },
        ForegroundApplications =
        [
            new AwarenessAppUsageRecord
            {
                ApplicationId = previousWeek ? "browser.exe" : "editor.exe",
                Name = previousWeek ? "browser" : "editor",
                UsedSeconds = previousWeek ? 7200 : 3600
            }
        ]
    });
}
RhythmSummary rhythm = RhythmAnalyzer.Analyze(rhythmSettings, rhythmLedger, rhythmToday);
Assert(rhythm.IsBaselineReady && rhythm.BaselineDays == 14, "Başlangıç ritmi 7-14 günlük pencerede oluşmadı.");
Assert(Math.Abs((rhythm.WeekChangePercent ?? 0) - (-50)) < 0.1, "Haftalık günlük ortalama karşılaştırması yanlış.");
Assert(rhythm.PlanAlignedDays == 7, "Planla uyumlu günler yanlış hesaplandı.");
Assert(rhythm.ReclaimedSeconds == 12600, "Başlangıç ritmine göre geri kazanılan süre yanlış.");
ControlSettings reviewStreakSettings = new() { Mode = UsageMode.Insights };
UsageLedger reviewStreakLedger = new() { LocalDay = rhythmToday, SummaryReviewed = true, AwarenessUsedSeconds = 60 };
reviewStreakLedger.History =
[
    new DailyUsageRecord { LocalDay = rhythmToday.AddDays(-2), AwarenessUsedSeconds = 60, SummaryReviewed = true, RhythmGoal = RhythmGoalKind.ReviewSummary, RhythmOutcome = RhythmDayOutcome.Success, RhythmMeasurementAvailable = true },
    new DailyUsageRecord { LocalDay = rhythmToday.AddDays(-1), AwarenessUsedSeconds = 60, SummaryReviewed = true, RhythmGoal = RhythmGoalKind.ReviewSummary, RhythmOutcome = RhythmDayOutcome.Success, RhythmMeasurementAvailable = true }
];
RhythmStreakSummary reviewStreak = RhythmStreakAnalyzer.Analyze(reviewStreakSettings, reviewStreakLedger, rhythmToday);
Assert(reviewStreak.Goal == RhythmGoalKind.ReviewSummary && reviewStreak.CurrentStreak == 3 &&
       reviewStreak.BestStreak == 3 && reviewStreak.ReachedMilestone == 3,
    "Farkındalık Ritim Serisi özet inceleme hedefini veya kilometre taşını hesaplamadı.");
ControlSettings focusStreakSettings = new()
{
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
};
UsageLedger focusStreakLedger = new() { LocalDay = rhythmToday };
focusStreakLedger.History = Enumerable.Range(1, 8)
    .Select(offset => new DailyUsageRecord
    {
        LocalDay = rhythmToday.AddDays(-9 + offset),
        UsedSeconds = 60,
        FocusSessionCount = offset <= 7 ? 1 : 0,
        FocusCompletedSeconds = offset <= 7 ? 1500 : 0,
        RhythmGoal = RhythmGoalKind.CompleteFocus,
        RhythmOutcome = offset <= 7 ? RhythmDayOutcome.Success : RhythmDayOutcome.Missed,
        RhythmMeasurementAvailable = true
    })
    .ToList();
RhythmStreakSummary protectedStreak = RhythmStreakAnalyzer.Analyze(focusStreakSettings, focusStreakLedger, rhythmToday);
Assert(protectedStreak.CurrentStreak == 7 && protectedStreak.Protectors == 0 &&
       protectedStreak.TodayOutcome == RhythmDayOutcome.Pending,
    "Yedi günlük odak serisinin kazandırdığı Ritim Koruyucu kaçırılan günü korumadı.");
UsageLedger todayProtectedLedger = new()
{
    LocalDay = rhythmToday,
    UsedSeconds = 60,
    History = Enumerable.Range(1, 7)
        .Select(offset => new DailyUsageRecord
        {
            LocalDay = rhythmToday.AddDays(-8 + offset),
            UsedSeconds = 60,
            FocusSessionCount = 1,
            FocusCompletedSeconds = 1500,
            RhythmGoal = RhythmGoalKind.CompleteFocus,
            RhythmOutcome = RhythmDayOutcome.Success,
            RhythmMeasurementAvailable = true
        })
        .ToList()
};
RhythmStreakSummary todayProtected = RhythmStreakAnalyzer.Analyze(focusStreakSettings, todayProtectedLedger, rhythmToday);
Assert(todayProtected.CurrentStreak == 7 && todayProtected.Protectors == 1 &&
       todayProtected.TodayOutcome == RhythmDayOutcome.Pending,
    "Tamamlanmamış bugünkü hedef gün kapanmadan Ritim Koruyucu tüketti.");
todayProtectedLedger.RhythmExcused = true;
RhythmStreakSummary excusedStreak = RhythmStreakAnalyzer.Analyze(focusStreakSettings, todayProtectedLedger, rhythmToday);
Assert(excusedStreak.CurrentStreak == 7 && excusedStreak.Protectors == 1 &&
       excusedStreak.TodayOutcome == RhythmDayOutcome.Excused,
    "Geçici izin veya kurtarma günü seriyi bozdu ya da koruyucu tüketti.");
ControlSettings disabledFocusSchedule = new()
{
    Mode = UsageMode.Personal,
    PersonalProtectionLevel = PersonalProtectionLevel.Flexible
};
disabledFocusSchedule.Schedule.Single(item => item.Day == rhythmToday.DayOfWeek).IsEnabled = false;
RhythmStreakSummary disabledFocusToday = RhythmStreakAnalyzer.Analyze(
    disabledFocusSchedule,
    new UsageLedger { LocalDay = rhythmToday, UsedSeconds = 60 },
    rhythmToday);
Assert(disabledFocusToday.TodayOutcome == RhythmDayOutcome.Pending,
    "Uygulama erişim planının kapalı günü Esnek odak ritmine dinlenme günü olarak sızdı.");

UsageLedger snapshottedGoalLedger = new()
{
    LocalDay = rhythmToday,
    UsedSeconds = 1500,
    FocusSessionCount = 1,
    FocusCompletedSeconds = 1500
};
_ = new SessionEngine(focusStreakSettings, snapshottedGoalLedger, new DateTimeOffset(2026, 8, 24, 20, 0, 0, TimeSpan.FromHours(3)));
Assert(snapshottedGoalLedger.RhythmGoal == RhythmGoalKind.CompleteFocus,
    "Günün ritim hedefi ilk kullanımda sabitlenmedi.");
Assert(snapshottedGoalLedger.RhythmFocusTargetKind == FocusRhythmTargetKind.Minutes &&
       snapshottedGoalLedger.RhythmGoalTarget == 25,
    "Esnek kişisel modun dakika hedefi gün kaydına sabitlenmedi.");
DailyUsageRecord minuteTargetDay = new()
{
    LocalDay = rhythmToday,
    UsedSeconds = 60,
    FocusSessionCount = 1,
    FocusCompletedSeconds = 10 * 60,
    RhythmGoal = RhythmGoalKind.CompleteFocus,
    RhythmFocusTargetKind = FocusRhythmTargetKind.Minutes,
    RhythmGoalTarget = 25,
    RhythmMeasurementAvailable = true
};
RhythmStreakAnalyzer.FinalizeDay(minuteTargetDay);
Assert(minuteTargetDay.RhythmOutcome == RhythmDayOutcome.Missed,
    "Kısa bir odak oturumu 25 dakikalık hedefi yanlışlıkla tamamladı.");
DailyUsageRecord sessionTargetDay = new()
{
    LocalDay = rhythmToday,
    UsedSeconds = 60,
    FocusSessionCount = 2,
    FocusCompletedSeconds = 10 * 60,
    RhythmGoal = RhythmGoalKind.CompleteFocus,
    RhythmFocusTargetKind = FocusRhythmTargetKind.Sessions,
    RhythmGoalTarget = 2,
    RhythmMeasurementAvailable = true
};
RhythmStreakAnalyzer.FinalizeDay(sessionTargetDay);
Assert(sessionTargetDay.RhythmOutcome == RhythmDayOutcome.Success,
    "Oturum sayısı hedefi gerçek tamamlanan oturumları saymadı.");
DailyUsageRecord clippedBalanceDay = new()
{
    LocalDay = rhythmToday,
    UsedSeconds = 60 * 60,
    LimitReachedCount = 1,
    RhythmGoal = RhythmGoalKind.KeepBalance,
    RhythmDailyLimitMinutes = 60,
    RhythmMeasurementAvailable = true
};
RhythmStreakAnalyzer.FinalizeDay(clippedBalanceDay);
Assert(clippedBalanceDay.RhythmOutcome == RhythmDayOutcome.Missed,
    "Limitte kesilmiş sayaç ölçülebilir limit ihlalini yanlışlıkla başarı saydı.");
IReadOnlyList<RhythmDayResult> recentRhythmDays = RhythmStreakAnalyzer.BuildRecentDays(
    focusStreakSettings, focusStreakLedger, rhythmToday);
Assert(recentRhythmDays.Count == 7 && recentRhythmDays.Select(day => day.Day).Distinct().Count() == 7,
    "Yedi günlük ritim şeridi aynı gün kayıtlarından oluşturulmadı.");
ControlSettings changedNextDaySettings = new()
{
    Mode = UsageMode.Insights,
    AwarenessTrackingEnabled = true
};
_ = new SessionEngine(changedNextDaySettings, snapshottedGoalLedger, new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(3)));
DailyUsageRecord snapshottedDay = snapshottedGoalLedger.History.Single(item => item.LocalDay == rhythmToday);
Assert(snapshottedDay.RhythmGoal == RhythmGoalKind.CompleteFocus &&
       snapshottedDay.RhythmOutcome == RhythmDayOutcome.Success,
    "Mod değişikliği arşivlenen günün hedefini veya sonucunu geriye dönük değiştirdi.");
RhythmStreakSummary changedModeStreak = RhythmStreakAnalyzer.Analyze(changedNextDaySettings, snapshottedGoalLedger, rhythmToday.AddDays(1));
Assert(changedModeStreak.CurrentStreak == 1 && changedModeStreak.TodayOutcome == RhythmDayOutcome.Pending,
    "Geçmiş hedef sonucu yeni modla yeniden yorumlandı.");

UsageLedger unknownLegacyRhythm = new()
{
    LocalDay = rhythmToday,
    History =
    [
        new DailyUsageRecord { LocalDay = rhythmToday.AddDays(-1), UsedSeconds = 60 }
    ]
};
RhythmStreakSummary unknownLegacyResult = RhythmStreakAnalyzer.Analyze(focusStreakSettings, unknownLegacyRhythm, rhythmToday);
Assert(unknownLegacyResult.CurrentStreak == 0 && unknownLegacyResult.TodayOutcome == RhythmDayOutcome.Pending,
    "Hedefi bilinmeyen eski gün bugünkü ayarla ödül veya ceza üretti.");

string rhythmCheckpointPath = Path.Combine(testDirectory, "rhythm-checkpoint-usage.json");
DateOnly checkpointToday = DateOnly.FromDateTime(DateTime.Today);
UsageLedger checkpointLedger = new() { LocalDay = checkpointToday };
checkpointLedger.History = Enumerable.Range(1, 35)
    .Select(offset => new DailyUsageRecord
    {
        LocalDay = checkpointToday.AddDays(-36 + offset),
        RhythmGoal = RhythmGoalKind.CompleteFocus,
        RhythmOutcome = RhythmDayOutcome.Success,
        RhythmMeasurementAvailable = true,
        UsedSeconds = 1500,
        FocusSessionCount = 1,
        FocusCompletedSeconds = 1500
    })
    .ToList();
JsonUsageStore rhythmCheckpointStore = new(rhythmCheckpointPath);
await rhythmCheckpointStore.ReplaceAsync(checkpointLedger);
await rhythmCheckpointStore.TrimHistoryAsync(30);
UsageLedger trimmedRhythmLedger = await rhythmCheckpointStore.LoadAsync();
RhythmStreakSummary retainedLongStreak = RhythmStreakAnalyzer.Analyze(focusStreakSettings, trimmedRhythmLedger, checkpointToday);
Assert(trimmedRhythmLedger.History.Count == 29 &&
       trimmedRhythmLedger.RhythmCheckpoint.ProcessedThroughDay == checkpointToday.AddDays(-30) &&
       retainedLongStreak.CurrentStreak == 35 && retainedLongStreak.BestStreak == 35,
    "Ham geçmiş budanınca uzun dönem ritim özeti veya en iyi seri kayboldu.");
Assert(rhythm.IsGoalEnabled && rhythm.IsGoalMet && rhythm.GoalDailySeconds == 4860, "Kullanıcı onaylı azaltma hedefi yanlış değerlendirildi.");
Assert(rhythm.RisingApplication == "editor" && rhythm.FallingApplication == "browser", "Uygulama artış/azalış eğilimi yanlış bulundu.");
Assert(rhythm.FocusCompletedSeconds == 1200, "Haftalık tamamlanan odak süresi özete eklenmedi.");
Assert(rhythm.PeakHour == 20 && rhythm.PeakHourSeconds == 7 * 3600, "Yoğun kullanım saati yanlış hesaplandı.");
Assert(rhythm.WeekdayObservedDays > 0 && rhythm.WeekendObservedDays > 0 && rhythm.WeekendDifferencePercent is not null,
    "Hafta içi/hafta sonu ritmi yeterli veride hesaplanmadı.");
UsageLedger sessionOnlyRhythmLedger = new() { LocalDay = rhythmToday };
for (int offset = 1; offset <= 7; offset++)
{
    sessionOnlyRhythmLedger.History.Add(new DailyUsageRecord
    {
        LocalDay = rhythmToday.AddDays(-offset),
        UsedSeconds = 3600,
        AwarenessUsedSeconds = 0
    });
}
RhythmSummary sessionOnlyRhythm = RhythmAnalyzer.Analyze(rhythmSettings, sessionOnlyRhythmLedger, rhythmToday);
Assert(sessionOnlyRhythm.BaselineDays == 0 && !sessionOnlyRhythm.IsBaselineReady,
    "Kural oturumu verisi farkındalık başlangıç ritmi olarak sayıldı.");
Assert(migratedSettings.SetupCompleted, "Mevcut kullanıcıya ilk kurulum ekranı yeniden gösterilmemeli.");
Assert(migratedSettings.Mode == UsageMode.Family, "Mevcut kullanıcı Aile kullanımına taşınmalı.");

string awarenessSettingsPath = Path.Combine(testDirectory, "awareness-settings.json");
ControlSettings awarenessModeSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Insights,
    AwarenessTrackingEnabled = false
};
foreach (DaySchedule day in awarenessModeSettings.Schedule)
{
    day.IsEnabled = false;
    day.DailyLimitMinutes = 0;
}
awarenessModeSettings.AppRules.Add(new AppRule
{
    Name = "Eski engel",
    ExecutablePath = "C:\\Blocked.exe",
    Mode = AppRuleMode.Blocked
});
JsonSettingsStore awarenessSettingsStore = new(awarenessSettingsPath);
await awarenessSettingsStore.SaveAsync(awarenessModeSettings);
ControlSettings loadedAwarenessSettings = await awarenessSettingsStore.LoadAsync();
Assert(loadedAwarenessSettings.AwarenessTrackingEnabled, "Farkındalık modu yerel ölçümü zorunlu olarak açmadı.");
UsageLedger awarenessModeLedger = new()
{
    LocalDay = DateOnly.FromDateTime(blockedTime.DateTime),
    UsedSeconds = 24 * 60 * 60,
    State = SessionState.TimeExpired
};
SessionEngine awarenessModeEngine = new(loadedAwarenessSettings, awarenessModeLedger, blockedTime);
Assert(awarenessModeEngine.StartOrResume(blockedTime), "Farkındalık modu plan dışında serbestçe başlayamadı.");
awarenessModeEngine.Accrue(TimeSpan.FromMinutes(1), blockedTime.AddMinutes(1));
Assert(awarenessModeEngine.GetSnapshot(blockedTime.AddMinutes(1)).State == SessionState.Active && awarenessModeLedger.LimitReachedCount == 0,
    "Farkındalık modu günlük limit uyguladı.");
Assert(!new ApplicationRuleEnforcer().Enforce(loadedAwarenessSettings, awarenessModeLedger, TimeSpan.FromSeconds(1), SessionState.Active),
    "Farkındalık modu eski uygulama engellerini uyguladı.");
string silentAwarenessUsagePath = Path.Combine(testDirectory, "silent-awareness-usage.json");
SessionViewModel silentAwarenessViewModel = new(awarenessSettingsStore, new JsonUsageStore(silentAwarenessUsagePath));
await silentAwarenessViewModel.InitializeAsync();
Assert(!silentAwarenessViewModel.ShouldShowSessionSurfaces,
    "Farkındalık modunda sayaç veya oturum yüzeyi görünür bırakıldı.");
string awarenessUsagePath = Path.Combine(testDirectory, "awareness-mode-usage.json");
await new JsonUsageStore(awarenessUsagePath).SaveAsync(new UsageLedger
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    UsedSeconds = 3600,
    AwarenessUsedSeconds = 600,
    AppUsedSeconds = new Dictionary<Guid, long> { [awarenessModeSettings.AppRules[0].Id] = 300 },
    ForegroundAppUsedSeconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
    {
        ["=SUM(1,1).exe"] = 600
    }
});
MainViewModel awarenessViewModel = new(awarenessSettingsStore, new JsonUsageStore(awarenessUsagePath));
await awarenessViewModel.InitializeAsync();
Assert(awarenessViewModel.IsInsightsMode && !awarenessViewModel.HasRestrictions,
    "Farkındalık profili arayüz durumuna yansımadı.");
awarenessViewModel.SelectedPageIndex = 2;
Assert(awarenessViewModel.SelectedPageIndex == 0, "Farkındalık modunda uygulama kuralı paneli açılabildi.");
Assert(awarenessViewModel.UsedTodayMinutes == 10 && awarenessViewModel.TodayLimitText is "Sınırsız" or "Unlimited",
    "Farkındalık profili gerçek ön plan süresini sınırsız özet olarak göstermedi.");

string flexibleSettingsPath = Path.Combine(testDirectory, "flexible-settings.json");
string flexibleUsagePath = Path.Combine(testDirectory, "flexible-usage.json");
JsonSettingsStore flexibleSettingsStore = new(flexibleSettingsPath);
await flexibleSettingsStore.SaveAsync(flexibleSettings);
MainViewModel flexibleViewModel = new(flexibleSettingsStore, new JsonUsageStore(flexibleUsagePath));
await flexibleViewModel.InitializeAsync();
Assert(flexibleViewModel.IsFlexiblePersonalMode && !flexibleViewModel.HasScheduledPlan &&
       flexibleViewModel.TodayLimitText is "Manuel" or "Manual",
    "Esnek kişisel modun manuel arayüz durumu oluşturulmadı.");
flexibleViewModel.SelectedPageIndex = 1;
Assert(flexibleViewModel.SelectedPageIndex == 0, "Esnek kişisel modda Plan sayfası açılabildi.");
SessionViewModel flexibleSessionViewModel = new(flexibleSettingsStore, new JsonUsageStore(flexibleUsagePath));
await flexibleSessionViewModel.InitializeAsync();
Assert(!flexibleSessionViewModel.HasCountdown && flexibleSessionViewModel.RemainingText == "00:00",
"Esnek kişisel oturum kronometre yerine geri sayımla hazırlandı.");
Assert(await flexibleSessionViewModel.StartOrResumeAsync(), "Esnek kişisel kronometre başlatılamadı.");
await Task.Delay(1100);
await flexibleSessionViewModel.TickAsync();
Assert(flexibleSessionViewModel.RemainingText != "00:00",
"Esnek kişisel kronometre başlatıldıktan sonra ilerlemedi.");
await flexibleSessionViewModel.EndSessionAsync();
Assert(flexibleSessionViewModel.RemainingText == "00:00",
"Esnek kişisel kronometre yeni oturum için sıfırlanmadı.");
string administrativePauseSettingsPath = Path.Combine(testDirectory, "administrative-pause-settings.json");
string administrativePauseUsagePath = Path.Combine(testDirectory, "administrative-pause-usage.json");
JsonSettingsStore administrativePauseSettingsStore = new(administrativePauseSettingsPath);
await administrativePauseSettingsStore.SaveAsync(flexibleSettings);
SessionViewModel administrativePauseViewModel = new(
administrativePauseSettingsStore,
new JsonUsageStore(administrativePauseUsagePath));
await administrativePauseViewModel.InitializeAsync();
Assert(await administrativePauseViewModel.StartOrResumeAsync(),
    "Yönetici süresi testi için oturum başlatılamadı.");
administrativePauseViewModel.SuspendUsageForAdministration();
await Task.Delay(1100);
await administrativePauseViewModel.TickAsync();
await administrativePauseViewModel.ReloadSettingsAsync();
await administrativePauseViewModel.SaveAsync();
Assert((await new JsonUsageStore(administrativePauseUsagePath).LoadAsync()).UsedSeconds == 0,
    "Kontrol Merkezi açıkken geçen süre kullanıcı kullanımına yazıldı.");
administrativePauseViewModel.ResumeUsageAfterAdministration();
await Task.Delay(1100);
await administrativePauseViewModel.TickAsync();
await administrativePauseViewModel.SaveAsync();
Assert((await new JsonUsageStore(administrativePauseUsagePath).LoadAsync()).UsedSeconds >= 1,
    "Kontrol Merkezi kapandıktan sonra kullanıcı sayacı devam etmedi.");
int elapsedWeekDays = ((int)DateTime.Today.DayOfWeek + 6) % 7 + 1;
long expectedAverageMinutes = 10 / elapsedWeekDays;
Assert(awarenessViewModel.HistoryDailyAverageText.StartsWith($"{expectedAverageMinutes} ", StringComparison.Ordinal),
    $"Günlük ortalama haftada geçen gün sayısına bölünmedi: {awarenessViewModel.HistoryDailyAverageText}");
awarenessViewModel.RetentionPeriod = awarenessViewModel.RetentionOptions[0];
Assert(await awarenessViewModel.SaveAsync() && (await awarenessSettingsStore.LoadAsync()).UsageRetentionDays == 30,
    "Geçmiş saklama süresi kaydedilemedi.");
Assert((await awarenessViewModel.ExportUsageJsonAsync()).Contains("SchemaVersion", StringComparison.Ordinal),
    "Kullanım verisi JSON olarak dışa aktarılamadı.");
string exportedCsv = await awarenessViewModel.ExportUsageCsvAsync();
Assert(exportedCsv.StartsWith("date,type,name,seconds,minutes", StringComparison.Ordinal) &&
       exportedCsv.Contains(",session_total,\"\",3600,60", StringComparison.Ordinal) &&
       exportedCsv.Contains(",rule_application,", StringComparison.Ordinal) &&
       exportedCsv.Contains(",foreground_application,\"'=SUM(1,1)\",600,10", StringComparison.Ordinal),
    "Kullanım verisi CSV olarak eksiksiz veya formül güvenli biçimde dışa aktarılamadı.");
SessionViewModel clearingBackgroundViewModel = new(awarenessSettingsStore, new JsonUsageStore(awarenessUsagePath));
await clearingBackgroundViewModel.InitializeAsync();
await clearingBackgroundViewModel.StartOrResumeAsync();
await Task.Delay(1100);
await clearingBackgroundViewModel.TickAsync();
await clearingBackgroundViewModel.ReloadSettingsAsync();
Assert((await new JsonUsageStore(awarenessUsagePath).LoadAsync()).UsedSeconds > 3600,
    "Ayar yenilemesi kaydedilmemiş oturum süresini düşürdü.");
UsageLedger staleAwarenessLedger = await new JsonUsageStore(awarenessUsagePath).LoadAsync();
await awarenessViewModel.ClearUsageHistoryAsync();
await new JsonUsageStore(awarenessUsagePath).SaveAsync(staleAwarenessLedger);
await clearingBackgroundViewModel.ReloadUsageAfterClearAsync();
await clearingBackgroundViewModel.SaveAsync();
UsageLedger clearedAwarenessLedger = await new JsonUsageStore(awarenessUsagePath).LoadAsync();
Assert(clearedAwarenessLedger.DataGeneration > staleAwarenessLedger.DataGeneration &&
       clearedAwarenessLedger.AwarenessUsedSeconds == 0 && clearedAwarenessLedger.History.Count == 0,
    "Kullanım geçmişi cihazdan temizlenemedi.");

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
    Mode = UsageMode.Personal,
    DeviceName = "Şimdiki Ad"
};
ControlSettings pendingTarget = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
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
ControlSettings shortcutSettings = new() { SetupCompleted = true, Mode = UsageMode.Personal };
shortcutSettings.PendingChange = new PendingPolicyChange
{
    ApplyAfterUtc = DateTimeOffset.UtcNow.AddHours(1),
    TargetSettings = new ControlSettings
    {
        SetupCompleted = true,
        Mode = UsageMode.Family,
        AdminPin = credential
    }
};
JsonSettingsStore shortcutStore = new(shortcutSettingsPath);
await shortcutStore.SaveAsync(shortcutSettings);
SessionViewModel shortcutViewModel = new(shortcutStore, new JsonUsageStore(shortcutUsagePath));
await shortcutViewModel.InitializeAsync();
#if KVIETA_DEVELOPMENT_BUILD
await shortcutViewModel.ForceUnlockForTestingAsync();
ControlSettings shortcutApplied = await shortcutStore.LoadAsync();
Assert(shortcutApplied.Mode == UsageMode.Family && shortcutApplied.PendingChange is null, "Gizli yönetici kısayolu bekleyen değişikliği hemen uygulamadı.");
#else
Assert(!BuildInfo.IsDevelopmentBuild, "Public Release testi geliştirme paketi olarak derlendi.");
#endif

string personalSettingsPath = Path.Combine(testDirectory, "personal-settings.json");
string personalUsagePath = Path.Combine(testDirectory, "personal-usage.json");
ControlSettings personalSettings = new()
{
    SetupCompleted = true,
    Mode = UsageMode.Personal,
    PersonalChangeDelayMinutes = 60
};
personalSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes = 60;
JsonSettingsStore personalSettingsStore = new(personalSettingsPath);
await personalSettingsStore.SaveAsync(personalSettings);
MainViewModel personalViewModel = new(personalSettingsStore, new JsonUsageStore(personalUsagePath));
await personalViewModel.InitializeAsync();

string stagedModeSettingsPath = Path.Combine(testDirectory, "staged-mode-settings.json");
JsonSettingsStore stagedModeStore = new(stagedModeSettingsPath);
await stagedModeStore.SaveAsync(new ControlSettings { SetupCompleted = true, Mode = UsageMode.Personal });
MainViewModel stagedModeViewModel = new(stagedModeStore, new JsonUsageStore(Path.Combine(testDirectory, "staged-mode-usage.json")));
await stagedModeViewModel.InitializeAsync();
stagedModeViewModel.StageUsageMode(UsageMode.Family, PersonalProtectionLevel.Balanced, "4826");
Assert((await stagedModeStore.LoadAsync()).Mode == UsageMode.Personal,
    "Korumalı mod Kaydet'e basılmadan etkinleşti.");
Assert(await stagedModeViewModel.SaveAsync(), "Hazırlanan korumalı mod kaydedilemedi.");
ControlSettings savedStagedMode = await stagedModeStore.LoadAsync();
Assert(savedStagedMode.Mode == UsageMode.Family && AdminPinService.Verify("4826", savedStagedMode.AdminPin),
    "Korumalı mod Kaydet sonrasında uygulanmadı.");

personalViewModel.AwarenessTrackingEnabled = true;
Assert(await personalViewModel.SaveAsync(), "Ritim farkındalığı tercihi kaydedilemedi.");
Assert((await personalSettingsStore.LoadAsync()).AwarenessTrackingEnabled, "Ritim farkındalığı tercihi kalıcı olmadı.");
await new JsonUsageStore(personalUsagePath).SaveAsync(new UsageLedger
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    AwarenessUsedSeconds = 150,
    ForegroundAppUsedSeconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
    {
        ["one.exe"] = 50,
        ["two.exe"] = 40,
        ["three.exe"] = 30,
        ["four.exe"] = 20,
        ["five.exe"] = 10
    }
});
await personalViewModel.ReloadUsageAsync();
Assert(personalViewModel.HistoryApplications.Count == 3 && personalViewModel.HistoryAllApplications.Count == 5, "Uygulama özetindeki üç kayıt sınırı uygulanmadı.");
Assert(personalViewModel.HistoryAllApplications.Select(item => item.Rank).SequenceEqual([1, 2, 3, 4, 5]), "Uygulama ayrıntıları kullanım sırasına göre numaralanmadı.");
personalViewModel.ScheduleRows.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes = 90;
Assert(await personalViewModel.SaveAsync(), $"Kişisel mod değişikliği kaydedilemedi: {personalViewModel.StatusMessage}");
ControlSettings queuedPersonalSettings = await personalSettingsStore.LoadAsync();
Assert(queuedPersonalSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 60, "Gevşeten değişiklik hemen uygulandı.");
Assert(queuedPersonalSettings.PendingChange?.TargetSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 90, "Gevşeten değişiklik beklemeye alınmadı.");

personalViewModel.ScheduleRows.Single(day => day.Day == DayOfWeek.Tuesday).DailyLimitMinutes = 90;
Assert(await personalViewModel.SaveAsync(), "Bekleyen değişiklik sırasında yeni sıkılaştırma kaydedilemedi.");
ControlSettings tightenedWhilePending = await personalSettingsStore.LoadAsync();
Assert(tightenedWhilePending.Schedule.Single(day => day.Day == DayOfWeek.Tuesday).DailyLimitMinutes == 90,
    "Yeni sıkılaştırma hemen uygulanmadı.");
Assert(tightenedWhilePending.PendingChange?.TargetSettings.Schedule.Single(day => day.Day == DayOfWeek.Monday).DailyLimitMinutes == 90 &&
       tightenedWhilePending.PendingChange.TargetSettings.Schedule.Single(day => day.Day == DayOfWeek.Tuesday).DailyLimitMinutes == 90,
    "Yeni sıkılaştırma bekleyen hedefe birleştirilmedi veya önceki hedef kayboldu.");

ControlSettings shorterDelay = CloneForTest(tightenedWhilePending);
shorterDelay.PersonalChangeDelayMinutes = 15;
Assert(SettingsPolicyComparer.HasRelaxation(tightenedWhilePending, shorterDelay), "Bekleme süresini azaltma gevşetme olarak algılanmadı.");

ControlSettings strictPersonal = CloneForTest(tightenedWhilePending);
strictPersonal.StrictPersonalMode = true;
ControlSettings relaxedPersonal = CloneForTest(strictPersonal);
relaxedPersonal.StrictPersonalMode = false;
Assert(SettingsPolicyComparer.HasRelaxation(strictPersonal, relaxedPersonal), "Sıkı kişisel modu kapatma gevşetme olarak algılanmadı.");
ControlSettings guardedPersonal = CloneForTest(strictPersonal);
guardedPersonal.Mode = UsageMode.Personal;
guardedPersonal.PersonalProtectionLevel = PersonalProtectionLevel.Protected;
ControlSettings balancedPersonal = CloneForTest(guardedPersonal);
balancedPersonal.PersonalProtectionLevel = PersonalProtectionLevel.Balanced;
Assert(guardedPersonal.RequiresGuardian, "Sıkı kişisel seviye Guardian gerektirmedi.");
Assert(SettingsPolicyComparer.HasRelaxation(guardedPersonal, balancedPersonal),
    "Guardian destekli kişisel seviyeyi düşürme gevşetme olarak algılanmadı.");

await personalViewModel.SetUsageModeAsync(UsageMode.Insights);
ControlSettings queuedAwarenessSettings = await personalSettingsStore.LoadAsync();
Assert(queuedAwarenessSettings.Mode == UsageMode.Personal, "Farkındalık kullanımına geçiş kişisel beklemeyi deldi.");
Assert(queuedAwarenessSettings.PendingChange?.TargetSettings.Mode == UsageMode.Insights &&
       queuedAwarenessSettings.PendingChange.TargetSettings.AwarenessTrackingEnabled &&
       queuedAwarenessSettings.PendingChange.TargetSettings.PersonalProtectionLevel == PersonalProtectionLevel.Balanced &&
       !queuedAwarenessSettings.PendingChange.TargetSettings.StrictPersonalMode,
    "Bekleyen farkındalık profili doğru hazırlanmadı.");
#if KVIETA_DEVELOPMENT_BUILD
Assert(await personalViewModel.ForceApplyPendingForTestingAsync(), "Kontrol merkezi test atlaması bekleyen değişikliği uygulamadı.");
ControlSettings bypassedAwarenessSettings = await personalSettingsStore.LoadAsync();
Assert(bypassedAwarenessSettings.Mode == UsageMode.Insights && bypassedAwarenessSettings.PendingChange is null,
    "Kontrol merkezi test atlaması bekleme süresini kaldıramadı.");
Assert(!await personalViewModel.ForceApplyPendingForTestingAsync(), "Bekleyen değişiklik yokken test atlaması başarılı göründü.");
#else
Assert(typeof(MainViewModel).GetMethod("ForceApplyPendingForTestingAsync") is null,
    "Public pakette kontrol merkezi test atlaması derlenmiş.");
#endif

string strictSettingsPath = Path.Combine(testDirectory, "strict-personal-settings.json");
string strictUsagePath = Path.Combine(testDirectory, "strict-personal-usage.json");
ControlSettings strictSessionSettings = new() { SetupCompleted = true, Mode = UsageMode.Personal, StrictPersonalMode = true };
DaySchedule strictToday = strictSessionSettings.Schedule.Single(day => day.Day == DateTime.Today.DayOfWeek);
strictToday.AllowedFrom = TimeOnly.MinValue;
strictToday.AllowedUntil = TimeOnly.MinValue;
strictToday.DailyLimitMinutes = 1;
await new JsonSettingsStore(strictSettingsPath).SaveAsync(strictSessionSettings);
await new JsonUsageStore(strictUsagePath).SaveAsync(new UsageLedger { LocalDay = DateOnly.FromDateTime(DateTime.Today), UsedSeconds = 60 });
SessionViewModel protectedSession = new(new JsonSettingsStore(strictSettingsPath), new JsonUsageStore(strictUsagePath));
await protectedSession.InitializeAsync();
Assert(protectedSession.State == SessionState.TimeExpired && !protectedSession.CanRequestExtraTime, "Sıkı kişisel modda ek süre isteği kapatılmadı.");
await protectedSession.AddBonusMinutesAsync(30);
Assert((await new JsonUsageStore(strictUsagePath).LoadAsync()).BonusMinutes == 0, "Sıkı kişisel mod ek süreyi model katmanında reddetmedi.");
ControlSettings familyExtraTimeSettings = new() { Mode = UsageMode.Family };
Assert(SessionViewModel.ShouldAllowExtraTimeRequest(familyExtraTimeSettings, SessionState.Active) &&
       SessionViewModel.ShouldAllowExtraTimeRequest(familyExtraTimeSettings, SessionState.Paused) &&
       SessionViewModel.ShouldAllowExtraTimeRequest(familyExtraTimeSettings, SessionState.TimeExpired) &&
       !SessionViewModel.ShouldAllowExtraTimeRequest(familyExtraTimeSettings, SessionState.Ready) &&
       !SessionViewModel.ShouldAllowExtraTimeRequest(familyExtraTimeSettings, SessionState.OutsideSchedule),
    "Aile modunda ek süre eylemi oturum sürerken ve süre dolduğunda doğru görünmüyor.");
UsageLedger explanationLedger = new()
{
    LocalDay = DateOnly.FromDateTime(DateTime.Today),
    State = SessionState.TimeExpired,
    BonusMinutes = 15
};
SessionStatusExplanation expiredExplanation = SessionStatusExplainer.Explain(
    familyExtraTimeSettings, explanationLedger, SessionState.TimeExpired, DateTimeOffset.Now);
Assert(expiredExplanation.Reason == SessionStatusReason.DailyLimit &&
       expiredExplanation.ActionAvailable && expiredExplanation.AccessibleText.Contains("PIN", StringComparison.Ordinal),
    "Ortak durum modeli günlük limit nedenini ve yerel PIN eylemini açıklamadı.");
AppRule previewRule = new() { Id = Guid.NewGuid(), Name = "Preview", Mode = AppRuleMode.Limited, DailyLimitMinutes = 1 };
explanationLedger.AppUsedSeconds[previewRule.Id] = 60;
SessionStatusExplanation applicationPreview = SessionStatusExplainer.PreviewApplication(
    familyExtraTimeSettings, explanationLedger, previewRule, SessionState.Active, DateTimeOffset.Now);
Assert(applicationPreview.Reason == SessionStatusReason.ApplicationLimit &&
       applicationPreview.WhatHappened.Contains("engellenir", StringComparison.OrdinalIgnoreCase),
    "Kural önizlemesi gerçek uygulama enforcement hesabıyla eşleşmedi.");

AdminCredential guardedCredential = AdminPinService.CreateInternalCredential();
await personalViewModel.SetUsageModeAsync(
    UsageMode.Personal,
    PersonalProtectionLevel.Protected,
    newCredential: guardedCredential);
ControlSettings guardedModeSettings = await personalSettingsStore.LoadAsync();
Assert(guardedModeSettings.Mode == UsageMode.Personal &&
       guardedModeSettings.PersonalProtectionLevel == PersonalProtectionLevel.Protected &&
       guardedModeSettings.RequiresGuardian &&
       guardedModeSettings.PendingChange is null,
    "Sıkı kişisel seviye hemen ve Guardian zorunlu olarak uygulanmadı.");
Assert(guardedModeSettings.AdminPin.HashBase64 == guardedCredential.HashBase64 &&
       !AdminPinService.Verify("4826", guardedModeSettings.AdminPin),
    "Sıkı kişisel teknik Guardian anahtarı kullanıcı PIN'ine dönüştü.");

await personalViewModel.SetUsageModeAsync(
    UsageMode.Personal,
    PersonalProtectionLevel.Balanced);
ControlSettings queuedGuardedExit = await personalSettingsStore.LoadAsync();
Assert(queuedGuardedExit.PersonalProtectionLevel == PersonalProtectionLevel.Protected &&
       queuedGuardedExit.PendingChange?.TargetSettings.PersonalProtectionLevel == PersonalProtectionLevel.Balanced,
    "Sıkı kişisel moddan çıkış bekleme süresini atladı.");

await personalViewModel.SetUsageModeAsync(UsageMode.Family, "4826");
ControlSettings protectedFromGuarded = await personalSettingsStore.LoadAsync();
Assert(protectedFromGuarded.Mode == UsageMode.Family && protectedFromGuarded.PendingChange is null,
    "Sıkı kişisel moddan daha korumalı moda geçiş gereksiz yere bekletildi.");
Assert(AdminPinService.Verify("4826", protectedFromGuarded.AdminPin),
    "Korumalı moda geçişte kullanıcı yönetici PIN'i uygulanmadı.");

string blockedExecutable = Path.Combine(testDirectory, "kvieta-rule-test.exe");
File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), blockedExecutable);
string tamperedSignedExecutable = Path.Combine(testDirectory, "tampered-signed-test.exe");
File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), tamperedSignedExecutable);
await File.AppendAllTextAsync(tamperedSignedExecutable, "tampered");
Assert(!AuthenticodeTrustVerifier.IsTrusted(tamperedSignedExecutable),
    "İçeriği değiştirilmiş Authenticode dosyası güvenilir kabul edildi.");
AppRule capturedApplicationRule = ApplicationIdentityService.CaptureRule(blockedExecutable);
Assert(!string.IsNullOrWhiteSpace(capturedApplicationRule.OriginalFileName) &&
    !string.IsNullOrWhiteSpace(capturedApplicationRule.Sha256) &&
    ApplicationIdentityService.MatchesRule(capturedApplicationRule, blockedExecutable),
    "Publisher/original filename/SHA-256 uygulama kimliği yakalanamadı.");
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
    new ApplicationRuleEnforcer().Enforce(enforcementSettings, new UsageLedger(), TimeSpan.FromSeconds(1), SessionState.Active);
    Assert(blockedProcess.WaitForExit(3000), "Engelli uygulama çalışan süreç olarak bırakıldı.");
}

AppRule limitedRule = enforcementSettings.AppRules[0];
limitedRule.Mode = AppRuleMode.Limited;
limitedRule.DailyLimitMinutes = 1;
Assert(!ApplicationRuleEnforcer.ShouldBlock(limitedRule, 59, SessionState.Active) &&
       ApplicationRuleEnforcer.ShouldBlock(limitedRule, 60, SessionState.Active),
    "Günlük uygulama limiti dolmadan veya dolduktan sonra yanlış değerlendirildi.");
limitedRule.Mode = AppRuleMode.ScheduleOnly;
Assert(!ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.Ready) &&
       ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.OutsideSchedule) &&
       ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.TimeExpired),
    "Yalnız plan içinde izin verilen uygulama plan dışında açık bırakıldı.");
limitedRule.Mode = AppRuleMode.FocusBlocked;
Assert(ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.Active) &&
       !ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.Paused) &&
       !ApplicationRuleEnforcer.ShouldBlock(limitedRule, 0, SessionState.Ready),
    "Odakta engelleme kuralı oturum durumunu doğru izlemedi.");
limitedRule.Mode = AppRuleMode.Limited;
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
    limitedEnforcer.Enforce(enforcementSettings, applicationLedger, TimeSpan.FromSeconds(2), SessionState.Active);
    Assert(applicationLedger.AppUsedSeconds.GetValueOrDefault(limitedRule.Id) == 2, "Uygulama kullanım süresi kaydedilmedi.");
    limitedRule.DailyLimitMinutes = 0;
    limitedEnforcer.Enforce(enforcementSettings, applicationLedger, TimeSpan.Zero, SessionState.Active);
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
    new ApplicationRuleEnforcer().Enforce(enforcementSettings, unlimitedLedger, TimeSpan.FromSeconds(2), SessionState.Active);
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
    AppUsedSeconds = new Dictionary<Guid, long> { [historyRule.Id] = 900 },
    AwarenessUsedSeconds = 1200,
    ForegroundAppUsedSeconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["browser.exe"] = 1200 }
};
DateTimeOffset historyNow = new(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(3));
SessionEngine historyEngine = new(historySettings, historyLedger, historyNow);
DailyUsageRecord archivedDay = historyLedger.History.Single(item => item.LocalDay == new DateOnly(2026, 8, 23));
Assert(archivedDay.UsedSeconds == 3600 && archivedDay.BreakCount == 2, "Önceki gün kullanım geçmişine arşivlenmedi.");
Assert(archivedDay.Applications.Single().Name == "Geçmiş uygulaması", "Uygulama geçmişi adıyla arşivlenmedi.");
Assert(archivedDay.AwarenessUsedSeconds == 1200 && archivedDay.ForegroundApplications.Single().ApplicationId == "browser.exe", "Ön plan farkındalık verisi ayrı biçimde arşivlenmedi.");
Assert(historyLedger.UsedSeconds == 0 && historyLedger.BreakCount == 0, "Yeni günde aktif sayaçlar sıfırlanmadı.");
Assert(historyLedger.AwarenessUsedSeconds == 0 && historyLedger.ForegroundAppUsedSeconds.Count == 0, "Yeni günde farkındalık sayaçları sıfırlanmadı.");
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
Console.WriteLine("Kvieta çekirdek kontrolleri başarılı.");

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
    AwarenessTrackingEnabled = settings.AwarenessTrackingEnabled,
    UsageRetentionDays = settings.UsageRetentionDays,
    PersonalChangeDelayMinutes = settings.PersonalChangeDelayMinutes,
    StrictPersonalMode = settings.StrictPersonalMode,
    PersonalProtectionLevel = settings.PersonalProtectionLevel,
    WeeklyReductionGoalPercent = settings.WeeklyReductionGoalPercent,
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

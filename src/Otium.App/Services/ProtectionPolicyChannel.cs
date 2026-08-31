using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public static class ProtectionPolicyChannel
{
    private const string PipeName = "OtiumGuardian.Policy.v1";
    private const int MaximumRequestCharacters = 1_500_000;
    private static readonly TimeSpan ClientRequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly HashSet<Guid> RecentRequestIds = [];
    private static readonly Queue<Guid> RecentRequestOrder = [];
    private static string ThrottleStatePath => Path.Combine(
        ProtectionServiceManager.ProtectionDataDirectory,
        "guardian-auth-throttle.json");
    private static string ManagerRecoveryReplayPath => Path.Combine(
        ProtectionServiceManager.ProtectionDataDirectory,
        "manager-recovery-replay.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<bool> SyncAsync(string settingsJson, string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || string.IsNullOrWhiteSpace(pin))
        {
            return Task.FromResult(false);
        }

        string settingsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));
        return SendAuthenticatedRequestAsync(
            new PolicyRequest("sync", pin, settingsBase64, null, null),
            challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
            cancellationToken);
    }

    public static Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        return !AdminPinService.IsValidFormat(pin)
            ? Task.FromResult(false)
            : SendAuthenticatedRequestAsync(
                new PolicyRequest("verify-pin", pin, null, null, null),
                challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
                cancellationToken);
    }

    public static Task<bool> SyncGuardedPersonalAsync(
        string settingsJson,
        AdminCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || !credential.IsConfigured)
        {
            return Task.FromResult(false);
        }

        string settingsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));
        return SendAuthenticatedRequestAsync(
            new PolicyRequest("sync-guarded", null, settingsBase64, null, null),
            _ => Convert.FromBase64String(credential.HashBase64),
            cancellationToken);
    }

    public static Task<bool> ResetPinWithRecoveryCodeAsync(
        string recoveryCode,
        AdminCredential newCredential,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryCode) || !newCredential.IsConfigured)
        {
            return Task.FromResult(false);
        }

        return SendAuthenticatedRequestAsync(
            new PolicyRequest("recovery-pin-reset", null, null, recoveryCode, newCredential),
            _ => SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeRecoveryCode(recoveryCode))),
            cancellationToken);
    }

    public static Task<bool> EnrollManagerDeviceAsync(
        ManagerDeviceEnrollmentRequest request,
        string pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!AdminPinService.IsValidFormat(pin))
        {
            return Task.FromResult(false);
        }

        return SendAuthenticatedRequestAsync(
            new PolicyRequest("manager-device-enroll", pin, null, null, null, request),
            challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
            cancellationToken);
    }

    public static Task<bool> RevokeManagerDeviceAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (!AdminPinService.IsValidFormat(pin))
        {
            return Task.FromResult(false);
        }

        return SendAuthenticatedRequestAsync(
            new PolicyRequest("manager-device-revoke", pin, null, null, null),
            challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
            cancellationToken);
    }

    public static Task<bool> TransferManagerDeviceAsync(
        ManagerDeviceTransferRequest request,
        string pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!AdminPinService.IsValidFormat(pin))
        {
            return Task.FromResult(false);
        }

        return SendAuthenticatedRequestAsync(
            new PolicyRequest(
                "manager-device-transfer",
                pin,
                null,
                null,
                null,
                null,
                null,
                null,
                request),
            challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
            cancellationToken);
    }

    public static Task<bool> BeginAdministrativeActivityAsync(
        string pin,
        CancellationToken cancellationToken = default) =>
        SendPinAuthenticatedOperationAsync("admin-activity-begin", pin, cancellationToken);

    public static Task<bool> EndAdministrativeActivityAsync(
        string pin,
        CancellationToken cancellationToken = default) =>
        SendPinAuthenticatedOperationAsync("admin-activity-end", pin, cancellationToken);

    public static bool IsAdministrativeActivityActive()
    {
        try
        {
            if (!File.Exists(ProtectionServiceManager.AdministrativeActivityPath)) return false;
            AdministrativeActivityLease? lease = JsonSerializer.Deserialize<AdministrativeActivityLease>(
                File.ReadAllText(ProtectionServiceManager.AdministrativeActivityPath),
                JsonOptions);
            return lease is not null && lease.ExpiresAtUtc > DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> SendPinAuthenticatedOperationAsync(
        string operation,
        string pin,
        CancellationToken cancellationToken)
    {
        return !AdminPinService.IsValidFormat(pin)
            ? Task.FromResult(false)
            : SendAuthenticatedRequestAsync(
                new PolicyRequest(operation, pin, null, null, null),
                challenge => AdminPinService.DeriveHash(pin, challenge.SaltBase64, challenge.Iterations),
                cancellationToken);
    }

    public static Task<bool> ResetPinWithManagerDeviceAsync(
        RecoveryChallenge challenge,
        RecoveryChallengeResponse response,
        AdminCredential newCredential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(newCredential);
        if (!ManagerDeviceRecoveryService.MatchesPinReset(challenge, newCredential) ||
            string.IsNullOrWhiteSpace(response.SignatureBase64))
        {
            return Task.FromResult(false);
        }

        return SendAuthenticatedRequestAsync(
            new PolicyRequest(
                "manager-device-pin-reset",
                null,
                null,
                null,
                newCredential,
                null,
                challenge,
                response),
            _ => SHA256.HashData(Convert.FromBase64String(response.SignatureBase64)),
            cancellationToken);
    }

    private static async Task<bool> SendAuthenticatedRequestAsync(
        PolicyRequest payload,
        Func<PolicyChallenge, byte[]> keyFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            using NamedPipeClientStream client = new(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await client.ConnectAsync(timeout.Token);
            if (!IsGuardianServiceServer(client)) return false;

            using StreamWriter writer = new(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(client, Encoding.UTF8, leaveOpen: true);
            PolicyChallenge? challenge = await ReadChallengeAsync(reader, timeout.Token);
            if (challenge is null) return false;
            string request = CreateAuthenticatedRequest(payload, challenge.NonceBase64, keyFactory(challenge));
            await writer.WriteLineAsync(request.AsMemory(), timeout.Token);
            return string.Equals(await reader.ReadLineAsync(timeout.Token), "OK", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
                if (enrollment is null || string.IsNullOrWhiteSpace(enrollment.UserSid))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }

                PipeSecurity security = new();
                security.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(enrollment.UserSid),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));
                security.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                server = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
                    0,
                    0,
                    security);
                await server.WaitForConnectionAsync(cancellationToken);
                await HandleClientAsync(server, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // A per-client timeout closes only that connection; the policy channel stays online.
            }
            catch
            {
                // A malformed or disconnected client must not stop policy protection.
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private static async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(ClientRequestTimeout);
        using StreamReader reader = new(server, Encoding.UTF8, leaveOpen: true);
        using StreamWriter writer = new(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
        if (enrollment is null || !IsAuthorizedClient(server))
        {
            await AuditAsync("guardian.ipc.authorization", "rejected", requestTimeout.Token);
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        string challengeNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        PolicyChallenge challenge = new(challengeNonce, enrollment.AdminPin.SaltBase64, enrollment.AdminPin.Iterations);
        await writer.WriteLineAsync(JsonSerializer.Serialize(challenge).AsMemory(), requestTimeout.Token);
        string? line = await ReadBoundedLineAsync(reader, MaximumRequestCharacters, requestTimeout.Token);
        if (line is null)
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        AuthenticatedPolicyRequest? envelope;
        PolicyRequest? request;
        try
        {
            envelope = JsonSerializer.Deserialize<AuthenticatedPolicyRequest>(line);
            request = envelope is null
                ? null
                : JsonSerializer.Deserialize<PolicyRequest>(
                    Encoding.UTF8.GetString(Convert.FromBase64String(envelope.PayloadBase64)));
        }
        catch
        {
            envelope = null;
            request = null;
        }

        AuthenticationThrottleState throttle = LoadThrottleState();
        if (request is not null &&
            DateTimeOffset.UtcNow < throttle.BlockedUntilUtc &&
            !CanAttemptDuringPinCooldown(request.Operation))
        {
            await AuditAsync("guardian.ipc.throttle", "rejected", requestTimeout.Token);
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        if (request is null || envelope is null ||
            !ValidateEnvelope(envelope, request, challengeNonce, enrollment))
        {
            RegisterAuthenticationFailure(throttle);
            await AuditAsync("guardian.ipc.integrity", "rejected", requestTimeout.Token);
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        if (string.Equals(request.Operation, "recovery-pin-reset", StringComparison.Ordinal))
        {
            await HandleRecoveryPinResetAsync(request, enrollment, writer, requestTimeout.Token);
            return;
        }

        if (string.Equals(request.Operation, "manager-device-pin-reset", StringComparison.Ordinal))
        {
            await HandleManagerDevicePinResetAsync(request, enrollment, writer, requestTimeout.Token);
            return;
        }

        if (request.Operation is "admin-activity-begin" or "admin-activity-end")
        {
            await HandleAdministrativeActivityAsync(request, enrollment, writer, requestTimeout.Token);
            return;
        }

        if (request.Operation is "manager-device-enroll" or "manager-device-revoke" or
            "manager-device-transfer")
        {
            await HandleManagerDeviceRequestAsync(request, enrollment, writer, requestTimeout.Token);
            return;
        }

        if (string.Equals(request.Operation, "verify-pin", StringComparison.Ordinal))
        {
            bool verified = !string.IsNullOrWhiteSpace(request.Pin) &&
                (AdminPinService.Verify(request.Pin, enrollment.AdminPin) ||
                 enrollment.PreviousAdminPin is { IsConfigured: true } previousVerificationPin &&
                 AdminPinService.Verify(request.Pin, previousVerificationPin));
            if (verified)
            {
                ResetAuthenticationThrottle();
                await AuditAsync("guardian.ipc.pin", "accepted", requestTimeout.Token);
                await writer.WriteLineAsync("OK".AsMemory(), requestTimeout.Token);
            }
            else
            {
                RegisterAuthenticationFailure(throttle);
                await AuditAsync("guardian.ipc.pin", "rejected", requestTimeout.Token);
                await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            }
            return;
        }

        bool pinSync = string.Equals(request.Operation, "sync", StringComparison.Ordinal);
        bool guardedSync = string.Equals(request.Operation, "sync-guarded", StringComparison.Ordinal);
        if ((!pinSync && !guardedSync) ||
            pinSync && string.IsNullOrWhiteSpace(request.Pin) ||
            string.IsNullOrWhiteSpace(request.SettingsBase64))
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        bool authenticated = guardedSync
            ? IsGuardedPersonalPolicy(enrollment)
            : AdminPinService.Verify(request.Pin!, enrollment.AdminPin) ||
              enrollment.PreviousAdminPin is { IsConfigured: true } previousPin &&
              AdminPinService.Verify(request.Pin!, previousPin);
        if (!authenticated)
        {
            RegisterAuthenticationFailure(throttle);
            await AuditAsync("guardian.ipc.pin", "rejected", requestTimeout.Token);
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        ResetAuthenticationThrottle();

        byte[] settingsBytes;
        ControlSettings? candidate;
        try
        {
            settingsBytes = Convert.FromBase64String(request.SettingsBase64);
            candidate = JsonSerializer.Deserialize<ControlSettings>(settingsBytes, JsonOptions);
        }
        catch
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        if (candidate is null || !candidate.RequiresGuardian || !candidate.AdminPin.IsConfigured)
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        bool remainsGuardedPersonal = candidate.Mode == ControlMode.Personal &&
            candidate.PersonalProtectionLevel == PersonalProtectionLevel.Guarded;
        if (guardedSync &&
            (!remainsGuardedPersonal && candidate.Mode != ControlMode.Protected ||
             remainsGuardedPersonal && !CredentialsEqual(enrollment.AdminPin, candidate.AdminPin)))
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        if (candidate.AdminPin.IsPublicMarker)
        {
            candidate.AdminPin = enrollment.AdminPin;
        }

        bool pinChanged = !CredentialsEqual(enrollment.AdminPin, candidate.AdminPin);
        if (pinChanged)
        {
            GuardianEnrollment transition = enrollment with
            {
                AdminPin = candidate.AdminPin,
                PreviousAdminPin = enrollment.AdminPin
            };
            await WriteEnrollmentAtomicallyAsync(transition, requestTimeout.Token);
        }

        byte[] protectedSettingsBytes = CreateProtectedPolicyBytes(candidate);
        await WriteBytesAtomicallyAsync(
            ProtectionServiceManager.ProtectedSettingsPath,
            protectedSettingsBytes,
            requestTimeout.Token);
        ProtectionServiceManager.HardenProtectedPolicyAcl(enrollment.UserSid);
        if (!string.IsNullOrWhiteSpace(enrollment.SettingsPath))
        {
            await WriteBytesAtomicallyAsync(enrollment.SettingsPath, protectedSettingsBytes, requestTimeout.Token);
        }

        GuardianEnrollment updatedEnrollment = enrollment with
        {
            AdminPin = candidate.AdminPin,
            PreviousAdminPin = null
        };
        await WriteEnrollmentAtomicallyAsync(updatedEnrollment, requestTimeout.Token);
        await writer.WriteLineAsync("OK".AsMemory(), requestTimeout.Token);
    }

    private static async Task HandleRecoveryPinResetAsync(
        PolicyRequest request,
        GuardianEnrollment enrollment,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        SecurityAuditLog auditLog = new();
        if (string.IsNullOrWhiteSpace(request.RecoveryCode) || request.NewCredential?.IsConfigured != true)
        {
            await auditLog.AppendAsync("recovery.pin-reset", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        ControlSettings? settings;
        try
        {
            byte[] settingsBytes = await File.ReadAllBytesAsync(
                ProtectionServiceManager.ProtectedSettingsPath,
                cancellationToken);
            settings = JsonSerializer.Deserialize<ControlSettings>(settingsBytes, JsonOptions);
        }
        catch
        {
            settings = null;
        }

        if (settings is null || !RecoveryCodeService.TryConsume(settings, request.RecoveryCode))
        {
            RegisterAuthenticationFailure(LoadThrottleState());
            await auditLog.AppendAsync("recovery.pin-reset", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        settings.AdminPin = request.NewCredential;
        byte[] updatedSettings = CreateProtectedPolicyBytes(settings);
        GuardianEnrollment transition = enrollment with
        {
            AdminPin = request.NewCredential,
            PreviousAdminPin = enrollment.AdminPin
        };
        await WriteEnrollmentAtomicallyAsync(transition, cancellationToken);
        await WriteBytesAtomicallyAsync(
            ProtectionServiceManager.ProtectedSettingsPath,
            updatedSettings,
            cancellationToken);
        ProtectionServiceManager.HardenProtectedPolicyAcl(enrollment.UserSid);
        if (!string.IsNullOrWhiteSpace(enrollment.SettingsPath))
        {
            await WriteBytesAtomicallyAsync(enrollment.SettingsPath, updatedSettings, cancellationToken);
        }
        await WriteEnrollmentAtomicallyAsync(transition with { PreviousAdminPin = null }, cancellationToken);
        ResetAuthenticationThrottle();
        await auditLog.AppendAsync("recovery.pin-reset", "accepted", cancellationToken);
        await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
    }

    private static async Task HandleManagerDeviceRequestAsync(
        PolicyRequest request,
        GuardianEnrollment guardianEnrollment,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pin) ||
            !AdminPinService.Verify(request.Pin, guardianEnrollment.AdminPin))
        {
            RegisterAuthenticationFailure(LoadThrottleState());
            await AuditAsync("recovery.manager-device", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        // Authentication succeeded. Enrollment payload validation is a separate
        // concern and must never poison the administrator PIN throttle.
        ResetAuthenticationThrottle();

        if (string.Equals(request.Operation, "manager-device-enroll", StringComparison.Ordinal))
        {
            if (ManagerDeviceEnrollmentStore.Load()?.IsActive == true ||
                request.ManagerDeviceEnrollment is null ||
                !ManagerDeviceEnrollmentService.VerifyRequest(
                    request.ManagerDeviceEnrollment,
                    DateTimeOffset.UtcNow))
            {
                await AuditAsync("recovery.manager-device.enroll", "rejected", cancellationToken);
                await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
                return;
            }

            ManagerDeviceEnrollmentStore.Save(request.ManagerDeviceEnrollment.Enrollment);
            ResetAuthenticationThrottle();
            await AuditAsync("recovery.manager-device.enroll", "accepted", cancellationToken);
            await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
            return;
        }

        if (string.Equals(request.Operation, "manager-device-transfer", StringComparison.Ordinal))
        {
            if (request.ManagerDeviceTransfer is null ||
                !ManagerDeviceEnrollmentService.IsValid(request.ManagerDeviceTransfer.Replacement) ||
                !ManagerDeviceEnrollmentStore.CompleteTransfer(
                    request.ManagerDeviceTransfer.Replacement,
                    request.ManagerDeviceTransfer.Transfer,
                    DateTimeOffset.UtcNow))
            {
                await AuditAsync("recovery.manager-device.transfer", "rejected", cancellationToken);
                await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
                return;
            }

            ResetAuthenticationThrottle();
            await AuditAsync("recovery.manager-device.transfer", "accepted", cancellationToken);
            await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
            return;
        }

        ManagerDeviceEnrollmentStore.Revoke(DateTimeOffset.UtcNow);
        ResetAuthenticationThrottle();
        await AuditAsync("recovery.manager-device.revoke", "accepted", cancellationToken);
        await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
    }

    private static async Task HandleAdministrativeActivityAsync(
        PolicyRequest request,
        GuardianEnrollment enrollment,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pin) ||
            !AdminPinService.Verify(request.Pin, enrollment.AdminPin))
        {
            RegisterAuthenticationFailure(LoadThrottleState());
            await AuditAsync("guardian.admin-activity", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        if (string.Equals(request.Operation, "admin-activity-begin", StringComparison.Ordinal))
        {
            AdministrativeActivityLease lease = new(DateTimeOffset.UtcNow.AddMinutes(3));
            await WriteBytesAtomicallyAsync(
                ProtectionServiceManager.AdministrativeActivityPath,
                JsonSerializer.SerializeToUtf8Bytes(lease, JsonOptions),
                cancellationToken);
            ProtectionServiceManager.HardenReadOnlyFileAcl(
                ProtectionServiceManager.AdministrativeActivityPath,
                enrollment.UserSid);
            await AuditAsync("guardian.admin-activity", "started", cancellationToken);
        }
        else
        {
            try
            {
                if (File.Exists(ProtectionServiceManager.AdministrativeActivityPath))
                {
                    File.Delete(ProtectionServiceManager.AdministrativeActivityPath);
                }
            }
            catch
            {
                await AuditAsync("guardian.admin-activity", "release-failed", cancellationToken);
                await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
                return;
            }
            await AuditAsync("guardian.admin-activity", "ended", cancellationToken);
        }

        ResetAuthenticationThrottle();
        await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
    }

    private static async Task HandleManagerDevicePinResetAsync(
        PolicyRequest request,
        GuardianEnrollment guardianEnrollment,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        ManagerDeviceEnrollment? managerEnrollment = ManagerDeviceEnrollmentStore.Load();
        if (managerEnrollment?.IsActive != true ||
            request.RecoveryChallenge is null ||
            request.RecoveryChallengeResponse is null ||
            request.NewCredential?.IsConfigured != true ||
            !ManagerDeviceRecoveryService.MatchesPinReset(request.RecoveryChallenge, request.NewCredential) ||
            !ManagerDeviceAuthorizationService.VerifyResponse(
                managerEnrollment,
                request.RecoveryChallenge,
                request.RecoveryChallengeResponse,
                DateTimeOffset.UtcNow) ||
            !TryConsumeManagerRecoveryChallenge(request.RecoveryChallenge))
        {
            RegisterAuthenticationFailure(LoadThrottleState());
            await AuditAsync("recovery.manager-device.pin-reset", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        ControlSettings? settings;
        try
        {
            byte[] settingsBytes = await File.ReadAllBytesAsync(
                ProtectionServiceManager.ProtectedSettingsPath,
                cancellationToken);
            settings = JsonSerializer.Deserialize<ControlSettings>(settingsBytes, JsonOptions);
        }
        catch
        {
            settings = null;
        }

        if (settings is null)
        {
            await AuditAsync("recovery.manager-device.pin-reset", "rejected", cancellationToken);
            await writer.WriteLineAsync("ERR".AsMemory(), cancellationToken);
            return;
        }

        settings.AdminPin = request.NewCredential;
        byte[] updatedSettings = CreateProtectedPolicyBytes(settings);
        GuardianEnrollment transition = guardianEnrollment with
        {
            AdminPin = request.NewCredential,
            PreviousAdminPin = guardianEnrollment.AdminPin
        };
        await WriteEnrollmentAtomicallyAsync(transition, cancellationToken);
        await WriteBytesAtomicallyAsync(
            ProtectionServiceManager.ProtectedSettingsPath,
            updatedSettings,
            cancellationToken);
        ProtectionServiceManager.HardenProtectedPolicyAcl(guardianEnrollment.UserSid);
        if (!string.IsNullOrWhiteSpace(guardianEnrollment.SettingsPath))
        {
            await WriteBytesAtomicallyAsync(guardianEnrollment.SettingsPath, updatedSettings, cancellationToken);
        }

        await WriteEnrollmentAtomicallyAsync(transition with { PreviousAdminPin = null }, cancellationToken);
        ResetAuthenticationThrottle();
        await AuditAsync("recovery.manager-device.pin-reset", "accepted", cancellationToken);
        await writer.WriteLineAsync("OK".AsMemory(), cancellationToken);
    }

    private static bool TryConsumeManagerRecoveryChallenge(RecoveryChallenge challenge)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ConsumedManagerChallenge> entries;
        try
        {
            entries = File.Exists(ManagerRecoveryReplayPath)
                ? JsonSerializer.Deserialize<List<ConsumedManagerChallenge>>(
                    File.ReadAllText(ManagerRecoveryReplayPath),
                    JsonOptions) ?? []
                : [];
        }
        catch
        {
            return false;
        }

        entries.RemoveAll(item => item.ExpiresAtUtc <= now);
        if (entries.Any(item => string.Equals(item.ChallengeId, challenge.ChallengeId, StringComparison.Ordinal)))
        {
            return false;
        }

        entries.Add(new ConsumedManagerChallenge(challenge.ChallengeId, challenge.ExpiresAtUtc));
        Directory.CreateDirectory(ProtectionServiceManager.ProtectionDataDirectory);
        string temporary = $"{ManagerRecoveryReplayPath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temporary, ManagerRecoveryReplayPath, overwrite: true);
            ProtectionServiceManager.HardenSensitiveFileAcl(ManagerRecoveryReplayPath);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task<string?> ReadBoundedLineAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[4096];
        StringBuilder value = new(Math.Min(maximumCharacters, buffer.Length));
        while (value.Length <= maximumCharacters)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return value.Length == 0 ? null : value.ToString();
            }

            int lineEnd = Array.IndexOf(buffer, '\n', 0, read);
            int charactersToAppend = lineEnd >= 0 ? lineEnd : read;
            if (value.Length + charactersToAppend > maximumCharacters)
            {
                return null;
            }

            value.Append(buffer, 0, charactersToAppend);
            if (lineEnd >= 0)
            {
                if (value.Length > 0 && value[^1] == '\r')
                {
                    value.Length--;
                }

                return value.ToString();
            }
        }

        return null;
    }

    private static bool IsGuardianServiceServer(NamedPipeClientStream client)
    {
        nint serviceManager = 0;
        nint service = 0;
        try
        {
            if (!GetNamedPipeServerProcessId(client.SafePipeHandle, out uint pipeServerProcessId) ||
                pipeServerProcessId == 0)
            {
                return false;
            }

            serviceManager = OpenSCManager(null, null, ScManagerConnect);
            if (serviceManager == 0)
            {
                return false;
            }

            service = OpenService(
                serviceManager,
                ProtectionServiceManager.ServiceName,
                ServiceQueryStatus);
            if (service == 0)
            {
                return false;
            }

            ServiceStatusProcess status = new();
            return QueryServiceStatusEx(
                    service,
                    ScStatusProcessInfo,
                    ref status,
                    Marshal.SizeOf<ServiceStatusProcess>(),
                    out _) &&
                status.CurrentState == ServiceRunning &&
                status.ProcessId == pipeServerProcessId;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (service != 0) CloseServiceHandle(service);
            if (serviceManager != 0) CloseServiceHandle(serviceManager);
        }
    }

    private static async Task<PolicyChallenge?> ReadChallengeAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        string? line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line) || string.Equals(line, "ERR", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            PolicyChallenge? challenge = JsonSerializer.Deserialize<PolicyChallenge>(line);
            return challenge is { NonceBase64.Length: > 0, SaltBase64.Length: > 0, Iterations: >= 100_000 }
                ? challenge
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string CreateAuthenticatedRequest(
        PolicyRequest payload,
        string nonceBase64,
        byte[] key)
    {
        string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        Guid requestId = Guid.NewGuid();
        string issuedAtUtc = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        byte[] mac = HMACSHA256.HashData(
            key,
            Encoding.UTF8.GetBytes(CreateMacInput(nonceBase64, requestId, issuedAtUtc, payloadBase64)));
        return JsonSerializer.Serialize(new AuthenticatedPolicyRequest(
            requestId,
            issuedAtUtc,
            payloadBase64,
            Convert.ToBase64String(mac)));
    }

    private static bool ValidateEnvelope(
        AuthenticatedPolicyRequest envelope,
        PolicyRequest request,
        string challengeNonce,
        GuardianEnrollment enrollment)
    {
        if (envelope.RequestId == Guid.Empty ||
            !DateTimeOffset.TryParseExact(
                envelope.IssuedAtUtc,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTimeOffset issuedAt) ||
            Math.Abs((DateTimeOffset.UtcNow - issuedAt).TotalSeconds) > 30 ||
            RecentRequestIds.Contains(envelope.RequestId))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = request.Operation is "sync" or "sync-guarded" or "verify-pin" or
                "manager-device-enroll" or "manager-device-revoke" or "manager-device-transfer" or
                "admin-activity-begin" or "admin-activity-end"
                ? Convert.FromBase64String(enrollment.AdminPin.HashBase64)
                : string.Equals(request.Operation, "manager-device-pin-reset", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(request.RecoveryChallengeResponse?.SignatureBase64)
                    ? SHA256.HashData(Convert.FromBase64String(
                        request.RecoveryChallengeResponse.SignatureBase64))
                : string.Equals(request.Operation, "recovery-pin-reset", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(request.RecoveryCode)
                    ? SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeRecoveryCode(request.RecoveryCode)))
                    : [];
            byte[] suppliedMac = Convert.FromBase64String(envelope.MacBase64);
            byte[] expectedMac = HMACSHA256.HashData(
                key,
                Encoding.UTF8.GetBytes(CreateMacInput(
                    challengeNonce,
                    envelope.RequestId,
                    envelope.IssuedAtUtc,
                    envelope.PayloadBase64)));
            if (key.Length == 0 || !CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        RecentRequestIds.Add(envelope.RequestId);
        RecentRequestOrder.Enqueue(envelope.RequestId);
        while (RecentRequestOrder.Count > 256)
        {
            RecentRequestIds.Remove(RecentRequestOrder.Dequeue());
        }

        return true;
    }

    private static string CreateMacInput(
        string nonceBase64,
        Guid requestId,
        string issuedAtUtc,
        string payloadBase64) =>
        $"{nonceBase64}.{requestId:D}.{issuedAtUtc}.{payloadBase64}";

    private static string NormalizeRecoveryCode(string code) =>
        new(code.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool CanAttemptDuringPinCooldown(string? operation) =>
        operation is "recovery-pin-reset" or "manager-device-pin-reset";

    private static bool IsGuardedPersonalPolicy(GuardianEnrollment enrollment)
    {
        try
        {
            ControlSettings? current = JsonSerializer.Deserialize<ControlSettings>(
                File.ReadAllBytes(ProtectionServiceManager.ProtectedSettingsPath),
                JsonOptions);
            return current?.Mode == ControlMode.Personal &&
                current.PersonalProtectionLevel == PersonalProtectionLevel.Guarded &&
                CredentialsEqual(enrollment.AdminPin, current.AdminPin);
        }
        catch
        {
            return false;
        }
    }

    public static byte[] CreateProtectedPolicyBytes(ControlSettings settings)
    {
        ControlSettings copy = CreatePublicPolicy(settings);
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(copy, JsonOptions));
    }

    public static ControlSettings CreatePublicPolicy(ControlSettings settings)
    {
        if (!ContainsUnredactedProtectedCredential(settings)) return settings;
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        ControlSettings copy = JsonSerializer.Deserialize<ControlSettings>(json, JsonOptions)
            ?? throw new InvalidOperationException("Policy could not be cloned.");
        RedactProtectedCredential(copy);
        return copy;
    }

    private static bool ContainsUnredactedProtectedCredential(ControlSettings settings) =>
        settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured && !settings.AdminPin.IsPublicMarker ||
        settings.PendingChange?.TargetSettings is { } target && ContainsUnredactedProtectedCredential(target);

    private static void RedactProtectedCredential(ControlSettings settings)
    {
        if (settings.Mode == ControlMode.Protected && settings.AdminPin.IsConfigured && !settings.AdminPin.IsPublicMarker)
        {
            settings.AdminPin = AdminPinService.CreatePublicMarker(settings.AdminPin);
        }
        if (settings.PendingChange?.TargetSettings is { } target) RedactProtectedCredential(target);
    }

    private static bool IsAuthorizedClient(NamedPipeServerStream server)
    {
        try
        {
            if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out uint processId) || processId == 0)
            {
                return false;
            }

            using Process process = Process.GetProcessById(checked((int)processId));
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (BuildInfo.IsDevelopmentBuild)
            {
                return string.Equals(Path.GetFileName(path), "Otium.exe", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(ProtectionServiceManager.InstalledExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? pinnedThumbprint = ProtectionServiceManager.RegisteredSignerThumbprint;
            if (string.Equals(
                    ProtectionServiceManager.RegisteredPackageKind,
                    "community",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!ProtectionServiceManager.IsInstallerManaged)
                {
                    return false;
                }

                string actualHash = ProtectionServiceManager.ComputeSha256(path);
                return ProtectionServiceManager.IsCommunityClientIdentityValid(
                    ProtectionServiceManager.RegisteredPackageKind,
                    ProtectionServiceManager.RegisteredExecutableSha256,
                    actualHash,
                    ProtectionServiceManager.ReadRegisteredVersionForIdentity(),
                    ProtectionServiceManager.ReadProductVersionForIdentity(path));
            }

            if (string.IsNullOrWhiteSpace(pinnedThumbprint) || !AuthenticodeTrustVerifier.IsTrusted(path)) return false;
#pragma warning disable SYSLIB0057
            using X509Certificate2 signer = new(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            using X509Chain chain = new();
            return string.Equals(
                    signer.Thumbprint?.Replace(" ", string.Empty, StringComparison.Ordinal),
                    pinnedThumbprint.Replace(" ", string.Empty, StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase) &&
                chain.Build(signer);
        }
        catch
        {
            return false;
        }
    }

    private static AuthenticationThrottleState LoadThrottleState()
    {
        try
        {
            return File.Exists(ThrottleStatePath)
                ? JsonSerializer.Deserialize<AuthenticationThrottleState>(File.ReadAllText(ThrottleStatePath))
                    ?? new AuthenticationThrottleState()
                : new AuthenticationThrottleState();
        }
        catch
        {
            return new AuthenticationThrottleState(FailureCount: 5, DateTimeOffset.UtcNow.AddSeconds(30));
        }
    }

    private static void RegisterAuthenticationFailure(AuthenticationThrottleState current)
    {
        int failures = Math.Min(current.FailureCount + 1, 20);
        int delaySeconds = Math.Min(300, 1 << Math.Min(failures, 8));
        SaveThrottleState(new AuthenticationThrottleState(failures, DateTimeOffset.UtcNow.AddSeconds(delaySeconds)));
    }

    private static void ResetAuthenticationThrottle() =>
        SaveThrottleState(new AuthenticationThrottleState());

    public static void ResetAuthenticationThrottleAfterProvisioning() =>
        ResetAuthenticationThrottle();

    private static void SaveThrottleState(AuthenticationThrottleState state)
    {
        Directory.CreateDirectory(ProtectionServiceManager.ProtectionDataDirectory);
        string temporary = $"{ThrottleStatePath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, ThrottleStatePath, overwrite: true);
            ProtectionServiceManager.HardenSensitiveFileAcl(ThrottleStatePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task AuditAsync(
        string eventName,
        string outcome,
        CancellationToken cancellationToken)
    {
        try
        {
            await new SecurityAuditLog().AppendAsync(eventName, outcome, cancellationToken);
        }
        catch
        {
            // Audit storage failure must reject the request path but must not stop Guardian.
        }
    }

    private static bool CredentialsEqual(AdminCredential left, AdminCredential right) =>
        left.Version == right.Version &&
        left.Iterations == right.Iterations &&
        string.Equals(left.SaltBase64, right.SaltBase64, StringComparison.Ordinal) &&
        string.Equals(left.HashBase64, right.HashBase64, StringComparison.Ordinal);

    private static async Task WriteEnrollmentAtomicallyAsync(
        GuardianEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        await WriteBytesAtomicallyAsync(
            ProtectionServiceManager.EnrollmentPath,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(enrollment)),
            cancellationToken);
        ProtectionServiceManager.HardenSensitiveFileAcl(ProtectionServiceManager.EnrollmentPath);
    }

    private static async Task WriteBytesAtomicallyAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string temporaryPath = $"{path}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint OpenService(nint serviceManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(
        nint service,
        int infoLevel,
        ref ServiceStatusProcess buffer,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(nint serviceHandle);

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const int ScStatusProcessInfo = 0;
    private const uint ServiceRunning = 0x00000004;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    private sealed record PolicyRequest(
        string Operation,
        string? Pin,
        string? SettingsBase64,
        string? RecoveryCode,
        AdminCredential? NewCredential,
        ManagerDeviceEnrollmentRequest? ManagerDeviceEnrollment = null,
        RecoveryChallenge? RecoveryChallenge = null,
        RecoveryChallengeResponse? RecoveryChallengeResponse = null,
        ManagerDeviceTransferRequest? ManagerDeviceTransfer = null);
    private sealed record PolicyChallenge(string NonceBase64, string SaltBase64, int Iterations);
    private sealed record AuthenticatedPolicyRequest(
        Guid RequestId,
        string IssuedAtUtc,
        string PayloadBase64,
        string MacBase64);
    private sealed record AuthenticationThrottleState(
        int FailureCount = 0,
        DateTimeOffset BlockedUntilUtc = default);
    private sealed record ConsumedManagerChallenge(
        string ChallengeId,
        DateTimeOffset ExpiresAtUtc);
    private sealed record AdministrativeActivityLease(DateTimeOffset ExpiresAtUtc);
}

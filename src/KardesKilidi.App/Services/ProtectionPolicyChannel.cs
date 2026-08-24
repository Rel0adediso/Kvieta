using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KardesKilidi.Core.Models;
using KardesKilidi.Core.Services;

namespace KardesKilidi.App.Services;

public static class ProtectionPolicyChannel
{
    private const string PipeName = "OtiumGuardian.Policy.v1";
    private static int _failedPinAttempts;
    private static DateTimeOffset _pinBlockedUntilUtc;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<bool> SyncAsync(string settingsJson, string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || string.IsNullOrWhiteSpace(pin))
        {
            return false;
        }

        try
        {
            using NamedPipeClientStream client = new(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            await client.ConnectAsync(timeout.Token);

            string settingsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));
            string request = JsonSerializer.Serialize(new PolicySyncRequest(pin, settingsBase64));
            using StreamWriter writer = new(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(client, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(request.AsMemory(), timeout.Token);
            string? response = await reader.ReadLineAsync(timeout.Token);
            return string.Equals(response, "OK", StringComparison.Ordinal);
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
                PipeSecurity security = new();
                security.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));
                security.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                server = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    2,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    security);
                await server.WaitForConnectionAsync(cancellationToken);
                await HandleClientAsync(server, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
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
        using StreamReader reader = new(server, Encoding.UTF8, leaveOpen: true);
        using StreamWriter writer = new(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        string? line = await reader.ReadLineAsync(cancellationToken);
        if (line is null || line.Length > 1_500_000)
        {
            await writer.WriteLineAsync("ERR");
            return;
        }

        PolicySyncRequest? request = JsonSerializer.Deserialize<PolicySyncRequest>(line);
        GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
        if (request is null || enrollment is null || DateTimeOffset.UtcNow < _pinBlockedUntilUtc)
        {
            await writer.WriteLineAsync("ERR");
            return;
        }

        if (!AdminPinService.Verify(request.Pin, enrollment.AdminPin))
        {
            _failedPinAttempts++;
            int delaySeconds = Math.Min(30, 1 << Math.Min(_failedPinAttempts, 5));
            _pinBlockedUntilUtc = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            await writer.WriteLineAsync("ERR");
            return;
        }

        _failedPinAttempts = 0;
        _pinBlockedUntilUtc = DateTimeOffset.MinValue;

        byte[] settingsBytes;
        ControlSettings? candidate;
        try
        {
            settingsBytes = Convert.FromBase64String(request.SettingsBase64);
            candidate = JsonSerializer.Deserialize<ControlSettings>(settingsBytes, JsonOptions);
        }
        catch
        {
            await writer.WriteLineAsync("ERR");
            return;
        }

        if (candidate is null || candidate.Mode != ControlMode.Protected || !candidate.AdminPin.IsConfigured)
        {
            await writer.WriteLineAsync("ERR");
            return;
        }

        string temporaryPath = ProtectionServiceManager.ProtectedSettingsPath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, settingsBytes, cancellationToken);
        File.Move(temporaryPath, ProtectionServiceManager.ProtectedSettingsPath, overwrite: true);

        GuardianEnrollment updatedEnrollment = enrollment with { AdminPin = candidate.AdminPin };
        string enrollmentTemporaryPath = ProtectionServiceManager.EnrollmentPath + ".tmp";
        await File.WriteAllTextAsync(
            enrollmentTemporaryPath,
            JsonSerializer.Serialize(updatedEnrollment),
            cancellationToken);
        File.Move(enrollmentTemporaryPath, ProtectionServiceManager.EnrollmentPath, overwrite: true);
        await writer.WriteLineAsync("OK");
    }

    private sealed record PolicySyncRequest(string Pin, string SettingsBase64);
}

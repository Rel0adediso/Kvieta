using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
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
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await client.ConnectAsync(timeout.Token);
            if (!IsLocalSystemServer(client))
            {
                return false;
            }

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
        string? line = await ReadBoundedLineAsync(reader, MaximumRequestCharacters, requestTimeout.Token);
        if (line is null)
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        PolicySyncRequest? request = JsonSerializer.Deserialize<PolicySyncRequest>(line);
        GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
        if (request is null || enrollment is null || DateTimeOffset.UtcNow < _pinBlockedUntilUtc)
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        bool pinVerified = AdminPinService.Verify(request.Pin, enrollment.AdminPin) ||
            enrollment.PreviousAdminPin is { IsConfigured: true } previousPin &&
            AdminPinService.Verify(request.Pin, previousPin);
        if (!pinVerified)
        {
            _failedPinAttempts++;
            int delaySeconds = Math.Min(30, 1 << Math.Min(_failedPinAttempts, 5));
            _pinBlockedUntilUtc = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
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
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
        }

        if (candidate is null || candidate.Mode != ControlMode.Protected || !candidate.AdminPin.IsConfigured)
        {
            await writer.WriteLineAsync("ERR".AsMemory(), requestTimeout.Token);
            return;
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

        await WriteBytesAtomicallyAsync(
            ProtectionServiceManager.ProtectedSettingsPath,
            settingsBytes,
            requestTimeout.Token);

        GuardianEnrollment updatedEnrollment = enrollment with
        {
            AdminPin = candidate.AdminPin,
            PreviousAdminPin = null
        };
        await WriteEnrollmentAtomicallyAsync(updatedEnrollment, requestTimeout.Token);
        await writer.WriteLineAsync("OK".AsMemory(), requestTimeout.Token);
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

    private static bool IsLocalSystemServer(NamedPipeClientStream client)
    {
        try
        {
            if (!GetNamedPipeServerProcessId(client.SafePipeHandle, out uint processId) || processId == 0)
            {
                return false;
            }

            using Process serverProcess = Process.GetProcessById(checked((int)processId));
            if (!OpenProcessToken(serverProcess.SafeHandle, TokenAccessLevels.Query, out SafeAccessTokenHandle token))
            {
                return false;
            }

            using (token)
            using (WindowsIdentity identity = new(token.DangerousGetHandle()))
            {
                SecurityIdentifier localSystem = new(WellKnownSidType.LocalSystemSid, null);
                return identity.User?.Equals(localSystem) == true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool CredentialsEqual(AdminCredential left, AdminCredential right) =>
        left.Version == right.Version &&
        left.Iterations == right.Iterations &&
        string.Equals(left.SaltBase64, right.SaltBase64, StringComparison.Ordinal) &&
        string.Equals(left.HashBase64, right.HashBase64, StringComparison.Ordinal);

    private static Task WriteEnrollmentAtomicallyAsync(
        GuardianEnrollment enrollment,
        CancellationToken cancellationToken) =>
        WriteBytesAtomicallyAsync(
            ProtectionServiceManager.EnrollmentPath,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(enrollment)),
            cancellationToken);

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

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        TokenAccessLevels desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    private sealed record PolicySyncRequest(string Pin, string SettingsBase64);
}

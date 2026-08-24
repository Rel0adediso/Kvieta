using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace KardesKilidi.App.Services;

public sealed class OtiumGuardianService : ServiceBase
{
    private const uint InvalidSessionId = 0xFFFFFFFF;
    private CancellationTokenSource? _cancellation;
    private Task? _worker;

    public OtiumGuardianService()
    {
        ServiceName = ProtectionServiceManager.ServiceName;
        CanStop = true;
        CanShutdown = true;
        AutoLog = false;
    }

    public static void RunService()
    {
        Run(new OtiumGuardianService());
    }

    protected override void OnStart(string[] args)
    {
        _cancellation = new CancellationTokenSource();
        _worker = Task.WhenAll(
            Task.Run(() => WatchAsync(_cancellation.Token)),
            Task.Run(() => ProtectionPolicyChannel.RunServerAsync(_cancellation.Token)));
    }

    protected override void OnStop()
    {
        StopWorker();
    }

    protected override void OnShutdown()
    {
        StopWorker();
        base.OnShutdown();
    }

    private void StopWorker()
    {
        _cancellation?.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Service shutdown should continue even if a poll was interrupted.
        }

        _worker = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private static async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                EnsureProtectedSession();
            }
            catch
            {
                // The next short poll retries transient logon and desktop transitions.
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static void EnsureProtectedSession()
    {
        GuardianEnrollment? enrollment = ProtectionServiceManager.LoadEnrollment();
        if (enrollment is null)
        {
            return;
        }

        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == InvalidSessionId ||
            !TryGetUserEnvironment(sessionId, out UserEnvironment? user) ||
            user is null)
        {
            return;
        }

        using (user)
        {
            if (!string.Equals(user.UserSid, enrollment.UserSid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (HasTrackedGuardian((int)sessionId))
            {
                return;
            }

            int? processId = LaunchProtectedSession(user);
            if (processId is null)
            {
                return;
            }

            try
            {
                using Process process = Process.GetProcessById(processId.Value);
                WriteProcessState(new GuardianProcessState(
                    process.Id,
                    process.SessionId,
                    process.StartTime.ToUniversalTime().Ticks));
            }
            catch
            {
                // The launched process exited before it could be tracked; retry next poll.
            }
        }
    }

    private static bool HasTrackedGuardian(int sessionId)
    {
        try
        {
            if (!File.Exists(ProtectionServiceManager.ProcessStatePath))
            {
                return false;
            }

            GuardianProcessState? state = JsonSerializer.Deserialize<GuardianProcessState>(
                File.ReadAllText(ProtectionServiceManager.ProcessStatePath));
            if (state is null || state.SessionId != sessionId)
            {
                return false;
            }

            using Process process = Process.GetProcessById(state.ProcessId);
            string? executablePath = process.MainModule?.FileName;
            return !process.HasExited &&
                process.SessionId == sessionId &&
                process.StartTime.ToUniversalTime().Ticks == state.StartTimeUtcTicks &&
                string.Equals(
                    Path.GetFullPath(executablePath ?? string.Empty),
                    Path.GetFullPath(ProtectionServiceManager.InstalledExecutablePath),
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int? LaunchProtectedSession(UserEnvironment user)
    {
        string executable = ProtectionServiceManager.InstalledExecutablePath;
        if (!File.Exists(executable))
        {
            return null;
        }

        if (!DuplicateTokenEx(
                user.Token,
                0x02000000,
                IntPtr.Zero,
                SecurityImpersonationLevel.SecurityIdentification,
                TokenType.TokenPrimary,
                out SafeAccessTokenHandle primaryToken))
        {
            return null;
        }

        using (primaryToken)
        {
            if (!CreateEnvironmentBlock(out IntPtr environment, primaryToken, false))
            {
                return null;
            }

            try
            {
                StartupInfo startupInfo = new()
                {
                    Cb = Marshal.SizeOf<StartupInfo>(),
                    Desktop = "winsta0\\default"
                };
                string commandLine = $"\"{executable}\" --guardian-session";
                if (CreateProcessAsUser(
                        primaryToken,
                        executable,
                        new StringBuilder(commandLine),
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        0x00000400,
                        environment,
                        Path.GetDirectoryName(executable),
                        ref startupInfo,
                        out ProcessInformation processInformation))
                {
                    CloseHandle(processInformation.Process);
                    CloseHandle(processInformation.Thread);
                    return processInformation.ProcessId;
                }
            }
            finally
            {
                DestroyEnvironmentBlock(environment);
            }
        }

        return null;
    }

    private static bool TryGetUserEnvironment(uint sessionId, out UserEnvironment? environment)
    {
        environment = null;
        if (!WTSQueryUserToken(sessionId, out SafeAccessTokenHandle token))
        {
            return false;
        }

        if (!CreateEnvironmentBlock(out IntPtr block, token, false))
        {
            token.Dispose();
            return false;
        }

        try
        {
            string? localAppData = ReadEnvironmentVariable(block, "LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                token.Dispose();
                return false;
            }

            using WindowsIdentity identity = new(token.DangerousGetHandle());
            string? userSid = identity.User?.Value;
            if (string.IsNullOrWhiteSpace(userSid))
            {
                token.Dispose();
                return false;
            }

            environment = new UserEnvironment(token, localAppData, userSid);
            return true;
        }
        finally
        {
            DestroyEnvironmentBlock(block);
        }
    }

    private static string? ReadEnvironmentVariable(IntPtr block, string name)
    {
        IntPtr cursor = block;
        while (true)
        {
            string? entry = Marshal.PtrToStringUni(cursor);
            if (string.IsNullOrEmpty(entry))
            {
                return null;
            }

            if (entry.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
            {
                return entry[(name.Length + 1)..];
            }

            cursor = IntPtr.Add(cursor, (entry.Length + 1) * sizeof(char));
        }
    }

    private static void WriteProcessState(GuardianProcessState state)
    {
        Directory.CreateDirectory(ProtectionServiceManager.ProtectionDataDirectory);
        string temporaryPath = ProtectionServiceManager.ProcessStatePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
        File.Move(temporaryPath, ProtectionServiceManager.ProcessStatePath, overwrite: true);
    }

    private sealed class UserEnvironment(SafeAccessTokenHandle token, string localAppData, string userSid) : IDisposable
    {
        public SafeAccessTokenHandle Token { get; } = token;
        public string LocalAppData { get; } = localAppData;
        public string UserSid { get; } = userSid;
        public void Dispose() => Token.Dispose();
    }

    private sealed record GuardianProcessState(int ProcessId, int SessionId, long StartTimeUtcTicks);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr ReservedPointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }

    private enum SecurityImpersonationLevel { SecurityAnonymous, SecurityIdentification, SecurityImpersonation, SecurityDelegation }
    private enum TokenType { TokenPrimary = 1, TokenImpersonation }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(SafeAccessTokenHandle existingToken, uint desiredAccess, IntPtr tokenAttributes,
        SecurityImpersonationLevel impersonationLevel, TokenType tokenType, out SafeAccessTokenHandle newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token, bool inherit);

    [DllImport("userenv.dll")]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessAsUser(SafeAccessTokenHandle token, string? applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment,
        string? currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

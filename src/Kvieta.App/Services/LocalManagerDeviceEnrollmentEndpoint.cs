using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Kvieta.Core.Models;
using Kvieta.Core.Services;

namespace Kvieta.App.Services;

public sealed class LocalManagerDeviceEnrollmentEndpoint : IAsyncDisposable
{
    private readonly LocalNetworkHttpServer _server;
    private readonly Func<ManagerDeviceEnrollmentRequest, Task<bool>> _enroll;
    private readonly string _routeToken;
    private readonly CancellationTokenSource _expirationCancellation = new();
    private readonly Task _expirationTask;
    private ManagerDeviceEnrollmentRequest? _pendingRequest;
    private int _requestConsumed;

    private LocalManagerDeviceEnrollmentEndpoint(
        LocalNetworkHttpServer server,
        string routeToken,
        DateTimeOffset expiresAtUtc,
        Func<ManagerDeviceEnrollmentRequest, Task<bool>> enroll)
    {
        _server = server;
        _routeToken = routeToken;
        ExpiresAtUtc = expiresAtUtc;
        _enroll = enroll;
        PairingUri = new Uri($"http://{server.Address}:{server.Port}/{routeToken}/");
        _expirationTask = StopWhenExpiredAsync(_expirationCancellation.Token);
    }

    public Uri PairingUri { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public event Action<string, string>? EnrollmentProposed;

    public static LocalManagerDeviceEnrollmentEndpoint Start(
        Func<ManagerDeviceEnrollmentRequest, Task<bool>> enroll,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enroll);
        string routeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        LocalManagerDeviceEnrollmentEndpoint? endpoint = null;
        LocalNetworkHttpServer server = LocalNetworkHttpServer.Start(
            (request, cancellationToken) => endpoint!.HandleRequestAsync(request, cancellationToken));
        endpoint = new LocalManagerDeviceEnrollmentEndpoint(
            server,
            routeToken,
            now.ToUniversalTime().AddMinutes(2),
            enroll);
        return endpoint;
    }

    private async Task<LocalNetworkHttpResponse> HandleRequestAsync(
        LocalNetworkHttpRequest request,
        CancellationToken cancellationToken)
    {
        string sitePath = $"/{_routeToken}/";
        string apiPath = $"/{_routeToken}/api";
        if (string.Equals(request.Path, sitePath, StringComparison.Ordinal))
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return new LocalNetworkHttpResponse(HttpStatusCode.MethodNotAllowed);
            }

            return new LocalNetworkHttpResponse(
                HttpStatusCode.OK,
                TextPayload: LocalCompanionSiteContent.CreateHtml(new Uri(PairingUri, "api").AbsoluteUri),
                ContentType: "text/html; charset=utf-8");
        }

        if (!string.Equals(request.Path, apiPath, StringComparison.Ordinal))
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.NotFound);
        }

        if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.OK, new
            {
                service = "kvieta-enrollment",
                expiresAtUtc = ExpiresAtUtc
            });
        }

        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.MethodNotAllowed);
        }

        ManagerDeviceEnrollmentRequest? enrollmentRequest;
        try
        {
            enrollmentRequest = JsonSerializer.Deserialize<ManagerDeviceEnrollmentRequest>(request.Body);
        }
        catch (JsonException)
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.BadRequest);
        }

        if (enrollmentRequest is null ||
            !ManagerDeviceEnrollmentService.VerifyRequest(enrollmentRequest, DateTimeOffset.UtcNow) ||
            Volatile.Read(ref _requestConsumed) != 0 ||
            Interlocked.CompareExchange(ref _pendingRequest, enrollmentRequest, null) is not null)
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized);
        }

        string verificationCode = ManagerDeviceVerificationCode.ForEnrollmentRequest(enrollmentRequest);
        EnrollmentProposed?.Invoke(verificationCode, enrollmentRequest.Enrollment.DeviceName);
        return new LocalNetworkHttpResponse(HttpStatusCode.OK, new
        {
            accepted = false,
            pendingComputerConfirmation = true,
            verificationCode
        });
    }

    public async Task<bool> ConfirmPendingAsync()
    {
        ManagerDeviceEnrollmentRequest? request = Volatile.Read(ref _pendingRequest);
        if (request is null ||
            Interlocked.CompareExchange(ref _requestConsumed, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            return await _enroll(request);
        }
        catch
        {
            return false;
        }
        finally
        {
            _server.Stop();
        }
    }

    public void RejectPending()
    {
        Interlocked.Exchange(ref _requestConsumed, 1);
        _server.Stop();
    }

    private async Task StopWhenExpiredAsync(CancellationToken cancellationToken)
    {
        TimeSpan remaining = ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            try { await Task.Delay(remaining, cancellationToken); }
            catch (OperationCanceledException) { return; }
        }
        _server.Stop();
    }

    public async ValueTask DisposeAsync()
    {
        _expirationCancellation.Cancel();
        await _server.DisposeAsync();
        try { await _expirationTask; } catch (OperationCanceledException) { }
        _expirationCancellation.Dispose();
    }
}

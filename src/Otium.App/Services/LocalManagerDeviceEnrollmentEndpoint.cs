using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public sealed class LocalManagerDeviceEnrollmentEndpoint : IAsyncDisposable
{
    private readonly LocalNetworkHttpServer _server;
    private readonly Func<ManagerDeviceEnrollmentRequest, Task<bool>> _enroll;
    private readonly string _routeToken;
    private readonly CancellationTokenSource _expirationCancellation = new();
    private readonly Task _expirationTask;
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
        VerificationCode = RandomNumberGenerator.GetInt32(1_000_000)
            .ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        _expirationTask = StopWhenExpiredAsync(_expirationCancellation.Token);
    }

    public Uri PairingUri { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public string VerificationCode { get; }

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
                service = "otium-enrollment",
                expiresAtUtc = ExpiresAtUtc,
                verificationCode = VerificationCode
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
            Interlocked.CompareExchange(ref _requestConsumed, 1, 0) != 0)
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized);
        }

        bool accepted = await _enroll(enrollmentRequest);
        return new LocalNetworkHttpResponse(
            accepted ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            new { accepted },
            StopAfterResponse: true);
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

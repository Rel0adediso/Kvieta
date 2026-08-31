using System.Net;
using System.Security.Cryptography;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public sealed class LocalRecoveryEndpoint : IAsyncDisposable
{
    private readonly LocalNetworkHttpServer _server;
    private readonly ManagerDeviceEnrollment _enrollment;
    private readonly RecoveryChallenge _challenge;
    private readonly Func<RecoveryChallengeResponse, Task<bool>> _authorizeResponse;
    private readonly string _routeToken;
    private readonly CancellationTokenSource _expirationCancellation = new();
    private readonly Task _expirationTask;
    private int _challengeConsumed;

    private LocalRecoveryEndpoint(
        LocalNetworkHttpServer server,
        ManagerDeviceEnrollment enrollment,
        RecoveryChallenge challenge,
        Func<RecoveryChallengeResponse, Task<bool>> authorizeResponse,
        string routeToken)
    {
        _server = server;
        _enrollment = enrollment;
        _challenge = challenge;
        _authorizeResponse = authorizeResponse;
        _routeToken = routeToken;
        RecoveryUri = new Uri($"http://{server.Address}:{server.Port}/{routeToken}/");
        _expirationTask = StopWhenExpiredAsync(_expirationCancellation.Token);
    }

    public Uri RecoveryUri { get; }

    public static LocalRecoveryEndpoint Start(
        ManagerDeviceEnrollment enrollment,
        Func<RecoveryChallengeResponse, Task<bool>> authorizeResponse,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        return Start(
            enrollment,
            new RecoveryChallengeService().Issue(enrollment.DeviceId, now),
            authorizeResponse,
            now);
    }

    public static LocalRecoveryEndpoint Start(
        ManagerDeviceEnrollment enrollment,
        RecoveryChallenge challenge,
        Func<RecoveryChallengeResponse, Task<bool>> authorizeResponse,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(authorizeResponse);
        DateTimeOffset utcNow = now.ToUniversalTime();
        if (!enrollment.IsActive ||
            !string.Equals(enrollment.DeviceId, challenge.DeviceId, StringComparison.Ordinal) ||
            challenge.ExpiresAtUtc <= utcNow ||
            challenge.ExpiresAtUtc > utcNow.AddMinutes(10))
        {
            throw new InvalidOperationException("The recovery challenge or manager device is invalid.");
        }

        string routeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        LocalRecoveryEndpoint? endpoint = null;
        LocalNetworkHttpServer server = LocalNetworkHttpServer.Start(
            (request, cancellationToken) => endpoint!.HandleRequestAsync(request, cancellationToken));
        endpoint = new LocalRecoveryEndpoint(
            server,
            enrollment,
            challenge,
            authorizeResponse,
            routeToken);
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
                TextPayload: LocalCompanionSiteContent.CreateHtml(new Uri(RecoveryUri, "api").AbsoluteUri),
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
                service = "otium-recovery",
                challenge = _challenge,
                expiresAtUtc = _challenge.ExpiresAtUtc
            });
        }

        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.MethodNotAllowed);
        }

        RecoveryChallengeResponse? response;
        try
        {
            response = System.Text.Json.JsonSerializer.Deserialize<RecoveryChallengeResponse>(request.Body);
        }
        catch (System.Text.Json.JsonException)
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.BadRequest);
        }

        bool authorized = response is not null &&
            ManagerDeviceAuthorizationService.VerifyResponse(
                _enrollment,
                _challenge,
                response,
                DateTimeOffset.UtcNow) &&
            Interlocked.CompareExchange(ref _challengeConsumed, 1, 0) == 0;
        if (!authorized)
        {
            return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized);
        }

        bool accepted = await _authorizeResponse(response!);
        return new LocalNetworkHttpResponse(
            accepted ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            new { accepted },
            StopAfterResponse: true);
    }

    private async Task StopWhenExpiredAsync(CancellationToken cancellationToken)
    {
        TimeSpan remaining = _challenge.ExpiresAtUtc - DateTimeOffset.UtcNow;
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

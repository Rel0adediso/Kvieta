using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public sealed class LocalManagerDeviceTransferEndpoint : IAsyncDisposable
{
    private sealed record CurrentDeviceApproval(string CurrentDeviceSignatureBase64);

    private readonly LocalNetworkHttpServer _server;
    private readonly ManagerDeviceEnrollment _current;
    private readonly Func<ManagerDeviceTransferRequest, Task<bool>> _transfer;
    private readonly string _routeToken;
    private readonly CancellationTokenSource _expirationCancellation = new();
    private readonly Task _expirationTask;
    private ManagerDeviceTransferRequest? _pending;
    private int _requestConsumed;

    private LocalManagerDeviceTransferEndpoint(
        LocalNetworkHttpServer server,
        ManagerDeviceEnrollment current,
        Func<ManagerDeviceTransferRequest, Task<bool>> transfer,
        string routeToken,
        DateTimeOffset expiresAtUtc)
    {
        _server = server;
        _current = current;
        _transfer = transfer;
        _routeToken = routeToken;
        ExpiresAtUtc = expiresAtUtc;
        TransferUri = new Uri($"http://{server.Address}:{server.Port}/{routeToken}/");
        VerificationCode = RandomNumberGenerator.GetInt32(1_000_000)
            .ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        _expirationTask = StopWhenExpiredAsync(_expirationCancellation.Token);
    }

    public Uri TransferUri { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public string VerificationCode { get; }

    public static LocalManagerDeviceTransferEndpoint Start(
        ManagerDeviceEnrollment current,
        Func<ManagerDeviceTransferRequest, Task<bool>> transfer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(transfer);
        if (!current.IsActive) throw new InvalidOperationException("The manager device is not active.");
        string routeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        LocalManagerDeviceTransferEndpoint? endpoint = null;
        LocalNetworkHttpServer server = LocalNetworkHttpServer.Start(
            (request, cancellationToken) => endpoint!.HandleRequestAsync(request, cancellationToken));
        endpoint = new LocalManagerDeviceTransferEndpoint(
            server, current, transfer, routeToken, now.ToUniversalTime().AddMinutes(5));
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
                return new LocalNetworkHttpResponse(HttpStatusCode.MethodNotAllowed);
            return new LocalNetworkHttpResponse(
                HttpStatusCode.OK,
                TextPayload: LocalCompanionSiteContent.CreateHtml(new Uri(TransferUri, "api").AbsoluteUri),
                ContentType: "text/html; charset=utf-8");
        }

        if (!string.Equals(request.Path, apiPath, StringComparison.Ordinal))
            return new LocalNetworkHttpResponse(HttpStatusCode.NotFound);
        if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            ManagerDeviceTransferRequest? pending = Volatile.Read(ref _pending);
            return new LocalNetworkHttpResponse(HttpStatusCode.OK, pending is null
                ? new
                {
                    service = "otium-transfer-new",
                    currentDeviceId = _current.DeviceId,
                    expiresAtUtc = ExpiresAtUtc,
                    verificationCode = VerificationCode
                }
                : (object)new
                {
                    service = "otium-transfer-current",
                    request = pending,
                    expiresAtUtc = ExpiresAtUtc,
                    verificationCode = VerificationCode
                });
        }

        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            return new LocalNetworkHttpResponse(HttpStatusCode.MethodNotAllowed);

        ManagerDeviceTransferRequest? existing = Volatile.Read(ref _pending);
        if (existing is null)
        {
            ManagerDeviceTransferRequest? proposal;
            try { proposal = JsonSerializer.Deserialize<ManagerDeviceTransferRequest>(request.Body); }
            catch (JsonException) { return new LocalNetworkHttpResponse(HttpStatusCode.BadRequest); }
            if (proposal is null ||
                !ManagerDeviceTransferService.VerifyNewDeviceProposal(
                    _current, proposal.Replacement, proposal.Transfer, DateTimeOffset.UtcNow) ||
                Interlocked.CompareExchange(ref _pending, proposal, null) is not null)
            {
                return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized);
            }
            return new LocalNetworkHttpResponse(HttpStatusCode.OK, new { accepted = true, next = "current-device" });
        }

        CurrentDeviceApproval? approval;
        try { approval = JsonSerializer.Deserialize<CurrentDeviceApproval>(request.Body); }
        catch (JsonException) { return new LocalNetworkHttpResponse(HttpStatusCode.BadRequest); }
        if (approval is null || Interlocked.CompareExchange(ref _requestConsumed, 1, 0) != 0)
            return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized);

        ManagerDeviceTransferRequest completed = existing with
        {
            Transfer = existing.Transfer with
            {
                CurrentDeviceSignatureBase64 = approval.CurrentDeviceSignatureBase64
            }
        };
        if (ManagerDeviceTransferService.CompleteTransfer(
                _current, completed.Replacement, completed.Transfer, DateTimeOffset.UtcNow) is null)
            return new LocalNetworkHttpResponse(HttpStatusCode.Unauthorized, StopAfterResponse: true);

        bool accepted = await _transfer(completed);
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

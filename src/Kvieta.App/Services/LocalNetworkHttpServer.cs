using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Kvieta.App.Services;

internal sealed record LocalNetworkHttpRequest(string Method, string Path, string Body);
internal sealed record LocalNetworkHttpResponse(
    HttpStatusCode Status,
    object? Payload = null,
    bool StopAfterResponse = false,
    string? TextPayload = null,
    string ContentType = "application/json; charset=utf-8");

internal sealed class LocalNetworkHttpServer : IAsyncDisposable
{
    internal const int FirstCompanionPort = 24873;
    internal const int LastCompanionPort = 24882;
    private const int MaximumHeaderBytes = 8 * 1024;
    private const int MaximumBodyBytes = 16 * 1024;
    private readonly TcpListener _listener;
    private readonly Func<LocalNetworkHttpRequest, CancellationToken, Task<LocalNetworkHttpResponse>> _handler;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _serveTask;

    private LocalNetworkHttpServer(
        TcpListener listener,
        IPAddress advertisedAddress,
        Func<LocalNetworkHttpRequest, CancellationToken, Task<LocalNetworkHttpResponse>> handler)
    {
        _listener = listener;
        _handler = handler;
        IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
        Address = advertisedAddress;
        Port = endpoint.Port;
        _serveTask = ServeAsync(_cancellation.Token);
    }

    public IPAddress Address { get; }
    public int Port { get; }

    public static LocalNetworkHttpServer Start(
        Func<LocalNetworkHttpRequest, CancellationToken, Task<LocalNetworkHttpResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        IPAddress advertisedAddress = GetLocalIpv4Address();
        SocketException? lastError = null;
        for (int port = FirstCompanionPort; port <= LastCompanionPort; port++)
        {
            TcpListener listener = new(IPAddress.Any, port)
            {
                ExclusiveAddressUse = true
            };
            try
            {
                listener.Start(backlog: 4);
                return new LocalNetworkHttpServer(listener, advertisedAddress, handler);
            }
            catch (SocketException exception)
            {
                lastError = exception;
                listener.Stop();
            }
        }

        throw new InvalidOperationException(
            $"Kvieta local pairing ports {FirstCompanionPort}-{LastCompanionPort} are unavailable.",
            lastError);
    }

    public void Stop()
    {
        _cancellation.Cancel();
        _listener.Stop();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                using CancellationTokenSource requestTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(12));
                await HandleClientAsync(client, requestTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Malformed and disconnected clients must not stop the one-time endpoint.
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        using NetworkStream stream = client.GetStream();
        LocalNetworkHttpRequest? request = await ReadRequestAsync(stream, cancellationToken);
        LocalNetworkHttpResponse response;
        if (request is null)
        {
            response = new LocalNetworkHttpResponse(HttpStatusCode.BadRequest);
        }
        else
        {
            try
            {
                response = await _handler(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                response = new LocalNetworkHttpResponse(HttpStatusCode.InternalServerError);
            }
        }

        await WriteResponseAsync(stream, response, cancellationToken);
        if (response.StopAfterResponse) Stop();
    }

    private static async Task<LocalNetworkHttpRequest?> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        List<byte> headerBytes = [];
        while (headerBytes.Count < MaximumHeaderBytes)
        {
            byte[] single = new byte[1];
            if (await stream.ReadAsync(single, cancellationToken) == 0) return null;
            headerBytes.Add(single[0]);
            int count = headerBytes.Count;
            if (count >= 4 &&
                headerBytes[count - 4] == '\r' && headerBytes[count - 3] == '\n' &&
                headerBytes[count - 2] == '\r' && headerBytes[count - 1] == '\n')
            {
                break;
            }
        }

        if (headerBytes.Count < 4 || headerBytes.Count >= MaximumHeaderBytes) return null;
        string[] lines = Encoding.ASCII.GetString(headerBytes.ToArray())
            .Split("\r\n", StringSplitOptions.None);
        string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length != 3 || requestLine[2] is not ("HTTP/1.1" or "HTTP/1.0")) return null;

        int contentLength = 0;
        foreach (string line in lines.Skip(1))
        {
            if (line.Length == 0) break;
            int separator = line.IndexOf(':');
            if (separator <= 0) return null;
            string name = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                (!int.TryParse(value, out contentLength) || contentLength < 0 || contentLength > MaximumBodyBytes))
            {
                return null;
            }
        }

        byte[] body = new byte[contentLength];
        int offset = 0;
        while (offset < body.Length)
        {
            int read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken);
            if (read == 0) return null;
            offset += read;
        }

        string path;
        try
        {
            path = new Uri("http://local" + requestLine[1]).AbsolutePath;
        }
        catch (UriFormatException)
        {
            return null;
        }

        return new LocalNetworkHttpRequest(
            requestLine[0],
            path,
            new UTF8Encoding(false, true).GetString(body));
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        LocalNetworkHttpResponse response,
        CancellationToken cancellationToken)
    {
        byte[] payload = response.TextPayload is not null
            ? Encoding.UTF8.GetBytes(response.TextPayload)
            : response.Payload is null
                ? []
                : JsonSerializer.SerializeToUtf8Bytes(response.Payload);
        string reason = response.Status switch
        {
            HttpStatusCode.OK => "OK",
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.MethodNotAllowed => "Method Not Allowed",
            HttpStatusCode.RequestEntityTooLarge => "Content Too Large",
            _ => "Internal Server Error"
        };
        string headers =
            $"HTTP/1.1 {(int)response.Status} {reason}\r\n" +
            $"Content-Type: {response.ContentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Referrer-Policy: no-referrer\r\n" +
            "Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; base-uri 'none'; frame-ancestors 'none'; form-action 'none'\r\n" +
            "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
    }

    private static IPAddress GetLocalIpv4Address()
    {
        IPAddress? address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and
                    not NetworkInterfaceType.Tunnel)
            .Select(adapter => new
            {
                Adapter = adapter,
                Properties = adapter.GetIPProperties(),
                IsVirtual = adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase) ||
                            adapter.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(item => item.IsVirtual ? 1 : 0)
            .ThenByDescending(item => item.Properties.GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                !gateway.Address.Equals(IPAddress.Any)))
            .ThenByDescending(item => item.Adapter.Speed)
            .SelectMany(item => item.Properties.UnicastAddresses)
            .Select(item => item.Address)
            .Where(item => item.AddressFamily == AddressFamily.InterNetwork && IsUsableLanAddress(item))
            .OrderByDescending(IsPrivate)
            .FirstOrDefault();

        if (address is not null)
        {
            return address;
        }

        IPAddress? routeAddress = TryGetRouteSelectedIpv4Address();
        if (routeAddress is not null && IsUsableLanAddress(routeAddress))
        {
            return routeAddress;
        }

        throw new InvalidOperationException(
            "No LAN IPv4 address is available. Connect this computer to the same private Wi-Fi or local network as the phone.");
    }

    private static IPAddress? TryGetRouteSelectedIpv4Address()
    {
        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("1.1.1.1", 53);
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUsableLanAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] is not (0 or >= 224) &&
            !(bytes[0] == 169 && bytes[1] == 254);
    }

    private static bool IsPrivate(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
            bytes[0] == 192 && bytes[1] == 168 ||
            bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        try { await _serveTask; } catch (OperationCanceledException) { }
        _cancellation.Dispose();
    }
}

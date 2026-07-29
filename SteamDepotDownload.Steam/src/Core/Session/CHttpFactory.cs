using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace SteamDepotDownload.Steam.Core.Session;

internal static class CHttpFactory
{
    private static readonly string UserAgent =
        $"SteamDepotDownload/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    internal static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectIPv4Async,
        };

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private static async ValueTask<Stream> ConnectIPv4Async(SocketsHttpConnectionContext context,
        CancellationToken ct)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, ct).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

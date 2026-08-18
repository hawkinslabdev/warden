using System.Net;
using System.Net.Sockets;

namespace Warden.Configuration;

public static class PortAvailabilityChecker
{
    // Probes each port before Kestrel binds, so a conflict fails fast instead of throwing from app.Run().
    public static bool TryEnsureUrlsAvailable(IEnumerable<string> urls, out int conflictingPort)
    {
        foreach (var rawUrl in urls)
        {
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)) continue;
            if (uri.Scheme is not ("http" or "https")) continue;

            var address = uri.Host is "localhost" or "0.0.0.0" or "*" or "+"
                ? IPAddress.Loopback
                : IPAddress.Parse(uri.Host);

            try
            {
                using var probe = new TcpListener(address, uri.Port);
                probe.Start();
                probe.Stop();
            }
            catch (SocketException)
            {
                conflictingPort = uri.Port;
                return false;
            }
        }

        conflictingPort = 0;
        return true;
    }
}

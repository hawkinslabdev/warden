using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace Warden.Configuration;

/// <summary>Which proxies may set X-Forwarded-For, bound from <c>Proxy</c>.</summary>
public sealed class ProxyOptions
{
    /// <summary>Proxy addresses or CIDR networks to trust, e.g. <c>10.0.0.0/8</c>. Loopback is always trusted.</summary>
    public string[] Trusted { get; set; } = [];

    /// <summary>Trusts whatever forwarded the request. Only safe when nothing but your proxy can reach the port.</summary>
    public bool TrustAny { get; set; }
}

public static class ForwardedHeaderSetup
{
    /// <summary>Rate limits partition on caller IP, which is the proxy's own unless this header is honoured.</summary>
    public static void Configure(ForwardedHeadersOptions options, ProxyOptions proxy)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        if (proxy.TrustAny)
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.ForwardLimit = null;
            Log.Warning("Proxy:TrustAny is on: any caller may set X-Forwarded-For, so bind only to your proxy");
            return;
        }

        foreach (var entry in proxy.Trusted)
        {
            var trimmed = entry.Trim();
            if (System.Net.IPNetwork.TryParse(trimmed, out var network))
                options.KnownIPNetworks.Add(network);
            else if (IPAddress.TryParse(trimmed, out var address))
                options.KnownProxies.Add(address);
            else
                Log.Warning("Proxy:Trusted entry {Entry} is not an IP address or CIDR network; ignored", entry);
        }
    }
}

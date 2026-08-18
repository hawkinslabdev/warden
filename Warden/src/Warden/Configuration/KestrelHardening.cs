using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Warden.Configuration;

public static class KestrelHardening
{
    public static void Configure(KestrelServerOptions serverOptions)
    {
        serverOptions.AddServerHeader = false;
        serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
        serverOptions.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
        serverOptions.Limits.MaxConcurrentConnections = 1000;
        serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
        serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
        serverOptions.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
        serverOptions.Limits.Http2.MaxStreamsPerConnection = 100;
        serverOptions.Limits.Http2.MaxFrameSize = 16 * 1024;
        serverOptions.Limits.Http2.InitialConnectionWindowSize = 128 * 1024;
        serverOptions.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
        serverOptions.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(60);
    }
}

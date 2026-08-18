namespace Warden.Endpoints;

internal static class HttpVerbs
{
    /// <summary>MapGet alone answers HEAD with 405, which trips uptime monitors and link checkers.</summary>
    public static readonly string[] GetAndHead = ["GET", "HEAD"];
}

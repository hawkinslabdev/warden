using Microsoft.AspNetCore.Http.HttpResults;
using Warden.Models;
using Warden.Services;

namespace Warden.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // No rate limit: an uptime probe should be free to poll.
        app.MapMethods("/health", HttpVerbs.GetAndHead, GetHealth);
        return app;
    }

    /// <summary>503 until content has been built, so a failed rebuild is visible to an uptime probe.</summary>
    internal static async Task<IResult> GetHealth(ContentService content, CancellationToken cancellationToken)
    {
        var pages = (await content.GetAllPagesAsync(cancellationToken)).Count;
        var response = new HealthResponse(
            Status: pages > 0 ? "ok" : "empty",
            BuildVersion: content.BuildVersion,
            Pages: pages,
            UptimeSeconds: (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds);

        return pages > 0
            ? TypedResults.Ok(response)
            : TypedResults.Json(response, statusCode: 503);
    }

    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;
}

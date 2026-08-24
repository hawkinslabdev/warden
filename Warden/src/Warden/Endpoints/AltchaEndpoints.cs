using Ixnas.AltchaNet;
using Warden.Configuration;

namespace Warden.Endpoints;

internal static class AltchaEndpoints
{
    public static IEndpointRouteBuilder MapAltchaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/altcha");
        group.MapGet("/challenge", (AltchaService service) => Results.Json(service.Generate()));
        group.MapPost("/verify", VerifyAsync);
        return app;
    }

    private static async Task<IResult> VerifyAsync(HttpContext context, AltchaService service)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var result = await service.Validate(form["altcha"].ToString(), context.RequestAborted);
        if (!result.IsValid)
            return Results.BadRequest();

        context.Response.Cookies.Append(AltchaGate.CookieName, AltchaGate.IssueSessionCookieValue(context), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = AltchaGate.SessionLifetime,
            Path = "/",
        });
        return Results.Ok();
    }
}

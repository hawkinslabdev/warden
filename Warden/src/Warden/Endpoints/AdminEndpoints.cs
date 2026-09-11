using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Antiforgery;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;
using Warden.Services.Admin;
using Warden.Services.Layout;
using Warden.Services.Rendering;

namespace Warden.Endpoints;

internal static class AdminEndpoints
{
    private const int MaxWebhooks = 20;
    private const int MaxBodyLength = 20_000;
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 300;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app, AuthOptions auth)
    {
        var admin = app.MapGroup(auth.AdminPath)
            .RequireAuthorization(AuthEndpoints.AdminPolicy)
            .RequireRateLimiting(RateLimitPolicies.Admin);

        // ReadFormAsync skips UseAntiforgery's automatic validation
        admin.AddEndpointFilter(async (context, next) =>
        {
            if (!HttpMethods.IsPost(context.HttpContext.Request.Method))
                return await next(context);
            try
            {
                await context.HttpContext.RequestServices
                    .GetRequiredService<IAntiforgery>()
                    .ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Back(context.HttpContext, "stale-form");
            }
            return await next(context);
        });

        admin.MapGet("", RenderAdmin);
        // one form, one write: monitors, their order, webhooks and timing land together or not at all
        admin.MapPost("/settings", SaveSettings);
        admin.MapPost("/reset", ResetMonitor);
        admin.MapPost("/incident", ComposeIncident);
        return app;
    }

    private static Task RenderAdmin(HttpContext ctx, ContentService content, HeartbeatStore store, PageResponder responder, AuthOptions auth, IAntiforgery antiforgery) =>
        WriteAdminAsync(ctx, content, store, responder, auth, antiforgery, ctx.Request.Query["flash"].ToString(), draft: null);

    private static async Task WriteAdminAsync(HttpContext ctx, ContentService content, HeartbeatStore store, PageResponder responder, AuthOptions auth, IAntiforgery antiforgery, string? flash, string? draft)
    {
        var html = AdminPage.Render(
            content.SiteConfig?.Monitoring,
            store,
            auth,
            responder.BasePath,
            antiforgery.GetAndStoreTokens(ctx),
            Subject(ctx),
            flash,
            draft);

        await responder.WriteAsync(ctx, new PageView(
            Title: Localization.Current.AdminTitle,
            ContentHtml: html,
            CanonicalPath: auth.AdminPath.TrimStart('/'),
            // a settings surface, not an article: no 680px reading measure or 1.125rem/1.7 prose rhythm
            Prose: false,
            NoIndex: true,
            NoStore: true));
    }

    private static async Task<IResult> SaveSettings(HttpContext ctx, AdminConfigWriter writer, ContentService content, CancellationToken ct)
    {
        var form = await ctx.Request.ReadFormAsync(ct);

        // parsed and validated before the file is touched, so a rejected value never half-writes the config
        var edits = new List<MonitorEdit>();
        foreach (var raw in form["monitorId"])
        {
            if (raw is not { Length: > 0 } id)
                continue;
            edits.Add(new MonitorEdit(
                id,
                Enabled: form[$"enabled:{id}"] == "on",
                Hidden: form[$"hidden:{id}"] == "on",
                Retries: ParseBoundedInt(form[$"retries:{id}"], 0, 100)));
        }

        // empty when scripting is off; the stored order is then left exactly as it is
        var order = form["order"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // the panel always renders at least one blank webhook row, so no "url" at all means the section
        // was not part of this post and the stored list is left alone rather than emptied
        var webhooksPosted = form["url"].Count > 0;
        if (ReadWebhooks(form, out var webhooks) is { } webhookError)
            return Back(ctx, webhookError);

        var timingPosted = form.ContainsKey("intervalSeconds")
            || form.ContainsKey("retentionDays")
            || form.ContainsKey("webhookCooldownMinutes");
        var interval = ParseBoundedInt(form["intervalSeconds"], 5, 86_400);
        var retention = ParseBoundedInt(form["retentionDays"], 1, 3650);
        var cooldown = ParseBoundedInt(form["webhookCooldownMinutes"], 0, 10_080);

        // the mutation cannot abort a write once it starts, so an id the config never had is refused here
        var known = (content.SiteConfig?.Monitoring?.Targets ?? []).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        if (edits.Any(e => !known.Contains(e.Id)))
            return Back(ctx, "unknown-monitor");

        var ok = await writer.UpdateMonitoringAsync(monitoring =>
        {
            foreach (var edit in edits)
            {
                var target = AdminConfigWriter.GetOrAddTarget(monitoring, edit.Id);
                AdminConfigWriter.SetFlag(target, "enabled", edit.Enabled, omitWhen: true);
                AdminConfigWriter.SetFlag(target, "hidden", edit.Hidden, omitWhen: false);
                AdminConfigWriter.SetInt(target, "retries", edit.Retries is > 0 ? edit.Retries : null);
            }

            ApplyOrder(monitoring, order);

            if (webhooksPosted)
                ApplyWebhooks(monitoring, webhooks);

            if (timingPosted)
            {
                AdminConfigWriter.SetInt(monitoring, "intervalSeconds", interval);
                AdminConfigWriter.SetInt(monitoring, "retentionDays", retention);
                AdminConfigWriter.SetInt(monitoring, "webhookCooldownMinutes", cooldown is > 0 ? cooldown : null);
            }
        }, ct);

        if (ok) await content.ForceRebuildAsync(ct);
        return Back(ctx, ok ? "saved" : "write-failed");
    }

    private sealed record MonitorEdit(string Id, bool Enabled, bool Hidden, int? Retries);

    private static void ApplyOrder(JsonObject monitoring, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0 || monitoring["targets"] is not JsonArray targets)
            return;

        var remaining = targets.OfType<JsonObject>().ToList();
        var ordered = new JsonArray();
        foreach (var id in ids)
        {
            // an id the config never had is dropped rather than invented; reordering is not a way to add targets
            if (remaining.FirstOrDefault(t => (string?)t["id"] == id) is not { } match)
                continue;
            remaining.Remove(match);
            ordered.Add(match.DeepClone());
        }

        // a target the form never mentioned keeps its place at the end rather than being dropped
        foreach (var leftover in remaining)
            ordered.Add(leftover.DeepClone());

        monitoring["targets"] = ordered;
    }

    /// <summary>Null when every row is acceptable, otherwise the flash key naming what was wrong.</summary>
    private static string? ReadWebhooks(IFormCollection form, out List<WebhookEdit> webhooks)
    {
        webhooks = [];
        var urls = form["url"];
        var headerNames = form["headerName"];
        var headerValues = form["headerValue"];

        for (var i = 0; i < urls.Count && webhooks.Count < MaxWebhooks; i++)
        {
            var url = (urls[i] ?? "").Trim();
            if (url.Length == 0)
                continue;
            if (!IsPostableUrl(url))
                return "bad-webhook-url";
            var name = Nth(headerNames, i);
            var value = Nth(headerValues, i);
            // a blank name means "no header"; a blank value means "keep the stored secret"
            if (!string.IsNullOrWhiteSpace(name) && !MonitorScheduler.IsSafeHeader(name, value))
                return "bad-webhook-header";
            webhooks.Add(new WebhookEdit(url, name, value));
        }

        return null;
    }

    private sealed record WebhookEdit(string Url, string? Name, string? Value);

    private static void ApplyWebhooks(JsonObject monitoring, IReadOnlyList<WebhookEdit> webhooks)
    {
        var existing = ExistingHeaders(monitoring);
        if (webhooks.Count == 0)
        {
            // empty array, not removed key.
            monitoring["webhooks"] = new JsonArray();
            return;
        }

        var array = new JsonArray();
        foreach (var (url, name, value) in webhooks)
        {
            var node = new JsonObject { ["url"] = url };
            // ponytail: blank keeps the stored secret; one header per webhook, repeated rows if two are needed
            var headers = string.IsNullOrWhiteSpace(name)
                ? null
                : string.IsNullOrEmpty(value)
                    ? existing.GetValueOrDefault(url)
                    : new JsonObject { [name!] = value };
            if (headers is not null)
                node["headers"] = headers.DeepClone();
            array.Add(node);
        }
        monitoring["webhooks"] = array;
    }

    private static Dictionary<string, JsonNode> ExistingHeaders(JsonObject monitoring)
    {
        var map = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        if (monitoring["webhooks"] is not JsonArray array)
            return map;
        foreach (var node in array.OfType<JsonObject>())
            if ((string?)node["url"] is { } url && node["headers"] is { } headers)
                map[url] = headers;
        return map;
    }

    private static async Task<IResult> ResetMonitor(HttpContext ctx, HeartbeatStore store, ContentService content, CancellationToken ct)
    {
        var form = await ctx.Request.ReadFormAsync(ct);
        var id = form["id"].ToString();

        // configured ids only; this reaches a DELETE
        var known = (content.SiteConfig?.Monitoring?.Targets ?? []).Any(t => t.Id == id);
        if (!known)
            return Back(ctx, "unknown-monitor");

        // no rebuild: the status page reads heartbeats from the store on every render
        store.DeleteFor(id);
        return Back(ctx, "reset");
    }

    private static async Task<IResult> ComposeIncident(HttpContext ctx, AuthOptions auth, ContentService content, HeartbeatStore store, PageResponder responder, IAntiforgery antiforgery, CancellationToken ct)
    {
        var form = await ctx.Request.ReadFormAsync(ct);
        var title = form["title"].ToString().Trim();
        if (title.Length is 0 or > MaxTitleLength)
            return Back(ctx, "bad-incident");

        if (!DateTimeOffset.TryParse(form["start"].ToString(), out var start))
            start = DateTimeOffset.UtcNow;
        DateTimeOffset? end = DateTimeOffset.TryParse(form["end"].ToString(), out var parsedEnd) ? parsedEnd : null;

        var maintenance = form["maintenance"] == "on";
        if (maintenance && end is null)
            return Back(ctx, "maintenance-needs-end");
        if (end is { } e && e < start)
            return Back(ctx, "end-before-start");

        var known = (content.SiteConfig?.Monitoring?.Targets ?? []).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var monitors = form["monitors"].Where(m => m is not null && known.Contains(m)).Select(m => m!).ToList();

        var body = form["body"].ToString();
        var description = form["description"].ToString().Trim();
        if (body.Length > MaxBodyLength || description.Length > MaxDescriptionLength)
            return Back(ctx, "bad-incident");

        var draft = new IncidentDraft(
            title, start, end, maintenance, monitors,
            Degraded: form["degraded"] == "on",
            Description: description,
            Body: body);

        if (draft.NewFileUrl(auth) is { } url)
            return Results.Redirect(url);

        // rendered in place; ?draft= would reflect the whole file out of the query string
        await WriteAdminAsync(ctx, content, store, responder, auth, antiforgery, "draft-ready", draft.Markdown());
        return Results.Empty;
    }

    private static IResult Back(HttpContext ctx, string flash)
    {
        var adminPath = ctx.RequestServices.GetRequiredService<AuthOptions>().AdminPath;
        return Results.Redirect($"{ctx.Request.PathBase}{adminPath}?flash={flash}");
    }

    private static string? Subject(HttpContext ctx) =>
        ctx.User.FindFirst(AuthEndpoints.SubjectClaim)?.Value
        ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private static string? Nth(IReadOnlyList<string?> values, int index) =>
        index < values.Count ? values[index] : null;

    private static int? ParseBoundedInt(string? raw, int min, int max) =>
        int.TryParse(raw, out var value) ? Math.Clamp(value, min, max) : null;

    // absolute http(s) only; this value is handed to HttpClient
    internal static bool IsPostableUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

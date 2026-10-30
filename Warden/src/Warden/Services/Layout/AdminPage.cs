using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Warden.Configuration;
using Warden.Models;
using Warden.Services.Admin;
using Warden.Services.Rendering;

namespace Warden.Services.Layout;

internal static class AdminPage
{
    /// <summary>The one form every settings section posts to; the action bar saves it as a whole.</summary>
    public const string SettingsFormId = "admin-settings";

    public static string Render(
        MonitoringConfig? monitoring,
        IReadOnlyDictionary<string, MonitorStatus> statuses,
        AuthOptions auth,
        string basePath,
        AntiforgeryTokenSet tokens,
        string? subject,
        string? flash,
        string? draft)
    {
        var l = Localization.Current;
        var prefix = basePath.TrimEnd('/');
        var targets = monitoring?.Targets ?? [];
        var sb = new StringBuilder();

        AppendHeader(sb, prefix, auth, tokens, subject);
        AppendFlash(sb, flash);

        if (!string.IsNullOrEmpty(draft))
            AppendDraft(sb, draft);

        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/settings\" id=\"")
          .Append(SettingsFormId).Append("\" data-dirty-form>");
        AppendToken(sb, tokens);
        // empty when scripting is off, which the server reads as "leave the order alone"
        sb.Append("<input type=\"hidden\" name=\"order\" id=\"admin-order\">");
        AppendMonitors(sb, targets, statuses);
        AppendWebhooks(sb, monitoring);
        AppendTiming(sb, monitoring);
        AppendActionBar(sb, l);
        sb.Append("</form>");

        AppendIncident(sb, prefix, targets, auth, tokens);
        AppendResetForm(sb, prefix, auth, tokens);
        // own form, so Enter in a settings field still saves
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/webhook-test\" id=\"webhook-test-form\" hidden>");
        AppendToken(sb, tokens);
        sb.Append("</form>");
        AppendResetDialog(sb, l);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, string prefix, AuthOptions auth, AntiforgeryTokenSet tokens, string? subject)
    {
        var l = Localization.Current;
        var who = l.AdminSignedInAs(subject ?? l.AdminUnnamedOperator);

        sb.Append("<header class=\"admin-header\"><h1 class=\"admin-title\">")
          .Append(E(l.AdminTitle)).Append("</h1>");
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(E(auth.AuthPath))
          .Append("/logout\" class=\"admin-identity\">");
        AppendToken(sb, tokens);
        sb.Append("<button type=\"submit\" class=\"icon-btn\" aria-label=\"").Append(E(l.AdminSignOut))
          .Append("\" data-tip=\"").Append(E(who)).Append("\">")
          .Append(Icon("<path d=\"M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4\"/><path d=\"m16 17 5-5-5-5\"/><path d=\"M21 12H9\"/>"))
          .Append("</button></form></header>");
    }

    // inline, so it never covers the save bar
    private static void AppendFlash(StringBuilder sb, string? flash)
    {
        sb.Append("<div class=\"toast-stack\" id=\"toast-stack\" role=\"status\" aria-live=\"polite\">");
        if (!string.IsNullOrEmpty(flash) && Flash(flash) is var (tone, text))
            sb.Append("<div class=\"toast toast--").Append(tone).Append("\"><span>").Append(E(text)).Append("</span></div>");
        sb.Append("</div>");
    }

    private static void AppendDraft(StringBuilder sb, string draft)
    {
        var l = Localization.Current;
        OpenSection(sb, l.AdminIncidentFileHeading);
        sb.Append("<p class=\"admin-note\">").Append(l.AdminIncidentFileNote).Append("</p>");
        sb.Append("<pre class=\"admin-draft\"><code>").Append(E(draft)).Append("</code></pre></section>");
    }

    private static void AppendMonitors(StringBuilder sb, IReadOnlyList<MonitorTarget> targets, IReadOnlyDictionary<string, MonitorStatus> statuses)
    {
        var l = Localization.Current;
        OpenSection(sb, l.AdminMonitorsHeading);

        if (targets.Count == 0)
        {
            sb.Append("<div class=\"admin-empty\"><p class=\"admin-empty-title\">")
              .Append(E(l.AdminMonitorsEmptyTitle)).Append("</p><p class=\"admin-empty-note\">")
              .Append(l.AdminMonitorsEmptyNote).Append("</p></div></section>");
            return;
        }

        sb.Append("<ul class=\"admin-list\" id=\"monitor-order\">");

        foreach (var target in targets)
        {
            var status = target.Enabled == false ? (MonitorStatus?)null : statuses.GetValueOrDefault(target.Id);
            var (state, label) = status switch
            {
                null => ("off", l.AdminStateOff),
                MonitorStatus.Up => ("up", l.AdminStateOperational),
                MonitorStatus.Down => ("down", l.AdminStateDown),
                MonitorStatus.Degraded => ("degraded", l.StatusDegraded),
                MonitorStatus.Maintenance => ("maintenance", l.StatusMonitorMaintenance),
                _ => ("unknown", l.AdminStateUnknown),
            };

            sb.Append("<li class=\"admin-row admin-row--").Append(state)
              .Append("\" data-monitor-id=\"").Append(E(target.Id)).Append("\">");
            sb.Append("<input type=\"hidden\" name=\"monitorId\" value=\"").Append(E(target.Id)).Append("\">");

            AppendGrip(sb, target.Name);
            sb.Append("<span class=\"admin-row-name\" title=\"").Append(E(target.Id)).Append(" &middot; ").Append(E(target.Type))
              .Append("\">").Append(E(target.Name)).Append("</span>");
            sb.Append("<span class=\"admin-badge select-none\">").Append(E(label)).Append("</span>");
            AppendSwitch(sb, $"enabled:{target.Id}", l.AdminEnabled, target.Enabled != false);
            AppendSwitch(sb, $"hidden:{target.Id}", l.AdminHidden, target.Hidden == true);

            sb.Append("<details class=\"admin-more\"><summary class=\"icon-btn\" aria-label=\"")
              .Append(E(l.AdminMoreFor(target.Name))).Append("\" data-tip=\"").Append(E(l.AdminMore)).Append("\">")
              .Append("<svg viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"5\" cy=\"12\" r=\"1.8\"/><circle cx=\"12\" cy=\"12\" r=\"1.8\"/><circle cx=\"19\" cy=\"12\" r=\"1.8\"/></svg>")
              .Append("</summary><div class=\"admin-more-body\">");
            AppendNumber(sb, $"retries:{target.Id}", l.AdminRetries, target.Retries, 0, 100);
            sb.Append("<button type=\"button\" class=\"icon-btn icon-btn--danger\" data-reset=\"")
              .Append(E(target.Id)).Append("\" data-reset-name=\"").Append(E(target.Name)).Append("\" aria-label=\"")
              .Append(E(l.AdminResetHistory)).Append("\" data-tip=\"").Append(E(l.AdminResetHistory)).Append("\">")
              .Append(Icon("<path d=\"M3 6h18\"/><path d=\"M8 6V4h8v2\"/><path d=\"M19 6l-1 14H6L5 6\"/>"))
              .Append("</button></div></details>");
            sb.Append("</li>");
        }

        sb.Append("</ul></section>");
    }

    private static void AppendWebhooks(StringBuilder sb, MonitoringConfig? monitoring)
    {
        var l = Localization.Current;
        var webhooks = monitoring?.Webhooks ?? [];

        OpenSection(sb, l.AdminWebhooksHeading);
        sb.Append("<div class=\"admin-rows\">");

        // one blank row is the "add another" affordance; a stack of them reads as broken config
        for (var i = 0; i <= webhooks.Count; i++)
        {
            var hook = i < webhooks.Count ? webhooks[i] : null;
            var header = hook?.Headers?.FirstOrDefault();
            sb.Append("<div class=\"admin-grid admin-grid--hook\">");
            AppendText(sb, "url", l.AdminWebhookUrl, hook?.Url, null, "url");
            AppendText(sb, "headerName", l.AdminWebhookHeaderName, header?.Key, null, "text");
            AppendText(sb, "headerValue", l.AdminWebhookHeaderValue, null, null, "password",
                title: header is null ? null : l.AdminWebhookSecretKept);
            sb.Append("<label class=\"admin-field admin-field--switch\"><span class=\"admin-field-label\">").Append(E(l.AdminWebhookEnabled))
              .Append("</span><span class=\"admin-switch-box\"><input class=\"admin-switch-input\" type=\"checkbox\" role=\"switch\" name=\"webhookEnabled:")
              .Append(i).Append('"').Append(hook?.Enabled != false ? " checked" : "").Append("></span></label>");
            // tests the saved webhook; the blank row has nothing saved yet
            if (hook is not null)
                sb.Append("<button type=\"submit\" class=\"icon-btn admin-hook-test\" form=\"webhook-test-form\" name=\"test\" value=\"")
                  .Append(i).Append("\" aria-label=\"").Append(E(l.AdminWebhookTest)).Append("\" data-tip=\"").Append(E(l.AdminWebhookTest)).Append("\">")
                  .Append(Icon("<path d=\"m22 2-7 20-4-9-9-4Z\"/><path d=\"M22 2 11 13\"/>")).Append("</button>");
            else
                sb.Append("<span></span>");
            sb.Append("</div>");
        }

        sb.Append("</div></section>");
    }

    private static void AppendTiming(StringBuilder sb, MonitoringConfig? monitoring)
    {
        var l = Localization.Current;
        OpenSection(sb, l.AdminTimingHeading);
        sb.Append("<div class=\"admin-grid\">");
        AppendNumber(sb, "intervalSeconds", l.AdminInterval, monitoring?.IntervalSeconds, 5, 86_400);
        AppendNumber(sb, "retentionDays", l.AdminRetention, monitoring?.RetentionDays, 1, 3650);
        AppendNumber(sb, "webhookCooldownMinutes", l.AdminCooldown, monitoring?.WebhookCooldownMinutes, 0, 10_080);
        sb.Append("</div></section>");
    }

    private static void AppendActionBar(StringBuilder sb, Localization l) =>
        sb.Append("<div class=\"admin-actionbar\" id=\"admin-actionbar\" hidden>")
          .Append("<span class=\"admin-actionbar-count select-none\">").Append(E(l.AdminUnsavedChanges)).Append("</span>")
          .Append("<button type=\"button\" class=\"admin-btn\" id=\"admin-discard\">").Append(E(l.AdminDiscard))
          .Append("</button><button type=\"submit\" class=\"admin-btn admin-btn--primary\">")
          .Append(E(l.AdminSaveChanges)).Append("</button></div>");

    private static void AppendIncident(StringBuilder sb, string prefix, IReadOnlyList<MonitorTarget> targets, AuthOptions auth, AntiforgeryTokenSet tokens)
    {
        var l = Localization.Current;
        var hasForge = IncidentDraft.ResolveBase(auth) is not null;

        OpenSection(sb, l.AdminIncidentHeading);

        // only the forge path leaves the panel; the in-place path must not open a second copy of it
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/incident\"")
          .Append(hasForge ? " target=\"_blank\" rel=\"noopener\"" : "")
          .Append(" class=\"admin-grid admin-incident\">");
        AppendToken(sb, tokens);

        sb.Append("<div class=\"admin-kind admin-span\" role=\"radiogroup\" aria-label=\"").Append(E(l.AdminIncidentKind)).Append("\">");
        AppendKind(sb, "outage", "down", l.AdminIncidentOutage, isDefault: true);
        AppendKind(sb, "degraded", "degraded", l.AdminIncidentDegraded, isDefault: false);
        AppendKind(sb, "maintenance", "maintenance", l.AdminIncidentMaintenance, isDefault: false);
        sb.Append("</div>");

        AppendText(sb, "title", l.AdminIncidentTitle, null, null, "text", "admin-span", required: true, maxLength: 200);

        if (targets.Count > 0)
        {
            sb.Append("<fieldset class=\"admin-fieldset admin-span\"><legend class=\"admin-field-label\">")
              .Append(E(l.AdminIncidentAffected)).Append("</legend><div class=\"admin-choices\">");
            foreach (var target in targets)
                sb.Append("<label class=\"admin-choice\"><input type=\"checkbox\" name=\"monitors\" value=\"")
                  .Append(E(target.Id)).Append("\"><span>").Append(E(target.Name)).Append("</span></label>");
            sb.Append("</div></fieldset>");
        }

        // server-local, matching how the post is parsed
        sb.Append("<div class=\"admin-pair admin-span\">");
        AppendDateTime(sb, "start", l.AdminIncidentStart, required: true, value: DateTime.Now.AddMinutes(-60));
        AppendDateTime(sb, "end", l.AdminIncidentEnd, required: false);
        sb.Append("</div>");
        AppendText(sb, "description", l.AdminIncidentDescription, null, null, "text", "admin-span", maxLength: 300);

        sb.Append("<label class=\"admin-field admin-span\"><span class=\"admin-field-label\">")
          .Append(E(l.AdminIncidentBody)).Append("</span><textarea class=\"admin-input\" name=\"body\" rows=\"6\" maxlength=\"20000\"></textarea></label>");

        sb.Append("<div class=\"admin-incident-actions admin-span\"><button type=\"submit\" class=\"admin-btn admin-btn--primary\">")
          .Append(E(l.AdminCompose)).Append("</button></div>");
        sb.Append("</form></section>");
    }

    private static void AppendKind(StringBuilder sb, string value, string tone, string label, bool isDefault) =>
        sb.Append("<label class=\"admin-kind-option admin-kind-option--").Append(tone).Append("\"><input type=\"radio\" name=\"kind\" value=\"")
          .Append(value).Append('"').Append(isDefault ? " checked" : "").Append("><span>").Append(E(label)).Append("</span></label>");

    private static void AppendResetForm(StringBuilder sb, string prefix, AuthOptions auth, AntiforgeryTokenSet tokens)
    {
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/reset\" id=\"reset-form\" hidden>");
        AppendToken(sb, tokens);
        sb.Append("<input type=\"hidden\" name=\"id\" id=\"reset-id\"></form>");
    }

    private static void AppendResetDialog(StringBuilder sb, Localization l) =>
        sb.Append("<dialog id=\"reset-dialog\" class=\"admin-dialog\" aria-labelledby=\"reset-title\">")
          .Append("<h2 id=\"reset-title\" class=\"admin-dialog-title\">").Append(E(l.AdminResetDialogTitle)).Append("</h2>")
          // filled by the script from the row that opened the dialog, so the name never round-trips the locale string
          .Append("<p class=\"admin-dialog-target\"><strong id=\"reset-name\"></strong></p>")
          .Append("<p class=\"admin-dialog-body\">").Append(E(l.AdminResetDialogBody)).Append("</p>")
          .Append("<form method=\"dialog\" class=\"admin-dialog-actions\">")
          .Append("<button value=\"cancel\" class=\"admin-btn\" autofocus>").Append(E(l.AdminCancel)).Append("</button>")
          .Append("<button value=\"confirm\" class=\"admin-btn admin-btn--danger\" id=\"reset-confirm\">")
          .Append(E(l.AdminResetConfirm)).Append("</button></form></dialog>");

    /// <summary>Flash keys are a closed set, so an unknown one renders nothing rather than echoing the query.</summary>
    private static (string Tone, string Text)? Flash(string key)
    {
        var l = Localization.Current;
        return key switch
        {
            "saved" => ("ok", l.AdminFlashSaved),
            "reset" => ("ok", l.AdminFlashReset),
            "draft-ready" => ("warn", l.AdminFlashDraftReady),
            "write-failed" => ("bad", l.AdminFlashWriteFailed),
            "unknown-monitor" => ("bad", l.AdminFlashUnknownMonitor),
            "bad-webhook-url" => ("bad", l.AdminFlashBadWebhookUrl),
            "bad-webhook-header" => ("bad", l.AdminFlashBadWebhookHeader),
            "bad-incident" => ("bad", l.AdminFlashBadIncident),
            "maintenance-needs-end" => ("bad", l.AdminFlashMaintenanceNeedsEnd),
            "end-before-start" => ("bad", l.AdminFlashEndBeforeStart),
            "stale-form" => ("bad", l.AdminFlashStaleForm),
            "webhook-test-ok" => ("ok", l.AdminFlashWebhookTestOk),
            "webhook-test-failed" => ("bad", l.AdminFlashWebhookTestFailed),
            _ => null,
        };
    }

    private static void OpenSection(StringBuilder sb, string heading) =>
        sb.Append("<section class=\"admin-section\"><h2 class=\"admin-section-title select-none\">")
          .Append(E(heading)).Append("</h2>");

    private static string Icon(string paths) =>
        "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">"
        + paths + "</svg>";

    private static void AppendGrip(StringBuilder sb, string name)
    {
        var l = Localization.Current;
        sb.Append("<button type=\"button\" class=\"admin-grip\" data-grip aria-label=\"")
          .Append(E(l.AdminReorderLabel(name))).Append("\" title=\"").Append(E(l.AdminReorderHint))
          .Append("\"><svg viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\">")
          .Append("<circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/>")
          .Append("<circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/>")
          .Append("<circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button>");
    }

    private static void AppendSwitch(StringBuilder sb, string name, string label, bool value) =>
        sb.Append("<label class=\"admin-switch\"><input class=\"admin-switch-input\" type=\"checkbox\" role=\"switch\" name=\"")
          .Append(E(name)).Append('"').Append(value ? " checked" : "")
          .Append("><span class=\"admin-switch-label\">").Append(E(label)).Append("</span></label>");

    private static void AppendNumber(StringBuilder sb, string name, string label, int? value, int min, int max) =>
        sb.Append("<label class=\"admin-field\"><span class=\"admin-field-label\">").Append(E(label))
          .Append("</span><input class=\"admin-input\" type=\"number\" inputmode=\"numeric\" name=\"").Append(E(name))
          .Append("\" min=\"").Append(min).Append("\" max=\"").Append(max)
          .Append('"').Append(value is { } v ? $" value=\"{v}\"" : "").Append("></label>");

    private static void AppendText(StringBuilder sb, string name, string label, string? value, string? placeholder, string type, string extraClass = "", bool required = false, int maxLength = 0, string? title = null)
    {
        sb.Append("<label class=\"admin-field ").Append(extraClass).Append("\"><span class=\"admin-field-label\">")
          .Append(E(label)).Append("</span><input class=\"admin-input\" type=\"").Append(type)
          .Append("\" name=\"").Append(name).Append('"');
        if (value is { Length: > 0 }) sb.Append(" value=\"").Append(E(value)).Append('"');
        if (placeholder is { Length: > 0 }) sb.Append(" placeholder=\"").Append(E(placeholder)).Append('"');
        if (title is { Length: > 0 }) sb.Append(" title=\"").Append(E(title)).Append('"');
        if (type == "password") sb.Append(" autocomplete=\"new-password\"");
        if (maxLength > 0) sb.Append(" maxlength=\"").Append(maxLength).Append('"');
        if (required) sb.Append(" required");
        sb.Append("></label>");
    }

    // native picker; posts yyyy-MM-ddTHH:mm
    private static void AppendDateTime(StringBuilder sb, string name, string label, bool required, DateTime? value = null) =>
        sb.Append("<label class=\"admin-field\"><span class=\"admin-field-label\">").Append(E(label))
          .Append("</span><input class=\"admin-input\" type=\"datetime-local\" name=\"").Append(name).Append('"')
          .Append(value is { } v ? " value=\"" + v.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "\"" : "")
          .Append(required ? " required" : "").Append("></label>");

    private static void AppendToken(StringBuilder sb, AntiforgeryTokenSet tokens) =>
        sb.Append("<input type=\"hidden\" name=\"").Append(E(tokens.FormFieldName))
          .Append("\" value=\"").Append(E(tokens.RequestToken)).Append("\">");

    private static string E(string? value) => LayoutProvider.HtmlEncode(value);
}

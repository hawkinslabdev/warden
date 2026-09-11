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
        HeartbeatStore store,
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

        if (!string.IsNullOrEmpty(draft))
            AppendDraft(sb, draft);

        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/settings\" id=\"")
          .Append(SettingsFormId).Append("\" data-dirty-form>");
        AppendToken(sb, tokens);
        // empty when scripting is off, which the server reads as "leave the order alone"
        sb.Append("<input type=\"hidden\" name=\"order\" id=\"admin-order\">");
        AppendMonitors(sb, targets, store);
        AppendWebhooks(sb, monitoring);
        AppendTiming(sb, monitoring);
        AppendActionBar(sb, l);
        sb.Append("</form>");

        AppendIncident(sb, prefix, targets, auth, tokens);
        AppendResetForm(sb, prefix, auth, tokens);
        AppendResetDialog(sb, l);
        AppendToasts(sb, flash);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, string prefix, AuthOptions auth, AntiforgeryTokenSet tokens, string? subject)
    {
        var l = Localization.Current;
        var who = l.AdminSignedInAs(subject ?? l.AdminUnnamedOperator);

        sb.Append("<header class=\"admin-header\"><h1 class=\"list-heading\">")
          .Append(E(l.AdminTitle)).Append("</h1>");
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(E(auth.AuthPath))
          .Append("/logout\" class=\"admin-identity\">");
        AppendToken(sb, tokens);
        sb.Append("<button type=\"submit\" class=\"admin-btn\" data-tip=\"").Append(E(who)).Append("\">")
          .Append(E(l.AdminSignOut)).Append("</button>");
        sb.Append("</form></header>");
    }

    private static void AppendDraft(StringBuilder sb, string draft)
    {
        var l = Localization.Current;
        OpenSection(sb, l.AdminIncidentFileHeading);
        sb.Append("<p class=\"admin-note\">").Append(l.AdminIncidentFileNote).Append("</p>");
        sb.Append("<pre class=\"admin-draft\"><code>").Append(E(draft)).Append("</code></pre></section>");
    }

    private static void AppendMonitors(StringBuilder sb, IReadOnlyList<MonitorTarget> targets, HeartbeatStore store)
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

        sb.Append("<p class=\"admin-note\">").Append(E(l.AdminMonitorsNote)).Append("</p>");
        sb.Append("<ul class=\"admin-list\" id=\"monitor-order\">");

        foreach (var target in targets)
        {
            var latest = store.GetLatest(target.Id);
            var (state, label) = target.Enabled == false
                ? ("off", l.AdminStateOff)
                : latest is null
                    ? ("unknown", l.AdminStateUnknown)
                    : latest.Data.Up
                        ? ("up", l.AdminStateOperational)
                        : ("down", l.AdminStateDown);

            sb.Append("<li class=\"admin-card admin-card--").Append(state)
              .Append("\" data-monitor-id=\"").Append(E(target.Id)).Append("\">");
            sb.Append("<input type=\"hidden\" name=\"monitorId\" value=\"").Append(E(target.Id)).Append("\">");

            sb.Append("<div class=\"admin-card-head\">");
            AppendGrip(sb, target.Name);
            sb.Append("<span class=\"admin-card-name\">").Append(E(target.Name)).Append("</span>");
            sb.Append("<span class=\"admin-card-meta select-none\">").Append(E(target.Id))
              .Append(" &middot; ").Append(E(target.Type)).Append("</span>");
            sb.Append("<span class=\"admin-badge select-none\">").Append(E(label)).Append("</span>");
            sb.Append("</div>");

            sb.Append("<div class=\"admin-card-controls\">");
            AppendSwitch(sb, $"enabled:{target.Id}", l.AdminEnabled, target.Enabled != false);
            AppendSwitch(sb, $"hidden:{target.Id}", l.AdminHidden, target.Hidden == true);
            AppendNumber(sb, $"retries:{target.Id}", l.AdminRetries, target.Retries, 0, 100, "0");
            sb.Append("<button type=\"button\" class=\"admin-btn admin-btn--danger admin-card-reset\" data-reset=\"")
              .Append(E(target.Id)).Append("\" data-reset-name=\"").Append(E(target.Name)).Append("\">")
              .Append(E(l.AdminResetHistory)).Append("</button>");
            sb.Append("</div></li>");
        }

        sb.Append("</ul></section>");
    }

    private static void AppendWebhooks(StringBuilder sb, MonitoringConfig? monitoring)
    {
        var l = Localization.Current;
        var webhooks = monitoring?.Webhooks ?? [];

        OpenSection(sb, l.AdminWebhooksHeading);
        sb.Append("<p class=\"admin-note\">").Append(E(l.AdminWebhooksNote)).Append("</p>");
        sb.Append("<ul class=\"admin-list\">");

        // one blank row is the "add another" affordance; a stack of them reads as broken config
        for (var i = 0; i <= webhooks.Count; i++)
        {
            var hook = i < webhooks.Count ? webhooks[i] : null;
            var header = hook?.Headers?.FirstOrDefault();
            sb.Append("<li class=\"admin-card\"><div class=\"admin-card-controls\">");
            AppendText(sb, "url", l.AdminWebhookUrl, hook?.Url, l.AdminWebhookUrlPlaceholder, "url", "admin-field--url");
            AppendText(sb, "headerName", l.AdminWebhookHeaderName, header?.Key, l.AdminWebhookHeaderNamePlaceholder, "text", "admin-field--head");
            AppendText(sb, "headerValue", l.AdminWebhookHeaderValue, null,
                header is null ? l.AdminWebhookHeaderValuePlaceholder : l.AdminWebhookSecretKept, "password", "admin-field--head");
            sb.Append("</div></li>");
        }

        sb.Append("</ul></section>");
    }

    private static void AppendTiming(StringBuilder sb, MonitoringConfig? monitoring)
    {
        var l = Localization.Current;
        OpenSection(sb, l.AdminTimingHeading);
        sb.Append("<p class=\"admin-note\">").Append(E(l.AdminTimingNote)).Append("</p>");
        sb.Append("<div class=\"admin-card\"><div class=\"admin-card-controls\">");
        AppendNumber(sb, "intervalSeconds", l.AdminInterval, monitoring?.IntervalSeconds, 5, 86_400, "60");
        AppendNumber(sb, "retentionDays", l.AdminRetention, monitoring?.RetentionDays, 1, 3650, "30");
        AppendNumber(sb, "webhookCooldownMinutes", l.AdminCooldown, monitoring?.WebhookCooldownMinutes, 0, 10_080, "0");
        sb.Append("</div></div></section>");
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
        sb.Append("<p class=\"admin-note\">")
          .Append(hasForge ? l.AdminIncidentNoteForge : l.AdminIncidentNoteManual)
          .Append("</p>");

        // only the forge path leaves the panel; the in-place path must not open a second copy of it
        sb.Append("<form method=\"post\" action=\"").Append(prefix).Append(auth.AdminPath).Append("/incident\"")
          .Append(hasForge ? " target=\"_blank\" rel=\"noopener\"" : "")
          .Append(" class=\"admin-card admin-incident\">");
        AppendToken(sb, tokens);

        sb.Append("<div class=\"admin-card-controls\">");
        AppendText(sb, "title", l.AdminIncidentTitle, null, l.AdminIncidentTitlePlaceholder, "text", "admin-field--full", required: true, maxLength: 200);
        AppendDateTime(sb, "start", l.AdminIncidentStart, required: true, defaultOffsetMinutes: -60);
        AppendDateTime(sb, "end", l.AdminIncidentEnd, required: false);
        AppendText(sb, "description", l.AdminIncidentDescription, null, l.AdminIncidentDescriptionPlaceholder, "text", "admin-field--full", maxLength: 300);
        sb.Append("</div>");

        if (targets.Count > 0)
        {
            sb.Append("<fieldset class=\"admin-fieldset\"><legend class=\"admin-legend\">")
              .Append(E(l.AdminIncidentAffected)).Append("</legend><div class=\"admin-choices\">");
            foreach (var target in targets)
                sb.Append("<label class=\"admin-choice\"><input class=\"admin-choice-input\" type=\"checkbox\" name=\"monitors\" value=\"")
                  .Append(E(target.Id)).Append("\"><span class=\"admin-choice-dot\" aria-hidden=\"true\"></span>")
                  .Append("<span class=\"admin-choice-name\">").Append(E(target.Name)).Append("</span></label>");
            sb.Append("</div></fieldset>");
        }

        sb.Append("<div class=\"admin-card-controls\">");
        AppendSwitch(sb, "maintenance", l.AdminIncidentMaintenance, false);
        AppendSwitch(sb, "degraded", l.AdminIncidentDegraded, false);
        sb.Append("</div>");

        sb.Append("<label class=\"admin-field admin-field--full\"><span class=\"admin-field-label\">")
          .Append(E(l.AdminIncidentBody)).Append("</span><textarea class=\"admin-input\" name=\"body\" rows=\"6\" maxlength=\"20000\" placeholder=\"")
          .Append(E(l.AdminIncidentBodyPlaceholder)).Append("\"></textarea></label>");

        sb.Append("<div class=\"admin-incident-actions\"><button type=\"submit\" class=\"admin-btn admin-btn--primary\">")
          .Append(E(l.AdminCompose)).Append("</button></div>");
        sb.Append("</form></section>");
    }

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

    private static void AppendToasts(StringBuilder sb, string? flash)
    {
        sb.Append("<div class=\"toast-stack\" id=\"toast-stack\" role=\"status\" aria-live=\"polite\">");
        if (!string.IsNullOrEmpty(flash) && Flash(flash) is var (tone, text))
            sb.Append("<div class=\"toast toast--").Append(tone).Append("\"><span>").Append(E(text)).Append("</span></div>");
        sb.Append("</div>");
    }

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
            _ => null,
        };
    }

    private static void OpenSection(StringBuilder sb, string heading) =>
        sb.Append("<section class=\"status-group admin-section\"><h2 class=\"status-group-heading select-none\">")
          .Append(E(heading)).Append("</h2>");

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

    private static void AppendNumber(StringBuilder sb, string name, string label, int? value, int min, int max, string placeholder) =>
        sb.Append("<label class=\"admin-field admin-field--num\"><span class=\"admin-field-label\">").Append(E(label))
          .Append("</span><input class=\"admin-input\" type=\"number\" inputmode=\"numeric\" name=\"").Append(E(name))
          .Append("\" min=\"").Append(min).Append("\" max=\"").Append(max)
          .Append("\" placeholder=\"").Append(E(placeholder)).Append('"')
          .Append(value is { } v ? $" value=\"{v}\"" : "").Append("></label>");

    private static void AppendText(StringBuilder sb, string name, string label, string? value, string? placeholder, string type, string extraClass, bool required = false, int maxLength = 0)
    {
        sb.Append("<label class=\"admin-field ").Append(extraClass).Append("\"><span class=\"admin-field-label\">")
          .Append(E(label)).Append("</span><input class=\"admin-input\" type=\"").Append(type)
          .Append("\" name=\"").Append(name).Append('"');
        if (value is { Length: > 0 }) sb.Append(" value=\"").Append(E(value)).Append('"');
        if (placeholder is { Length: > 0 }) sb.Append(" placeholder=\"").Append(E(placeholder)).Append('"');
        if (type == "password") sb.Append(" autocomplete=\"new-password\"");
        if (maxLength > 0) sb.Append(" maxlength=\"").Append(maxLength).Append('"');
        if (required) sb.Append(" required");
        sb.Append("></label>");
    }

    /// <summary>Posts a simple "yyyy-MM-dd HH:mm" string, which DateTimeOffset.TryParse reads the same
    /// way in every culture. The popover is built by the script; without it this stays a typable field.</summary>
    private static void AppendDateTime(StringBuilder sb, string name, string label, bool required, int defaultOffsetMinutes = 0)
    {
        var l = Localization.Current;
        var id = "dt-" + name;

        sb.Append("<div class=\"admin-field admin-field--time admin-datetime\" data-datetime")
          .Append(" data-prev=\"").Append(E(l.AdminPickerPrevMonth))
          .Append("\" data-next=\"").Append(E(l.AdminPickerNextMonth))
          .Append("\" data-now=\"").Append(E(l.AdminPickerNow))
          .Append("\" data-clear=\"").Append(E(l.AdminPickerClear))
          .Append("\" data-done=\"").Append(E(l.AdminPickerDone))
          .Append("\" data-hours=\"").Append(E(l.AdminPickerHours))
          .Append("\" data-minutes=\"").Append(E(l.AdminPickerMinutes))
          .Append("\" data-default-offset=\"").Append(defaultOffsetMinutes)
          .Append("\">");

        sb.Append("<label class=\"admin-field-label\" for=\"").Append(id).Append("\">").Append(E(label)).Append("</label>");
        sb.Append("<div class=\"admin-datetime-control\">");
        sb.Append("<input class=\"admin-input admin-datetime-input\" id=\"").Append(id)
          .Append("\" name=\"").Append(name)
          .Append("\" type=\"text\" autocomplete=\"off\" spellcheck=\"false\" inputmode=\"numeric\"")
          .Append(" placeholder=\"").Append(E(l.AdminPickerFormat)).Append('"')
          .Append(" pattern=\"\\d{4}-\\d{2}-\\d{2}[ T]\\d{2}:\\d{2}\"")
          .Append(" aria-haspopup=\"dialog\" aria-expanded=\"false\"")
          .Append(required ? " required" : "").Append(">");
        sb.Append("<button type=\"button\" class=\"admin-datetime-open\" aria-label=\"").Append(E(l.AdminPickerOpen))
          .Append("\"><svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">")
          .Append("<rect x=\"3\" y=\"4\" width=\"18\" height=\"17\" rx=\"2\"/><line x1=\"3\" x2=\"21\" y1=\"9\" y2=\"9\"/>")
          .Append("<line x1=\"8\" x2=\"8\" y1=\"2\" y2=\"6\"/><line x1=\"16\" x2=\"16\" y1=\"2\" y2=\"6\"/></svg></button>");
        sb.Append("</div>");
        sb.Append("<div class=\"admin-datetime-pop\" role=\"dialog\" aria-label=\"").Append(E(l.AdminPickerDialog)).Append("\" hidden></div>");
        sb.Append("</div>");
    }

    private static void AppendToken(StringBuilder sb, AntiforgeryTokenSet tokens) =>
        sb.Append("<input type=\"hidden\" name=\"").Append(E(tokens.FormFieldName))
          .Append("\" value=\"").Append(E(tokens.RequestToken)).Append("\">");

    private static string E(string? value) => LayoutProvider.HtmlEncode(value);
}

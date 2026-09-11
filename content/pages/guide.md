---
title: Guide
description: Get your first uptime monitor running in under five minutes.
page-next: /deploy/
---

A status page and uptime monitor. Point it at a URL, run it, and it checks that URL on a timer, no database to set up, no build step.

## Get your first monitor running

The status page is the site's root ("/") and isn't authored as Markdown: it checks your targets itself and renders the page live from what it's collected. List what to watch in `content/config.json`:

```json [content/config.json]
{
  "monitoring": {
    "intervalSeconds": 60,
    "retentionDays": 90,
    "targets": [
      { "id": "forgejo", "name": "Forgejo", "url": "https://forgejo.org" },
      { "id": "codeberg", "name": "Codeberg", "url": "https://codeberg.org" }
    ]
  }
}
```

`id` is a short slug you choose. History is stored under it, so don't rename it later. Changes to this block take effect on the next check, no restart needed. Every check result goes into a local SQLite database; there is no external service to sign up for.

A target defaults to `"type": "http"` (a simple GET, checking for a successful status code). Set `type` for anything else: `ping`, `tcp`, `dns`, `ssl` (certificate expiry), `ftp`, `sftp`, `database` (TCP reachability), or `service_backend` (an HTTP health check with a JSON body assertion via `expectedJsonPath`/`expectedValue`). Non-HTTP types take `host`/`port` instead of `url`:

```json [content/config.json]
{ "id": "db", "name": "Postgres", "type": "tcp", "host": "db.internal", "port": 5432 }
```

Every target field for every type, and every other `config.json` setting, is listed in the [config.json reference](/examples/config/).

Now run it. See [Deployment](/deploy/) for Docker, Windows, Linux, or running from source. Once it's up, the status page is live with that target on it.

## Going further

### Add a page

Everything besides the status page is a simple Markdown file under `content/pages/`, each needing a [front matter](/examples/frontmatter/) block:

```md [content/pages/about.md]
---
title: About
description: What this status page covers.
---

Incidents and maintenance windows are reported here as they happen.
```

Only `title` is required. Callouts, folded asides, titled/highlighted code blocks, image widths, galleries, and maps are all covered in the [Markdown reference](/examples/markdown/).

### Incidents and maintenance

Automated checks can't tell you *why* something's down, so incidents and planned maintenance are hand-written pages under `content/incidents/`, with a few extra front matter fields:

```md [content/incidents/database-upgrade.md]
---
title: Database upgrade
start: 2026-09-01T02:00:00Z
end: 2026-09-01T04:00:00Z
maintenance: true
monitors: [forgejo]
description: Forgejo may be briefly unreachable during the upgrade.
---

We're upgrading the database behind Forgejo. Expect brief interruptions.
```

`start` is when it begins (`date` means the same thing). Dates are ISO 8601: `2026-09-01T02:00:00Z` is UTC, `2026-09-01T04:00:00+02:00` uses that offset, and a simple `2026-09-01 04:00` is local time in the container's `TZ`.

`maintenance: true` makes it a maintenance window. Leave it out for an incident.

| | Needs | Badge |
|---|---|---|
| Maintenance window | `start` and `end` | **Planned** before `start`, **Active** until `end`, then gone from the status page |
| Incident | `start` | **Down** until you add `end`, then **Resolved**. Add `status: degraded` for a partial outage. |

Both stay under **Incidents** for a while after they end, and keep their own URL forever.

The URL is the file path: `content/incidents/database-upgrade.md` becomes `/incidents/database-upgrade/`. Folders work too, so `content/incidents/2026/database-upgrade.md` becomes `/incidents/2026/database-upgrade/`.

`monitors: [forgejo]` (or `monitors: forgejo` for one) links it to monitor ids from `content/config.json`. Use `monitors: all` for everything. What that does:

- Active maintenance window: the monitor shows **Maintenance** instead of Up/Down, and doesn't count toward the "some systems are experiencing issues" banner.
- Unresolved incident: the monitor shows **Down**, or **Degraded** with `status: degraded`, whatever the automated check says. Useful when the ping succeeds but the service is slow or half-broken: uptime stays the measured number, the badge tells the truth.
- Both on one monitor: the incident takes precedence. Two incidents: the more severe one takes precedence.

```md [content/incidents/api-latency.md]
---
title: API responses degraded
start: 2026-08-18T07:20:00Z
monitors: [forgejo]
status: degraded
---
```

By default the status page shows unresolved incidents plus anything resolved in the last 7 days, and maintenance windows starting within the next 14 days, capped at 10 items each. Older items are still reachable by clicking their day on a monitor's history bar. Tune all of this in `content/config.json`:

```json [content/config.json]
{
  "monitoring": {
    "incidentWindowDays": 7,
    "incidentMaxShown": 10,
    "maintenanceWindowDays": 14,
    "maintenanceMaxShown": 10
  }
}
```

### Publish your site

Running it as a service is enough. See [Deployment](/deploy/) for reverse proxy setup. For a static host, `--export` writes a folder of simple HTML (also in [Deployment](/deploy/)). An export is a snapshot; keep the service running if the page should stay current.

### Translate the interface

The interface text, like "Keep reading" and the 404 page, reads from `content/locale/`. English is the default. Point `locale.code` at a locale file to switch:

```json [content/config.json]
{
  "locale": { "code": "nl" }
}
```

Copy `en.json` to `{code}.json` and translate the values. Missing keys fall back to English, so a partial file is fine.

### Customize the look

Nine themes are built in. Name one in `config.json`:

```json [content/config.json]
{
  "theme": "ocean"
}
```

| Name | Look |
|---|---|
| `default` | Warm paper and forest green. Used when you set nothing. |
| `casper` | White paper, near-black type, violet accent. |
| `ocean` | Cool blue-grey paper and a deep harbour accent. |
| `deep-space` | Near-black navy and a periwinkle accent. |
| `solarized` | Solarized base tones: warm paper by day, deep teal by night. |
| `laserwave` | Synthwave violet with a hot magenta accent. |
| `signal-dark` | Deep charcoal and an amber accent. |
| `limelight` | Pale off-white, a sage-lime accent with a cyan counterpart. |
| `midnight` | White paper and bright blue by day; deep navy at night. |
| `oled` | White paper and a true-black dark mode, so OLED screens turn every unlit pixel off. |

Every theme adapts to a full light and dark palette, so the toggle behaves the same whichever you pick. The dark-sounding names aren't dark-only: `signal-dark` has a paper-toned light mode that swaps its amber for bronze. An unrecognized name logs a warning and falls back to `default`. The value hot-reloads with the rest of `config.json`, and [environment variables](/deploy/environment/) can override it per deployment.

`theme` picks the palette; page *structure* is a separate setting, so any theme can pair with either structure:

```json [content/config.json]
{
  "theme": "ocean",
  "structure": "dashboard"
}
```

| Name | Shape |
|---|---|
| `clean` | The default. A simple monitor list, full 90-day history bars, centered narrow column: the same shape every other page on the site uses. |
| `dashboard` | Monitors as a card grid (status dot, badge, uptime, response-time chart, history bar), a pinned "ongoing incidents" panel above the grid, and a wider column to fit it. Only the status page changes; every other page still renders like `clean`. |

`dashboard` looks like Upptime or Kener: a grid instead of a list.

Grouping is separate from the structure: `monitoring.group` sections the monitors under headings in whichever layout the structure renders, and leaving it unset never groups anything.

```json [content/config.json]
{
  "structure": "clean",
  "monitoring": {
    "group": "custom"
  }
}
```

Leaving `group` unset renders one ungrouped list or grid, no section headings, useful when most of your targets share a type anyway. `"type"` groups by monitor type, `"custom"` by each target's own `group` field.

To pin one mode instead of following the reader's system, add `dark` or `light` to the same value:

```json [content/config.json]
{
  "theme": "ocean dark"
}
```

A pinned mode removes the toggle, so readers can't switch. `"dark"` or `"light"` on its own pins the mode and keeps the `default` theme. Palette and mode resolve separately, so `Docs:Themes:Name` can pin the palette per deployment while `config.json` keeps the mode.

Custom styles live in `wwwroot/theme/custom.css`, picked up at startup. The whole theme runs on CSS variables, so overriding a handful on `:root` restyles the entire site and stays correct in dark mode:

```css [wwwroot/theme/custom.css]
:root {
  --accent: #b4513a;
  --font-sans: "Iowan Old Style", Georgia, serif;
}
```

The common variables are `--accent`, `--text-color`, `--text-muted`, `--bg-color`, `--border`, and the fonts (`--font-sans`, `--font-display`, `--font-mono`). Put dark-mode tweaks behind `:root[data-theme="dark"]`.

To style one page only, use its `data-page` attribute: `home` for the status page, otherwise the page slug, like `about`.

```css [wwwroot/theme/custom.css]
[data-page="home"] { --accent: #4a7c59; }
```

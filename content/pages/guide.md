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

`id` is a short slug you choose, it's the key each check is stored under, so keep it stable once you've started collecting history. This block hot-reloads like the rest of `config.json`: add, remove, or re-time a target and it takes effect on the next check cycle. Every heartbeat lands in a local SQLite database; there's no external backend to run or register with.

A target defaults to `"type": "http"` (a plain GET, checking for a successful status code). Set `type` for anything else: `ping`, `tcp`, `dns`, `ssl` (certificate expiry), `ftp`, `sftp`, `database` (TCP reachability), or `service_backend` (an HTTP health check with a JSON body assertion via `expectedJsonPath`/`expectedValue`). Non-HTTP types take `host`/`port` instead of `url`:

```json [content/config.json]
{ "id": "db", "name": "Postgres", "type": "tcp", "host": "db.internal", "port": 5432 }
```

Every target field for every type, and every other `config.json` setting, is listed in the [config.json reference](/examples/config/).

Now run it. See [Deployment](/deploy/) for Docker, Windows, Linux, or running from source. Once it's up, the status page is live with that target on it.

## Going further

### Add a page

Everything besides the status page is a plain Markdown file under `content/pages/`, each needing a [front matter](/examples/frontmatter/) block:

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

Property `start` is the start (`date` also works, same field). `maintenance: true` marks it a maintenance window instead of an incident; omit it (or set `false`) for an incident. A maintenance window needs both `start` and `end`, shows as **Planned** before `start` and **Active** between `start` and `end`, and drops off the status page once `end` passes. An incident only needs `start`, stays badged **Down** while unresolved, and switches to **Resolved** the moment you add `end`. Add `status: degraded` for an incident that impairs a service without taking it offline. Either way, it keeps showing under **Incidents** for a while, then ages out, and always stays published at its own URL.

The URL comes straight from the file path, so `content/incidents/database-upgrade.md` is `/incidents/database-upgrade/`. Nest incidents under a year or a year and month by putting the file deeper, e.g. `content/incidents/2026/database-upgrade.md` for `/incidents/2026/database-upgrade/`; no setting to turn on, they're still found and listed the same way.

The property `monitors: [forgejo]` (a single id also works, e.g. `monitors: forgejo`) links a window to one or more monitor ids from `content/config.json`, on either an incident or a maintenance window. A linked maintenance window shows that monitor with a **Maintenance** badge instead of Up/Down, and leaves it out of the "some systems are experiencing issues" banner. A linked, unresolved incident shows that monitor as **Down** regardless of what the automated check says — or **Degraded** when the incident sets `status: degraded`. That's useful for a real problem the heartbeat's simple up/down ping can't see, like slow or partially-failing responses: the probe keeps succeeding, so uptime stays a measured number while the badge tells the real story. `degraded` is the only recognised value; anything else means down. When both an incident and a maintenance window are active on the same monitor, the incident takes priority, and two open incidents on one monitor settle on the harsher of the two.

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

Running it as a live service is enough on its own. See [Deployment](/deploy/) for reverse proxy setup. To hand a folder of plain HTML to a static host instead, export one with the `--export` flag, also covered in [Deployment](/deploy/). The status page is a live view, so that export is a snapshot of the moment it ran; keep the service running if you want it to stay current.

### Translate the interface

The interface text, like "Keep reading" and the 404 page, reads from `content/locale/`. English is the default. Point `locale.code` at a locale file to switch:

```json [content/config.json]
{
  "locale": { "code": "nl" }
}
```

Copy `en.json` to `{code}.json` and translate the values. Missing keys fall back to English, so a partial file is fine.

### Customize the look

Nine built-in themes ship out of the box. Naming one in `config.json` is the whole opt-in:

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
| `clean` | The default. A plain monitor list, full 90-day history bars, centered narrow column: the same shape every other page on the site uses. |
| `dashboard` | Monitors as a card grid (status dot, badge, uptime, response-time chart, history bar), a pinned "ongoing incidents" panel above the grid, and a wider column to fit it. Only the status page changes; every other page still renders like `clean`. |

`dashboard` reads more like Upptime or Kener. Pick it for a grid instead of a scrolling list.

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

A pinned mode ships only that palette and drops the toggle, so readers can't switch. Written on its own, `"dark"` or `"light"` pins the mode and keeps the `default` theme. Palette and mode resolve separately, so `Docs:Themes:Name` can pin the palette per deployment while `config.json` keeps the mode.

Custom styles live in `wwwroot/theme/custom.css`, picked up at startup. The whole theme runs on CSS variables, so overriding a handful on `:root` restyles the entire site and stays correct in dark mode:

```css [wwwroot/theme/custom.css]
:root {
  --accent: #b4513a;
  --font-sans: "Iowan Old Style", Georgia, serif;
}
```

The common variables are `--accent`, `--text-color`, `--text-muted`, `--bg-color`, `--border`, and the fonts (`--font-sans`, `--font-display`, `--font-mono`). Put dark-mode tweaks behind `:root[data-theme="dark"]`.

To reach only one surface, every page carries a `data-page` hook that follows its URL: `home` for the status page, or a page slug like `about`.

```css [wwwroot/theme/custom.css]
[data-page="home"] { --accent: #4a7c59; }
```

---
title: config.json Reference
description: Every field content/config.json accepts, in one place.
page-prev: /examples/frontmatter/
---

`content/config.json` is entirely optional and hot-reloads with the rest of `content/`. Every field below is optional too, with a sensible default filling in whatever's missing.

A config using most of them at once:

```json [content/config.json]
{
  "title": "Acme Status",
  "titleTemplate": ":title · Acme Status",
  "description": "Uptime and incident history for Acme's public services.",
  "lang": "en",
  "culture": "en-GB",
  "theme": "ocean",
  "structure": "dashboard",
  "author": "Acme Ops",
  "favicon": "🛡️",
  "scrollIndicator": true,
  "noIndex": { "pages": true },
  "socialLinks": [
    { "icon": "github", "url": "https://github.com/acme", "title": "GitHub" }
  ],
  "menu": [
    { "title": "Status", "path": "/" },
    { "title": "About", "path": "/about/" }
  ],
  "footerMenu": [
    { "title": "Status API", "path": "/api/status", "external": true }
  ],
  "footer": "© {year} {author}. Built with [Warden](https://github.com/melosso/warden).",
  "monitoring": {
    "intervalSeconds": 60,
    "retentionDays": 90,
    "group": "type",
    "targets": [
      { "id": "site", "name": "Main site", "url": "https://acme.example" }
    ]
  }
}
```

## Site

| Field | Description |
| --- | --- |
| `title` | The site name: browser tab, social previews, and the `{title}` token elsewhere. |
| `titleTemplate` | Wraps `title` for the tab text, e.g. `":title · Acme"`. `:title` is the placeholder. |
| `description` | The default meta description, used on any page that doesn't set its own. |
| `brand` | Text shown next to the logo in the topbar. Falls back to `title`. |
| `brandImage` | A logo image, replacing `brand` in the topbar. |
| `image` | Default social preview image for pages that don't set their own `cover`. |
| `favicon` | An emoji or an image path for the browser tab icon. |
| `author` | The name in the `{author}` footer token. `organization`, `organisation`, and `owner` are aliases, checked in that order, ahead of `author`. |

## Language and dates

| Field | Description |
| --- | --- |
| `lang` | The page's `<html lang>` attribute and default UI language. |
| `culture` | Date and number formatting, e.g. `"en-GB"`. |
| `locale` | Either a bare code string (`"nl"`) or an object: `{ "culture": "en-GB", "code": "en" }`. Merges with `culture` above. |

## Appearance

| Field | Description |
| --- | --- |
| `theme` | One of the built-in palettes. See the [guide](/guide/#customize-the-look) for the full list. Add `" dark"` or `" light"` to pin a mode and drop the toggle. |
| `structure` | `clean` (default, a flat list) or `dashboard` (card grid). Independent of `theme`; any theme pairs with either. |
| `scrollIndicator` | Set `false` to hide the reading-progress bar at the top of the page. |

## Navigation

| Field | Description |
| --- | --- |
| `menu` | Topbar links: `[{ "title": "...", "path": "..." }]`. An entry with `items` renders as a dropdown instead of a link. `external: true` opens it in a new tab. |
| `footerMenu` | Same shape, rendered in the footer. |
| `footer` | Markdown for the footer note. Tokens: `{year}`, `{author}`, `{title}`. Falls back to a plain copyright line built from `title`. |
| `socialLinks` | Icon links in the topbar: `[{ "icon": "github", "url": "...", "title": "..." }]`. |
| `head` | Raw tags injected into `<head>`: `[{ "tag": "meta", "attrs": { "name": "...", "content": "..." }, "content": "..." }]`. |

## Search visibility

| Field | Description |
| --- | --- |
| `noIndex.pages` | Set `true` to noindex every standalone page under `content/pages/` at once, without touching each page's own front matter. A page's own `noindex: true` still applies regardless. |
| `redirectHosts` | Off-site hosts a page's `redirect` front matter is allowed to send readers to. Same-host redirects always work without listing anything here. |

## Monitoring

`monitoring` controls what gets checked and how the status page groups it. See the [guide](/guide/#get-your-first-monitor-running) for a walkthrough; this is the full field list.

| Field | Description |
| --- | --- |
| `intervalSeconds` | How often every target is checked. Default `60`. |
| `retentionDays` | How long heartbeat history is kept before it's pruned. Default `30`. |
| `group` | `"type"` sections the dashboard grid by monitor type, `"custom"` sections it by each target's own `group` field (falling back to its type when unset). Unset renders one flat grid. Has no effect on the `clean` structure. |
| `incidentWindowDays`, `incidentMaxShown` | How far back and how many resolved incidents show on the status page. Defaults `7` and `10`. |
| `maintenanceWindowDays`, `maintenanceMaxShown` | How far ahead and how many upcoming maintenance windows show. Defaults `14` and `10`. |
| `targets` | The list of monitors, covered below. |

### Target fields

Every target needs `id` (a stable slug, it's the database key) and `name` (the display label). Everything else depends on `type`.

| Field | Applies to | Description |
| --- | --- | --- |
| `type` | all | `http` (default), `service_backend`, `ping`, `tcp`, `database`, `ftp`, `sftp`, `dns`, or `ssl`. |
| `url` | `http`, `service_backend` | The address to request. |
| `host`, `port` | everything except `http`/`service_backend` | Where to connect. Ports default per type: `21` for `ftp`, `22` for `sftp`, `443` for `ssl`. |
| `expectedStatus` | `service_backend` | HTTP status the response must match. Default `200`. |
| `expectedJsonPath`, `expectedValue` | `service_backend` | A JSON path into the response body (e.g. `$.status`) and the value it must equal. |
| `expectedIp` | `dns` | The IP the hostname must resolve to. Left unset, any successful resolution counts as up. |
| `dnsServer` | `dns` | Query this resolver directly instead of the OS default. |
| `family` | `dns` | `"ipv4"` or `"ipv6"` to query one record type only. Unset queries both. |
| `warnDaysBefore` | `ssl` | How many days before certificate expiry the check starts failing. Default `14`. |
| `secure` | `ftp` | Set `true` to require an explicit `AUTH TLS` upgrade after connecting. |
| `dbType` | `database` | Informational only; the check itself is a TCP reachability probe. |
| `insecure` | `http`, `service_backend` | Set `true` to skip TLS certificate validation, for a self-signed internal service. |
| `retries` | all | Consecutive failures allowed before the target is recorded down. Default `0`, down on the first failed check. |
| `group` | all | This target's own group heading, used when `monitoring.group` is `"custom"`. |

A few examples across types:

```json [content/config.json]
{
  "monitoring": {
    "targets": [
      { "id": "site", "name": "Main site", "url": "https://acme.example" },
      { "id": "api", "name": "API health", "type": "service_backend", "url": "https://api.acme.example/healthz", "expectedJsonPath": "$.status", "expectedValue": "ok" },
      { "id": "db", "name": "Postgres", "type": "tcp", "host": "db.internal", "port": 5432, "retries": 2 },
      { "id": "cert", "name": "Certificate", "type": "ssl", "host": "acme.example", "warnDaysBefore": 21 },
      { "id": "resolver", "name": "DNS", "type": "dns", "host": "acme.example", "expectedIp": "203.0.113.10" }
    ]
  }
}
```

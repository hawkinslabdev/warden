# 🛡️ Warden

[![License](https://img.shields.io/badge/license-EUPL%201.2-blue)](LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/melosso/warden)](https://github.com/melosso/warden/commits/main)
[![Docker](https://img.shields.io/badge/ghcr.io-melosso%2Fwarden-blue?logo=docker)](https://github.com/melosso/warden/pkgs/container/warden)

Meet Warden: a self-contained status page. It checks the sites you list itself, on a timer, keeps the history in its own local SQLite database, and renders a Markdown-themed status page from it, uptime, downtime, per-monitor statistics, and outage reporting, no external monitoring backend, no build step, no extra dependencies.

Warden is a child project of [Teatime](https://github.com/melosso/teatime), inheriting its markdown engine, theming and page-structure system unchanged. Where Teatime renders a blog, Warden renders a status page: it checks your configured targets, stores every heartbeat itself, and layers your own Markdown pages, theme, and single-language UI strings on top.

<div>
      <p align="center"><strong>🔍 <a href="https://melosso.github.io/warden/">See it in action!</a></strong></p>
</div>

![Screenshot of Warden](.github/images/preview.webp)

## How it works

Your writing lives in a `content/` folder:

```
content/
  pages/        standalone pages like About, served at /about
  config.json   optional site settings
  locale/en.json    UI string overrides (single language)
```

The status page itself is the site's root ("/") and isn't authored as Markdown. Warden checks your configured targets on a timer and renders it live from what it's collected, using the same theme and layout as the rest of the site. Everything else (`/about`, `/guide`, any page you add under `content/pages/`) is a plain Markdown file with front matter:

```markdown
---
title: About
description: What this status page covers.
---

Outages are reported here as they happen.
```

Once a page is saved, it appears right away. Warden watches your files and rebuilds in memory, so there is nothing to recompile.

## Installation

The quickest way to run Warden is the published container image, which has everything bundled and ready.

### Docker

Create a `docker-compose.yml` next to your writing:

```yaml
services:
  warden:
    image: ghcr.io/melosso/warden:latest
    container_name: warden
    ports:
      - "8080:8080"
    volumes:
      - ./content:/app/content
    environment:
      PublicBaseUrl: https://status.example.com
      AllowedHosts: status.example.com
    volumes:
      - ./content:/app/content
      - ./data:/app/data
```

Your own `content/` folder, holding your `.md` pages and `config.json`, is mounted from the host, and so is `data/`, that's where the SQLite heartbeat history lives, and without a volume it resets on every container recreate. `PublicBaseUrl` is set to the origin you serve from. What to check lives in `content/config.json` (see [Configuring your site](#configuring-your-site)). With that in place, you can bring it up:

```bash
docker compose up -d
```

Your status page is then waiting at `http://localhost:8080`.

Running locally only? The `PublicBaseUrl`/`AllowedHosts` lines can be left out entirely. Without them, absolute URLs in your sitemap fall back to whatever `Host` header the request carried, which is perfectly fine on localhost.

> **Note:** On a public host that fallback is worth avoiding, since the `Host` header is supplied by the caller. Setting `PublicBaseUrl` and `AllowedHosts` keeps those URLs pinned to your own origin.

### Windows and IIS

If you would rather host on Windows, each release ships a ready to run build:

1. Download the latest `*-Windows_x64.zip` from the [Releases](https://github.com/melosso/warden/releases) page.
2. Extract it into your site folder, for example `C:\inetpub\warden`.
3. Create an IIS site pointed at that folder, with the CLR version set to "No Managed Code".
4. Make sure the [.NET 11 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/11.0) is installed.
5. Start the site and browse to it.

The zip already includes a `web.config` wired for in process hosting, so no manual edits are needed. A `*-Linux_x64.zip` build is attached to each release as well.

## Writing

Warden renders your own pages through its Markdig pipeline. If you would like a refresher on the syntax itself, the [Markdown Guide](https://www.markdownguide.org/) is a friendly and thorough place to start. You also get:

- A live status page at the root: per-monitor up/down badges, a 90-day history bar, 24h uptime, and hand-written incidents/maintenance from `content/incidents/`, each with its own page
- Standalone pages under `content/pages/`, each rendered with the site's theme
- `/sitemap.xml`, `/robots.txt` and `/llms.txt` covering your authored pages and the status page
- A JSON status endpoint at `/api/status` for your own tooling
- Light and dark themes

A few more content features, such as diagrams, math, and footnotes, come along with the pipeline. The [Markdown examples page](content/pages/examples/markdown.md) shows the syntax for each of them side by side with its output.

## Configuring your site

A `content/config.json` file is entirely optional. It lets you set details like your site title, description, social links, and what to monitor:

```json
{
  "title": "Warden",
  "description": "Uptime, downtime and outage reporting for Example Corp.",
  "socialLinks": [
    { "icon": "github", "url": "https://github.com/you" }
  ],
  "monitoring": {
    "intervalSeconds": 60,
    "retentionDays": 90,
    "targets": [
      { "id": "forgejo", "name": "Forgejo", "url": "https://forgejo.org" }
    ]
  }
}
```

In this case, `id` is a short slug you choose, it's the row key in the database, so renaming it starts that target's history over. This block is hot-reloaded with the rest of `config.json`: add, remove, or re-time a target and it takes effect on the next check cycle, no restart needed. If a target can't be reached, the status page still renders it as down instead of erroring out.

Only where the SQLite file itself lives is a deployment concern, so that one stays in `appsettings.json` (or `Monitoring__DatabasePath`):

```json
{
  "Monitoring": {
    "DatabasePath": "data/warden.db"
  }
}
```

Both your pages and your config are hot reloaded, so they can be adjusted while the server keeps running. Theme details, such as CSS variables and dark mode, are read from `appsettings.json` or environment variables instead, which keeps your content folder focused purely on writing. Should you prefer to keep the choice next to your content, a `theme` value in `config.json` works too, and the `--theme` flag overrides both for a quick look at an alternative.

UI strings ship in a single language (`content/locale/en.json`); the file overrides the English built-in defaults key by key, so you can edit copy without touching code.

## License

Please see [LICENSE](LICENSE) for the full terms.

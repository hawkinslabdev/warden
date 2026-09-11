# 🧿 Warden

[![License](https://img.shields.io/badge/license-EUPL%201.2-blue)](LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/hawkinslabdev/warden)](https://github.com/hawkinslabdev/warden/commits/main)
[![Docker](https://img.shields.io/badge/ghcr.io-hawkinslabdev%2Fwarden-blue?logo=docker)](https://github.com/hawkinslabdev/warden/pkgs/container/warden)

Warden is a self-contained status page. It checks your sites on a timer, keeps the history in a local SQLite database, and reports uptime, downtime and outages from that. There is no external service to run or sign up for.

<div>
      <p align="center"><strong>🔍 <a href="https://hawkinslabdev.github.io/warden/">See it in action!</a></strong></p>
</div>

![Screenshot of Warden](.github/images/preview.webp)

## How it works

Your content lives in a `content/` folder:

```
content/
  incidents/    incident and maintenance reports, linked to your monitors
  pages/        standalone pages like About, served at /about
  config.json   optional site settings
  locale/en.json    locale overrides (single language)
```

The status page is the site's root (`/`). It is not a Markdown file: Warden checks your targets on a timer and renders the page from what it has collected, in the same theme and layout as the rest of the site. Everything else (`/about`, `/guide`, anything under `content/pages/`) is a Markdown file with front matter:

```markdown
---
title: About
description: What this status page covers.
---

Outages are reported here as they happen.
```

Save a page and it appears right away. Warden watches the files and rebuilds in memory; there is nothing to compile.

## Installation

### Docker

```bash
mkdir -p warden/content warden/data && cd warden
curl -O https://raw.githubusercontent.com/hawkinslabdev/warden/main/docker-compose.yml
curl -o content/config.json https://raw.githubusercontent.com/hawkinslabdev/warden/main/content/config.example.json
chown -R 1654:1654 data
docker compose up -d
```

Open `http://localhost:8080`. Edit `content/config.json` to change what is checked; it hot-reloads.

`content/` holds your pages and config, `data/` holds the history and session keys. Both are volumes, so they survive container recreates. The container runs as UID `1654`; if `data/` is not writable by that user, startup stops and tells you the `chown` to run. On SELinux hosts add `:Z` to the `data` volume line.

For a public host, set `PublicBaseUrl` and `AllowedHosts` in `docker-compose.yml` to your origin. Everything else, including the admin panel, is in [Environment variables](content/pages/deploy/environment.md).

### Windows and IIS

Each release includes a ready-to-run Windows build:

1. Download the latest `*-Windows_x64.zip` from the [Releases](https://github.com/hawkinslabdev/warden/releases) page.
2. Extract it into your site folder, for example `C:\inetpub\warden`.
3. Create an IIS site pointed at that folder, with the CLR version set to "No Managed Code".
4. Install the [.NET 11 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/11.0).
5. Start the site and browse to it.

The zip includes a `web.config` set up for in-process hosting, so no edits are needed. Each release also includes a `*-Linux_x64.zip`.

## Incidents and pages

- A live status page at the root: per-monitor up/down badges, a history bar (90 days by default), 24h uptime, and incidents and maintenance windows from `content/incidents/`, each a Markdown file with its own page
- Standalone pages under `content/pages/` (About, Guide, a status policy) for anything that is not a monitor or an incident, rendered with the site's theme
- `/sitemap.xml` and `/robots.txt` covering your pages and the status page
- An Atom feed of incidents and maintenance at `/incidents/feed.xml`
- A JSON status endpoint at `/api/status` for your own tooling
- Light and dark themes

Incident `start` and `end` dates are ISO 8601. `2026-09-01T02:00:00Z` is UTC, `2026-09-01T04:00:00+02:00` uses that offset, and a plain `2026-09-01 04:00` is local time in the container's `TZ`.

Incidents and standalone pages go through the same Markdig pipeline, so diagrams, math and footnotes work in both. The [Markdown examples page](content/pages/examples/markdown.md) shows each syntax next to its output; the [Markdown Guide](https://www.markdownguide.org/) covers the basics.

## Configuring your site

`content/config.json` is optional. It sets the site title, description, social links, and what to monitor:

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

`id` is the key each monitor's history is stored under, so renaming it starts that history over. Incidents reference the same id in their front matter. `retentionDays` is how long history is kept; it never goes below `historyDays`, so the history bar never loses days to pruning. The `monitoring` block hot-reloads with the rest of `config.json`: add, remove or re-time a target and it takes effect on the next check, no restart. An unreachable target shows as down rather than erroring.

The [config.json reference](content/pages/examples/config.md) lists every monitor field and type. Setting `lang` in `config.json` points the interface text at `content/locale/<lang>.json`, so you can override strings key by key without touching the source.

To keep `content/` in sync with a Git remote, including private-repo auth, see the [Git sync reference](content/pages/examples/git.md). Warden only runs `git pull` inside `content/`; it stays your own checkout. One gotcha: in a compose `environment:` list, write `- GIT_CRON=*/5 * * * *` without quotes, or the quotes become part of the value.

## License

Licensed under EUPL 1.2, see [LICENSE](LICENSE) for the details.

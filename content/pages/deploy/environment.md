---
title: Environment Variables
description: Every setting Warden reads from the environment.
page-prev: /deploy/
page-next: /examples/
---

None of these need to be set; the defaults work. They matter at deployment time: the port, which sites to watch, and how often to check them.

An environment variable overrides the matching `appsettings.json` value. Nested keys use a double underscore, so `Docs:PageSize` becomes `Docs__PageSize`.

## Hosting

| Variable | Default | What it does |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Address and port. The Docker image sets `http://+:8080`. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` logs more. |

| `Proxy__Trusted__0` | none | A proxy IP or CIDR network allowed to set `X-Forwarded-For`. |
| `Proxy__TrustAny` | `false` | Honours the forwarded header from any caller. |

### Behind a reverse proxy

Rate limits are counted per reader IP. Behind nginx, Caddy, or a container ingress, every request arrives from the proxy instead, so the API budget of thirty per minute ends up shared by everyone, and a single bot can close it out for the whole site.

Listing your proxy fixes it. Loopback is trusted already, so a proxy on the same host needs nothing:

```bash
Proxy__Trusted__0=10.0.0.0/8
Proxy__Trusted__1=172.18.0.5
```

`Proxy__TrustAny=true` skips the list entirely, useful when your ingress has no fixed address. It also means any caller who can reach the port may claim any IP, so it suits a container that only its proxy can talk to, and little else. Warden logs the choice at startup.

## Monitoring

What to check, and how often, lives in `content/config.json`, not here; see the [guide](/guide/#get-your-first-monitor-running) for the `monitoring` block. The database file's location is the one deployment concern:

```json [appsettings.json]
{
  "Monitoring": {
    "DatabasePath": "data/warden.db"
  }
}
```

| Variable | Default | What it does |
| --- | --- | --- |
| `Monitoring__DatabasePath` | `data/warden.db` | Where the SQLite heartbeat history lives, relative to the app unless rooted. |
| `DatabasePath` | none | A shorter name for the same setting, simpler to type in a `docker-compose.yml` `environment:` block. Takes priority over `Monitoring__DatabasePath` when both are set. |

## Content

| Variable | Default | What it does |
| --- | --- | --- |
| `Docs__RootPath` | `content` | Path to your content folder. |
| `Docs__EnableHotReload` | `true` | Rebuilds when a file changes. |
| `Docs__DefaultPage` | `index` | Filename used as a folder's own page. |
| `Docs__BasePath` | none | Subdirectory prefix, such as `/updates`. |
| `Docs__ContentSecurityPolicy` | built in | Replaces the default policy. |
| `Docs__Themes__Name` | none | Built-in theme name, overriding `config.json`. |

`Docs__BasePath` prefixes every internal link. Give a static export the same value with `--base-path` so the two agree.

`Docs__Themes__Name` suits a deployment that wants a different look than the one in version control. It outranks `theme` in `config.json`, and `--theme <name>` on the command line outranks both.

## Health checks

`GET /health` answers `200` with the current build version, page count, and uptime in seconds:

```json
{ "status": "ok", "buildVersion": 17, "pages": 42, "uptimeSeconds": 3600 }
```

It answers `503` with `"status": "empty"` when no content has been built. That's the signal an external uptime monitor watching this deployment should watch for. The route carries no rate limit, so polling it every few seconds is fine.

## Bot protection

Gates every page behind a self-hosted [ALTCHA](https://altcha.org) proof-of-work challenge - an alternative to fronting Warden with something like Cloudflare's JS-fingerprinting "Just a moment..." screen. Off by default; `/health` and `/api` stay reachable either way.

```json [appsettings.json]
{
  "Altcha": {
    "Enabled": true
  }
}
```

| Variable | Default | What it does |
| --- | --- | --- |
| `Altcha__Enabled` | `false` | Requires solving a challenge before any page loads. |

## Logs

Warnings and errors go to `logs/warden-<date>.log` beside the binary, rolling daily and keeping a fortnight. Everything at `Information` stays on the console only, so the file itself stays small.

The `Serilog` section of `appsettings.json` holds these settings, so pointing `path` at a mounted volume or lowering `restrictedToMinimumLevel` are both single-line changes. Setting `Serilog__WriteTo__1__Args__path` in the environment works too.

## In a container

```yaml [docker-compose.yml]
services:
  warden:
    image: warden
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./content:/app/content
      - ./data:/app/data
```

Mount `data/` too. That's where the SQLite database lives, and without a volume the check history resets on every container recreate.

::: Warning
If a setting seems ignored, check for a single underscore where a double belongs. `Docs_PageSize` is nothing at all; `Docs__PageSize` is the setting you meant.
:::

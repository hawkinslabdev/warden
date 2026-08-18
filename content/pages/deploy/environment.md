---
title: Environment Variables
description: Every setting Warden reads from the environment.
page-prev: /deploy/
page-next: /examples/
---

Warden runs fine with none of these set. They earn their keep at deployment time: the port, which sites to watch, and how often to check them.

Environment variables win over `appsettings.json`. Nested keys use a double underscore, so `Docs:PageSize` becomes `Docs__PageSize`.

## Hosting

| Variable | Default | What it does |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Address and port. The Docker image sets `http://+:8080`. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` logs more. |

| `Proxy__Trusted__0` | none | A proxy IP or CIDR network allowed to set `X-Forwarded-For`. |
| `Proxy__TrustAny` | `false` | Honours the forwarded header from any caller. |

### Behind a reverse proxy

Rate limits are counted per reader IP, and behind nginx, Caddy, or a container ingress every request arrives from the proxy instead. Left unconfigured, the API budget of thirty per minute ends up shared by everyone, and a single bot can close it out for the whole site.

Listing your proxy fixes it. Loopback is trusted already, so a proxy on the same host needs nothing:

```bash
Proxy__Trusted__0=10.0.0.0/8
Proxy__Trusted__1=172.18.0.5
```

`Proxy__TrustAny=true` skips the list entirely, which is convenient when your ingress has no fixed address. It does mean any caller who can reach the port may claim any IP, so it suits a container that only its proxy can talk to, and little else. Warden notes the choice in the startup log.

## Monitoring

Warden checks its own targets, there's no separate backend to point it at. What to check, how often, and for how long is editorial content, so it lives in `content/config.json` instead of here, see the [guide](/guide/#1-tell-warden-what-to-watch) for the `monitoring` block. Only where the database file lives is a deployment concern:

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

## Content

| Variable | Default | What it does |
| --- | --- | --- |
| `Docs__RootPath` | `content` | Path to your content folder. |
| `Docs__EnableHotReload` | `true` | Rebuilds when a file changes. |
| `Docs__DefaultPage` | `index` | Filename used as a folder's own page. |
| `Docs__BasePath` | none | Subdirectory prefix, such as `/updates`. |
| `Docs__ContentSecurityPolicy` | built in | Replaces the default policy. |
| `Docs__Themes__Name` | none | Built-in theme name, overriding `config.json`. |

`Docs__BasePath` prefixes every internal link. Pass the same value to a static export with `--base-path` so the two agree.

`Docs__Themes__Name` suits a deployment that wants a different look than the one in version control. It outranks `theme` in `config.json`, and `--theme <name>` on the command line outranks both.

## Health checks

`GET /health` answers `200` with the current build version, page count, and uptime in seconds:

```json
{ "status": "ok", "buildVersion": 17, "pages": 42, "uptimeSeconds": 3600 }
```

It answers `503` with `"status": "empty"` when no content has been built, which is what an external uptime monitor watching Warden itself should watch for. The route adapts no rate limit, so polling it every few seconds is fine.

## Logs

Warnings and errors are written to `logs/warden-<date>.log` beside the binary, rolling daily and keeping a fortnight. Everything at `Information` continues to go to the console only, which keeps the file small enough to read.

The `Serilog` section of `appsettings.json` holds the settings, so pointing `path` at a mounted volume or lowering `restrictedToMinimumLevel` are both single-line changes. Setting `Serilog__WriteTo__1__Args__path` in the environment works too.

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

Mount `data/` too, that's where the SQLite database lives, and without a volume the check history resets on every container recreate.

::: Warning
 If a setting seems ignored, check for a single underscore where a double belongs. `Docs_PageSize` is nothing at all, `Docs__PageSize` is the setting you meant.
:::

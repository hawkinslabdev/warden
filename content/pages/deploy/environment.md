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
| `TZ` | `UTC` | Server time zone (IANA name, e.g. `Europe/Amsterdam`). Used for log and cron timestamps, for incident dates written without an offset, and reported in `/api`'s `tz` field. Heartbeat history is always stored in UTC. |
| `PublicBaseUrl` | none | The origin readers use, e.g. `https://status.example.com`. Used for canonical URLs, feeds and `robots.txt`. Without it, the request's `Host` header is used. |
| `AllowedHosts` | `*` | Hostnames the server accepts. Set it to the same host as `PublicBaseUrl`. |
| `Proxy__Trusted__0` | none | A proxy IP or CIDR network allowed to set `X-Forwarded-For`. |
| `Proxy__TrustAny` | `false` | Trusts the forwarded header from any caller. |

### Behind a reverse proxy

Rate limits are counted per reader IP. Behind nginx, Caddy, or a container ingress, every request has the proxy's IP, so all readers share one limit and a single bot can lock everyone out.

List your proxy to fix that. Loopback is trusted already, so a proxy on the same host needs nothing:

```bash
Proxy__Trusted__0=10.0.0.0/8
Proxy__Trusted__1=172.18.0.5
```

`Proxy__TrustAny=true` skips the list, useful when your ingress has no fixed address. Anyone who can reach the port can then claim any IP, so only use it when nothing but the proxy can reach the container. Warden logs the choice at startup.

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

It answers `503` with `"status": "empty"` when no content has been built. Point an external uptime monitor at this route; it has no rate limit, so polling every few seconds is fine.

## Bot protection

Puts every page behind a self-hosted [ALTCHA](https://altcha.org) proof-of-work challenge, similar to Cloudflare's "Just a moment..." screen but without fingerprinting. Off by default. `/health` and `/api` stay reachable either way.

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
| `Altcha__SessionHours` | `24` | How long a solved challenge exempts a visitor from re-solving. |

## Admin panel

Off by default. Set the OIDC variables below to enable `/admin`, where you can toggle and reorder monitors and configure webhook notifications.

| Variable | Default | What it does |
| --- | --- | --- |
| `OIDC_ISSUER` | none | Provider URL. `https`, or `http` on loopback. |
| `OIDC_CLIENT_ID` | none | Client registered with that provider. |
| `OIDC_CLIENT_SECRET` | none | That client's secret. |
| `OIDC_ALLOWED_SUBJECTS` | none | Comma-separated `sub` claims allowed in. |
| `AUTH_PATH` | `auth` | Prefix for the sign-in routes. |
| `ADMIN_PATH` | `admin` | Path for the panel itself. |

Register `https://your-site/auth/callback` as the redirect URI. Warden asks for the `openid` scope only, uses PKCE, and stores no tokens.

`OIDC_ALLOWED_SUBJECTS` requires the `sub` claim. Your provider's user admin should include this value, Keycloak as the user ID, Authentik as `sub`.

Behind a reverse proxy, list it under `Proxy__Trusted__0` as well. Without it Warden cannot tell the request arrived over https, so the session cookie goes out without its `Secure` flag.

Session and antiforgery keys are stored in `data/keys/`, next to the database. Mount `data/`, or every container recreate logs everyone out.

## Logs

Warnings and errors go to `logs/warden-<date>.log` beside the binary, one file per day, 14 days kept. `Information` messages go to the console only, so the file stays small.

These settings live in the `Serilog` section of `appsettings.json`: change `path` to write to a mounted volume, or lower `restrictedToMinimumLevel` to log more. `Serilog__WriteTo__1__Args__path` in the environment works too.

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

Mount `data/` too. That's where the SQLite database and the session keys live; without a volume both reset on every container recreate. The container runs as UID `1654`, so the folder must be writable by that user: `chown -R 1654:1654 data`.

::: Warning
If a setting seems ignored, check for a single underscore where a double belongs. `Docs_PageSize` is nothing at all; `Docs__PageSize` is the setting you meant.
:::

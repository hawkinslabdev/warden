---
title: Installation
description: Run Warden with Docker, or on Windows with IIS.
page-prev: /deploy/
page-next: /examples/
---

Docker is the quickest way to run it. Every release also includes a ready-to-run Windows build.

## Docker

Create a `docker-compose.yml` next to your content:

```yaml [docker-compose.yml]
services:
  warden:
    image: ghcr.io/hawkinslabdev/warden:latest
    container_name: warden
    ports:
      - "8080:8080"
    volumes:
      - ./content:/app/content
      - ./data:/app/data
```

Mount your own `content/` folder (`.md` files and an optional `config.json`), and `data/` so the SQLite heartbeat history survives container recreates. Then bring it up:

```bash
mkdir -p data && chown -R 1654:1654 data
docker compose up -d
```

The container runs as UID `1654`, not root, so `data/` must be writable by that user. On a host with SELinux (Fedora, RHEL), add `:Z` to the `data` volume line as well.

The status page is now at `http://localhost:8080`. For running it as a long-lived service, the [Docker Compose notes](/deploy/containers/) go further.

## Windows and IIS

1. Download the latest `*-Windows_x64.zip` from [Releases](https://github.com/hawkinslabdev/warden/releases){target="_blank" rel="noopener"}.
2. Extract it into your site folder, for example `C:\inetpub\warden`.
3. Create an IIS site pointed at that folder, with the CLR version set to "No Managed Code".
4. Install the [.NET 11 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/11.0){target="_blank" rel="noopener"}.
5. Start the site and browse to it.

The zip includes a `web.config` set up for in-process hosting, no edits needed. Each release also includes a `*-Linux_x64.zip`; that install path isn't documented yet.

To change the port, hide drafts, or keep an API key out of your content folder, see [environment variables](/deploy/environment/).

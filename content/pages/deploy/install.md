---
title: Installation
description: Run Warden with Docker, or on Windows with IIS.
page-prev: /deploy/
page-next: /examples/markdown/
---

The container image is the fastest path: everything's bundled. A ready-to-run build ships with every release for Windows.

## Docker

Create a `docker-compose.yml` next to your content:

```yaml [docker-compose.yml]
services:
  warden:
    image: ghcr.io/melosso/warden:latest
    container_name: warden
    ports:
      - "8080:8080"
    volumes:
      - ./content:/app/content
```

Mount your own `content/` folder (`.md` files and an optional `config.json`), then bring it up:

```bash
docker compose up -d
```

The status page is now at `http://localhost:8080`. For running it as a long-lived service, the [Docker Compose notes](/deploy/containers/) go further.

## Windows and IIS

1. Download the latest `*-Windows_x64.zip` from [Releases](https://github.com/melosso/warden/releases){target="_blank" rel="noopener"}.
2. Extract it into your site folder, for example `C:\inetpub\warden`.
3. Create an IIS site pointed at that folder, with the CLR version set to "No Managed Code".
4. Install the [.NET 11 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/11.0){target="_blank" rel="noopener"}.
5. Start the site and browse to it.

The zip includes a `web.config` wired for in-process hosting, no manual edits needed. A `*-Linux_x64.zip` build ships with each release too; that installation path isn't documented yet.

To change the port, hide drafts, or keep an API key out of your content folder, see [environment variables](/deploy/environment/).

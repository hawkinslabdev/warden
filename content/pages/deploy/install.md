---
title: Installation
description: Run Warden with Docker, or on Windows with IIS.
page-prev: /deploy/
page-next: /examples/
---

The quickest way to run Warden is the published container image, which has everything bundled and ready. If you would rather host on Windows, a ready to run build ships with every release.

## Docker

Create a `docker-compose.yml` next to your writing:

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

Mount your own `content/` folder holding your `.md` files and an optional `config.json`. When that is in place, bring it up:

```bash
docker compose up -d
```

Your status page is then waiting at `http://localhost:8080`. For running it as a long lived service, the [Docker Compose notes](/deploy/containers/) go a little further.

## Windows and IIS

Each release ships a ready to run build for Windows:

1. Download the latest `*-Windows_x64.zip` from the [Releases](https://github.com/melosso/warden/releases){target="_blank" rel="noopener"} page.
2. Extract it into your site folder, for example `C:\inetpub\warden`.
3. Create an IIS site pointed at that folder, with the CLR version set to "No Managed Code".
4. Make sure the [.NET 11 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/11.0){target="_blank" rel="noopener"} is installed.
5. Start the site and browse to it.

The zip already includes a `web.config` wired for in process hosting, so no manual edits are needed. A `*-Linux_x64.zip` build is attached to each release as well, though we have yet to document this installation process.

To change the port, hide drafts, or keep an API key out of your content folder, see [environment variables](/deploy/environment/).

Have fun writing!
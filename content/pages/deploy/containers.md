---
title: Running Warden with Docker
description: A take on deploying Warden as a container.
page-prev: /deploy/
page-next: /examples/
---

Warden is built to be published as a containerized image, so it'll run anywhere Docker does. To get started, create a `docker-compose.yml`:

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

Mount your `content/` folder so your pages stay editable from the host, then bring it up:

```bash
docker compose up -d
```

Your status page is then available at `http://localhost:8080`.

::: tip
Because `content/` is a volume, dropping in a new Markdown page is enough for it to appear. There is no process to restart since the server will pick these changes up automagically.
:::

Behind a reverse proxy, set `Docs:BasePath` (or `--base-path` for a static export) so every internal link resolves under your chosen path. For the one time setup, the [installation guide](/deploy/install/) covers the rest, and [environment variables](/deploy/environment/) lists what you can set from your compose file.

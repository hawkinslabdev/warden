---
title: Running Warden with Docker
description: A take on deploying Warden as a container.
page-prev: /deploy/
page-next: /examples/
---

The published image runs on any host with Docker. Start with a `docker-compose.yml`:

```yaml [docker-compose.yml]
services:
  warden:
    image: ghcr.io/melosso/warden:latest
    container_name: warden
    ports:
      - "8080:8080"
    volumes:
      - ./content:/app/content:ro,Z
```

Mount `content/` so your pages stay editable from the host, then bring it up:

```bash
docker compose up -d
```

The status page is now at `http://localhost:8080`.

::: tip
`content/` is a volume, so a new Markdown file is picked up as soon as it's saved, no restart.
:::

Behind a reverse proxy, set `Docs:BasePath` (or `--base-path` for a static export) so internal links resolve under your chosen path. The [installation guide](/deploy/install/) covers first-time setup, and [environment variables](/deploy/environment/) lists what the compose file accepts.

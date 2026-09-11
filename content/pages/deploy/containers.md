---
title: Running Warden with Docker
description: Deploying Warden as a container.
page-prev: /deploy/
page-next: /examples/
---

The published image runs on any host with Docker. Start with a `docker-compose.yml`:

```yaml [docker-compose.yml]
services:
  warden:
    image: ghcr.io/hawkinslabdev/warden:latest
    container_name: warden
    ports:
      - "8080:8080"
    volumes:
      - ./content:/app/content:ro,Z
      - ./data:/app/data
```

`content/` is your pages and config. `data/` is `warden.db` (check history) and `keys/` (admin session keys). Both are bind mounts, so recreating the container doesn't delete them. Start it:

```bash
mkdir -p data && chown -R 1654:1654 data
docker compose up -d
```

The container runs as UID `1654`, so `data/` must be writable by that user. Otherwise startup fails with `SQLite Error 8: attempt to write a readonly database`.

The status page is now at `http://localhost:8080`.

::: tip
`content/` is a volume. A new Markdown file appears as soon as it is saved, without a restart.
:::

Behind a reverse proxy, set `Docs:BasePath` (or `--base-path` for a static export) so internal links resolve under your chosen path. The [installation guide](/deploy/install/) covers first-time setup, and [environment variables](/deploy/environment/) lists what the compose file accepts.

---
title: Git Sync
description: Keeping content/ in sync with a Git remote.
page-prev: /examples/config/
---

Warden can clone and pull `content/` for you, entirely from env vars: no manual `git clone` step, no credentials baked into a remote URL.

```yaml [docker-compose.yml]
services:
  warden:
    image: ghcr.io/melosso/warden:latest
    env_file: .env
    volumes:
      - ./content:/app/content:Z
      - ./data:/app/data
```

```bash [.env]
GIT_ENABLED=true
GIT_URL=https://github.com/you/your-content.git
GIT_USERNAME=you
GIT_PASSWORD=your-token
GIT_CRON=*/5 * * * *
```

Off by default. If `content/` isn't already a checkout, Warden clones `GIT_URL` into it on startup; either way it then runs `git pull --ff-only` on the `GIT_CRON` schedule (standard 5-field cron expression), picked up by the existing file watcher. A failed clone or pull logs a warning and never takes the site down. Needs write access to `content/`, no `:ro`.

`GIT_USERNAME`/`GIT_PASSWORD` are sent as an HTTP Basic auth header per git invocation, never written into the remote URL or `.git/config`. For a token-only host (e.g. GitHub PAT), set `GIT_PASSWORD` to the token and leave `GIT_USERNAME` as any non-empty value.

The ASP.NET Core convention `Git__Enabled`/`Git__Url`/`Git__Username`/`Git__Password`/`Git__Cron` also works and takes precedence if both are set.

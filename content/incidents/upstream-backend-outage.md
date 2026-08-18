---
title: Upstream backend service outage
date: 2026-08-17T14:12:00Z
end: 2026-08-17T15:03:00Z
description: An upstream backend service was unreachable, causing failed requests for about 50 minutes.
---

We identified that an upstream backend service was down, which caused failed requests for some users. The issue was traced to the upstream provider and resolved once their service recovered.

**Timeline:**

- 14:12 UTC — Upstream backend became unreachable, errors started.
- 14:35 UTC — Issue confirmed as upstream, not our infrastructure.
- 15:03 UTC — Upstream service recovered, errors stopped.

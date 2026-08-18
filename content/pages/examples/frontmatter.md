---
title: Front Matter
description: A complete guide to every front matter field supported by Warden.
page-prev: /examples/markdown/
---

Front matter is the configuration block at the top of a Markdown file, delimited by `---` lines. It defines page-level metadata for Warden. All fields are optional, with built-in defaults applied to missing values.

Example configuration using common fields:

```md [content/pages/about.md]
---
title: About
description: What this status page covers.
date: 2026-07-16
updated: 2026-07-20
cover: /assets/about.webp {.wide}
page-next: /guide/
keywords: [status, uptime, monitoring]
---

Your writing begins here.

```

**The essentials**

| Field | Description |
| --- | --- |
| `title` | Sets the page title and browser tab text. |
| `description` | Defines the meta description and social preview text. Hidden from page content. |

**Content and dates**

| Field | Description |
| --- | --- |
| `date` | Sets the creation date. Overrides file system modified time and populates "Last updated" if `updated` is missing. |
| `updated` | Sets a revision date. Replaces `date` in the "Last updated" display when newer. |
| `lastUpdated` | Set to `false` to suppress the "Last updated" timestamp on this page. |
| `cover` | Defines the feature and social preview image. Supports class attributes (e.g., `cover: /assets/hero.webp {.full}`). |
| `keywords` | Array of terms for the meta keywords tag. |

**Navigation**

| Field | Description |
| --- | --- |
| `redirect` | Forwards traffic to a target URL instead of rendering page content. |
| `page-next` | Renders a link to the next page using the target page's title. |
| `page-prev`, `page-previous` | Renders a link to the previous page. Accepts either key name. |
| `pagination` | Set to `false` to disable next and previous links on the page. |

**Search visibility**

| Field | Description |
| --- | --- |
| `sitemap` | Set to `false` to remove the page from `sitemap.xml` while keeping it accessible. |
| `noindex` | Set to `true` to append a `noindex` meta tag and header, which also sets `sitemap: false`. Set `"noIndex": { "pages": true }` in `content/config.json` to apply globally. |

**Incidents and maintenance**

Files in `content/incidents/*.md` support dedicated fields: `maintenance`, `end`, `start` (alias for `date`), and `monitors`. See [Incidents and maintenance](/guide/incidents-and-maintenance) for integration details.

Warden hot-reloads updates in real time, making it easy to test fields during site development.
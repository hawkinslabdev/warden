---
title: Front Matter
description: A complete guide to every front matter field supported by Warden.
page-prev: /examples/markdown/
page-next: /examples/config/
---

Front matter is the configuration block at the top of a Markdown file, delimited by `---` lines. It defines page-level metadata. All fields are optional, with defaults applied to whatever's missing.

Example using common fields:

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
| `description` | Sets the meta description and social preview text. Not shown on the page itself. |

**Content and dates**

| Field | Description |
| --- | --- |
| `date` | The creation date. Overrides the file system's modified time, and fills "Last updated" if `updated` is missing. |
| `updated` | A revision date. Shown as "Last updated" instead of `date` when newer. |
| `lastUpdated` | Set to `false` to hide the "Last updated" timestamp on this page. |
| `cover` | The feature and social preview image. Takes the same class attributes as inline images, e.g. `cover: /assets/hero.webp {.full}`. |
| `keywords` | A list of terms for the meta keywords tag. |

**Navigation**

| Field | Description |
| --- | --- |
| `redirect` | Sends the reader to another URL instead of rendering this page. |
| `page-next` | Adds a link to the next page, using its title. |
| `page-prev`, `page-previous` | Adds a link to the previous page. Either key name works. |
| `pagination` | Set to `false` to remove both next and previous links. |

**Search visibility**

| Field | Description |
| --- | --- |
| `sitemap` | Set to `false` to drop the page from `sitemap.xml`. It stays live and reachable. |
| `noindex` | Set to `true` to add a `noindex` meta tag and header, which also implies `sitemap: false`. Set `"noIndex": { "pages": true }` in `content/config.json` to apply this to every page at once. |

**Incidents and maintenance**

Files under `content/incidents/*.md` use a few extra fields of their own: `maintenance`, `end`, `start` (an alias for `date`), and `monitors`. See the guide's [Incidents and maintenance](/guide/#incidents-and-maintenance) section for how they link to a monitor.

Changes here hot-reload, so a field is easy to try and adjust while you write.

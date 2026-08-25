namespace Warden.Services.Layout;

public static partial class LayoutProvider
{
    private static GeneratedAsset? _cssAsset;

    private static string GetStylesLink(string themeTokenCss, string themeComponentCss, string basePath)
    {
        var asset = GetStylesAsset(themeTokenCss, themeComponentCss, basePath);
        return $"<link rel=\"stylesheet\" href=\"{basePath}/warden.css?v={asset.Version}\">";
    }

    internal static GeneratedAsset GetStylesAsset(string themeTokenCss, string themeComponentCss, string basePath) =>
        GetOrBuildAsset(ref _cssAsset, themeTokenCss + " " + themeComponentCss + " " + basePath,
            () => MinifyCss($@"
        @font-face {{
            font-family: ""Inter"";
            font-style: normal;
            font-weight: 100 900;
            font-display: swap;
            src: url(""{basePath}/fonts/Inter.woff2"") format(""woff2"");
        }}
        {themeTokenCss} * {{
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }}
        .select-none {{
            -webkit-touch-callout: none;
            -webkit-user-select: none;
            -moz-user-select: none;
            -ms-user-select: none;
            user-select: none;
        }}
        html, body {{
            /* `clip` not `hidden`: `hidden` makes body a scroll container and breaks sticky sidebars. */
            overflow-x: clip;
        }}
        html {{
            /* Reserve the scrollbar gutter so centered content does not shift between pages that scroll and pages that do not. */
            scrollbar-gutter: stable;
        }}
        body {{
            font-family: var(--font-sans);
            background-color: var(--bg-color);
            color: var(--text-color);
            line-height: 1.6;
            -webkit-font-smoothing: antialiased;
            transition: background-color 0.15s ease, color 0.15s ease;
        }}
        #scroll-indicator {{
            position: fixed; top: 0; left: 0; height: 3px; width: 100%;
            background-color: var(--accent); z-index: 1101;
            transform: scaleX(0); transform-origin: left;
            transition: transform 0.15s ease;
        }}
        :focus-visible {{
            outline: 2px solid var(--accent);
            outline-offset: 2px;
        }}
        .skip-link {{
            position: absolute; top: 0; left: 0; z-index: 1100;
            width: 1px; height: 1px; overflow: hidden;
            clip-path: inset(50%); white-space: nowrap;
            background: var(--accent); color: #fff; padding: 0.75rem 1.25rem;
            border-radius: 0 0 6px 0; text-decoration: none; font-size: 0.9rem;
        }}
        .skip-link:focus {{
            width: auto; height: auto; overflow: visible;
            clip-path: none; white-space: normal;
        }}
        .no-theme-transition, .no-theme-transition * {{
            transition: none !important;
        }}
        /* --topbar-height, --promo-bg and --promo-text live in ThemeDefaults with the rest of the tokens. */
        /* z-index scale: overlay 1001 < topbar 1002 < drawer 1003 < skip-link 1100 < scroll-indicator 1101. */
        .icon-btn {{
            display: inline-flex; align-items: center; justify-content: center;
            width: 36px; height: 36px; border-radius: 6px; border: none;
            background: transparent; color: var(--text-muted); cursor: pointer;
            flex-shrink: 0; text-decoration: none;
            transition: color 0.15s ease, background-color 0.15s ease;
        }}
        .icon-btn:hover {{
            color: var(--accent);
            background-color: var(--code-bg);
        }}
        .icon-btn svg {{
            width: 18px;
            height: 18px;
        }}
        .timezone-widget {{
            position: relative;
        }}
        .timezone-toggle {{
            position: relative;
        }}
        .timezone-toggle--overridden::before {{
            content: ""!"";
            position: absolute; top: 3px; right: 3px;
            width: 12px; height: 12px; border-radius: 50%;
            background: var(--alert-warning); color: var(--bg-color);
            font: 700 9px/12px var(--font-sans); text-align: center;
            box-shadow: 0 0 0 2px var(--bg-color);
        }}
        .timezone-dropdown {{
            position: absolute; top: calc(100% + 6px); right: -0.5rem; width: min(260px, calc(100vw - 2rem));
            background-color: var(--bg-color); border: 1px solid var(--border); border-radius: 8px;
            box-shadow: var(--shadow-md); z-index: 1003; padding: 0.5rem;
        }}
        .timezone-search {{
            width: 100%;
            box-sizing: border-box;
            padding: 0.45rem 0.6rem;
            border: 1px solid var(--border);
            border-radius: 6px;
            background: var(--bg-color);
            color: var(--text-color);
            font-size: 0.85rem;
        }}
        .timezone-search:focus {{
            outline: 2px solid var(--accent);
            outline-offset: 1px;
        }}
        .timezone-options {{
            max-height: 220px;
            overflow-y: auto;
            margin-top: 0.4rem;
            display: flex;
            flex-direction: column;
        }}
        .timezone-option {{
            appearance: none; -webkit-appearance: none;
            display: flex; align-items: center; gap: 0.5rem;
            width: 100%;
            text-align: left;
            font: inherit;
            font-size: 0.85rem;
            color: var(--text-color);
            background: transparent;
            border: none;
            border-radius: 6px;
            padding: 0.4rem 0.6rem;
            cursor: pointer;
        }}
        .timezone-option:hover, .timezone-option:focus {{
            background-color: var(--code-bg);
            color: var(--accent);
            outline: none;
        }}
        .timezone-option--current {{
            background-color: var(--accent-light);
            color: var(--accent);
        }}
        .timezone-option--current:hover, .timezone-option--current:focus {{
            background-color: var(--accent-light);
            filter: brightness(0.96);
        }}
        .timezone-option-tag {{
            display: inline-flex; flex: none; color: var(--text-muted);
        }}
        .timezone-option--current .timezone-option-tag {{
            color: var(--accent);
        }}
        .timezone-option-tag svg {{
            width: 14px; height: 14px;
        }}
        .timezone-options-divider {{
            height: 1px; background: var(--border); margin: 0.35rem 0;
        }}
        .topbar {{
            display: flex; align-items: center; justify-content: space-between;
            height: var(--topbar-height); padding: 0 1.5rem;
            background-color: var(--bg-color); border-bottom: 1px solid var(--border);
            position: sticky; top: 0; z-index: 1002;
        }}
        .topbar-left {{
            display: flex; align-items: center; gap: 0.75rem;
        }}
        .topbar-right {{
            display: flex; align-items: center; gap: 1rem;
        }}
        .top-nav {{
            position: absolute; left: 50%; transform: translateX(-50%);
            display: flex; align-items: center; gap: 1.5rem; height: 100%;
        }}
        .top-nav-item {{
            display: flex;
            align-items: center;
            height: 100%;
            position: relative;
        }}
        .top-nav-link {{
            display: inline-flex; align-items: center; gap: 0.3rem;
            font-size: 0.9rem; font-weight: 500; color: var(--text-muted);
            text-decoration: none; background: none; border: none; cursor: pointer;
            padding: 0; font-family: inherit; touch-action: manipulation;
        }}
        .top-nav-link:hover, .top-nav-link.active {{
            color: var(--accent);
        }}
        .top-nav-chevron {{
            width: 14px;
            height: 14px;
            transition: transform 0.15s ease;
        }}
        .top-nav-item.has-dropdown.open .top-nav-chevron {{
            transform: rotate(180deg);
        }}
        @media (hover: hover) and (pointer: fine) {{
            .site-nav:not(:has(.top-nav-item.has-dropdown.open)) .top-nav-item.has-dropdown:hover .top-nav-chevron {{
                transform: rotate(180deg);
            }}
        }}
        .top-nav-dropdown-menu {{
            display: none; position: absolute; top: 100%; left: 0; min-width: 180px;
            background-color: var(--bg-color); border: 1px solid var(--border); border-radius: 8px;
            padding: 0.4rem; box-shadow: var(--shadow-md); z-index: 1003;
        }}
        .top-nav-item.has-dropdown.open .top-nav-dropdown-menu {{
            display: block;
        }}
        @media (hover: hover) and (pointer: fine) {{
            .site-nav:not(:has(.top-nav-item.has-dropdown.open)) .top-nav-item.has-dropdown:hover .top-nav-dropdown-menu {{
                display: block;
            }}
        }}
        .top-nav-dropdown-link {{
            display: flex; align-items: center; justify-content: space-between; gap: 0.5rem;
            padding: 0.45rem 0.6rem; border-radius: 6px;
            font-size: 0.875rem; color: var(--text-color); text-decoration: none;
        }}
        .top-nav-dropdown-link:hover {{
            background-color: var(--code-bg); color: var(--accent);
        }}
        .layout {{
            display: grid;
            grid-template-columns: 270px 1fr 270px;
            min-height: calc(100vh - var(--topbar-height));
        }}
        .layout.no-left-sidebar {{
            grid-template-columns: 1fr 270px;
        }}
        @media (min-width: 769px) {{
            .layout.no-left-sidebar > .sidebar-left {{
                display: none;
            }}
        }}
        .sidebar-left {{
            background-color: var(--sidebar-bg);
            border-right: 1px solid var(--border);
            padding: 2.75rem 1.75rem;
            position: sticky; top: var(--topbar-height); align-self: start;
            height: calc(100vh - var(--topbar-height)); overflow-y: auto;
        }}
        .brand a {{
            font-size: 1.1rem; font-weight: 600; letter-spacing: -0.02em;
            color: var(--text-color); text-decoration: none;
        }}
        .brand a:hover {{
            color: var(--accent);
        }}
        .brand img {{
            height: 22px; width: auto; vertical-align: middle; margin-right: 0.75rem;
        }}
        .theme-toggle .icon-moon {{
            display: none;
        }}
        :root[data-theme=""dark""] .theme-toggle .icon-sun {{
            display: none;
        }}
        :root[data-theme=""dark""] .theme-toggle .icon-moon {{
            display: block;
        }}
        @media (prefers-color-scheme: dark) {{
            :root:not([data-theme=""light""]) .theme-toggle .icon-sun {{
                display: none;
            }}
            :root:not([data-theme=""light""]) .theme-toggle .icon-moon {{
                display: block;
            }}
        }}
        .sr-only {{
            position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px;
            overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0;
        }}
        .nav-group {{
            margin-bottom: 2.25rem;
        }}
        .nav-group-title {{
            font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.05em;
            color: var(--text-muted); margin-bottom: 1rem; font-weight: 600;
        }}
        .nav-list {{
            list-style: none;
        }}
        .nav-item a {{
            display: block; padding: 0.55rem 0.8rem; line-height: 1.4;
            color: var(--text-muted); text-decoration: none; font-size: 0.9rem;
            border-radius: 6px; margin-left: -0.8rem;
            transition: color 0.15s ease, background-color 0.15s ease;
        }}
        .nav-item a:hover {{
            color: var(--text-color); background-color: var(--nav-hover-bg);
        }}
        .nav-item.active a {{
            color: var(--accent); background-color: var(--nav-active-bg); font-weight: 500;
        }}
        .sidebar-tree {{
            font-size: 0.9rem;
        }}
        .sidebar-group {{
            margin-bottom: 0.25rem;
        }}
        .sidebar-group-summary {{
            display: block; list-style: none; cursor: pointer;
        }}
        .sidebar-group-summary::-webkit-details-marker {{
            display: none;
        }}
        .sidebar-group-summary::marker {{
            content: """";
        }}
        .sidebar-group.no-caret > .sidebar-group-title {{
            cursor: default;
        }}
        .sidebar-group-title {{
            display: flex; align-items: center; gap: 0.4rem;
            padding: 0.5rem 0.8rem; border-radius: 6px;
            user-select: none; transition: background-color 0.15s ease;
        }}
        .sidebar-group-summary:hover .sidebar-group-title {{
            background-color: var(--code-bg);
        }}
        /* Only the caret should distinguish colapsible from static groups, not typography */
        .sidebar-group-title h2, .sidebar-group-title h3 {{
            font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.05em;
            color: var(--text-muted); font-weight: 600; flex: 1; margin: 0;
        }}
        /* Ancestors get a color cue only; the background is reserved for the one active leaf. */
        .sidebar-group-title.has-active h2, .sidebar-group-title.has-active h3 {{
            color: var(--accent);
        }}
        .caret-icon {{
            display: inline-flex; flex-shrink: 0; width: 16px; height: 16px;
            color: var(--text-muted); transition: transform 0.2s ease;
        }}
        .caret-icon svg {{
            width: 100%;
            height: 100%;
        }}
        details[open] > .sidebar-group-summary .caret-icon {{
            transform: rotate(90deg);
        }}
        .sidebar-group-items {{
            padding-left: 0.9rem;
            margin-bottom: 0.5rem;
        }}
        .sidebar-tree > .sidebar-group > .sidebar-group-items {{
            padding-left: 0;
        }}
        .sidebar-link {{
            margin-bottom: 0.1rem;
        }}
        /* Direct children only, so items inside a group stay tightly packed. */
        .sidebar-tree > .sidebar-group + .sidebar-group,
        .sidebar-tree > .sidebar-group + .sidebar-link,
        .sidebar-tree > .sidebar-link + .sidebar-group,
        .sidebar-tree > .sidebar-link + .sidebar-link {{
            border-top: 1px solid var(--border);
            padding-top: 0.75rem;
            margin-top: 0.75rem;
        }}
        .sidebar-link a {{
            display: block; 
            padding: 0.45rem 0.8rem; 
            line-height: 1.4;
            color: var(--text-muted); 
            text-decoration: none; font-size: 0.875rem;
            border-radius: 6px; 
            transition: color 0.15s ease, background-color 0.15s ease;
        }}
        .sidebar-link a:hover {{
            color: var(--text-color);
            background-color: var(--nav-hover-bg);
        }}
        .sidebar-link.is-active a {{
            color: var(--accent); background-color: var(--nav-active-bg); font-weight: 500;
        }}
        .main-container {{
            padding: 3rem 4rem;
            max-width: 800px; justify-self: center; width: 100%;
            min-width: 0;
        }}
        .breadcrumb {{
            display: flex; align-items: center; gap: 0.4rem;
            margin-bottom: 1.5rem; font-size: 0.8rem; flex-wrap: wrap;
        }}
        .breadcrumb a {{
            color: var(--text-muted); text-decoration: none;
            transition: color 0.15s ease;
        }}
        .breadcrumb a:hover {{
            color: var(--accent);
        }}
        .breadcrumb .separator {{
            color: var(--text-muted);
        }}
        .breadcrumb .crumb-text {{
            color: var(--text-muted);
        }}
        .breadcrumb .current {{
            color: var(--text-color);
            font-weight: 500;
        }}
        .prose h1 {{
            font-size: 2.2rem; font-weight: 600; letter-spacing: -0.03em;
            margin-bottom: 1rem; scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose h2, .prose h3, .prose h4, .prose h5, .prose h6 {{
            position: relative;
        }}
        .prose h1:target, .prose h2:target, .prose h3:target,
        .prose h4:target, .prose h5:target, .prose h6:target {{
            animation: warden-target-flash 2s ease-out;
        }}
        .prose a.footnote-ref:target,
        .prose a.footnote-back-ref:target {{
            background-color: var(--accent-light); outline: 2px solid var(--accent);
            border-radius: 4px; padding: 0 0.2em; scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose .footnotes li:target {{
            background-color: var(--accent-light); outline: 2px solid var(--accent);
            border-radius: 6px; padding: 0.25rem 0.6rem; margin-left: -0.6rem;
            scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose abbr[data-tip], .status-tick[data-tip], .icon-btn[data-tip], .status-monitor-name[data-tip] {{
            position: relative;
            -webkit-tap-highlight-color: transparent;
        }}
        .prose abbr[data-tip] {{
            cursor: help;
            text-decoration: underline dotted var(--text-muted);
            text-decoration-thickness: 1px; text-underline-offset: 0.2em;
        }}
        .prose abbr[data-tip]:focus, .status-tick[data-tip]:focus, .icon-btn[data-tip]:focus, .status-monitor-name[data-tip]:focus {{
            outline: none;
        }}
        .prose abbr[data-tip]:focus-visible, .status-tick[data-tip]:focus-visible, .icon-btn[data-tip]:focus-visible, .status-monitor-name[data-tip]:focus-visible {{
            outline: 2px solid var(--accent); outline-offset: 2px; border-radius: 3px;
        }}
        .prose abbr[data-tip]::after, .status-tick[data-tip]::after, .icon-btn[data-tip]::after, .status-monitor-name[data-tip]::after {{
            content: attr(data-tip);
            position: absolute; left: 50%; bottom: calc(100% + 0.45rem);
            transform: translateX(calc(-50% + var(--tip-shift, 0px))) translateY(0.2rem); z-index: 20;
            width: max-content; max-width: min(15rem, 60vw);
            padding: 0.4rem 0.6rem;
            background-color: var(--sidebar-bg); color: var(--text-color);
            border: 1px solid var(--accent); border-radius: 6px;
            box-shadow: var(--shadow-md);
            font: 400 0.8rem/1.4 var(--font-sans);
            text-align: center; text-decoration: none; white-space: normal;
            opacity: 0; visibility: hidden; pointer-events: none;
            transition: opacity 0.12s ease, transform 0.12s ease, visibility 0.12s;
        }}
        /* topbar buttons sit at the very top of the viewport, so their bubble opens downward instead */
        .icon-btn[data-tip]::after {{
            bottom: auto; top: calc(100% + 0.45rem);
            transform: translateX(calc(-50% + var(--tip-shift, 0px))) translateY(-0.2rem);
        }}
        .prose abbr[data-tip]:hover::after, .prose abbr[data-tip]:focus::after,
        .status-tick[data-tip]:hover::after, .status-tick[data-tip]:focus::after,
        .status-monitor-name[data-tip]:hover::after, .status-monitor-name[data-tip]:focus::after,
        .status-monitor-name[data-tip].tip-open::after {{
            opacity: 1; visibility: visible; transform: translateX(calc(-50% + var(--tip-shift, 0px))) translateY(0);
        }}
        .icon-btn[data-tip]:hover::after, .icon-btn[data-tip]:focus::after {{
            opacity: 1; visibility: visible; transform: translateX(calc(-50% + var(--tip-shift, 0px))) translateY(0);
        }}
        /* an open dropdown panel sits right where the hover bubble would, so suppress the bubble while open */
        .icon-btn[aria-expanded=""true""]::after {{
            display: none;
        }}
        @media (prefers-reduced-motion: reduce) {{
            .prose abbr[data-tip]::after, .status-tick[data-tip]::after, .icon-btn[data-tip]::after {{
                transition: none;
            }}
        }}
        @keyframes warden-target-flash {{
            0%, 40% {{
                background-color: var(--accent-light);
            }}
            100% {{
                background-color: transparent;
            }}
        }}
        @media (prefers-reduced-motion: reduce) {{
            .prose h1:target, .prose h2:target, .prose h3:target,
            .prose h4:target, .prose h5:target, .prose h6:target {{
                animation: none; background-color: var(--accent-light);
            }}
        }}
        .header-anchor {{
            position: absolute; left: -1.5rem; top: 0; bottom: 0;
            display: inline-flex; align-items: center;
            opacity: 0; text-decoration: none; font-weight: 400;
            color: var(--text-muted);
            transition: opacity 0.15s ease, color 0.15s ease;
        }}
        .header-anchor::before {{
            content: ""#"";
        }}
        .header-anchor:hover {{
            color: var(--accent);
        }}
        .prose h2:hover .header-anchor, .prose h3:hover .header-anchor,
        .prose h4:hover .header-anchor, .prose h5:hover .header-anchor,
        .prose h6:hover .header-anchor, .header-anchor:focus {{
            opacity: 1;
        }}
        .prose h2 {{
            font-size: 1.4rem; font-weight: 500; letter-spacing: -0.02em;
            margin-top: 2.5rem; margin-bottom: 1rem; padding-bottom: 0.3rem;
            border-bottom: 1px solid var(--border); scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose p {{
            color: var(--text-color); margin-bottom: 1.25rem;
            text-decoration-color: var(--border); text-underline-offset: 2px;
        }}
        .prose a {{
            color: var(--accent); text-decoration: underline;
            text-decoration-color: var(--border); text-underline-offset: 2px;
            transition: text-decoration-color 0.15s ease;
        }}
        .prose a:hover {{
            text-decoration-color: var(--accent);
        }}
        .prose ul, .prose ol {{
            padding-left: 1.5rem; margin-bottom: 1.25rem;
        }}
        .prose li {{
            margin-bottom: 0.4rem;
        }}
        .prose li > ul, .prose li > ol {{
            margin-top: 0.4rem; margin-bottom: 0;
        }}
        .prose hr {{
            border: none; border-top: 1px solid var(--border); margin: 2.5rem 0;
        }}
        .prose h3 {{
            font-size: 1.15rem; font-weight: 500; letter-spacing: -0.01em;
            margin-top: 2rem; margin-bottom: 0.75rem; scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose h4 {{
            font-size: 1rem; font-weight: 500;
            margin-top: 1.5rem; margin-bottom: 0.5rem; scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        .prose h5, .prose h6 {{
            font-size: 0.9rem; font-weight: 600;
            margin-top: 1.25rem; margin-bottom: 0.5rem; scroll-margin-top: calc(var(--topbar-height) + 1rem);
        }}
        pre {{
            background-color: var(--code-bg);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 1.25rem;
            overflow-x: auto;
            font-family: var(--font-mono);
            font-size: 0.85rem;
            margin: 1.5rem 0;
        }}
        code {{
            font-family: var(--font-mono);
            background-color: var(--code-bg);
            padding: 0.2rem 0.4rem;
            border-radius: 4px;
            font-size: 0.85rem;
        }}
        pre code {{
            padding: 0; background-color: transparent; border-radius: 0;
        }}
        .prose dl {{
            margin: 1.25rem 0;
            padding: 0;
        }}
        .prose dt {{
            font-weight: 600;
            color: var(--text-color);
            margin-top: 1rem;
        }}
        .prose dl > dt:first-child {{
            margin-top: 0;
        }}
        .prose dd {{
            margin: 0.3rem 0 0;
            padding-left: 1rem;
            border-left: 2px solid var(--border);
            color: var(--text-muted);
        }}
        .prose h1 code, .prose h2 code, .prose h3 code,
        .prose h4 code, .prose h5 code, .prose h6 code {{
            background: none; padding: 0; border-radius: 0; font-size: inherit;
        }}
        /* Fenced code block chrome */
        .prose div[class^=""language-""] {{
            position: relative;
            margin: 1.5rem 0;
            background-color: var(--code-bg);
            border: 1px solid var(--border);
            border-radius: 8px;
            overflow: hidden;
        }}
        .prose div[class^=""language-""] pre {{
            margin: 0; border: none; border-radius: 0; padding-top: 2rem;
        }}
        .prose div[class^=""language-""] .lang {{
            position: absolute; top: 0.6rem; left: 1rem; right: auto;
            font-size: 0.7rem; color: var(--text-muted);
            font-family: var(--font-sans); text-transform: lowercase;
            user-select: none; z-index: 1;
        }}
        .prose div[class^=""language-""] button.copy {{
            display: none;
        }}
        .prose div[class^=""language-""] .code-title {{
            padding: 0.6rem 1rem; font-size: 0.8rem; font-family: var(--font-mono);
            color: var(--text-muted); border-bottom: 1px solid var(--border);
        }}
        .prose div[class^=""language-""].has-title .lang {{
            display: none;
        }}
        .prose div[class^=""language-""].has-title pre {{
            padding-top: 0.75rem;
        }}
        .shiki, .shiki span {{
            color: var(--shiki-light);
        }}
        .shiki {{
            background-color: var(--code-bg);
        }}
        @media (prefers-color-scheme: dark) {{
            :root:not([data-theme=""light""]) .shiki, :root:not([data-theme=""light""]) .shiki span {{
                color: var(--shiki-dark);
            }}
        }}
        :root[data-theme=""dark""] .shiki, :root[data-theme=""dark""] .shiki span {{
            color: var(--shiki-dark);
        }}
        .prose .line {{
            display: inline-block;
            width: 100%;
            min-height: 1.4em;
        }}
        .prose .line.highlighted {{
            background-color: var(--accent-light);
            margin: 0 -1.25rem; padding: 0 1.25rem;
            box-shadow: 2px 0 0 var(--accent) inset;
        }}
        .prose .line.highlighted.error {{
            box-shadow: 2px 0 0 var(--alert-caution) inset;
        }}
        .prose .line.highlighted.warning {{
            box-shadow: 2px 0 0 var(--alert-warning) inset;
        }}
        .prose .line.diff {{
            margin: 0 -1.25rem;
            padding: 0 1.25rem;
        }}
        .prose .line.diff.add {{
            background-color: color-mix(in srgb, var(--alert-tip) 15%, transparent);
        }}
        .prose .line.diff.remove {{
            background-color: color-mix(in srgb, var(--alert-caution) 15%, transparent);
            opacity: 0.7;
        }}
        .prose div[class^=""language-""].has-focused-lines .line {{
            opacity: 0.5;
            filter: blur(0.06rem);
            transition: opacity 0.2s, filter 0.2s;
        }}
        .prose div[class^=""language-""].has-focused-lines .line.has-focus {{
            opacity: 1;
            filter: none;
        }}
        .prose .line-numbers-mode pre {{
            padding-left: 2.5rem;
        }}
        .prose .line-numbers-wrapper {{
            position: absolute; top: 3.5rem; left: 0; width: 2rem;
            text-align: right; color: var(--text-muted); font-family: var(--font-mono);
            font-size: 0.85rem; line-height: 1.6; user-select: none;
        }}
        /* Custom containers: ::: tip / warning / danger / info / details */
        .prose .custom-block {{
            margin: 1rem 0; padding: 1rem !important; border-radius: 8px;
            line-height: 1.5; font-size: 0.95rem; color: var(--text-muted);
            background-color: var(--accent-light);
        }}
        .prose .custom-block p:not(.custom-block-title) {{
            margin: 0;
        }}
        .prose .custom-block ul {{
            color: var(--text-color);
            margin-top: 1rem;
            margin-bottom: 1.25rem;
        }}
        .prose .custom-block.tip {{
            color: var(--alert-tip);
            background-color: color-mix(in srgb, var(--alert-tip) 10%, var(--bg-color));
        }}
        .prose .custom-block.info {{
            color: var(--alert-note);
            background-color: color-mix(in srgb, var(--alert-note) 10%, var(--bg-color));
        }}
        .prose .custom-block.warning {{
            color: var(--alert-warning);
            background-color: color-mix(in srgb, var(--alert-warning) 10%, var(--bg-color));
        }}
        .prose .custom-block.danger {{
            color: var(--alert-caution);
            background-color: color-mix(in srgb, var(--alert-caution) 10%, var(--bg-color));
        }}
        .prose .custom-block-title {{
            font-weight: 700;
            margin: 0 0 0.5rem;
        }}
        .prose .custom-block a {{
            color: inherit; font-weight: 600; text-decoration: underline;
            text-decoration-color: currentColor; text-underline-offset: 2px;
        }}
        .prose .custom-block a:hover {{
            opacity: 0.75;
        }}
        .prose details.custom-block > summary {{
            font-weight: 700;
            cursor: pointer;
            margin: 0;
        }}
        .prose details.custom-block[open] > summary {{
            padding-bottom: 0.6rem;
        }}
        .prose details.custom-block:not([open]) {{
            padding-bottom: 0;
        }}
        .prose details.custom-block[open] > :last-child {{
            margin-bottom: 0;
        }}
        .prose details.custom-block > p:not(.custom-block-title) {{
            margin: 0.9rem 0;
        }}
        .prose .mermaid, .prose .nomnoml {{
            margin: 1.5rem 0;
        }}
        .prose .warden-code-group {{
            margin: 1.5rem 0;
        }}
        .prose .warden-code-group .tabs {{
            display: flex; gap: 0.25rem; border-bottom: 1px solid var(--border);
        }}
        .prose .warden-code-group .tabs input {{
            display: none;
        }}
        .prose .warden-code-group .tabs label {{
            display: inline-flex; align-items: center; gap: 0.35rem;
            padding: 0.5rem 0.9rem; font-size: 0.85rem; color: var(--text-muted);
            cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px;
        }}
        .prose .warden-code-group .tabs .tab-icon {{
            display: inline-block;
            width: 14px;
            height: 14px;
            flex-shrink: 0;
            background-color: currentColor;
            -webkit-mask: var(--icon) center / contain no-repeat;
            mask: var(--icon) center / contain no-repeat;
        }}
        .prose .warden-code-group .blocks > div[class^=""language-""] {{
            display: none;
            margin-top: 0;
            border-top-left-radius: 0;
            border-top-right-radius: 0;
        }}
        .prose .warden-code-group .blocks > div[class^=""language-""].active {{
            display: block;
        }}
        .prose .warden-code-group .tabs label.active-tab {{
            color: var(--text-color);
            border-bottom-color: var(--accent);
        }}
        .table-wrapper {{
            overflow-x: auto; -webkit-overflow-scrolling: touch;
            margin: 1.5rem 0; border-radius: 6px;
        }}
        .task-list-item input[type=""checkbox""] {{
            width: 1em; height: 1em; margin: 0 0.4em 0 0;
            vertical-align: middle;
        }}
        .prose table {{
            width: 100%; border-collapse: collapse;
            font-size: 0.875rem;
        }}
        .prose th, .prose td {{
            padding: 0.6rem 1rem; border: 1px solid var(--border);
            text-align: left; vertical-align: top;
        }}
        .prose th {{
            background-color: var(--accent-light); font-weight: 600;
            color: var(--text-color);
        }}
        .prose tr:nth-child(even) {{
            background-color: var(--code-bg);
        }}
        .prose tr:nth-child(even) code {{
            background-color: color-mix(in srgb, var(--accent) 8%, var(--code-bg));
        }}
        .code-block-wrapper {{
            position: relative;
        }}
        .code-block-buttons {{
            position: absolute; top: 0.5rem; right: 0.5rem;
            display: flex; gap: 0.25rem; opacity: 0;
            transition: opacity 0.15s ease;
        }}
        .code-block-wrapper:hover .code-block-buttons,
        .code-block-wrapper:focus-within .code-block-buttons {{
            opacity: 1;
        }}
        .code-block-buttons button {{
            background: var(--code-button-bg); border: 1px solid var(--code-button-border);
            border-radius: 6px; width: 32px; height: 32px;
            display: flex; align-items: center; justify-content: center;
            color: var(--text-muted); cursor: pointer; flex-shrink: 0;
            transition: color 0.15s ease, border-color 0.15s ease;
        }}
        .code-block-buttons button svg {{
            display: block; pointer-events: none;
        }}
        .code-block-buttons button:hover {{
            color: var(--code-button-hover); border-color: var(--code-button-hover);
        }}
        .code-block-buttons button.copied {{
            color: var(--code-button-hover); border-color: var(--code-button-hover);
        }}
        .code-block-buttons button.failed {{
            opacity: 0.5;
        }}
        .markdown-alert {{
            padding: 0.75rem 1rem; margin: 1.5rem 0;
            border-left: 4px solid var(--accent);
            border-radius: 0 8px 8px 0;
            background-color: var(--accent-light);
        }}
        .markdown-alert-title {{
            display: flex; align-items: center; gap: 0.5rem;
            font-weight: 600; margin-bottom: 0.25rem;
        }}
        .markdown-alert-title svg {{
            width: 18px; height: 18px; flex-shrink: 0;
            fill: currentColor;
        }}
        .markdown-alert-note {{
            border-left-color: var(--alert-note);
            background-color: color-mix(in srgb, var(--alert-note) 10%, var(--bg-color));
        }}
        .markdown-alert-tip {{
            border-left-color: var(--alert-tip);
            background-color: color-mix(in srgb, var(--alert-tip) 10%, var(--bg-color));
        }}
        .markdown-alert-important {{
            border-left-color: var(--alert-important);
            background-color: color-mix(in srgb, var(--alert-important) 10%, var(--bg-color));
        }}
        .markdown-alert-warning {{
            border-left-color: var(--alert-warning);
            background-color: color-mix(in srgb, var(--alert-warning) 10%, var(--bg-color));
        }}
        .markdown-alert-caution {{
            border-left-color: var(--alert-caution);
            background-color: color-mix(in srgb, var(--alert-caution) 10%, var(--bg-color));
        }}
        .markdown-alert-note .markdown-alert-title svg {{
            color: var(--alert-note);
        }}
        .markdown-alert-tip .markdown-alert-title svg {{
            color: var(--alert-tip);
        }}
        .markdown-alert-important .markdown-alert-title svg {{
            color: var(--alert-important);
        }}
        .markdown-alert-warning .markdown-alert-title svg {{
            color: var(--alert-warning);
        }}
        .markdown-alert-caution .markdown-alert-title svg {{
            color: var(--alert-caution);
        }}
        .markdown-alert > :last-child {{
            margin-bottom: 0;
        }}
        /* Markdig lowercases unknown tags, css <badge> needs no extension; self-closing `<Badge/>` swallows the paragraph as it requires a closing tag. */
        badge {{
            display: inline-flex; align-items: center; vertical-align: middle;
            margin: 0 0.3rem; padding: 0.15rem 0.55rem; border-radius: 6px;
            background-color: color-mix(in srgb, var(--alert-tip) 16%, var(--code-bg));
            color: var(--alert-tip); font-family: var(--font-sans);
            font-size: 0.7rem; font-weight: 600; letter-spacing: 0.03em;
            text-transform: uppercase; line-height: 1.5;
        }}
        badge[type=""info""] {{
            background-color: color-mix(in srgb, var(--alert-note) 16%, var(--code-bg));
            color: var(--alert-note);
        }}
        badge[type=""tip""] {{
            background-color: color-mix(in srgb, var(--alert-tip) 16%, var(--code-bg));
            color: var(--alert-tip);
        }}
        badge[type=""warning""] {{
            background-color: color-mix(in srgb, var(--alert-warning) 16%, var(--code-bg));
            color: var(--alert-warning);
        }}
        badge[type=""danger""] {{
            background-color: color-mix(in srgb, var(--alert-caution) 16%, var(--code-bg));
            color: var(--alert-caution);
        }}
        h1 badge, h2 badge, h3 badge, h4 badge {{
            font-size: 0.55em;
            margin-left: 0.5rem;
            vertical-align: middle;
        }}
        .pagination {{
            display: flex; justify-content: space-between;
            margin-top: 5rem; padding-top: 2rem;
            border-top: 1px solid var(--border);
        }}
        .pagination-link {{
            text-decoration: none; color: var(--text-muted);
            display: flex; flex-direction: column; gap: 0.25rem;
            transition: color 0.2s ease;
        }}
        .pagination-link:hover {{
            color: var(--accent);
        }}
        .pagination-link .label {{
            font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.05em;
        }}
        .pagination-link .title {{
            font-size: 1rem; font-weight: 500; color: var(--text-color);
        }}
        .pagination-link:hover .title {{
            color: var(--accent);
        }}
        .pagination-link.next {{
            text-align: right; margin-left: auto;
        }}
        .sidebar-right {{
            padding: 3.5rem 2rem;
            position: sticky; top: var(--topbar-height); align-self: start;
            height: calc(100vh - var(--topbar-height)); overflow-y: auto;
        }}
        .social-links {{
            display: flex; align-items: center; gap: 0.25rem;
        }}
        .social-icon-text {{
            font-size: 0.9rem;
        }}
        .sidebar-social-links {{
            display: none;
        }}
        .content-footer {{
            margin-top: 3rem; padding-top: 1.5rem;
            border-top: 1px solid var(--border);
            font-size: 0.8rem; color: var(--text-muted);
        }}
        .content-footer a {{
            color: var(--accent); text-decoration: none;
        }}
        .content-footer a:hover {{
            text-decoration: underline;
        }}
        .menu-toggle {{
            display: none;
        }}
        .sidebar-overlay {{
            display: none;
        }}
        @media (hover: none) and (pointer: coarse) {{
            .icon-btn {{
                width: 40px;
                height: 40px;
            }}
            .share-trigger {{
                padding: 0.65rem 0.9rem;
            }}
            .code-block-buttons {{
                opacity: 1;
            }}
        }}
        @media (max-width: 1024px) {{
            .layout {{
                grid-template-columns: 240px 1fr;
            }}
            .sidebar-right {{
                display: none;
            }}
            .main-container {{
                padding: 2rem 1.5rem;
            }}
        }}
        @media (prefers-reduced-motion: reduce) {{
            *, *::before, *::after {{
                animation-duration: 0.01ms !important;
                animation-iteration-count: 1 !important;
                transition-duration: 0.01ms !important;
                scroll-behavior: auto !important;
            }}
        }}

        h1, h2, h3, h4, h5, h6 {{
            font-family: var(--font-display);
            letter-spacing: -0.015em;
        }}

        .topbar {{
            --topbar-pad: clamp(1.25rem, 5vw, 2.75rem);
            --topbar-pad-block: 1.5rem;
            --topbar-fade: color-mix(in srgb, var(--bg-color) 92%, transparent);
            position: sticky; top: 0; z-index: 1002;
            display: flex; flex-direction: column; align-items: center; justify-content: flex-start; gap: 0.8rem;
            height: auto; min-height: 0;
            padding: var(--topbar-pad-block) var(--topbar-pad) 1.25rem;
            background: color-mix(in srgb, var(--bg-color) 86%, transparent);
            -webkit-backdrop-filter: blur(8px); backdrop-filter: blur(8px);
            border-bottom: 1px solid var(--border);
            -webkit-user-select: none; user-select: none;
        }}
        .masthead-actions {{
            position: absolute; top: 1.35rem; right: var(--topbar-pad);
            display: inline-flex; align-items: center; gap: 0.5rem;
        }}
        .brand {{
            font-family: var(--font-display); font-size: 1.45rem; font-weight: 600;
            letter-spacing: -0.015em; color: var(--text-color); text-decoration: none;
            display: inline-flex; align-items: center; gap: 0.5rem;
        }}
        .brand img {{
            height: 1.4rem;
            width: auto;
        }}
        .site-nav-wrap {{
            position: relative;
            display: flex;
            justify-content: center;
            width: 100%;
        }}
        .site-nav {{
            display: flex; flex-wrap: wrap; justify-content: center;
            gap: 1.75rem; font-size: 0.9rem;
        }}
        .site-nav a {{
            color: var(--text-muted);
            text-decoration: none;
            padding: 0.35rem 0;
            box-shadow: inset 0 -2px 0 transparent;
            transition: color 0.15s ease, box-shadow 0.15s ease;
        }}
        .site-nav a:hover {{
            color: var(--text-color);
        }}
        .site-nav a.here {{
            color: var(--text-color);
        }}
        .site-nav .top-nav-item {{
            position: relative;
            display: inline-flex;
            align-items: center;
            height: auto;
        }}
        .site-nav .top-nav-link {{
            padding: 0.35rem 0;
            font-size: 0.9rem;
            font-weight: 400;
            color: var(--text-muted);
        }}
        .site-nav .top-nav-link:hover {{
            color: var(--text-color);
        }}
        .site-nav .top-nav-link.active {{
            color: var(--text-color);
            box-shadow: inset 0 -2px 0 var(--accent);
        }}
        .site-nav .top-nav-chevron {{
            width: 13px;
            height: 13px;
        }}
        .site-nav .top-nav-dropdown-menu {{
            display: flex; flex-direction: column; gap: 0.2rem; opacity: 0; visibility: hidden;
            top: 100%; left: 50%; transform: translateX(-50%) translateY(6px);
            margin-top: 0.5rem; min-width: 170px; padding: 0.3rem;
            background: var(--bg-color); border: 1px solid var(--border);
            border-radius: 10px; box-shadow: var(--shadow-md);
            transition: opacity 0.15s ease, transform 0.15s ease, visibility 0.15s;
        }}
        .site-nav .top-nav-dropdown-menu::before {{
            content: """"; position: absolute; top: -0.9rem; left: 0; right: 0; height: 0.9rem;
        }}
        .site-nav .top-nav-item.has-dropdown.open .top-nav-dropdown-menu {{
            display: flex; opacity: 1; visibility: visible;
            transform: translateX(-50%) translateY(0);
        }}
        @media (hover: hover) and (pointer: fine) {{
            .site-nav:not(:has(.top-nav-item.has-dropdown.open)) .top-nav-item.has-dropdown:hover .top-nav-dropdown-menu {{
                display: flex; opacity: 1; visibility: visible;
                transform: translateX(-50%) translateY(0);
            }}
        }}
        .site-nav .top-nav-dropdown-link {{
            justify-content: flex-start; padding: 0.5rem 0.7rem; border-radius: 7px;
            font-size: 0.875rem; color: var(--text-color); white-space: nowrap;
        }}
        .site-nav .top-nav-dropdown-link:hover {{
            background: var(--code-bg); color: var(--text-color);
        }}
        .site-nav .top-nav-dropdown-link.here {{
            background: var(--accent-light); color: var(--accent); font-weight: 600;
        }}
        .site-nav .top-nav-dropdown-link.here:hover {{
            background: var(--accent-light); color: var(--accent);
        }}
        .top-nav-dropdown-menu.top-nav-portal {{
            display: flex; flex-direction: column; gap: 0.2rem;
            position: fixed; margin: 0; opacity: 1; visibility: visible; transform: none;
            min-width: 160px; max-width: calc(100vw - 16px);
            overflow-y: auto; overscroll-behavior: contain;
            padding: 0.3rem;
            background: var(--bg-color); border: 1px solid var(--border);
            border-radius: 10px; box-shadow: var(--shadow-md);
            z-index: 1050;
        }}
        .top-nav-portal .top-nav-dropdown-link {{
            display: flex; align-items: center; justify-content: flex-start; gap: 0.5rem;
            padding: 0.6rem 0.7rem; border-radius: 7px;
            font-size: 0.9rem; color: var(--text-color); text-decoration: none; white-space: nowrap;
            min-height: 44px;
        }}
        .top-nav-portal .top-nav-dropdown-link.here {{
            background: var(--accent-light); color: var(--accent); font-weight: 600;
        }}

        .sidebar-left, .sidebar-right, .sidebar-overlay {{
            display: none !important;
        }}
        .layout, .layout.no-left-sidebar {{
            display: block;
            min-height: 0;
        }}
        .main-container {{
            max-width: var(--measure-wide);
            margin: 0 auto;
            padding: 2.5rem clamp(1.25rem, 5vw, 2.75rem) 0;
            min-height: 0;
        }}
        .content {{
            max-width: none;
            margin: 0;
            padding: 0;
        }}
        .content > :first-child, .content.reading > :first-child,
        .content > .prose:first-child > :first-child {{
            margin-top: 0;
        }}
        .content.reading {{
            max-width: var(--measure);
            margin: 0 auto;
            font-size: 1.125rem;
            line-height: 1.7;
        }}
        .content.reading p {{
            margin: 0 0 1.6rem;
        }}
        .content.reading h1, .content.reading h2, .content.reading h3, .content.reading h4 {{
            font-family: var(--font-display);
            font-weight: 600;
            border-bottom: none;
            padding-bottom: 0;
        }}
        .content.reading h2 {{
            font-size: 1.6rem;
            line-height: 1.3;
            letter-spacing: -0.015em;
            margin: 2.75rem 0 1rem;
        }}
        .content.reading h3 {{
            font-size: 1.3rem;
            line-height: 1.35;
            margin: 2.25rem 0 0.75rem;
        }}
        .content.reading a {{
            text-decoration-color: color-mix(in srgb, var(--accent) 45%, transparent);
        }}

        #scroll-indicator {{
            height: 2px;
            background: linear-gradient(90deg, color-mix(in srgb, var(--accent) 55%, transparent), var(--accent));
        }}

        .list-heading, .prose .list-heading {{
            font-family: var(--font-display);
            font-size: clamp(1.7rem, 1.2rem + 1.5vw, 2.1rem);
            font-weight: 600;
            letter-spacing: -0.02em;
            margin: 2.5rem 0 0.4rem;
        }}
        .list-intro, .prose .list-intro {{
            color: var(--text-muted);
            margin: 0 0 2rem;
            font-size: 1.05rem;
            max-width: 60ch;
        }}
        .list-empty {{
            color: var(--text-muted);
            padding: 2rem 0;
        }}
        .post-card {{
            display: grid;
            grid-template-columns: 1fr 104px;
            gap: clamp(1rem, 3vw, 1.75rem);
            align-items: start;
            padding: 2.15rem 0;
            border-bottom: 1px solid var(--border);
            text-decoration: none;
        }}
        .post-card.post-card-plain {{
            grid-template-columns: 1fr;
        }}
        .post-card-body {{
            min-width: 0;
        }}
        .card-cover {{
            display: block;
            text-decoration: none;
            margin-top: var(--card-cap-inset);
            aspect-ratio: 4 / 3;
            border-radius: 8px;
            overflow: hidden;
            border: 1px solid var(--border);
            background: var(--accent-light);
        }}
        .card-cover img {{
            width: 100%;
            height: 100%;
            object-fit: cover;
            display: block;
        }}
        .card-cover-mark {{
            font-family: var(--font-display);
            font-size: 2rem;
            font-weight: 600;
            line-height: 1;
            color: var(--accent);
        }}
        .post-card-title {{
            font-family: var(--font-display);
            font-size: var(--card-title-size);
            font-weight: 600;
            line-height: 1.2;
            letter-spacing: -0.015em;
            margin: 0 0 0.4rem;
        }}
        .post-card-title a {{
            color: var(--text-color);
            text-decoration: none;
            transition: color 0.15s ease;
        }}
        .post-card-title a:hover {{
            color: var(--accent);
        }}
        .post-excerpt {{
            color: var(--text-muted);
            margin: 0.35rem 0 0;
            max-width: 62ch;
        }}
        .post-meta {{
            display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem;
            font-size: 0.85rem; color: var(--text-muted); letter-spacing: 0.01em;
            font-variant-numeric: tabular-nums;
        }}
        .post-tags {{
            display: inline-flex;
            flex-wrap: wrap;
            gap: 0.4rem;
            margin-left: 0.15rem;
        }}
        .tag-chip {{
            display: inline-block; text-decoration: none;
            color: var(--accent); background: var(--accent-light);
            padding: 0.12rem 0.55rem; border-radius: 999px; font-size: 0.72rem; letter-spacing: 0.01em;
        }}
        .tag-chip:hover {{
            background: color-mix(in srgb, var(--accent) 18%, var(--accent-light));
        }}

        .pager {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 1rem;
            margin-top: 2.5rem;
            padding-top: 1.5rem;
            border-top: 1px solid var(--border);
            font-size: 0.9rem;
        }}
        .pager--archive-only {{
            border-top: none;
            margin-top: 0;
        }}
        .pager a {{
            color: var(--accent);
            text-decoration: none;
        }}
        .pager .pager-older--archive {{
            color: var(--text-muted);
        }}
        .pager .pager-older--archive:hover {{
            color: var(--accent);
        }}
        .pager-status {{
            color: var(--text-muted);
            font-variant-numeric: tabular-nums;
        }}

        .load-more-wrap {{
            display: flex;
            justify-content: center;
            padding: 2.5rem 0 0;
        }}
        .load-more {{
            font-family: var(--font-sans); font-size: 0.9rem; font-weight: 600;
            color: var(--accent); background: none;
            border: 1px solid var(--border); border-radius: 999px;
            padding: 0.6rem 1.5rem; cursor: pointer;
            transition: border-color 0.15s ease, background-color 0.15s ease;
        }}
        .load-more:hover {{
            border-color: var(--accent);
            background-color: var(--accent-light);
        }}
        .load-more[disabled] {{
            opacity: 0.55;
            cursor: default;
        }}
        .skeleton-card {{
            border-bottom: 1px solid var(--border);
            padding: 2.15rem 0;
        }}
        .skeleton {{
            position: relative; overflow: hidden; border-radius: 6px;
            background: var(--sidebar-bg);
        }}
        .skeleton::after {{
            content: """"; position: absolute; inset: 0; transform: translateX(-100%);
            background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--accent) 9%, transparent), transparent);
            animation: warden-shimmer 1.4s ease-in-out infinite;
        }}
        .skeleton-title {{
            height: 1.5rem;
            width: 55%;
            margin: 0.45rem 0 0.7rem;
        }}
        .skeleton-meta {{
            height: 0.85rem;
            width: 32%;
            margin-bottom: 1rem;
        }}
        .skeleton-line {{
            height: 0.9rem;
            width: 100%;
            max-width: 62ch;
            margin-bottom: 0.5rem;
        }}
        .skeleton-line.short {{
            width: 68%;
        }}
        @keyframes warden-shimmer {{ 100% {{ transform: translateX(100%); }} }}
        @media (prefers-reduced-motion: reduce) {{ .skeleton::after {{ animation: none; }} }}

        .post-header {{
            margin-bottom: 0.5rem;
        }}
        /* Off by default: only a theme that wants a kicker above the title shows this. */
        .post-kicker {{
            display: none;
        }}
        .share-trigger {{
            display: inline-flex; align-items: center; gap: 0.35rem;
            font-family: var(--font-sans); font-size: 0.82rem; font-weight: 500;
            color: var(--text-muted); background: none;
            border: 1px solid var(--border); border-radius: 999px;
            padding: 0.25rem 0.75rem; cursor: pointer;
            transition: color 0.15s ease, border-color 0.15s ease;
        }}
        @media (min-width: 621px) {{
            .share-trigger {{
                margin-left: auto;
            }}
        }}
        .share-trigger:hover {{
            color: var(--accent);
            border-color: var(--accent);
        }}
        .share-trigger svg {{
            width: 15px;
            height: 15px;
        }}
        .share-overlay {{
            position: fixed; inset: 0; z-index: 1200; background-color: var(--overlay-bg);
            display: flex; align-items: center; justify-content: center; padding: 1.5rem;
            opacity: 0; transition: opacity 0.15s ease;
        }}
        .share-overlay[hidden] {{
            display: none;
        }}
        .share-overlay.open {{
            opacity: 1;
        }}
        .share-modal {{
            position: relative; width: 100%; max-width: 420px;
            background-color: var(--bg-color); border: 1px solid var(--border); border-radius: 14px;
            box-shadow: var(--shadow-lg); padding: 1.75rem;
            transform: translateY(-12px) scale(0.98); transition: transform 0.15s ease;
        }}
        .share-overlay.open .share-modal {{
            transform: translateY(0) scale(1);
        }}
        .share-modal-close {{
            position: absolute; top: 0.85rem; right: 0.85rem;
        }}
        .share-modal-title {{
            font-family: var(--font-display); font-size: 1.2rem; font-weight: 600;
            letter-spacing: -0.01em; margin: 0 0 1.25rem;
        }}
        .share-actions {{
            display: grid; grid-template-columns: 1fr 1fr; gap: 0.6rem;
        }}
        .share-action {{
            display: flex; align-items: center; gap: 0.6rem;
            padding: 0.7rem 0.9rem; border-radius: 10px;
            border: 1px solid var(--border); background-color: var(--sidebar-bg);
            color: var(--text-color); text-decoration: none;
            font-family: var(--font-sans); font-size: 0.9rem; font-weight: 500;
            cursor: pointer;
            transition: border-color 0.15s ease, background-color 0.15s ease;
        }}
        .share-action:hover {{
            border-color: var(--accent);
            background-color: var(--accent-light);
        }}
        .share-action-icon {{
            display: inline-flex; color: var(--text-muted);
        }}
        .share-action:hover .share-action-icon {{
            color: var(--accent);
        }}
        .share-action-icon svg {{
            width: 18px;
            height: 18px;
        }}
        @media (max-width: 620px) {{
            .share-actions {{
                grid-template-columns: 1fr;
            }}
        }}
        .post-title {{
            font-family: var(--font-display);
            font-size: clamp(1.9rem, 1.2rem + 3vw, 3rem);
            font-weight: 600;
            line-height: 1.1;
            letter-spacing: -0.02em;
            margin: 0.3rem 0 0.9rem;
            text-wrap: balance;
        }}
        .post-header .post-meta {{
            padding-bottom: 1.5rem;
            border-bottom: 1px solid var(--border);
        }}
        .content.reading > p:first-of-type {{
            margin-top: 1.75rem;
        }}
        .content.reading blockquote {{
            font-family: var(--font-display);
            font-weight: 500;
            font-style: normal;
            border-left: 2px solid var(--accent);
            padding: 0.1rem 0 0.1rem 1.5rem;
            margin: 2.2rem 0;
            font-size: 1.25rem;
            line-height: 1.5;
            letter-spacing: -0.01em;
            color: var(--text-color);
        }}
        .post-nav {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 0.75rem;
            margin-top: 4rem;
            padding-top: 1.5rem;
            border-top: 1px solid var(--border);
        }}
        .content .post-nav-link {{
            display: flex;
            flex-direction: column;
            gap: 0.35rem;
            padding: 0.9rem 1.1rem;
            border: 1px solid var(--border);
            border-radius: 12px;
            text-decoration: none;
            transition: border-color 0.15s ease, background-color 0.15s ease;
        }}
        .post-nav-link:hover {{
            border-color: color-mix(in srgb, var(--accent) 40%, var(--border));
            background-color: var(--accent-light);
        }}
        .post-nav-newer {{
            text-align: right;
            align-items: flex-end;
        }}
        .post-nav-label {{
            font-size: 0.72rem;
            text-transform: uppercase;
            letter-spacing: 0.06em;
            color: var(--text-muted);
            text-decoration: none;
        }}
        .post-nav-title {{
            font-family: var(--font-display);
            font-weight: 600;
            font-size: 0.95rem;
            line-height: 1.25;
            color: var(--text-color);
        }}
        .post-nav-link:hover .post-nav-title {{
            color: var(--accent);
        }}
        .page-header {{
            margin: 0 0 2.75rem;
            padding-top: 0.5rem;
        }}
        .status-group-heading:has(.status-filter-clear--header) {{
            display: flex;
            flex-wrap: wrap;
            align-items: baseline;
            justify-content: space-between;
            gap: 0.3rem 1rem;
        }}
        .status-filter-clear--header {{
            display: inline-flex;
            align-items: baseline;
            gap: 0.4rem;
            font-size: 0.85rem;
            font-weight: 500;
            white-space: nowrap;
            flex: none;
        }}
        .page-title {{
            font-family: var(--font-display);
            font-size: clamp(2rem, 1.2rem + 3.2vw, 3.1rem);
            font-weight: 600;
            line-height: 1.08;
            letter-spacing: -0.025em;
            margin: 0;
            text-wrap: balance;
        }}

        .content .status-banner {{
            margin: 0 0 2rem;
            padding: 0.9rem 1.15rem;
            border-radius: 10px;
            font-family: var(--font-display);
            font-weight: 600;
        }}
        .status-banner--up {{
            background: color-mix(in srgb, var(--alert-tip) 15%, var(--bg-color));
            color: var(--alert-tip);
        }}
        .status-banner--down {{
            background: color-mix(in srgb, var(--alert-caution) 15%, var(--bg-color));
            color: var(--alert-caution);
        }}
        .status-group {{
            margin: 0 0 2.5rem;
        }}
        .content.reading .status-group-heading {{
            font-family: var(--font-display);
            font-size: 1.1rem;
            font-weight: 600;
            margin: 0 0 0.9rem;
            padding-bottom: 0;
            border-bottom: none;
        }}
        .content .status-monitor-list {{
            list-style: none;
            margin: 0;
            padding: 0;
            display: flex;
            flex-direction: column;
            gap: 0.7rem;
        }}
        .content .status-monitor {{
            display: flex;
            flex-wrap: wrap;
            align-items: center;
            gap: 0.5rem 1rem;
            margin: 0;
            padding: 0.9rem 1.1rem;
            border: 1px solid var(--border);
            border-radius: 10px;
        }}
        .status-monitor-name {{
            font-weight: 600;
            flex: 1 1 0%;
            min-width: 0;
            overflow: hidden;
            white-space: nowrap;
            text-overflow: ellipsis;
        }}
        .status-monitor-badge {{
            font-size: 0.75rem;
            font-weight: 700;
            letter-spacing: 0.03em;
            text-transform: uppercase;
            padding: 0.2rem 0.6rem;
            border-radius: 999px;
            text-decoration: none;
            flex-shrink: 0;
        }}
        a.status-monitor-badge {{
            cursor: pointer;
            text-decoration: none;
            transition: filter 0.15s ease;
        }}
        a.status-monitor-badge:hover {{
            filter: brightness(1.15);
        }}
        .status-monitor--up .status-monitor-badge {{
            background: color-mix(in srgb, var(--alert-tip) 18%, transparent);
            color: var(--alert-tip);
        }}
        .status-monitor--down .status-monitor-badge {{
            background: color-mix(in srgb, var(--alert-caution) 18%, transparent);
            color: var(--alert-caution);
        }}
        .status-monitor--unknown .status-monitor-badge {{
            background: color-mix(in srgb, var(--text-muted) 20%, transparent);
            color: var(--text-muted);
        }}
        .status-monitor--maintenance .status-monitor-badge {{
            background: color-mix(in srgb, var(--alert-note) 18%, transparent);
            color: var(--alert-note);
        }}
        .status-monitor--degraded .status-monitor-badge {{
            background: color-mix(in srgb, var(--alert-warning) 18%, transparent);
            color: var(--alert-warning);
        }}
        .status-monitor-uptime {{
            font-size: 0.85rem;
            color: var(--text-muted);
            font-variant-numeric: tabular-nums;
        }}
        @media (max-width: 620px) {{
            /* name+badge length varies per monitor, so letting this wrap only when it doesn't fit
               made some rows one line and others two at random - always break it onto its own line instead */
            .status-monitor-uptime {{
                flex-basis: 100%;
            }}
        }}
        .status-monitor-bar, .status-response-chart {{
            display: flex;
            gap: 1px;
            width: 100%;
            margin-top: 0.15rem;
        }}
        .status-response-chart {{
            align-items: flex-end;
            height: 26px;
        }}
        .status-tick {{
            appearance: none; -webkit-appearance: none;
            display: block;
            font: inherit; color: inherit;
            flex: 1 1 0;
            height: 22px;
            min-width: 1px;
            border: 0;
            border-radius: 2px;
            padding: 0;
            text-decoration: none;
            box-shadow: 0 0 0 0 transparent;
            transition: box-shadow 0.2s ease;
        }}
        .status-tick--up {{ background: var(--alert-tip); }}
        .status-tick--down {{ background: var(--alert-caution); }}
        .status-tick--unknown {{ background: var(--border); }}
        .status-tick--degraded {{ background: var(--alert-warning); }}
        /* stripes carry the something-happened signal on their own, so it survives red/green colorblindness and doesn't depend on the accent ring below */
        .status-tick--down, .status-tick--degraded {{
            background-image: repeating-linear-gradient(135deg, rgba(0, 0, 0, 0.22) 0 3px, transparent 3px 7px);
        }}
        /* bg-color gap between the tick's own fill and the accent ring, so a selected down/degraded tick never reads as an accent-colored tick lost against its own red - box-shadow layers paint outer-first, so the gap comes second */
        .status-tick--active-day {{ position: relative; z-index: 1; box-shadow: 0 0 0 2px var(--bg-color), 0 0 0 4px var(--accent); }}
        @media (prefers-reduced-motion: reduce) {{
            .status-tick {{ transition: none; }}
        }}
        .status-filter-clear {{
            color: var(--accent);
        }}
        .status-maintenance, .status-incidents, .status-ongoing-incidents {{
            margin-top: 2.5rem;
        }}
        .content .status-no-incidents {{
            margin: 0;
            color: var(--text-muted);
        }}
        .status-incident, .status-maintenance-item {{
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 0.9rem 1.1rem;
            margin: 0 0 0.7rem;
        }}
        .status-incident-head, .status-maintenance-head {{
            display: flex;
            align-items: center;
            gap: 0.6rem;
            flex-wrap: wrap;
        }}
        @media (max-width: 620px) {{
            .status-incident-badge, .status-maintenance-badge {{
                flex-basis: 100%;
            }}
        }}
        .content.reading .status-incident-title, .content.reading .status-maintenance-title {{
            margin: 0;
            font-size: 1rem;
            font-weight: 500;
            font-family: var(--font-display);
            border-bottom: none;
            min-width: 0;
        }}
        .status-incident-badge, .status-maintenance-badge {{
            display: inline-flex;
            align-items: center;
            gap: 0.4rem;
            font-size: 0.85rem;
            line-height: 1;
        }}
        .status-incident-badge::before, .status-maintenance-badge::before {{
            content: """";
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background: currentColor;
            flex: none;
        }}
        .status-incident-badge--down {{
            color: var(--alert-caution);
            font-weight: 600;
        }}
        .status-incident-badge--degraded {{
            color: var(--alert-warning);
            font-weight: 600;
        }}
        .status-maintenance-badge--active {{
            color: var(--alert-note);
            font-weight: 600;
        }}
        .status-incident-badge--resolved, .status-maintenance-badge--planned, .status-maintenance-badge--ended {{
            color: var(--text-muted);
            font-weight: 500;
        }}
        .status-detail-chrome {{
            display: flex;
            align-items: center;
            gap: 0.75rem;
            flex-wrap: wrap;
            margin: 0.75rem 0 1.25rem;
        }}
        .status-detail-meta {{
            display: flex;
            flex-wrap: wrap;
            gap: 0 1.25rem;
            font-size: 0.9rem;
            color: var(--text-muted);
        }}
        .status-detail-meta-label {{
            font-weight: 600;
            color: var(--text-color);
            margin-right: 0.3rem;
        }}
        .content.reading .status-detail-back {{
            margin: 0 0 0.75rem;
            font-size: 0.9rem;
        }}
        .status-incident-content, .status-maintenance-window {{
            color: var(--text-muted);
            font-size: 0.9rem;
            margin-top: 0.35rem;
        }}
        .content.reading .status-maintenance-description {{
            font-size: 0.9rem;
            margin: 0.5rem 0 0;
        }}
        .content .status-unavailable {{
            margin: 0;
            color: var(--text-muted);
        }}
        .status-incident-title a, .status-maintenance-title a {{
            color: inherit;
            text-decoration: none;
        }}
        .status-incident-title a:hover, .status-maintenance-title a:hover {{
            color: var(--accent);
            text-decoration: underline;
        }}

        .tag-cloud {{
            list-style: none;
            padding: 0;
            margin: 1.5rem 0 0;
            display: flex;
            flex-wrap: wrap;
            gap: 0.6rem;
        }}
        .tag-cloud .tag-chip {{
            font-size: 0.85rem;
            padding: 0.3rem 0.7rem;
        }}
        .tag-count {{
            margin-left: 0.4rem;
            opacity: 0.7;
            font-variant-numeric: tabular-nums;
        }}
        .archive-year {{
            margin-top: 2.5rem;
            margin-bottom: 2.5rem;
        }}
        .archive-year h2 {{
            font-size: 1.2rem;
            font-weight: 500;
            color: var(--text-muted);
            font-variant-numeric: tabular-nums;
            margin-bottom: 0.75rem;
        }}
        .archive-list {{
            list-style: none;
            padding: 0;
            margin: 0;
        }}
        .content .archive-list, .content .tag-cloud, .content .author-grid {{
            padding-left: 0;
        }}
        .archive-list li {{
            display: flex;
            gap: 1rem;
            align-items: baseline;
            padding: 0.5rem 0;
            border-bottom: 1px solid var(--border);
        }}
        .archive-list time {{
            color: var(--text-muted);
            font-size: 0.85rem;
            min-width: 4.5rem;
            font-variant-numeric: tabular-nums;
        }}
        .archive-list a {{
            color: var(--text-color);
            text-decoration: none;
        }}
        .archive-list a:hover {{
            color: var(--accent);
        }}
        .archive-list .archive-title {{
            flex: 1;
            min-width: 0;
        }}
        .archive-list .archive-author {{
            color: var(--text-muted);
            font-size: 0.85rem;
            white-space: nowrap;
        }}
        .archive-author-avatar {{
            display: inline-flex;
            align-self: center;
            flex-shrink: 0;
        }}
        .post-card:last-child, .lead:last-child {{
            border-bottom: none;
        }}
        .archive-year:last-child .archive-list li:last-child {{
            border-bottom: none;
        }}

        .lead {{
            display: grid;
            grid-template-columns: 1.15fr 1fr;
            gap: clamp(1.75rem, 4vw, 3.5rem);
            align-items: start;
            padding: 2rem 0 3rem;
            border-bottom: 1px solid var(--border);
        }}
        .lead-cover {{
            display: block;
            text-decoration: none;
            order: 2;
            margin-top: var(--lead-cap-inset);
            aspect-ratio: 4 / 3;
            border-radius: 12px;
            overflow: hidden;
            border: 1px solid var(--border);
            background: var(--accent-light);
        }}
        .lead-cover img {{
            width: 100%;
            height: 100%;
            object-fit: cover;
            display: block;
        }}
        .cover-mono {{
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        .slug-tint {{
            background: hsl(var(--slug-hue, 145) 30% 88%);
        }}
        .slug-tint .lead-cover-mark, .slug-tint .card-cover-mark {{
            color: hsl(var(--slug-hue, 145) 38% 30%);
        }}
        @media (prefers-color-scheme: dark) {{
            :root:not([data-theme=""light""]) .slug-tint {{
                background: hsl(var(--slug-hue, 145) 20% 19%);
            }}
            :root:not([data-theme=""light""]) .slug-tint .lead-cover-mark,
            :root:not([data-theme=""light""]) .slug-tint .card-cover-mark {{
                color: hsl(var(--slug-hue, 145) 28% 70%);
            }}
        }}
        :root[data-theme=""dark""] .slug-tint {{
            background: hsl(var(--slug-hue, 145) 20% 19%);
        }}
        :root[data-theme=""dark""] .slug-tint .lead-cover-mark,
        :root[data-theme=""dark""] .slug-tint .card-cover-mark {{
            color: hsl(var(--slug-hue, 145) 28% 70%);
        }}
        .lead-cover-mark {{
            font-family: var(--font-display);
            font-size: clamp(3rem, 8vw, 5rem);
            font-weight: 600;
            line-height: 1;
            color: var(--accent);
        }}
        .lead-body {{
            order: 1;
        }}
        .content .lead-title {{
            font-family: var(--font-display);
            font-size: var(--lead-title-size);
            font-weight: 600;
            line-height: 1.14;
            letter-spacing: -0.02em;
            margin: 0 0 0.5rem;
            text-wrap: balance;
        }}
        .lead-title a {{
            color: var(--text-color);
            text-decoration: none;
        }}
        .lead-title a:hover {{
            color: var(--accent);
        }}
        .lead-excerpt {{
            color: var(--text-muted);
            margin: 0.5rem 0 1rem;
        }}
        .readmore {{
            display: inline-flex;
            align-items: center;
            gap: 0.35rem;
            color: var(--accent);
            text-decoration: none;
            font-weight: 600;
            font-size: 0.9rem;
        }}
        .readmore span {{
            transition: transform 0.15s ease;
        }}
        .readmore:hover span {{
            transform: translateX(3px);
        }}

        .byline {{
            gap: 0.55rem;
        }}
        .byline-author {{
            color: var(--text-color);
            font-weight: 500;
        }}
        .byline-link {{
            display: inline-flex;
            align-items: center;
            gap: 0.55rem;
            color: inherit;
            text-decoration: none;
        }}
        .byline-link:hover .byline-author {{
            color: var(--accent);
        }}
        .avatar {{
            width: 26px;
            height: 26px;
            border-radius: 50%;
            object-fit: cover;
            display: inline-grid;
            place-items: center;
            background: radial-gradient(circle at 35% 30%, color-mix(in srgb, var(--accent) 55%, var(--sidebar-bg)), var(--accent));
            color: var(--bg-color);
            font-size: 0.72rem;
            font-weight: 700;
            user-select: none;
        }}
        .avatar-initial {{
            display: inline-grid;
            place-items: center;
            background: radial-gradient(circle at 35% 30%, color-mix(in srgb, var(--accent) 55%, var(--sidebar-bg)), var(--accent));
            color: var(--bg-color);
            font-weight: 700;
        }}
        .author-header {{
            text-align: center;
            padding: 0.5rem 0 2rem;
            border-bottom: 1px solid var(--border);
            margin-bottom: 2.5rem;
        }}
        .author-header-avatar {{
            width: 76px;
            height: 76px;
            border-radius: 50%;
            object-fit: cover;
            margin: 0 auto 1rem;
        }}
        .author-header-avatar.avatar-initial {{
            font-size: 1.9rem;
        }}
        .author-name {{
            font-family: var(--font-display);
            font-size: clamp(1.8rem, 1.2rem + 2.5vw, 2.6rem);
            font-weight: 600;
            letter-spacing: -0.02em;
            margin: 0 0 0.6rem;
        }}
        .author-bio {{
            color: var(--text-muted);
            max-width: 52ch;
            margin: 0 auto;
        }}
        .author-bio p {{
            margin: 0;
        }}
        .author-grid {{
            list-style: none;
            padding: 0;
            margin: 1.5rem 0 0;
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
            gap: 1rem;
        }}
        .author-card {{
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 0.7rem 0.85rem;
            border: 1px solid var(--border);
            border-radius: 12px;
            text-decoration: none;
            color: var(--text-color);
            transition: border-color 0.15s ease;
        }}
        .author-card:hover {{
            border-color: color-mix(in srgb, var(--accent) 40%, var(--border));
        }}
        .author-card-avatar {{
            width: 44px;
            height: 44px;
            border-radius: 50%;
            object-fit: cover;
            flex-shrink: 0;
        }}
        .author-card-avatar.avatar-initial {{
            font-size: 1.1rem;
        }}
        .author-card-name {{
            font-weight: 500;
        }}
        .post-cover {{
            display: block;
            width: 100%;
            aspect-ratio: 16 / 9;
            object-fit: cover;
            border-radius: 12px;
            border: 1px solid var(--border);
            background: var(--sidebar-bg);
            margin: 1.75rem 0;
        }}
        .post-cover.short {{ 
            aspect-ratio: 3 / 1; 
        }}
        .post-cover.tall {{
            aspect-ratio: 5 / 4;
        }}
        .index-cover {{
            max-width: var(--measure);
            margin: 0 auto;
        }}
        .warden-map {{
            width: 100%;
            border-radius: 12px;
            border: 1px solid var(--border);
            margin: 1.75rem 0;
            position: relative;
            z-index: 0;
        }}
        .warden-map img {{
            background: none !important;
            border: 0 !important;
            border-radius: 0 !important;
            box-shadow: none !important;
            max-width: none !important;
        }}
        .warden-map .leaflet-popup-content-wrapper,
        .warden-map .leaflet-popup-tip {{
            background: var(--bg-color);
            color: var(--text-color);
        }}
        .warden-map .leaflet-popup-content-wrapper {{
            border-radius: 10px;
            border: 1px solid var(--border);
        }}
        .warden-map .leaflet-popup-close-button {{
            color: var(--text-muted);
        }}
        .map-popup strong {{
            display: block;
            margin-bottom: 0.35rem;
            font-size: 1.05em;
        }}
        .map-popup p {{
            margin: 0 0 0.4rem;
        }}
        .map-popup > div {{
            margin-top: 0.15rem;
        }}
        .map-popup a {{
            color: var(--accent);
        }}
        .warden-map-error {{
            padding: 0.85rem 1rem;
            border: 1px solid var(--border);
            border-radius: 10px;
            color: var(--text-muted);
            margin: 1.75rem 0 0;
        }}
        .content.reading img {{
            max-width: 100%;
            height: auto;
            border-radius: 10px;
            background: var(--sidebar-bg);
        }}
        .content.reading p > img:only-child,
        .content.reading figure.image-figure img {{
            display: block;
            width: 100%;
            aspect-ratio: 16 / 9;
            object-fit: cover;
            border: 1px solid var(--border);
            border-radius: 12px;
            background: var(--sidebar-bg);
            margin: 1.75rem 0;
        }}
        .content.reading figure.image-figure {{
            margin: 1.75rem 0;
        }}
        .content.reading figure.image-figure img {{
            margin: 0;
        }}
        .content.reading video {{
            display: block;
            width: 100%;
            height: auto;
            max-width: 100%;
            border: 1px solid var(--border);
            border-radius: 12px;
            background: #000;
            margin: 1.75rem 0;
        }}
        .content.reading iframe {{
            display: block;
            width: 100%;
            max-width: 100%;
            aspect-ratio: 16 / 9;
            border: 1px solid var(--border);
            border-radius: 12px;
            background: #000;
            margin: 1.75rem 0;
        }}
        .content.reading audio {{
            display: block;
            width: 100%;
            margin: 1.75rem 0;
        }}
        .content.reading figure.image-figure figcaption {{
            margin-top: 0.65rem;
            font-size: 0.85rem;
            line-height: 1.5;
            color: var(--text-muted);
            text-align: center;
        }}
        .content.reading img.natural,
        .content.reading p > img.natural:only-child,
        .content.reading p.natural > img:only-child,
        .content.reading figure.image-figure img.natural {{
            aspect-ratio: auto;
            object-fit: initial;
            width: auto;
            max-width: 100%;
            margin-inline: auto;
        }}
        .content.reading img.plain,
        .content.reading p > img.plain:only-child,
        .content.reading p.plain > img:only-child,
        .content.reading figure.image-figure img.plain {{
            border: none;
            background: none;
            border-radius: 0;
        }}
        .content.reading img.left, .content.reading p > img.left:only-child,
        .content.reading p.left > img:only-child {{
            float: left;
            width: min(45%, 20rem);
            margin: 0.4rem 1.4rem 1rem 0;
            aspect-ratio: auto;
        }}
        .content.reading img.right, .content.reading p > img.right:only-child,
        .content.reading p.right > img:only-child {{
            float: right;
            width: min(45%, 20rem);
            margin: 0.4rem 0 1rem 1.4rem;
            aspect-ratio: auto;
        }}
        .content.reading img.wide,
        .content.reading p > img.wide:only-child,
        .content.reading p.wide > img:only-child,
        .content.reading figure.image-figure:has(img.wide) {{
            width: min(var(--measure-wide), 94vw);
            max-width: none;
            margin-left: calc(50% - min(var(--measure-wide), 94vw) / 2);
        }}
        .content.reading img.full,
        .content.reading p > img.full:only-child,
        .content.reading p.full > img:only-child,
        .content.reading figure.image-figure:has(img.full) {{
            width: 100vw;
            max-width: none;
            margin-left: calc(50% - 50vw);
            border-radius: 0;
        }}
        .content.reading figure.image-figure:has(img.full) img {{
            border-radius: 0;
        }}
        .content.reading .gallery {{
            display: flex;
            gap: 0.75rem;
            margin: 1.75rem 0;
            align-items: stretch;
        }}
        .content.reading .gallery > p {{
            display: contents;
        }}
        .content.reading .gallery > figure.image-figure {{
            flex: 1 1 0;
            min-width: 0;
            margin: 0;
        }}
        .content.reading .gallery img {{
            flex: 1 1 0;
            min-width: 0;
            width: 100%;
            height: 100%;
            margin: 0;
            aspect-ratio: 4 / 3;
            object-fit: cover;
            border-radius: 10px;
        }}
        .content.reading .gallery + figure.image-figure,
        .content.reading .gallery + p {{
            margin-top: 0;
        }}
        .content.reading .page-updated {{
            margin: 2.5rem 0 0;
            font-size: 0.82rem;
            color: var(--text-muted);
        }}
        @media (max-width: 620px) {{
            .content.reading .gallery {{
                flex-direction: column;
            }}
            .content.reading .gallery > p {{
                display: flex;
                flex-direction: column;
                gap: 0.75rem;
                margin: 0;
            }}
            .content.reading .gallery img {{
                height: auto;
            }}
        }}

        .brand-mark {{
            font-size: 1.15rem;
            line-height: 1;
        }}
        .site-footer {{
            max-width: var(--measure-wide); margin: 5.5rem auto 3rem;
            padding: 1.75rem clamp(1.25rem, 5vw, 2.75rem) 0;
            border-top: 1px solid var(--border);
            display: flex; flex-wrap: wrap; align-items: center; gap: 0.6rem 1.2rem;
            font-size: 0.82rem; color: var(--text-muted);
        }}
        .site-footer a {{
            color: var(--accent);
            text-decoration: none;
        }}
        .site-footer a:hover {{
            text-decoration: underline;
        }}
        .site-footer {{
            justify-content: flex-start;
        }}
        .site-footer-note {{
            margin-right: auto;
            text-align: left;
        }}
        @media (hover: none) and (pointer: coarse) {{
            .site-nav a, .site-nav .top-nav-link {{
                min-height: 44px;
                display: inline-flex;
                align-items: center;
                touch-action: manipulation;
            }}
        }}

        @media (max-width: 620px) {{
            .topbar {{
                align-items: flex-start;
                transition: gap 0.25s ease, padding-bottom 0.25s ease;
            }}
            .topbar.topbar-condensed {{
                gap: 0;
                padding-bottom: var(--topbar-pad-block);
            }}
            .topbar.topbar-condensed .site-nav-wrap {{
                grid-template-rows: 0fr;
                opacity: 0;
                visibility: hidden;
                pointer-events: none;
            }}
            .site-nav {{
                gap: 1.1rem;
                font-size: 0.85rem;
                justify-content: flex-start;
                align-self: stretch;
                flex-wrap: nowrap;
                overflow-x: auto;
                overflow-y: hidden;
                scrollbar-width: none;
                overscroll-behavior-x: contain;
                scroll-padding-inline: var(--topbar-pad);
                padding-inline: var(--topbar-pad);
                min-height: 0;
            }}
            .site-nav::-webkit-scrollbar {{
                display: none;
            }}
            /* No scroll snap here: settling after a tap can drag the trigger out from under the finger. */
            .site-nav > a, .site-nav > .top-nav-item {{
                flex: 0 0 auto;
            }}
            .site-nav-wrap {{
                display: grid;
                grid-template-rows: 1fr;
                grid-template-columns: minmax(0, 1fr);
                justify-content: stretch;
                width: calc(100% + var(--topbar-pad) * 2);
                margin-inline: calc(var(--topbar-pad) * -1);
                min-width: 0;
                transition: grid-template-rows 0.25s ease, opacity 0.2s ease, visibility 0.25s;
            }}
            .site-nav-wrap::before, .site-nav-wrap::after {{
                content: """";
                position: absolute;
                top: 0;
                bottom: 0;
                width: var(--topbar-pad);
                pointer-events: none;
                opacity: 0;
                transition: opacity 0.2s ease;
                z-index: 2;
            }}
            .site-nav-wrap::before {{
                left: 0;
                background: linear-gradient(to right,
                    var(--topbar-fade) 0%,
                    color-mix(in srgb, var(--topbar-fade) 62%, transparent) 42%,
                    color-mix(in srgb, var(--topbar-fade) 22%, transparent) 72%,
                    transparent 100%);
            }}
            .site-nav-wrap::after {{
                right: 0;
                background: linear-gradient(to left,
                    var(--topbar-fade) 0%,
                    color-mix(in srgb, var(--topbar-fade) 62%, transparent) 42%,
                    color-mix(in srgb, var(--topbar-fade) 22%, transparent) 72%,
                    transparent 100%);
            }}
            .site-nav-wrap.can-scroll-left::before, .site-nav-wrap.can-scroll-right::after {{
                opacity: 1;
            }}
            .brand {{
                font-size: 1.3rem;
            }}
            .site-nav .top-nav-dropdown-menu {{
                display: none; opacity: 1; visibility: visible;
                top: 100%; left: 0; right: auto; transform: none;
                margin-top: 0.4rem; min-width: 160px;
                max-width: calc(100vw - 16px);
                max-height: 60vh;
                overflow-y: auto;
                overscroll-behavior: contain;
            }}
            .site-nav .top-nav-item.has-dropdown.open .top-nav-dropdown-menu {{
                display: flex;
                z-index: 1003;
            }}
            .post-nav {{
                grid-template-columns: 1fr;
            }}
            .post-nav > span:empty {{
                display: none;
            }}
            .post-nav-newer {{
                text-align: left;
                align-items: flex-start;
            }}
            .site-footer-note {{
                margin-right: 0;
                flex-basis: 100%;
            }}
            .lead {{
                grid-template-columns: 1fr;
            }}
            .lead-cover {{
                order: 1;
                margin-top: 0;
            }}
            .lead-body {{
                order: 2;
            }}
            .post-card {{
                grid-template-columns: 1fr 64px;
                gap: 1rem;
            }}
            .card-cover {{
                aspect-ratio: 1 / 1;
                border-radius: 6px;
            }}
            .card-cover-mark {{
                font-size: 1.35rem;
            }}
        }}
        @media (max-width: 380px) {{
            .post-card {{
                grid-template-columns: 1fr 52px;
                gap: 0.75rem;
            }}
            .card-cover-mark {{
                font-size: 1.05rem;
            }}
        }}
{themeComponentCss}
"));

    // CSS has no line comments or ASI to break, so collapsing all whitespace is safe here unlike in MinifyJs
    private static string MinifyCss(string css) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(css, @"/\*[\s\S]*?\*/", ""),
            @"[ \t]*[\r\n]+[ \t]*|[ \t]{2,}", " ").Trim();
}

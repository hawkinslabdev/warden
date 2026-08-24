namespace Warden.Services.Layout;

public static partial class LayoutProvider
{
    private static string GetScripts(bool enableLiveReload, bool enableDarkMode, long buildVersion, string basePath, string? nonce = null)
    {
        // Non-toggle theme swaps (bfcache/other tab/OS flip) hit an already-painted page: suppress transitions or every element cross-fades.
        var themeSyncScript = enableDarkMode
            ? @"var themeRoot = document.documentElement;
        var prefersDark = window.matchMedia('(prefers-color-scheme: dark)');
        function syncThemeToggle() {
            var toggle = document.getElementById('theme-toggle');
            if (!toggle) return;
            var current = themeRoot.getAttribute('data-theme');
            toggle.setAttribute('aria-checked', String(current ? current === 'dark' : prefersDark.matches));
        }
        function applyStoredTheme() {
            var t = null;
            try { t = localStorage.getItem('warden-theme'); } catch (_) {}
            themeRoot.classList.add('no-theme-transition');
            if (t === 'dark' || t === 'light') {
                themeRoot.setAttribute('data-theme', t);
                themeRoot.style.colorScheme = t;
            } else {
                themeRoot.removeAttribute('data-theme');
                themeRoot.style.colorScheme = '';
            }
            requestAnimationFrame(function() {
                requestAnimationFrame(function() {
                    themeRoot.classList.remove('no-theme-transition');
                });
            });
            syncThemeToggle();
        }
        window.addEventListener('pageshow', function(e) {
            if (e.persisted) applyStoredTheme();
        });
        window.addEventListener('storage', function(e) {
            if (e.key === 'warden-theme') applyStoredTheme();
        });
        prefersDark.addEventListener('change', syncThemeToggle);"
            : "";

        return MinifyJs($@"    <script{GetNonceAttr(nonce)}>
        {themeSyncScript}
        document.addEventListener('DOMContentLoaded', function() {{
            var scrollIndicator = document.getElementById('scroll-indicator');
            var themeToggle = document.getElementById('theme-toggle');

            if ({(enableLiveReload ? "true" : "false")}) {{
                var currentBuildVersion = {buildVersion};
                setInterval(function() {{
                    fetch('{basePath}/api/build-version')
                        .then(function(r) {{ return r.json(); }})
                        .then(function(data) {{
                            if (data.version !== currentBuildVersion) {{
                                location.reload();
                            }}
                        }})
                        ['catch'](function() {{}});
                }}, 5000);
            }}

            if (themeToggle) {{
                syncThemeToggle();

                themeToggle.addEventListener('click', function() {{
                    var current = themeRoot.getAttribute('data-theme');
                    var isDark = current ? current === 'dark' : prefersDark.matches;
                    var next = isDark ? 'light' : 'dark';
                    themeRoot.setAttribute('data-theme', next);
                    themeRoot.style.colorScheme = next;
                    themeToggle.setAttribute('aria-checked', String(next === 'dark'));
                    try {{ localStorage.setItem('warden-theme', next); }} catch (e) {{}}

                    // Mermaid bakes theme colors into its SVG at render time; reload to re-render.
                    if (document.querySelector('.mermaid')) {{
                        location.reload();
                    }}
                }});
            }}

            var mobileNav = window.matchMedia('(max-width: 620px)');
            function onMediaChange(mq, fn) {{
                if (mq.addEventListener) mq.addEventListener('change', fn);
                else if (mq.addListener) mq.addListener(fn);
            }}
            var siteNav = document.querySelector('.site-nav');
            var siteNavWrap = document.querySelector('.site-nav-wrap');
            var topbar = document.querySelector('.topbar');
            function expandTopbar() {{
                if (topbar) topbar.classList.remove('topbar-condensed');
            }}
            function navScrollMax() {{
                return siteNav ? siteNav.scrollWidth - siteNav.clientWidth : 0;
            }}
            function navIsScrollable() {{
                return navScrollMax() > 2;
            }}
            function updateNavScrollHints() {{
                if (!siteNav || !siteNavWrap) return;
                var max = navScrollMax();
                var scrollable = max > 2;
                siteNavWrap.classList.toggle('can-scroll-left', scrollable && siteNav.scrollLeft > 2);
                siteNavWrap.classList.toggle('can-scroll-right', scrollable && siteNav.scrollLeft < max - 2);
            }}
            if (siteNav && siteNavWrap) {{
                var navHintQueued = false;
                siteNav.addEventListener('scroll', function() {{
                    if (navHintQueued) return;
                    navHintQueued = true;
                    requestAnimationFrame(function() {{
                        navHintQueued = false;
                        updateNavScrollHints();
                    }});
                }}, {{ passive: true }});
                window.addEventListener('resize', updateNavScrollHints);
                if (window.ResizeObserver) new ResizeObserver(updateNavScrollHints).observe(siteNav);
                updateNavScrollHints();

                var currentNavItem = siteNav.querySelector('.here, .top-nav-link.active');
                if (currentNavItem && navIsScrollable()) {{
                    var target = currentNavItem.closest('.top-nav-item') || currentNavItem;
                    siteNav.scrollLeft = Math.max(0, target.offsetLeft - (siteNav.clientWidth - target.offsetWidth) / 2);
                    updateNavScrollHints();
                }}
            }}

            var navDropdowns = Array.prototype.slice.call(document.querySelectorAll('.site-nav .top-nav-item.has-dropdown'));
            navDropdowns.forEach(function(item) {{
                // Cache it now: once the menu is portaled out, item.querySelector no longer finds it.
                item.navMenu = item.querySelector('.top-nav-dropdown-menu');
                item.navButton = item.querySelector('.top-nav-link');
            }});
            function anyNavDropdownOpen() {{
                for (var i = 0; i < navDropdowns.length; i++) {{
                    if (navDropdowns[i].classList.contains('open')) return true;
                }}
                return false;
            }}
            function navEventInsideDropdown(item, target) {{
                return item.contains(target) || (item.navMenu && item.navMenu.contains(target));
            }}
            function resetNavDropdownPosition(menu) {{
                if (!menu) return;
                menu.style.position = '';
                menu.style.top = '';
                menu.style.left = '';
                menu.style.right = '';
                menu.style.transform = '';
            }}
            // iOS WebKit clips position:fixed descendants of the nav strip's scroller, so an open menu must move to <body>.
            function portalNavMenu(item) {{
                var menu = item.navMenu;
                if (!menu || menu.parentNode === document.body) return;
                menu.classList.add('top-nav-portal');
                document.body.appendChild(menu);
            }}
            function restoreNavMenu(item) {{
                var menu = item.navMenu;
                if (!menu || menu.parentNode === item) return;
                menu.classList.remove('top-nav-portal');
                resetNavDropdownPosition(menu);
                item.appendChild(menu);
            }}
            function positionNavDropdown(item) {{
                var menu = item.navMenu;
                if (!menu) return;
                if (!mobileNav.matches) {{
                    resetNavDropdownPosition(menu);
                    return;
                }}
                menu.style.position = 'fixed';
                menu.style.right = 'auto';
                menu.style.transform = 'none';
                var rect = item.getBoundingClientRect();
                var vw = document.documentElement.clientWidth || window.innerWidth;
                var vh = document.documentElement.clientHeight || window.innerHeight;
                var left = Math.min(Math.max(8, rect.left), Math.max(8, vw - menu.offsetWidth - 8));
                var top = rect.bottom + 6;
                // Portaled to <body>, so viewport coordinates apply directly with no offset parent.
                menu.style.left = left + 'px';
                menu.style.top = top + 'px';
                menu.style.maxHeight = Math.max(120, vh - top - 8) + 'px';
            }}
            function repositionOpenNavDropdowns() {{
                for (var i = 0; i < navDropdowns.length; i++) {{
                    if (navDropdowns[i].classList.contains('open')) positionNavDropdown(navDropdowns[i]);
                }}
            }}
            function closeNavDropdown(item) {{
                item.classList.remove('open');
                if (item.navButton) item.navButton.setAttribute('aria-expanded', 'false');
                if (item.navMenu) item.navMenu.style.maxHeight = '';
                restoreNavMenu(item);
            }}
            function closeAllNavDropdowns() {{
                navDropdowns.forEach(closeNavDropdown);
            }}
            function openNavDropdown(item, btn) {{
                closeAllNavDropdowns();
                // The trigger only exists while the bar is expanded, so never open into a collapsing bar.
                expandTopbar();
                if (mobileNav.matches && siteNav) {{
                    // Nudge a half-scrolled trigger fully into view so the pinned menu lines up under it.
                    var navRect = siteNav.getBoundingClientRect();
                    var itemRect = item.getBoundingClientRect();
                    if (itemRect.left < navRect.left + 8) siteNav.scrollLeft -= (navRect.left + 8 - itemRect.left);
                    else if (itemRect.right > navRect.right - 8) siteNav.scrollLeft += (itemRect.right - (navRect.right - 8));
                    portalNavMenu(item);
                }}
                item.classList.add('open');
                btn.setAttribute('aria-expanded', 'true');
                positionNavDropdown(item);
            }}
            navDropdowns.forEach(function(item) {{
                var btn = item.navButton;
                if (!btn) return;
                btn.setAttribute('aria-expanded', item.classList.contains('open') ? 'true' : 'false');
                btn.addEventListener('click', function(e) {{
                    e.preventDefault();
                    e.stopPropagation();
                    if (item.classList.contains('open')) closeAllNavDropdowns();
                    else openNavDropdown(item, btn);
                }});
            }});
            if (navDropdowns.length && window.matchMedia('(hover: hover) and (pointer: fine)').matches) {{
                // CSS opens these on hover; mirror it in aria without letting it desync from .open.
                navDropdowns.forEach(function(item) {{
                    var btn = item.querySelector('.top-nav-link');
                    if (!btn) return;
                    function sync(expanded) {{
                        if (!expanded && item.classList.contains('open')) return;
                        btn.setAttribute('aria-expanded', expanded ? 'true' : 'false');
                    }}
                    item.addEventListener('mouseenter', function() {{ sync(true); }});
                    item.addEventListener('mouseleave', function() {{ sync(false); }});
                    item.addEventListener('focusin', function() {{ sync(true); }});
                    item.addEventListener('focusout', function() {{
                        setTimeout(function() {{
                            if (!item.matches(':focus-within')) sync(false);
                        }}, 0);
                    }});
                }});
            }}
            if (navDropdowns.length) {{
                var navDropdownTicking = false;
                function queueNavDropdownReposition() {{
                    if (navDropdownTicking || !anyNavDropdownOpen()) return;
                    navDropdownTicking = true;
                    requestAnimationFrame(function() {{
                        navDropdownTicking = false;
                        repositionOpenNavDropdowns();
                    }});
                }}
                if (siteNav) siteNav.addEventListener('scroll', queueNavDropdownReposition, {{ passive: true }});
                // The menu is viewport-anchored, so anything that moves the topbar restages it.
                window.addEventListener('scroll', queueNavDropdownReposition, {{ passive: true }});
                window.addEventListener('resize', queueNavDropdownReposition);
                window.addEventListener('orientationchange', closeAllNavDropdowns);
                if (window.visualViewport) {{
                    window.visualViewport.addEventListener('resize', queueNavDropdownReposition);
                    window.visualViewport.addEventListener('scroll', queueNavDropdownReposition);
                }}
                onMediaChange(mobileNav, function() {{
                    expandTopbar();
                    navDropdowns.forEach(function(item) {{
                        closeNavDropdown(item);
                        resetNavDropdownPosition(item.querySelector('.top-nav-dropdown-menu'));
                    }});
                }});
            }}

            if (topbar) {{
                var lastScrollY = window.pageYOffset || 0;
                var lastViewportHeight = window.innerHeight;
                var topbarTicking = false;
                var revealAbove = 120;
                var condenseThreshold = 24;
                var revealThreshold = 2;
                var bottomDeadzone = 24;

                function condenseTopbar() {{
                    if (topbar.classList.contains('topbar-condensed')) return;
                    if (anyNavDropdownOpen()) return;
                    topbar.classList.add('topbar-condensed');
                }}
                function updateTopbar() {{
                    var raw = window.pageYOffset || 0;
                    var maxY = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
                    var y = Math.min(Math.max(raw, 0), maxY);
                    // A sliding mobile URL bar shifts scroll with no gesture behind it; resync instead of reading a swipe.
                    if (window.innerHeight !== lastViewportHeight) {{
                        lastViewportHeight = window.innerHeight;
                        lastScrollY = y;
                        return;
                    }}
                    if (raw < 0 || raw > maxY) {{
                        lastScrollY = y;
                        return;
                    }}
                    if (!mobileNav.matches || y <= revealAbove) {{
                        expandTopbar();
                        lastScrollY = y;
                        return;
                    }}
                    if (anyNavDropdownOpen()) {{
                        lastScrollY = y;
                        return;
                    }}
                    var delta = y - lastScrollY;
                    // Reveal eagerly, hide reluctantly: a nav that is missing costs more than one that lingers.
                    if (delta <= -revealThreshold) {{
                        expandTopbar();
                        lastScrollY = y;
                        return;
                    }}
                    if (maxY - y < bottomDeadzone) return;
                    if (delta < condenseThreshold) return;
                    condenseTopbar();
                    lastScrollY = y;
                }}

                window.addEventListener('scroll', function() {{
                    if (topbarTicking) return;
                    topbarTicking = true;
                    requestAnimationFrame(function() {{
                        topbarTicking = false;
                        updateTopbar();
                    }});
                }}, {{ passive: true }});
                topbar.addEventListener('focusin', expandTopbar);
                // Reaching for the bar is a request for the nav, whether or not the nav is on screen yet.
                topbar.addEventListener('pointerdown', expandTopbar);
                window.addEventListener('pageshow', function() {{
                    closeAllNavDropdowns();
                    expandTopbar();
                    lastViewportHeight = window.innerHeight;
                    lastScrollY = window.pageYOffset || 0;
                }});
                onMediaChange(mobileNav, function() {{
                    lastViewportHeight = window.innerHeight;
                    lastScrollY = window.pageYOffset || 0;
                }});
            }}
            if (navDropdowns.length) {{
                function closeNavDropdownsOutside(e) {{
                    navDropdowns.forEach(function(item) {{
                        if (item.classList.contains('open') && !navEventInsideDropdown(item, e.target)) closeNavDropdown(item);
                    }});
                }}
                // Touch taps on non-interactive elements do not reliably produce a click, so watch both.
                document.addEventListener('pointerdown', closeNavDropdownsOutside);
                document.addEventListener('click', closeNavDropdownsOutside);
                document.addEventListener('keydown', function(e) {{
                    if (e.key === 'Escape') closeAllNavDropdowns();
                }});
            }}

            document.querySelectorAll('.load-more').forEach(function(btn) {{
                var skeletonCard = '<article class=""post-card skeleton-card"" aria-hidden=""true"">' +
                    '<div class=""skeleton skeleton-title""></div>' +
                    '<div class=""skeleton skeleton-meta""></div>' +
                    '<div class=""skeleton skeleton-line""></div>' +
                    '<div class=""skeleton skeleton-line short""></div>' +
                    '</article>';
                btn.addEventListener('click', function() {{
                    var next = btn.getAttribute('data-next');
                    if (!next || btn.disabled) return;
                    var wrap = btn.closest('.load-more-wrap');
                    btn.disabled = true;
                    var skel = document.createElement('div');
                    skel.className = 'load-more-skeletons';
                    skel.innerHTML = skeletonCard + skeletonCard + skeletonCard;
                    wrap.parentNode.insertBefore(skel, wrap);
                    fetch(next)
                        .then(function(r) {{ return r.text(); }})
                        .then(function(html) {{
                            var doc = new DOMParser().parseFromString(html, 'text/html');
                            var cards = doc.querySelectorAll('.content .post-card');
                            var nextBtn = doc.querySelector('.load-more');
                            skel.remove();
                            cards.forEach(function(c) {{ wrap.parentNode.insertBefore(c, wrap); }});
                            var moreUrl = nextBtn ? nextBtn.getAttribute('data-next') : null;
                            if (moreUrl) {{ btn.setAttribute('data-next', moreUrl); btn.disabled = false; }}
                            else {{ wrap.remove(); }}
                        }})
                        ['catch'](function() {{
                            skel.remove();
                            btn.disabled = false;
                        }});
                }});
            }});

            var shareTrigger = document.querySelector('[data-share]');
            var shareOverlay = document.getElementById('share-overlay');
            if (shareTrigger && shareOverlay) {{
                var shareModalClose = document.getElementById('share-modal-close');
                var shareCopy = document.getElementById('share-copy');
                var shareCopyLabel = document.getElementById('share-copy-label');
                var shareLastFocused = null;
                var shareMastodon = document.getElementById('share-mastodon');
                if (shareMastodon) shareMastodon.addEventListener('click', function() {{
                    var saved = window.localStorage.getItem('mastodon-instance') || 'fosstodon.org';
                    var host = window.prompt('Your Mastodon instance', saved);
                    if (!host) return;
                    host = host.trim().replace(/^https?:\/\//, '').replace(/\/+$/, '');
                    if (!host) return;
                    window.localStorage.setItem('mastodon-instance', host);
                    var text = encodeURIComponent(document.title + ' ' + location.href);
                    window.open('https://' + host + '/share?text=' + text, '_blank', 'noopener');
                }});
                var openShare = function() {{
                    var url = location.href, eu = encodeURIComponent(url), et = encodeURIComponent(document.title);
                    var li = document.getElementById('share-linkedin');
                    var em = document.getElementById('share-email');
                    if (li) li.href = 'https://www.linkedin.com/sharing/share-offsite/?url=' + eu;
                    if (em) em.href = 'mailto:?subject=' + et + '&body=' + eu;
                    shareLastFocused = document.activeElement;
                    shareOverlay.hidden = false;
                    requestAnimationFrame(function() {{ shareOverlay.classList.add('open'); }});
                    document.documentElement.style.overflow = 'hidden';
                    if (shareModalClose) shareModalClose.focus();
                }};
                var closeShare = function() {{
                    shareOverlay.classList.remove('open');
                    shareOverlay.hidden = true;
                    document.documentElement.style.overflow = '';
                    if (shareLastFocused && shareLastFocused.focus) shareLastFocused.focus();
                }};
                shareTrigger.addEventListener('click', openShare);
                if (shareModalClose) shareModalClose.addEventListener('click', closeShare);
                shareOverlay.addEventListener('mousedown', function(e) {{ if (e.target === shareOverlay) closeShare(); }});
                document.addEventListener('keydown', function(e) {{ if (!shareOverlay.hidden && e.key === 'Escape') closeShare(); }});
                if (shareCopy) {{
                    shareCopy.addEventListener('click', function() {{
                        navigator.clipboard.writeText(location.href).then(function() {{
                            if (shareCopyLabel) {{
                                shareCopyLabel.textContent = 'Copied';
                                setTimeout(function() {{ shareCopyLabel.textContent = 'Copy link'; }}, 1800);
                            }}
                        }})['catch'](function() {{}});
                    }});
                }}
                shareOverlay.addEventListener('click', function(e) {{
                    if (e.target.closest('.share-action') && !e.target.closest('#share-copy')) closeShare();
                }});
            }}

            var topbarEl = document.querySelector('.topbar');
            if (topbarEl) {{
                var syncTopbarHeight = function() {{
                    document.documentElement.style.setProperty('--topbar-height', topbarEl.offsetHeight + 'px');
                }};
                syncTopbarHeight();
                window.addEventListener('resize', syncTopbarHeight);
                if (window.ResizeObserver) new ResizeObserver(syncTopbarHeight).observe(topbarEl);
            }}

            function updateScrollProgress() {{
                if (!scrollIndicator) return;
                var winScroll = document.documentElement.scrollTop || document.body.scrollTop;
                var height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
                var scrolled = height > 0 ? (winScroll / height) * 100 : 0;
                scrollIndicator.style.transform = 'scaleX(' + (scrolled / 100) + ')';
            }}

            window.addEventListener('scroll', updateScrollProgress);

            // Wrap wide tables in scroll container; overflow-x:auto on table itself doesn't reliably contain it.
            var tables = document.querySelectorAll('.content table');
            tables.forEach(function(table) {{
                var wrapper = document.createElement('div');
                wrapper.className = 'table-wrapper';
                table.parentNode.insertBefore(wrapper, table);
                wrapper.appendChild(table);
            }});


            var codeBlocks = document.querySelectorAll('.content pre');
            codeBlocks.forEach(function(pre) {{
                var wrapper = document.createElement('div');
                wrapper.className = 'code-block-wrapper';
                pre.parentNode.insertBefore(wrapper, pre);
                wrapper.appendChild(pre);

                var buttons = document.createElement('div');
                buttons.className = 'code-block-buttons';

                var iconCopy = '<svg xmlns=""http://www.w3.org/2000/svg"" width=""17"" height=""17"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><rect width=""14"" height=""14"" x=""8"" y=""8"" rx=""2"" ry=""2""/><path d=""M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2""/></svg>';
                var iconCheck = '<svg xmlns=""http://www.w3.org/2000/svg"" width=""17"" height=""17"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M20 6 9 17l-5-5""/></svg>';
                var iconX = '<svg xmlns=""http://www.w3.org/2000/svg"" width=""17"" height=""17"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M18 6 6 18""/><path d=""m6 6 12 12""/></svg>';
                var copyBtn = document.createElement('button');
                copyBtn.innerHTML = iconCopy;
                copyBtn.setAttribute('aria-label', 'Copy code');
                copyBtn.setAttribute('title', 'Copy code');
                copyBtn.addEventListener('click', function() {{
                    var code = pre.querySelector('code');
                    var text = code ? code.textContent : pre.textContent;
                    navigator.clipboard.writeText(text).then(function() {{
                        copyBtn.innerHTML = iconCheck;
                        copyBtn.classList.add('copied');
                        setTimeout(function() {{
                            copyBtn.innerHTML = iconCopy;
                            copyBtn.classList.remove('copied');
                        }}, 2000);
                    }})['catch'](function() {{
                        copyBtn.innerHTML = iconX;
                        copyBtn.classList.add('failed');
                        setTimeout(function() {{
                            copyBtn.innerHTML = iconCopy;
                            copyBtn.classList.remove('failed');
                        }}, 2000);
                    }});
                }});
                buttons.appendChild(copyBtn);

                var downloadBtn = document.createElement('button');
                downloadBtn.innerHTML = '<svg xmlns=""http://www.w3.org/2000/svg"" width=""17"" height=""17"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4""/><polyline points=""7 10 12 15 17 10""/><line x1=""12"" x2=""12"" y1=""3"" y2=""15""/></svg>';
                downloadBtn.setAttribute('aria-label', 'Download code');
                downloadBtn.setAttribute('title', 'Download code');
                downloadBtn.addEventListener('click', function() {{
                    var code = pre.querySelector('code');
                    var text = code ? code.textContent : pre.textContent;
                    var blob = new Blob([text], {{ type: 'text/plain' }});
                    var url = URL.createObjectURL(blob);
                    var a = document.createElement('a');
                    a.href = url;
                    var langBlock = pre.closest('[class^=""language-""]');
                    a.download = (langBlock && langBlock.dataset.filename) || 'code.txt';
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);
                }});
                buttons.appendChild(downloadBtn);

                wrapper.appendChild(buttons);
            }});

            var codeGroups = document.querySelectorAll('.warden-code-group');
            codeGroups.forEach(function(group) {{
                var inputs = group.querySelectorAll('.tabs input');
                var labels = group.querySelectorAll('.tabs label');
                var blocks = group.querySelectorAll('.blocks > [class^=""language-""]');
                inputs.forEach(function(input, index) {{
                    input.addEventListener('change', function() {{
                        blocks.forEach(function(block, blockIndex) {{
                            block.classList.toggle('active', blockIndex === index);
                        }});
                        labels.forEach(function(label, labelIndex) {{
                            label.classList.toggle('active-tab', labelIndex === index);
                        }});
                    }});
                    if (input.checked) labels[index] && labels[index].classList.add('active-tab');
                }});
            }});

            // Mermaid ignores CSS variables, so the theme's tokens are read once and passed in.
            var mermaidBlocks = document.querySelectorAll('.mermaid');
            if (mermaidBlocks.length && window.mermaid) {{
                var currentTheme = document.documentElement.getAttribute('data-theme');
                var prefersDarkNow = window.matchMedia('(prefers-color-scheme: dark)').matches;
                var mermaidIsDark = currentTheme ? currentTheme === 'dark' : prefersDarkNow;
                var rootStyle = getComputedStyle(document.documentElement);
                function themeToken(name) {{ return rootStyle.getPropertyValue(name).trim(); }}
                window.mermaid.initialize({{
                    theme: 'base',
                    themeVariables: {{
                        darkMode: mermaidIsDark,
                        fontFamily: themeToken('--font-sans'),
                        background: themeToken('--bg-color'),
                        primaryColor: themeToken('--accent-light'),
                        primaryTextColor: themeToken('--text-color'),
                        primaryBorderColor: themeToken('--accent'),
                        secondaryColor: themeToken('--code-bg'),
                        tertiaryColor: themeToken('--sidebar-bg'),
                        mainBkg: themeToken('--accent-light'),
                        nodeBorder: themeToken('--accent'),
                        clusterBkg: themeToken('--sidebar-bg'),
                        clusterBorder: themeToken('--border'),
                        lineColor: themeToken('--text-muted'),
                        textColor: themeToken('--text-color'),
                        titleColor: themeToken('--text-color'),
                        edgeLabelBackground: themeToken('--bg-color'),
                        noteBkgColor: themeToken('--code-bg'),
                        noteTextColor: themeToken('--text-color'),
                        noteBorderColor: themeToken('--border'),
                        // Alert hues, not accent tints: slices derived from one accent are indistinguishable.
                        pie1: themeToken('--accent'),
                        pie2: themeToken('--alert-note'),
                        pie3: themeToken('--alert-tip'),
                        pie4: themeToken('--alert-warning'),
                        pie5: themeToken('--alert-important'),
                        pie6: themeToken('--alert-caution'),
                        pieStrokeColor: themeToken('--bg-color'),
                        pieOuterStrokeColor: themeToken('--border'),
                        pieSectionTextColor: themeToken('--bg-color'),
                        pieTitleTextColor: themeToken('--text-color'),
                        pieLegendTextColor: themeToken('--text-color')
                    }}
                }});
                window.mermaid.run();
            }}

            // Self-hosted Leaflet maps. Tiles from OpenStreetMap; popups built via textContent (no HTML injection).
            var mapEls = document.querySelectorAll('.warden-map');
            if (mapEls.length && window.L) {{
                var iconBase = '{basePath}/css/images/';
                var mapIcon = window.L.icon({{
                    iconUrl: iconBase + 'marker-icon.png',
                    iconRetinaUrl: iconBase + 'marker-icon-2x.png',
                    shadowUrl: iconBase + 'marker-shadow.png',
                    iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34], shadowSize: [41, 41]
                }});
                mapEls.forEach(function(el) {{
                    if (el.dataset.rendered) return;
                    el.dataset.rendered = '1';
                    var pins;
                    try {{ pins = JSON.parse(el.dataset.pins || '[]'); }} catch (e) {{ pins = []; }}
                    var map = window.L.map(el, {{ scrollWheelZoom: false }});
                    window.L.tileLayer('https://tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                        maxZoom: 19,
                        attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a> contributors'
                    }}).addTo(map);
                    var markers = [];
                    pins.forEach(function(p) {{
                        if (typeof p.lat !== 'number' || typeof p.lng !== 'number') return;
                        var m = window.L.marker([p.lat, p.lng], {{ icon: mapIcon }}).addTo(map);
                        var box = document.createElement('div');
                        box.className = 'map-popup';
                        function row(node) {{ var d = document.createElement('div'); d.appendChild(node); box.appendChild(d); }}
                        if (p.name) {{ var h = document.createElement('strong'); h.textContent = p.name; row(h); }}
                        if (p.text) {{ var t = document.createElement('p'); t.textContent = p.text; box.appendChild(t); }}
                        if (p.phone) {{ var a = document.createElement('a'); a.href = 'tel:' + p.phone.replace(/\s+/g, ''); a.textContent = p.phone; row(a); }}
                        if (p.contact) {{
                            if (p.contact.indexOf('@') > -1) {{ var e = document.createElement('a'); e.href = 'mailto:' + p.contact; e.textContent = p.contact; row(e); }}
                            else {{ var c = document.createElement('span'); c.textContent = p.contact; row(c); }}
                        }}
                        if (p.url) {{ var u = document.createElement('a'); u.href = p.url; u.rel = 'noopener'; u.target = '_blank'; u.textContent = 'Website'; row(u); }}
                        m.bindPopup(box);
                        markers.push(m);
                    }});
                    var zoom = el.dataset.zoom ? parseInt(el.dataset.zoom, 10) : null;
                    if (el.dataset.center) {{
                        var c = el.dataset.center.split(',');
                        map.setView([parseFloat(c[0]), parseFloat(c[1])], zoom || 13);
                    }} else if (markers.length) {{
                        map.fitBounds(window.L.featureGroup(markers).getBounds().pad(0.2), {{ maxZoom: zoom || 15 }});
                    }} else {{
                        map.setView([0, 0], zoom || 2);
                    }}
                }});
            }}

            var pageControlsToggle = document.querySelector('.page-controls-toggle');
            var pageControlsMenu = document.querySelector('.page-controls-menu');
            if (pageControlsToggle && pageControlsMenu) {{
                function closePageControls() {{
                    pageControlsMenu.hidden = true;
                    pageControlsToggle.setAttribute('aria-expanded', 'false');
                }}
                function openPageControls() {{
                    pageControlsMenu.hidden = false;
                    pageControlsToggle.setAttribute('aria-expanded', 'true');
                    var rect = pageControlsMenu.getBoundingClientRect();
                    if (rect.left < 8) {{
                        pageControlsMenu.style.right = 'auto';
                        pageControlsMenu.style.left = '0';
                    }} else {{
                        pageControlsMenu.style.right = '';
                        pageControlsMenu.style.left = '';
                    }}
                    var first = pageControlsMenu.querySelector('.page-controls-item');
                    if (first) first.focus();
                }}
                pageControlsToggle.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    if (!pageControlsMenu.hidden) {{ closePageControls(); }} else {{ openPageControls(); }}
                }});
                document.addEventListener('click', function(e) {{
                    if (!pageControlsMenu.hidden && !pageControlsMenu.contains(e.target))
                        closePageControls();
                }});
                document.addEventListener('keydown', function(e) {{
                    if (e.key === 'Escape' && !pageControlsMenu.hidden) {{ closePageControls(); pageControlsToggle.focus(); }}
                }});
                pageControlsMenu.addEventListener('keydown', function(e) {{
                    var items = Array.prototype.slice.call(pageControlsMenu.querySelectorAll('.page-controls-item'));
                    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {{
                        e.preventDefault();
                        var idx = items.indexOf(document.activeElement);
                        idx = e.key === 'ArrowDown' ? (idx + 1) % items.length : (idx - 1 + items.length) % items.length;
                        items[idx].focus();
                    }} else if (e.key === 'Enter' || e.key === ' ') {{
                        var focused = document.activeElement;
                        if (focused && pageControlsMenu.contains(focused) && !focused.href) {{
                            e.preventDefault();
                            focused.click();
                        }}
                    }}
                }});

                var copiedHtml = '<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true"" width=""14"" height=""14""><polyline points=""20 6 9 17 4 12""/></svg>Copied!';
                var errorHtml = '<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true"" width=""14"" height=""14""><line x1=""18"" y1=""6"" x2=""6"" y2=""18""/><line x1=""6"" y1=""6"" x2=""18"" y2=""18""/></svg>Failed';
                function showCopied(btn, savedHtml) {{
                    btn.innerHTML = copiedHtml;
                    setTimeout(function() {{ closePageControls(); }}, 600);
                    setTimeout(function() {{ btn.innerHTML = savedHtml; }}, 2000);
                }}
                function showError(btn, savedHtml) {{
                    btn.innerHTML = errorHtml;
                    setTimeout(function() {{ btn.innerHTML = savedHtml; }}, 2000);
                }}

                pageControlsMenu.querySelectorAll('[data-copy-url]').forEach(function(btn) {{
                    btn.addEventListener('click', function() {{
                        if (btn.classList.contains('loading')) return;
                        var savedHtml = btn.innerHTML;
                        btn.classList.add('loading');
                        fetch(btn.getAttribute('data-copy-url'))
                            .then(function(r) {{ if (!r.ok) throw new Error(); return r.text(); }})
                            .then(function(text) {{ return navigator.clipboard.writeText(text); }})
                            .then(function() {{ btn.classList.remove('loading'); showCopied(btn, savedHtml); }})
                            ['catch'](function() {{ btn.classList.remove('loading'); showError(btn, savedHtml); }});
                    }});
                }});

                pageControlsMenu.querySelectorAll('[data-copy-value]').forEach(function(btn) {{
                    btn.addEventListener('click', function() {{
                        var savedHtml = btn.innerHTML;
                        var value = new URL(btn.getAttribute('data-copy-value'), window.location.href).href;
                        navigator.clipboard.writeText(value)
                            .then(function() {{ showCopied(btn, savedHtml); }})
                            ['catch'](function() {{ showError(btn, savedHtml); }});
                    }});
                }});
            }}

            var promoBar = document.getElementById('promo-bar');
            var promoClose = document.getElementById('promo-bar-close');
            if (promoBar && promoClose) {{
                promoClose.addEventListener('click', function() {{
                    try {{ localStorage.setItem('warden-promo-dismissed', promoBar.getAttribute('data-promo-id') || '1'); }} catch (_) {{}}
                    var removed = false;
                    var removeBar = function() {{ if (!removed) {{ removed = true; promoBar.remove(); }} }};
                    promoBar.addEventListener('transitionend', function(e) {{
                        if (e.propertyName === 'grid-template-rows') removeBar();
                    }});
                    setTimeout(removeBar, 400);
                    promoBar.classList.add('promo-bar-hiding');
                }});
            }}

            // tooltip is a ::after pseudo-element JS can't measure, so this clamps against the CSS `max-width: min(15rem, 60vw)` upper bound instead of real rendered width - can over-shift a short tip slightly, never lets one overflow
            function positionAbbrTip(e) {{
                var abbr = e.target.closest && e.target.closest('abbr[data-tip], .status-tick[data-tip], .icon-btn[data-tip]');
                if (!abbr) return;
                var vw = document.documentElement.clientWidth || window.innerWidth;
                var rootFontPx = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
                var half = Math.min(7.5 * rootFontPx, vw * 0.3);
                var rect = abbr.getBoundingClientRect();
                var center = rect.left + rect.width / 2;
                var shift = 0;
                if (center - half < 8) shift = 8 - (center - half);
                else if (center + half > vw - 8) shift = (vw - 8) - (center + half);
                abbr.style.setProperty('--tip-shift', shift + 'px');
            }}
            document.addEventListener('mouseover', positionAbbrTip);
            document.addEventListener('focusin', positionAbbrTip);

            // server renders a UTC fallback; this rewrites it to the viewer's own locale, in whatever
            // timezone is currently selected (device default until the timezone picker below overrides it)
            function currentTimezone() {{
                try {{
                    return localStorage.getItem('warden-tz') || Intl.DateTimeFormat().resolvedOptions().timeZone;
                }} catch (e) {{ return Intl.DateTimeFormat().resolvedOptions().timeZone; }}
            }}
            function formatLocalMoment(iso) {{
                try {{
                    return new Date(iso).toLocaleString(undefined, {{ day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: currentTimezone() }});
                }} catch (e) {{ return iso; }}
            }}
            function applyTimezone() {{
                document.querySelectorAll('.status-time[data-iso]').forEach(function(el) {{
                    el.textContent = formatLocalMoment(el.getAttribute('data-iso'));
                }});
            }}
            applyTimezone();

            // swaps a clicked day tick's or Clear filter's link in place instead of a full reload; falls back to a real navigation if fetch fails or JS is off
            document.addEventListener('click', function(e) {{
                var link = e.target.closest && e.target.closest('.status-tick[href], .status-filter-clear[href]');
                if (!link) return;
                var url = new URL(link.getAttribute('href'), window.location.href);
                if (url.origin !== window.location.origin) return;
                e.preventDefault();
                loadDayFilter(url);
            }});

            async function loadDayFilter(url) {{
                var article = document.querySelector('article.content');
                if (!article) {{ window.location.href = url.href; return; }}
                try {{
                    var response = await fetch(url.href, {{ headers: {{ 'X-Requested-With': 'fetch' }} }});
                    if (!response.ok) throw new Error('bad response');
                    var html = await response.text();
                    var next = new DOMParser().parseFromString(html, 'text/html').querySelector('article.content');
                    if (!next) throw new Error('no content in response');
                    article.replaceChildren(...next.childNodes);
                }} catch (e) {{
                    window.location.href = url.href;
                    return;
                }}

                history.pushState(null, '', url.href);
                applyTimezone();

                // the swap already gave every tick a fresh, unhighlighted element; this just marks the filtered day's, and stays until the next swap replaces them again
                var day = url.searchParams.get('on');
                if (day) {{
                    document.querySelectorAll('.status-tick[href*=""on=' + day + '""]').forEach(function(t) {{
                        t.classList.add('status-tick--active-day');
                    }});
                }}
            }}

            var tzToggle = document.getElementById('timezone-toggle');
            var tzDropdown = document.getElementById('timezone-dropdown');
            if (tzToggle && tzDropdown && typeof Intl.supportedValuesOf === 'function') {{
                var tzSearch = document.getElementById('timezone-search');
                var tzOptions = document.getElementById('timezone-options');
                var allZones = Intl.supportedValuesOf('timeZone');
                var MAX_RESULTS = 40;
                // the zone name shows as a hover/focus tooltip (existing [data-tip] bubble), not inline text,
                // so it also has to ride along in aria-label for anyone not hovering a mouse
                var tzBaseLabel = tzToggle.getAttribute('aria-label');

                function setTzBadge(zone) {{
                    tzToggle.setAttribute('data-tip', zone);
                    tzToggle.setAttribute('aria-label', tzBaseLabel + ' (' + zone + ')');
                    var isOverridden = zone !== Intl.DateTimeFormat().resolvedOptions().timeZone;
                    tzToggle.classList.toggle('timezone-toggle--overridden', isOverridden);
                }}

                function storedTimezone() {{ try {{ return localStorage.getItem('warden-tz'); }} catch (e) {{ return null; }} }}

                var TZ_ICON_AUTO = '<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><circle cx=""12"" cy=""12"" r=""10""/><path d=""M12 6v6l4 2""/></svg>';
                var TZ_ICON_CURRENT = '<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M12 21s-7-6.5-7-11a7 7 0 0 1 14 0c0 4.5-7 11-7 11z""/><circle cx=""12"" cy=""10"" r=""2.5""/></svg>';

                function makeOption(zone, iconSvg, tagLabel, highlight) {{
                    var btn = document.createElement('button');
                    btn.type = 'button';
                    btn.className = highlight ? 'timezone-option timezone-option--current' : 'timezone-option';
                    btn.setAttribute('data-tz', zone);
                    if (iconSvg) {{
                        var tag = document.createElement('span');
                        tag.className = 'timezone-option-tag';
                        tag.title = tagLabel;
                        tag.innerHTML = iconSvg;
                        btn.appendChild(tag);
                    }}
                    btn.appendChild(document.createTextNode(zone.replace(/_/g, ' ')));
                    btn.addEventListener('click', function() {{ selectTimezone(zone); }});
                    return btn;
                }}

                function renderOptions(query) {{
                    var q = query.trim().toLowerCase();
                    tzOptions.innerHTML = '';
                    if (q === '') {{
                        var autoZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
                        var stored = storedTimezone();
                        tzOptions.appendChild(makeOption(autoZone, TZ_ICON_AUTO, 'Auto'));
                        if (stored && stored !== autoZone) tzOptions.appendChild(makeOption(stored, TZ_ICON_CURRENT, 'Current', true));
                        var divider = document.createElement('div');
                        divider.className = 'timezone-options-divider';
                        tzOptions.appendChild(divider);
                    }}
                    var matches = (q === '' ? allZones : allZones.filter(function(z) {{ return z.toLowerCase().indexOf(q) !== -1; }})).slice(0, MAX_RESULTS);
                    matches.forEach(function(zone) {{ tzOptions.appendChild(makeOption(zone)); }});
                }}

                function selectTimezone(zone) {{
                    var autoZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
                    try {{ if (zone === autoZone) localStorage.removeItem('warden-tz'); else localStorage.setItem('warden-tz', zone); }} catch (e) {{}}
                    setTzBadge(currentTimezone());
                    applyTimezone();
                    closeTimezoneDropdown();
                    tzToggle.focus();
                }}

                function openTimezoneDropdown() {{
                    tzDropdown.hidden = false;
                    tzToggle.setAttribute('aria-expanded', 'true');
                    tzSearch.value = '';
                    renderOptions('');
                    tzSearch.focus();
                }}
                function closeTimezoneDropdown() {{
                    tzDropdown.hidden = true;
                    tzToggle.setAttribute('aria-expanded', 'false');
                }}

                setTzBadge(currentTimezone());
                tzToggle.addEventListener('click', function(e) {{
                    e.stopPropagation();
                    if (tzDropdown.hidden) openTimezoneDropdown(); else closeTimezoneDropdown();
                }});
                document.addEventListener('click', function(e) {{
                    if (!tzDropdown.hidden && !tzDropdown.contains(e.target) && e.target !== tzToggle)
                        closeTimezoneDropdown();
                }});
                document.addEventListener('keydown', function(e) {{
                    if (e.key === 'Escape' && !tzDropdown.hidden) {{ closeTimezoneDropdown(); tzToggle.focus(); }}
                }});
                tzSearch.addEventListener('input', function() {{ renderOptions(tzSearch.value); }});
                tzSearch.addEventListener('keydown', function(e) {{
                    if (e.key === 'ArrowDown') {{
                        e.preventDefault();
                        var first = tzOptions.querySelector('.timezone-option');
                        if (first) first.focus();
                    }} else if (e.key === 'Enter') {{
                        e.preventDefault();
                        var top = tzOptions.querySelector('.timezone-option');
                        if (top) selectTimezone(top.getAttribute('data-tz'));
                    }}
                }});
                tzOptions.addEventListener('keydown', function(e) {{
                    var items = Array.prototype.slice.call(tzOptions.querySelectorAll('.timezone-option'));
                    var idx = items.indexOf(document.activeElement);
                    if (e.key === 'ArrowDown') {{
                        e.preventDefault();
                        items[Math.min(idx + 1, items.length - 1)].focus();
                    }} else if (e.key === 'ArrowUp') {{
                        e.preventDefault();
                        if (idx <= 0) tzSearch.focus(); else items[idx - 1].focus();
                    }}
                }});
            }} else if (tzToggle) {{
                tzToggle.style.display = 'none';
            }}
        }});
    </script>
");
    }

    // Line-preserving: drops full-line comments and blank/indentation whitespace but never joins two lines,
    // so `//` comments still end where the source newline says and ASI never sees a token move to a new line.
    private static string MinifyJs(string js)
    {
        var lines = js.Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            kept.Add(trimmed);
        }
        return string.Join('\n', kept);
    }
}

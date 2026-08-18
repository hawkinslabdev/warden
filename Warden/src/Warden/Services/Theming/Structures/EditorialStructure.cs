namespace Warden.Services.Theming.Structures;

/// <summary>
/// Magazine-style page shape: kicker tag, square borderless bleed-width covers, a plain-text tag
/// style, a heavier title, a first-paragraph drop cap, and a single-row topbar (logo + nav left,
/// icons + Subscribe right). Pairs with any palette, not just "casper".
/// </summary>
public sealed class EditorialStructure : IWardenStructure
{
    public string Name => "editorial";

    public string Label => "Editorial";

    public string ComponentCss => """
                .lead-cover,
                .card-cover {
                    border-radius: 0;
                }
                .post-cover {
                    border: none;
                    border-radius: 0;
                    width: 100vw;
                    max-width: 920px;
                    margin-inline: calc(50% - 50vw);
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    text-transform: uppercase;
                    font-weight: 700;
                    font-size: 0.78rem;
                    letter-spacing: 0.04em;
                    text-decoration: none;
                    margin: 0.3rem 0 0.6rem;
                }
                .post-kicker:hover {
                    text-decoration: underline;
                }
                .tag-chip {
                    background: transparent;
                    color: var(--accent);
                    text-transform: uppercase;
                    font-weight: 600;
                    letter-spacing: 0.03em;
                    padding: 0;
                }
                .tag-chip:hover {
                    background: transparent;
                    text-decoration: underline;
                }
                .post-title {
                    font-weight: 800;
                    letter-spacing: -0.03em;
                }
                .post-header .post-meta {
                    border-bottom: 0;
                }
                .subscribe-trigger {
                    width: auto;
                    height: auto;
                    max-width: 14rem;
                    padding: 0.45rem 1.1rem;
                    border: 1px solid var(--accent);
                    border-radius: 999px;
                    background-color: var(--accent);
                    color: var(--bg-color);
                    font-family: var(--font-sans);
                    font-size: 0.85rem;
                    font-weight: 600;
                    line-height: 1.2;
                    white-space: nowrap;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    display: block;
                    transition: opacity 0.15s ease;
                }
                .subscribe-trigger::before {
                    display: none;
                }
                .subscribe-trigger:hover {
                    color: var(--bg-color);
                    background-color: var(--accent);
                    opacity: 0.88;
                }
                .content.reading > p:first-of-type::first-letter {
                    float: left;
                    font-family: var(--font-display);
                    font-size: 3.4em;
                    line-height: 0.86;
                    font-weight: 700;
                    padding: 0.03em 0.09em 0 0;
                }
                .pager .pager-older--archive {
                    color: var(--accent);
                    text-transform: uppercase;
                    font-weight: 700;
                    letter-spacing: 0.04em;
                    font-size: 0.75rem;
                }
                .pager .pager-older--archive:hover {
                    text-decoration: underline;
                }
                @media (min-width: 621px) {
                    .topbar {
                        flex-direction: row;
                        align-items: center;
                        justify-content: space-between;
                        flex-wrap: wrap;
                        gap: 1.5rem 2rem;
                    }
                    .masthead-actions {
                        position: static;
                        order: 3;
                    }
                    .brand {
                        order: 1;
                    }
                    .site-nav-wrap {
                        order: 2;
                        width: auto;
                        flex: 1 1 auto;
                        justify-content: flex-start;
                    }
                    .site-nav {
                        justify-content: flex-start;
                    }
                }
        """;
}

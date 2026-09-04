/**
 * ياقوت — Shared Layout (Header / Navigation / Footer)
 */
(function (window, document) {
    'use strict';

    var COLLAPSED_NAV_QUERY = typeof window.matchMedia === 'function'
        ? window.matchMedia('(max-width: 991.98px)')
        : { matches: false };
    var isTransferringPanel = false;

    function requestFrame(callback) {
        if (typeof window.requestAnimationFrame === 'function') {
            window.requestAnimationFrame(callback);
        } else {
            window.setTimeout(callback, 0);
        }
    }

    /* ── History panel state ──────────────────────
       Strategy:
         - When a panel opens  → pushState (adds a fake entry so "Back" hits us first)
         - When popstate fires  → if a panel is open, close it and do NOT push again
                                  so the next "Back" navigates normally
    ──────────────────────────────────────────── */
    function pushPanelState() {
        window.history.pushState({ yqPanel: true }, '');
    }

    /* ────────────────────────────────────────────
       Header scroll effect
    ──────────────────────────────────────────── */
    function initHeaderScroll() {
        var header = document.getElementById('yaqutHeader');
        if (!header) return;

        var ticking = false;

        var spacer = null;

        function update() {
            var currentY = window.scrollY;
            var shouldBeScrolled = currentY > 24;

            if (shouldBeScrolled !== header.classList.contains('is-scrolled')) {
                if (shouldBeScrolled) {
                    header.classList.add('is-scrolled');
                    if (!spacer) {
                        spacer = document.createElement('div');
                        spacer.id = 'yqHeaderSpacer';
                        header.parentNode.insertBefore(spacer, header);
                    }
                    spacer.style.height = header.offsetHeight + 'px';
                } else {
                    header.classList.remove('is-scrolled');
                    if (spacer && spacer.parentNode) {
                        spacer.parentNode.removeChild(spacer);
                        spacer = null;
                    }
                }
            } else if (shouldBeScrolled && spacer) {
                // Keep height updated in case of resize while scrolled
                spacer.style.height = header.offsetHeight + 'px';
            }
            ticking = false;
        }

        function onScroll() {
            if (!ticking) {
                requestFrame(update);
                ticking = true;
            }
        }

        window.addEventListener('scroll', onScroll, { passive: true });
        window.addEventListener('resize', onScroll, { passive: true });
        window.addEventListener('orientationchange', onScroll, { passive: true });
        update();
    }

    /* ────────────────────────────────────────────
       Mobile nav toggle + back-button intercept
    ──────────────────────────────────────────── */
    function syncNavState(nav, btn, isOpen) {
        nav.classList.toggle('is-open', isOpen);
        nav.setAttribute('aria-hidden', isOpen || !COLLAPSED_NAV_QUERY.matches ? 'false' : 'true');
        btn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        document.body.classList.toggle('has-nav-open', isOpen);
    }

    function initMenuToggleAria() {
        var btn = document.querySelector('[data-yq-nav-toggle]');
        var nav = document.getElementById('yaqutNav');
        var header = document.getElementById('yaqutHeader');
        if (!btn || !nav) return;

        function closeNav(restoreFocus) {
            if (!nav.classList.contains('is-open') && COLLAPSED_NAV_QUERY.matches) {
                nav.setAttribute('aria-hidden', 'true');
                btn.setAttribute('aria-expanded', 'false');
                document.body.classList.remove('has-nav-open');
                header && header.classList.remove('has-nav-open');
                return;
            }
            syncNavState(nav, btn, false);
            header && header.classList.remove('has-nav-open');
            if (restoreFocus) btn.focus();
        }

        function openNav() {
            var drawer = document.querySelector('[data-yq-cart-drawer]');
            var cartWasOpen = drawer && drawer.classList.contains('is-open');
            if (cartWasOpen && typeof window.dispatchEvent === 'function') {
                window.dispatchEvent(new window.CustomEvent('yq:cart-close'));
            }
            syncNavState(nav, btn, true);
            header && header.classList.add('has-nav-open');
            if (!cartWasOpen) {
                pushPanelState(); // fake history entry so Back closes nav first
            }
            var firstLink = nav.querySelector('a');
            if (firstLink) firstLink.focus();
        }

        btn.addEventListener('click', function () {
            nav.classList.contains('is-open') ? closeNav(false) : openNav();
        });

        // Close when a nav link is clicked (normal navigation)
        nav.querySelectorAll('a').forEach(function (link) {
            link.addEventListener('click', function () { closeNav(false); });
        });

        // Close on outside click
        document.addEventListener('click', function (e) {
            if (!nav.classList.contains('is-open')) return;
            if (nav.contains(e.target) || btn.contains(e.target)) return;
            closeNav(false);
        });

        // Keyboard: Guarded Escape to close + Focus containment while open
        document.addEventListener('keydown', function (e) {
            if (!nav.classList.contains('is-open') || !COLLAPSED_NAV_QUERY.matches) return;

            if (e.key === 'Escape') {
                e.preventDefault();
                closeNav(true);
                return;
            }

            if (e.key === 'Tab') {
                var candidates = Array.prototype.slice.call(nav.querySelectorAll(
                    'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                ));
                var focusable = candidates.filter(function (el) {
                    if (el.disabled || el.closest('[hidden]')) return false;
                    var style = window.getComputedStyle(el);
                    return style.display !== 'none' && style.visibility !== 'hidden';
                });
                if (!focusable.length) {
                    e.preventDefault();
                    return;
                }

                var first = focusable[0];
                var last = focusable[focusable.length - 1];

                if (e.shiftKey && (document.activeElement === first || !nav.contains(document.activeElement))) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && (document.activeElement === last || !nav.contains(document.activeElement))) {
                    e.preventDefault();
                    first.focus();
                }
            }
        });

        // Handle viewport resize
        var onViewportChange = function () {
            if (!COLLAPSED_NAV_QUERY.matches) {
                closeNav(false);
                nav.setAttribute('aria-hidden', 'false');
            } else if (!nav.classList.contains('is-open')) {
                nav.setAttribute('aria-hidden', 'true');
            }
        };
        if (COLLAPSED_NAV_QUERY.addEventListener) {
            COLLAPSED_NAV_QUERY.addEventListener('change', onViewportChange);
        } else if (COLLAPSED_NAV_QUERY.addListener) {
            COLLAPSED_NAV_QUERY.addListener(onViewportChange);
        }

        nav.setAttribute('aria-hidden', COLLAPSED_NAV_QUERY.matches ? 'true' : 'false');
        document.body.classList.remove('has-nav-open');

        // Mutual exclusivity: close Sidebar when Cart Drawer requests it
        window.addEventListener('yq:nav-close', function (e) {
            if (nav.classList.contains('is-open') && COLLAPSED_NAV_QUERY.matches) {
                if (e && e.detail && e.detail.transferringToCart) {
                    isTransferringPanel = true;
                    window.setTimeout(function () { isTransferringPanel = false; }, 400);
                }
                var restoreFocus = !!(e && e.detail && e.detail.restoreFocus);
                closeNav(restoreFocus);
            }
        });

        // Back button: close nav instead of navigating away.
        // Do NOT re-push after closing — the consumed fake entry is gone,
        // so the very next Back press will navigate normally.
        window.addEventListener('popstate', function (e) {
            if (nav.classList.contains('is-open')) {
                closeNav(true);
                // stop here — no pushPanelState, so next Back navigates away
            }
        });
    }

    /* ────────────────────────────────────────────
       Cart drawer back-button intercept
       Watches class changes via MutationObserver
    ──────────────────────────────────────────── */
    function initCartDrawerBackIntercept() {
        var drawer = document.querySelector('[data-yq-cart-drawer]');
        if (!drawer) return;

        var wasOpen = false;

        // Push a fake history entry the moment the drawer opens
        if (typeof window.MutationObserver === 'function') {
            var observer = new window.MutationObserver(function () {
                var isNowOpen = drawer.classList.contains('is-open');
                if (isNowOpen && !wasOpen) {
                    if (isTransferringPanel) {
                        isTransferringPanel = false;
                    } else {
                        pushPanelState();
                    }
                }
                wasOpen = isNowOpen;
            });
            observer.observe(drawer, { attributes: true, attributeFilter: ['class'] });
        }

        // Back button: close drawer instead of navigating away.
        // Same rule — do NOT re-push after closing.
        window.addEventListener('popstate', function (e) {
            if (drawer.classList.contains('is-open')) {
                var closeBtn = drawer.querySelector('[data-yq-cart-close]');
                if (closeBtn) closeBtn.click();
                // stop here — next Back navigates normally
            }
        });
    }

    /* ────────────────────────────────────────────
       Boot
    ──────────────────────────────────────────── */
    function init() {
        initHeaderScroll();
        initMenuToggleAria();
        initCartDrawerBackIntercept();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

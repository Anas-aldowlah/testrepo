/**
 * ياقوت — Shared Layout (Header / Navigation / Footer)
 */
(function (window, document) {
    'use strict';

    var COLLAPSED_NAV_QUERY = window.matchMedia('(max-width: 991.98px)');

    function initHeaderScroll() {
        var header = document.getElementById('yaqutHeader');
        if (!header) return;

        var ticking = false;
        function update() {
            header.classList.toggle('is-scrolled', window.scrollY > 24);
            ticking = false;
        }
        function onScroll() {
            if (!ticking) {
                window.requestAnimationFrame(update);
                ticking = true;
            }
        }

        window.addEventListener('scroll', onScroll, { passive: true });
        update();
    }

    function syncNavState(nav, btn, isOpen) {
        nav.classList.toggle('is-open', isOpen);
        nav.setAttribute('aria-hidden', isOpen || !COLLAPSED_NAV_QUERY.matches ? 'false' : 'true');
        btn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    }

    function initMenuToggleAria() {
        var btn = document.querySelector('[data-yq-nav-toggle]');
        var nav = document.getElementById('yaqutNav');
        if (!btn || !nav) return;

        function closeNav(restoreFocus) {
            if (!nav.classList.contains('is-open') && COLLAPSED_NAV_QUERY.matches) {
                nav.setAttribute('aria-hidden', 'true');
                btn.setAttribute('aria-expanded', 'false');
                return;
            }

            syncNavState(nav, btn, false);
            if (restoreFocus) btn.focus();
        }

        function openNav() {
            syncNavState(nav, btn, true);
            var firstLink = nav.querySelector('a');
            if (firstLink) firstLink.focus();
        }

        btn.addEventListener('click', function () {
            var shouldOpen = !nav.classList.contains('is-open');
            shouldOpen ? openNav() : closeNav(false);
        });

        nav.querySelectorAll('a').forEach(function (link) {
            link.addEventListener('click', function () {
                closeNav(false);
            });
        });

        document.addEventListener('click', function (e) {
            if (!nav.classList.contains('is-open')) return;
            if (nav.contains(e.target) || btn.contains(e.target)) return;
            closeNav(false);
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') closeNav(true);
        });

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
    }

    function init() {
        initHeaderScroll();
        initMenuToggleAria();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

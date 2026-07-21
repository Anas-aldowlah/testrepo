/**
 * ياقوت — Shared Layout (Header / Navigation / Footer)
 */
(function (window, document) {
    'use strict';

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

    function initMenuToggleAria() {
        var btn = document.querySelector('[data-yq-nav-toggle]');
        var nav = document.getElementById('yaqutNav');
        if (!btn || !nav) return;

        btn.addEventListener('click', function () {
            var shouldOpen = !nav.classList.contains('is-open');
            nav.classList.toggle('is-open', shouldOpen);
            btn.setAttribute('aria-expanded', shouldOpen ? 'true' : 'false');
        });
    }

    function initNavAutoClose() {
        var nav = document.querySelector('.yaqut-nav');
        if (!nav) return;

        nav.querySelectorAll('a').forEach(function (link) {
            link.addEventListener('click', function () {
                nav.classList.remove('is-open');
                var btn = document.querySelector('.yaqut-menu-toggle');
                if (btn) btn.setAttribute('aria-expanded', 'false');
            });
        });
    }

    function initAccountDropdown() {
        document.querySelectorAll('[data-yq-dropdown]').forEach(function (root) {
            var trigger = root.querySelector('.yaqut-account__trigger');
            if (!trigger) return;

            function close() {
                root.classList.remove('is-open');
                trigger.setAttribute('aria-expanded', 'false');
            }
            function open() {
                root.classList.add('is-open');
                trigger.setAttribute('aria-expanded', 'true');
            }

            trigger.addEventListener('click', function (e) {
                e.stopPropagation();
                root.classList.contains('is-open') ? close() : open();
            });

            document.addEventListener('click', function (e) {
                if (!root.contains(e.target)) close();
            });

            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') close();
            });
        });
    }

    function init() {
        initHeaderScroll();
        initMenuToggleAria();
        initNavAutoClose();
        initAccountDropdown();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

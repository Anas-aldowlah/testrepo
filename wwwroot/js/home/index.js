(function (window, document) {
    'use strict';

    function initAuthStorageCleanup() {
        var root = document.querySelector('[data-yq-auth-clear="true"]');
        if (!root) return;

        try {
            ['regName', 'regPhone', 'regConfirmPhone', 'regPassword', 'regConfirm', 'AuthPanel']
                .forEach(function (key) { window.localStorage.removeItem(key); });
        } catch (error) {
            return;
        }
    }

    function initHeroCarousel(carousel) {
        var slides = Array.prototype.slice.call(carousel.querySelectorAll('[data-yq-hero-slide]'));
        var dots = Array.prototype.slice.call(carousel.querySelectorAll('[data-yq-hero-dot]'));
        var previousButton = carousel.querySelector('[data-yq-hero-prev]');
        var nextButton = carousel.querySelector('[data-yq-hero-next]');
        var prefersReducedMotion = typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var currentIndex = 0;
        var autoplayTimer = null;
        var touchStartX = 0;
        var autoplayDelay = 4800;

        if (slides.length < 2) return;

        function showSlide(nextIndex) {
            var normalizedIndex = (nextIndex + slides.length) % slides.length;

            slides.forEach(function (slide, index) {
                var isActive = index === normalizedIndex;
                var productLink = slide.querySelector('a');
                slide.classList.toggle('is-active', isActive);
                slide.setAttribute('aria-hidden', isActive ? 'false' : 'true');
                if (productLink) productLink.tabIndex = isActive ? 0 : -1;
            });

            dots.forEach(function (dot, index) {
                var isActive = index === normalizedIndex;
                dot.classList.toggle('is-active', isActive);
                dot.setAttribute('aria-current', isActive ? 'true' : 'false');
            });

            currentIndex = normalizedIndex;
        }

        function stopAutoplay() {
            if (autoplayTimer !== null) window.clearInterval(autoplayTimer);
            autoplayTimer = null;
        }

        function startAutoplay() {
            stopAutoplay();
            if (prefersReducedMotion || document.hidden) return;
            autoplayTimer = window.setInterval(function () {
                showSlide(currentIndex + 1);
            }, autoplayDelay);
        }

        function moveTo(nextIndex) {
            showSlide(nextIndex);
            startAutoplay();
        }

        if (previousButton) previousButton.addEventListener('click', function () { moveTo(currentIndex - 1); });
        if (nextButton) nextButton.addEventListener('click', function () { moveTo(currentIndex + 1); });

        dots.forEach(function (dot) {
            dot.addEventListener('click', function () {
                moveTo(Number(dot.getAttribute('data-yq-hero-dot')) || 0);
            });
        });

        carousel.addEventListener('mouseenter', stopAutoplay);
        carousel.addEventListener('mouseleave', startAutoplay);
        carousel.addEventListener('focusin', stopAutoplay);
        carousel.addEventListener('focusout', function (event) {
            if (!carousel.contains(event.relatedTarget)) startAutoplay();
        });
        carousel.addEventListener('touchstart', function (event) {
            touchStartX = event.touches[0].clientX;
            stopAutoplay();
        }, { passive: true });
        carousel.addEventListener('touchend', function (event) {
            var distance = event.changedTouches[0].clientX - touchStartX;
            if (Math.abs(distance) > 45) moveTo(currentIndex + (distance > 0 ? -1 : 1));
            else startAutoplay();
        }, { passive: true });

        document.addEventListener('visibilitychange', function () {
            if (document.hidden) stopAutoplay();
            else startAutoplay();
        });

        startAutoplay();
    }

    function init() {
        initAuthStorageCleanup();
        document.querySelectorAll('[data-yq-hero-carousel]').forEach(initHeroCarousel);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window, document);

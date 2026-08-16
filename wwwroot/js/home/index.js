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
        var interactiveElementsBySlide = slides.map(function (slide) {
            return Array.prototype.slice.call(slide.querySelectorAll('a[href]')).map(function (element) {
                return {
                    element: element,
                    authoredTabIndex: element.getAttribute('tabindex')
                };
            });
        });
        var prefersReducedMotion = typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var initialActiveIndex = slides.findIndex(function (slide) { return slide.classList.contains('is-active'); });
        var currentIndex = initialActiveIndex >= 0 ? initialActiveIndex : 0;
        var autoplayTimer = null;
        var touchStartX = 0;
        var autoplayDelay = 4800;
        var isPointerOver = false;
        var hasFocusWithin = false;

        if (slides.length === 0) return;

        function showSlide(nextIndex) {
            var normalizedIndex = (nextIndex + slides.length) % slides.length;

            slides.forEach(function (slide, index) {
                var isActive = index === normalizedIndex;
                slide.classList.toggle('is-active', isActive);
                slide.setAttribute('aria-hidden', isActive ? 'false' : 'true');
                interactiveElementsBySlide[index].forEach(function (entry) {
                    if (!isActive) {
                        entry.element.setAttribute('tabindex', '-1');
                    } else if (entry.authoredTabIndex === null) {
                        entry.element.removeAttribute('tabindex');
                    } else {
                        entry.element.setAttribute('tabindex', entry.authoredTabIndex);
                    }
                });
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
            if (prefersReducedMotion || document.hidden || isPointerOver || hasFocusWithin) return;
            autoplayTimer = window.setInterval(function () {
                showSlide(currentIndex + 1);
            }, autoplayDelay);
        }

        function moveTo(nextIndex) {
            showSlide(nextIndex);
            startAutoplay();
        }

        showSlide(currentIndex);
        if (slides.length < 2) return;

        if (previousButton) previousButton.addEventListener('click', function () { moveTo(currentIndex - 1); });
        if (nextButton) nextButton.addEventListener('click', function () { moveTo(currentIndex + 1); });

        dots.forEach(function (dot) {
            dot.addEventListener('click', function () {
                moveTo(Number(dot.getAttribute('data-yq-hero-dot')) || 0);
            });
        });

        carousel.addEventListener('mouseenter', function () {
            isPointerOver = true;
            stopAutoplay();
        });
        carousel.addEventListener('mouseleave', function () {
            isPointerOver = false;
            startAutoplay();
        });
        carousel.addEventListener('focusin', function () {
            hasFocusWithin = true;
            stopAutoplay();
        });
        carousel.addEventListener('focusout', function (event) {
            if (!carousel.contains(event.relatedTarget)) {
                hasFocusWithin = false;
                startAutoplay();
            }
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

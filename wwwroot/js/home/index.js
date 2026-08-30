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

    function initHeroFader() {
        var hero = document.querySelector('.yq-home-hero');
        var bgContainer = document.getElementById('yqHeroBgFader');
        var productContainer = document.getElementById('yqHeroFader');
        var scrollIndicator = document.querySelector('.yq-home-hero__scroll');

        if (!hero) return;

        // Scroll Down Click Handling
        if (scrollIndicator) {
            scrollIndicator.addEventListener('click', function () {
                var nextSection = hero.nextElementSibling;
                if (nextSection) {
                    nextSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
            scrollIndicator.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    var nextSection = hero.nextElementSibling;
                    if (nextSection) {
                        nextSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                }
            });
        }

        var bgSlides = bgContainer ? Array.prototype.slice.call(bgContainer.querySelectorAll('.yq-hero-bg-slide')) : [];
        var productSlides = productContainer ? Array.prototype.slice.call(productContainer.querySelectorAll('.yq-showcase__card, .yq-grand-product__item')) : [];
        var dots = hero.querySelectorAll('[data-yq-fader-dot]');
        var prevBtn = hero.querySelector('[data-yq-fader-prev]');
        var nextBtn = hero.querySelector('[data-yq-fader-next]');

        var slideCount = Math.max(bgSlides.length, productSlides.length);
        if (slideCount <= 1) return;

        var currentIndex = 0;
        var autoplayTimer = null;
        var autoplayDelay = 5000;
        var isPaused = false;
        var touchStartX = 0;

        var prefersReducedMotion = typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        function showSlide(nextIndex) {
            var normalizedIndex = (nextIndex + slideCount) % slideCount;

            bgSlides.forEach(function (slide, idx) {
                slide.classList.toggle('is-active', idx === normalizedIndex);
            });

            productSlides.forEach(function (slide, idx) {
                var isActive = idx === normalizedIndex;
                slide.classList.toggle('is-active', isActive);
                var link = slide.querySelector('a');
                if (link) {
                    if (isActive) {
                        link.removeAttribute('tabindex');
                    } else {
                        link.setAttribute('tabindex', '-1');
                    }
                }
            });

            if (dots && dots.length > 0) {
                dots.forEach(function (dot, idx) {
                    dot.classList.toggle('is-active', idx === normalizedIndex);
                    dot.setAttribute('aria-current', idx === normalizedIndex ? 'true' : 'false');
                });
            }

            currentIndex = normalizedIndex;
        }

        function stopAutoplay() {
            if (autoplayTimer !== null) {
                window.clearInterval(autoplayTimer);
                autoplayTimer = null;
            }
        }

        function startAutoplay() {
            stopAutoplay();
            if (prefersReducedMotion || document.hidden || isPaused) return;
            autoplayTimer = window.setInterval(function () {
                showSlide(currentIndex + 1);
            }, autoplayDelay);
        }

        function moveTo(nextIndex) {
            showSlide(nextIndex);
            startAutoplay();
        }

        // Initial setup
        showSlide(currentIndex);

        // Prev & Next Buttons
        if (prevBtn) {
            prevBtn.addEventListener('click', function (e) {
                e.preventDefault();
                moveTo(currentIndex - 1);
            });
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', function (e) {
                e.preventDefault();
                moveTo(currentIndex + 1);
            });
        }

        // Dots
        if (dots && dots.length > 0) {
            dots.forEach(function (dot) {
                dot.addEventListener('click', function (e) {
                    e.preventDefault();
                    var targetIdx = parseInt(dot.getAttribute('data-yq-fader-dot'), 10);
                    if (!isNaN(targetIdx)) {
                        moveTo(targetIdx);
                    }
                });
            });
        }

        hero.addEventListener('mouseenter', function () {
            isPaused = true;
            stopAutoplay();
        });

        hero.addEventListener('mouseleave', function () {
            isPaused = false;
            startAutoplay();
        });

        hero.addEventListener('focusin', function () {
            isPaused = true;
            stopAutoplay();
        });

        hero.addEventListener('focusout', function (e) {
            if (!hero.contains(e.relatedTarget)) {
                isPaused = false;
                startAutoplay();
            }
        });

        hero.addEventListener('touchstart', function (e) {
            touchStartX = e.touches[0].clientX;
            stopAutoplay();
        }, { passive: true });

        hero.addEventListener('touchend', function (e) {
            var distance = e.changedTouches[0].clientX - touchStartX;
            if (Math.abs(distance) > 45) {
                moveTo(currentIndex + (distance > 0 ? -1 : 1));
            } else {
                startAutoplay();
            }
        }, { passive: true });

        document.addEventListener('visibilitychange', function () {
            if (document.hidden) stopAutoplay();
            else startAutoplay();
        });

        startAutoplay();
    }

    function init() {
        initAuthStorageCleanup();
        initHeroFader();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window, document);

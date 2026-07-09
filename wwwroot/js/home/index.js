/**
 * ياقوت — Home / Index page interactions
 */
(function (window, document) {
    'use strict';

    function initHeroCarousel() {
        var carousel = document.querySelector('[data-yq-hero-carousel]');
        if (!carousel) return;

        var slides = carousel.querySelectorAll('.yq-home-hero__slide');
        var dots = carousel.querySelectorAll('[data-yq-hero-dot]');
        if (slides.length < 2) return;

        var AUTOPLAY_MS = 5000;
        var reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var index = 0;
        var intervalId = null;

        function goTo(nextIndex) {
            slides[index].classList.remove('is-active');
            if (dots[index]) dots[index].classList.remove('is-active');

            index = (nextIndex + slides.length) % slides.length;

            slides[index].classList.add('is-active');
            if (dots[index]) dots[index].classList.add('is-active');
        }

        function start() {
            if (reducedMotion) return;
            stop();
            intervalId = setInterval(function () { goTo(index + 1); }, AUTOPLAY_MS);
        }

        function stop() {
            if (intervalId) clearInterval(intervalId);
            intervalId = null;
        }

        function restart() {
            stop();
            start();
        }

        dots.forEach(function (dot) {
            dot.addEventListener('click', function () {
                var target = parseInt(dot.getAttribute('data-yq-hero-dot'), 10);
                goTo(target);
                restart();
            });
        });

        carousel.addEventListener('mouseenter', stop);
        carousel.addEventListener('mouseleave', start);

        var touchStartX = 0;
        carousel.addEventListener('touchstart', function (e) {
            touchStartX = e.touches[0].clientX;
        }, { passive: true });

        carousel.addEventListener('touchend', function (e) {
            var diff = e.changedTouches[0].clientX - touchStartX;
            if (Math.abs(diff) > 40) {
                goTo(index + (diff > 0 ? -1 : 1));
                restart();
            }
        }, { passive: true });

        start();
    }

    function init() {
        initHeroCarousel();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
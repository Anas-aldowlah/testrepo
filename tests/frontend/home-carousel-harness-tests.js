(function (window, document) {
    'use strict';

    function runHarness() {
    var state = window.__homeCarouselHarness;
    var carousel = document.querySelector('[data-yq-hero-carousel]');
    var slides = Array.prototype.slice.call(carousel.querySelectorAll('[data-yq-hero-slide]'));
    var dots = Array.prototype.slice.call(carousel.querySelectorAll('[data-yq-hero-dot]'));
    var singleCarousel = document.querySelector('[data-yq-hero-carousel-single]');
    var singleSlide = singleCarousel.querySelector('[data-yq-hero-slide]');
    var failures = [];
    var passes = 0;

    function check(condition, name) {
        if (condition) passes += 1;
        else failures.push(name);
    }

    function activeIndex() {
        return slides.findIndex(function (slide) { return slide.classList.contains('is-active'); });
    }

    function checkState(expectedIndex, label) {
        check(slides.filter(function (slide) { return slide.classList.contains('is-active'); }).length === 1,
            label + ': exactly one slide is active');
        check(activeIndex() === expectedIndex, label + ': expected slide is active');
        check(slides.every(function (slide, index) {
            return slide.getAttribute('aria-hidden') === (index === expectedIndex ? 'false' : 'true');
        }), label + ': aria-hidden matches visibility');
        check(slides.every(function (slide, index) {
            return Array.prototype.every.call(slide.querySelectorAll('a[href]'), function (link) {
                if (index !== expectedIndex) return link.getAttribute('tabindex') === '-1';
                var authoredTabIndex = link.getAttribute('data-yq-harness-authored-tabindex');
                return authoredTabIndex === null
                    ? link.getAttribute('tabindex') === null && link.tabIndex >= 0
                    : link.getAttribute('tabindex') === authoredTabIndex;
            });
        }), label + ': every link matches slide visibility and authored tabindex');
        check(dots.every(function (dot, index) {
            return dot.getAttribute('aria-current') === (index === expectedIndex ? 'true' : 'false');
        }), label + ': dot state matches the active slide');
    }

    function checkSingleSlideState(label) {
        check(singleCarousel.querySelectorAll('[data-yq-hero-slide]').length === 1,
            label + ': exactly one slide exists');
        check(singleSlide.classList.contains('is-active'), label + ': the single slide is active');
        check(singleSlide.getAttribute('aria-hidden') === 'false', label + ': the single slide is aria-visible');
        check(Array.prototype.every.call(singleSlide.querySelectorAll('a[href]'), function (link) {
            return link.getAttribute('tabindex') === null && link.tabIndex >= 0;
        }), label + ': single-slide native links remain natively focusable');
        check(singleCarousel.querySelectorAll('.yq-home-carousel__product[tabindex]').length === 0,
            label + ': single-slide wrapper has no artificial tabindex');
    }

    function dispatchTouch(type, x) {
        var event = new Event(type, { bubbles: true });
        Object.defineProperty(event, type === 'touchstart' ? 'touches' : 'changedTouches', {
            value: [{ clientX: x }]
        });
        carousel.dispatchEvent(event);
    }

    try {
        checkSingleSlideState('single-slide initial state');
        var singleTimerBaseline = state.activeTimerCount();
        check(singleTimerBaseline === (state.reducedMotion ? 0 : 1),
            'single-slide initialization creates no autoplay timer');
        singleCarousel.dispatchEvent(new Event('mouseenter'));
        singleCarousel.dispatchEvent(new Event('mouseleave'));
        check(state.activeTimerCount() === singleTimerBaseline,
            'single-slide hover resume creates no autoplay timer');
        singleCarousel.querySelector('a[href]').focus();
        document.getElementById('outsideControl').focus();
        check(state.activeTimerCount() === singleTimerBaseline,
            'single-slide focus resume creates no autoplay timer');
        document.dispatchEvent(new Event('visibilitychange'));
        check(state.activeTimerCount() === singleTimerBaseline,
            'single-slide visibility resume creates no autoplay timer');
        checkSingleSlideState('single-slide after resume events');

        checkState(0, 'initial state');
        check(carousel.querySelectorAll('.yq-home-carousel__product[tabindex]').length === 0,
            'non-actionable product wrappers create no Tab stops');
        check(slides[0].querySelector('.yq-home-carousel__product-link').getAttribute('tabindex') === null,
            'active native link has no forced tabindex');
        check(slides[0].querySelector('.yq-home-button').getAttribute('tabindex') === '2',
            'active authored tabindex is preserved');
        check(carousel.querySelector('[data-yq-hero-prev]').tabIndex === 0
            && carousel.querySelector('[data-yq-hero-next]').tabIndex === 0
            && dots.every(function (dot) { return dot.tabIndex === 0; }),
            'previous, next, and dot controls remain natively reachable');

        carousel.querySelector('[data-yq-hero-next]').click();
        checkState(1, 'next navigation');
        carousel.querySelector('[data-yq-hero-prev]').click();
        checkState(0, 'previous navigation');
        carousel.querySelector('[data-yq-hero-next]').click();
        checkState(1, 'tabindex zero restoration');
        dots[2].click();
        checkState(2, 'dot navigation');
        dots[0].click();
        dots[2].click();
        checkState(2, 'tabindex minus one restoration');

        dots[0].focus();
        check(state.activeTimerCount() === 0, 'focus within the carousel pauses autoplay');
        dots[1].click();
        state.tickAutoplay();
        checkState(1, 'focused navigation remains paused');
        check(document.activeElement === dots[0] && !document.activeElement.closest('[aria-hidden="true"]'),
            'navigation does not force focus or leave focus in a hidden slide');

        document.getElementById('outsideControl').focus();
        check(state.activeTimerCount() === (state.reducedMotion ? 0 : 1),
            'leaving the carousel resumes autoplay only when motion is allowed');
        var beforeOutsideTick = activeIndex();
        state.tickAutoplay();
        check(activeIndex() === (state.reducedMotion ? beforeOutsideTick : (beforeOutsideTick + 1) % slides.length),
            'controlled autoplay follows reduced-motion preference');

        var beforeHover = activeIndex();
        carousel.dispatchEvent(new Event('mouseenter'));
        state.tickAutoplay();
        check(activeIndex() === beforeHover, 'pointer hover pauses autoplay');
        carousel.dispatchEvent(new Event('mouseleave'));
        state.tickAutoplay();
        check(activeIndex() === (state.reducedMotion ? beforeHover : (beforeHover + 1) % slides.length),
            'pointer leave resumes autoplay only when motion is allowed');

        var beforeSwipe = activeIndex();
        dispatchTouch('touchstart', 200);
        dispatchTouch('touchend', 100);
        checkState((beforeSwipe + 1) % slides.length, 'touch navigation');
        check(state.reducedMotion || state.timerDelays().every(function (delay) { return delay === 4800; }),
            'autoplay timing remains 4800 milliseconds');
        check(window.matchMedia('(prefers-reduced-motion: reduce)').matches === state.reducedMotion,
            'production reads the controlled reduced-motion preference');
        check(document.documentElement.scrollWidth <= window.innerWidth,
            'Home carousel harness has no document overflow');
        check(state.consoleErrors.length === 0, 'console error channel remains clear');
        check(state.pageErrors.length === 0, 'page error channel remains clear');
        check(state.unhandledRejections.length === 0, 'unhandled rejection channel remains clear');
    } catch (error) {
        failures.push('unhandled harness error: ' + error.message);
    }

    var result = document.getElementById('harnessResult');
    result.textContent = failures.length === 0 ? 'PASS ' + passes : 'FAIL ' + failures.join(' | ');
    result.dataset.result = failures.length === 0 ? 'pass' : 'fail';
    result.dataset.width = String(window.innerWidth);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', runHarness);
    else runHarness();
})(window, document);

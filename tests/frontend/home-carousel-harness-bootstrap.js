(function (window) {
    'use strict';

    var nextTimerId = 1;
    var timers = new Map();
    var reducedMotion = new URLSearchParams(window.location.search).get('reduced') === '1';
    var consoleErrors = [];
    var pageErrors = [];
    var unhandledRejections = [];
    var originalConsoleError = window.console.error;

    window.console.error = function () {
        consoleErrors.push(Array.prototype.join.call(arguments, ' '));
        originalConsoleError.apply(window.console, arguments);
    };

    window.addEventListener('error', function (event) {
        pageErrors.push(event.message || 'unknown page error');
    });
    window.addEventListener('unhandledrejection', function (event) {
        unhandledRejections.push(String(event.reason));
    });

    window.setInterval = function (callback, delay) {
        var timerId = nextTimerId;
        nextTimerId += 1;
        timers.set(timerId, { callback: callback, delay: delay });
        return timerId;
    };
    window.clearInterval = function (timerId) {
        timers.delete(timerId);
    };
    window.matchMedia = function (query) {
        return {
            matches: query === '(prefers-reduced-motion: reduce)' && reducedMotion,
            media: query,
            addEventListener: function () {},
            removeEventListener: function () {}
        };
    };

    window.__homeCarouselHarness = {
        consoleErrors: consoleErrors,
        pageErrors: pageErrors,
        unhandledRejections: unhandledRejections,
        reducedMotion: reducedMotion,
        activeTimerCount: function () { return timers.size; },
        tickAutoplay: function () {
            Array.from(timers.values()).forEach(function (timer) { timer.callback(); });
        },
        timerDelays: function () {
            return Array.from(timers.values()).map(function (timer) { return timer.delay; });
        }
    };
})(window);

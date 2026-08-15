(function () {
    'use strict';

    const popovers = new Map();
    const fetchQueue = [];
    const fetchCalls = [];
    const consoleErrors = [];
    const pageErrors = [];
    const unhandledRejections = [];
    let modalShows = 0;
    let popoverConstructions = 0;

    const originalConsoleError = console.error;
    console.error = function () {
        consoleErrors.push(Array.from(arguments).join(' '));
        originalConsoleError.apply(console, arguments);
    };
    window.addEventListener('error', function (event) {
        pageErrors.push(event.message || 'Unknown page error');
    });
    window.addEventListener('unhandledrejection', function (event) {
        unhandledRejections.push(String(event.reason || 'Unknown rejection'));
    });

    class Popover {
        constructor(element, options) {
            popoverConstructions += 1;
            this.element = element;
            this.options = options;
            this.hideCalls = 0;
            popovers.set(element, this);
        }

        hide() {
            this.hideCalls += 1;
        }

        static getInstance(element) {
            return popovers.get(element) || null;
        }
    }

    class Modal {
        constructor(element) {
            this.element = element;
        }

        show() {
            modalShows += 1;
        }
    }

    window.bootstrap = { Popover, Modal };
    window.fetch = function (url, options) {
        fetchCalls.push({ url, options });
        if (fetchQueue.length === 0) return Promise.reject(new Error('No mocked response queued.'));
        return fetchQueue.shift()();
    };
    window.__ordersHarness = {
        popovers,
        fetchQueue,
        fetchCalls,
        consoleErrors,
        pageErrors,
        unhandledRejections,
        modalShows: function () { return modalShows; },
        popoverConstructions: function () { return popoverConstructions; }
    };
})();

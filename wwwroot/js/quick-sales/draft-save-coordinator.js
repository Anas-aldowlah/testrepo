(function (global) {
    'use strict';

    function create(options) {
        var delay = Number.isFinite(options.delay) ? options.delay : 1000;
        var timer = null;
        var revision = 0;
        var savedRevision = 0;
        var requestPending = false;
        var manualPending = false;
        var requestInFlight = false;
        var drainPromise = null;

        function clearScheduled() {
            if (timer !== null) {
                global.clearTimeout(timer);
                timer = null;
            }
        }

        function drain() {
            return (async function () {
                var lastResult = { success: false, skipped: true };

                while (requestPending) {
                    requestPending = false;
                    var manual = manualPending;
                    manualPending = false;
                    var requestRevision = revision;
                    var snapshot = options.getSnapshot();

                    requestInFlight = true;
                    try {
                        lastResult = await options.save(snapshot, {
                            manual: manual,
                            revision: requestRevision
                        });
                    } catch (error) {
                        lastResult = { success: false, error: error };
                    } finally {
                        requestInFlight = false;
                    }

                    if (lastResult && lastResult.success) {
                        savedRevision = Math.max(savedRevision, requestRevision);
                    }
                    if (typeof options.onSettled === 'function') {
                        options.onSettled(lastResult);
                    }
                }

                return lastResult;
            })().finally(function () {
                drainPromise = null;
                if (requestPending) {
                    drainPromise = drain();
                }
            });
        }

        function request(manual) {
            clearScheduled();
            requestPending = true;
            manualPending = manualPending || manual === true;

            if (!drainPromise) {
                drainPromise = drain();
            }

            return drainPromise;
        }

        function markDirty() {
            revision++;
            clearScheduled();

            if (requestInFlight) {
                requestPending = true;
                return revision;
            }

            timer = global.setTimeout(function () {
                timer = null;
                request(false);
            }, delay);

            return revision;
        }

        function waitForIdle() {
            return drainPromise || Promise.resolve({ success: true, skipped: true });
        }

        function getState() {
            return {
                revision: revision,
                savedRevision: savedRevision,
                dirty: revision > savedRevision,
                requestInFlight: requestInFlight,
                requestPending: requestPending,
                scheduled: timer !== null
            };
        }

        return {
            clearScheduled: clearScheduled,
            getState: getState,
            markDirty: markDirty,
            request: request,
            waitForIdle: waitForIdle
        };
    }

    global.YaqutDraftSaveCoordinator = { create: create };
})(window);

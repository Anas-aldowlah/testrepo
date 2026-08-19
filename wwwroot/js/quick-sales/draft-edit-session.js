(function (global) {
    'use strict';

    function create(options) {
        var saleId = Number(options.saleId) || 0;
        var revision = Number(options.initialRevision) || 0;
        var editSessionId = null;
        var ownsLock = false;
        var terminal = false;
        var authenticationFailed = false;
        var lastSaveFailed = false;
        var editingEnabled = false;
        var originalDisabled = new WeakMap();
        var renewTimer = null;
        var stateTimer = null;
        var root = options.root;
        var token = options.token || '';

        function headers() {
            return {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': token
            };
        }

        async function readJson(response) {
            try {
                var result = await global.YaqutAdminAjax.readJson(response);
                result.success = result.success === true;
                result.status = response.status;
                return result;
            } catch (value) {
                var requestError = global.YaqutAdminAjax.normalizeError(value);
                if (requestError.kind === 'http' && requestError.payload) {
                    requestError.payload.success = false;
                    requestError.payload.status = requestError.status;
                    return requestError.payload;
                }

                if (requestError.kind === 'session_expired' || requestError.kind === 'forbidden') {
                    expireAuthentication(requestError.message);
                    if (typeof options.onAuthFailure === 'function') options.onAuthFailure(requestError);
                }

                return {
                    success: false,
                    code: requestError.kind,
                    message: requestError.message,
                    status: requestError.status
                };
            }
        }

        async function post(url, body, keepalive) {
            try {
                return await readJson(await global.fetch(url, {
                    method: 'POST',
                    headers: headers(),
                    body: JSON.stringify(body),
                    keepalive: keepalive === true
                }));
            } catch (value) {
                var requestError = global.YaqutAdminAjax.normalizeError(value);
                return { success: false, code: requestError.kind, message: requestError.message, status: requestError.status };
            }
        }

        function editableControls() {
            return root ? root.querySelectorAll('input:not([type="hidden"]), textarea, select, button') : [];
        }

        function setEditable(enabled) {
            editingEnabled = enabled;
            editableControls().forEach(function (control) {
                if (control.id === 'btnEnterDraftEdit' || control.closest('#saleSuccessModal')) return;
                if (!originalDisabled.has(control)) originalDisabled.set(control, control.disabled);
                control.disabled = enabled ? originalDisabled.get(control) : true;
            });

            var enterButton = document.getElementById('btnEnterDraftEdit');
            if (enterButton) {
                enterButton.hidden = ownsLock || terminal;
                enterButton.disabled = terminal || authenticationFailed;
            }
        }

        function show(message, type) {
            if (typeof options.showAlert === 'function') options.showAlert(message, type || 'warning');
        }

        function setViewer(message) {
            ownsLock = false;
            editSessionId = null;
            setEditable(false);
            if (typeof options.setStatus === 'function') options.setStatus('error', message);
        }

        function stopTimers() {
            if (renewTimer) global.clearInterval(renewTimer);
            if (stateTimer) global.clearInterval(stateTimer);
            renewTimer = null;
            stateTimer = null;
        }

        function expireAuthentication(message) {
            authenticationFailed = true;
            stopTimers();
            setViewer(message);
        }

        function handleTerminal(state) {
            if (terminal) return;
            terminal = true;
            ownsLock = false;
            stopTimers();
            setEditable(false);
            show(state === 'Completed'
                ? 'تم اعتماد هذه المسودة في جلسة أخرى. سيتم الرجوع إلى قائمة المسودات.'
                : 'تم حذف هذه المسودة في جلسة أخرى. سيتم الرجوع إلى قائمة المسودات.', 'warning');
            global.setTimeout(function () {
                global.location.assign(options.draftsUrl);
            }, 1400);
        }

        async function acquire() {
            if (terminal || authenticationFailed || saleId <= 0) return false;
            var result = await post(options.acquireUrl, { saleId: saleId, editSessionId: '00000000-0000-0000-0000-000000000000' });
            if (!result.success) {
                setViewer(result.message || 'تعذر الحصول على جلسة تعديل للمسودة.');
                show(result.message || 'المسودة متاحة للعرض فقط.', 'warning');
                return false;
            }

            editSessionId = result.editSessionId;
            revision = Number(result.draftRevision) || 0;
            ownsLock = true;
            lastSaveFailed = false;
            setEditable(true);
            if (typeof options.setStatus === 'function') options.setStatus('saved', 'تم بدء جلسة التعديل. المسودة محفوظة.');
            return true;
        }

        async function renew() {
            if (!ownsLock || !editSessionId || terminal) return;
            var result;
            try {
                result = await post(options.renewUrl, { saleId: saleId, editSessionId: editSessionId });
            } catch (error) {
                return;
            }
            if (!result.success && result.status === 409) {
                setViewer(result.message || 'انتهت جلسة التعديل.');
                show(result.message || 'انتهت جلسة التعديل وأصبحت الصفحة للعرض فقط.', 'danger');
            }
        }

        async function pollState() {
            if (terminal || saleId <= 0) return;
            var query = '?saleId=' + encodeURIComponent(saleId);
            if (editSessionId) query += '&editSessionId=' + encodeURIComponent(editSessionId);
            try {
                var result = await readJson(await global.fetch(options.stateUrl + query, {
                    cache: 'no-store',
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                }));
                if (!result.success) return;
                if (!result.exists || result.state === 'Deleted') {
                    handleTerminal('Deleted');
                    return;
                }
                if (result.state === 'Completed') {
                    handleTerminal('Completed');
                    return;
                }
                if (ownsLock && !result.ownsEditLock) {
                    setViewer('انتهت جلسة التعديل أو انتقلت إلى جلسة أخرى.');
                    show('لم تعد هذه الصفحة تملك صلاحية تعديل المسودة.', 'danger');
                }
            } catch (error) {
                // A transient viewer poll failure must not change write ownership.
            }
        }

        function attachWriteContract(data) {
            data.editSessionId = editSessionId;
            data.expectedDraftRevision = revision;
            return data;
        }

        function updateFromWriteResult(result) {
            if (result && result.success && Number.isFinite(Number(result.draftRevision))) {
                revision = Number(result.draftRevision);
                lastSaveFailed = false;
            }
        }

        function handleWriteResult(result) {
            updateFromWriteResult(result);
            if (!result || result.success) return;
            lastSaveFailed = true;
            if (result.code === 'draft_unavailable') {
                setViewer(result.message || 'المسودة لم تعد متاحة للتعديل.');
                pollState();
            } else if (result.code === 'stale_revision' || result.code === 'lock_expired' || result.code === 'session_mismatch' || result.code === 'lock_lost') {
                setViewer(result.message || 'لم تعد هذه الصفحة تملك صلاحية التعديل.');
            }
        }

        async function release(keepalive) {
            if (!ownsLock || !editSessionId) return true;
            var body = { saleId: saleId, editSessionId: editSessionId };
            try {
                var result = await post(options.releaseUrl, body, keepalive);
                if (result.success) {
                    ownsLock = false;
                    editSessionId = null;
                }
                return result.success;
            } catch (error) {
                return false;
            }
        }

        async function leaveTo(url) {
            if (!ownsLock) {
                global.location.assign(url);
                return;
            }

            setEditable(false);

            var saveResult = { success: true, skipped: true };
            var saveState = options.coordinator.getState();
            if (saveState.dirty || saveState.requestInFlight || lastSaveFailed) {
                saveResult = await options.coordinator.request(true);
                handleWriteResult(saveResult);
            }

            if (!saveResult.success) {
                var leaveAnyway = global.confirm('فشل حفظ أحدث التغييرات. هل تريد المغادرة مع فقدان هذه التغييرات؟');
                if (!leaveAnyway) {
                    setEditable(true);
                    return;
                }
            }

            await release(false);
            global.location.assign(url);
        }

        async function deleteDraft() {
            if (!ownsLock) {
                show('الحذف متاح فقط لجلسة التعديل الحالية.', 'danger');
                return false;
            }
            if (!global.confirm('هل تريد حذف هذه المسودة نهائياً؟')) return false;

            options.coordinator.clearScheduled();
            await options.coordinator.waitForIdle();
            var result = await post(options.deleteUrl, {
                saleId: saleId,
                editSessionId: editSessionId,
                expectedDraftRevision: revision
            });
            handleWriteResult(result);
            if (result.success) {
                terminal = true;
                ownsLock = false;
                global.location.assign(options.draftsUrl);
            } else {
                show(result.message || 'تعذر حذف المسودة.', 'danger');
            }
            return result.success;
        }

        function start() {
            setEditable(false);
            if (root && global.MutationObserver) {
                new global.MutationObserver(function () {
                    if (!editingEnabled) setEditable(false);
                }).observe(root, { childList: true, subtree: true });
            }
            var enterButton = document.getElementById('btnEnterDraftEdit');
            if (enterButton) enterButton.addEventListener('click', acquire);
            var deleteButton = document.getElementById('btnDeleteDraft');
            if (deleteButton) deleteButton.addEventListener('click', deleteDraft);

            document.addEventListener('click', function (event) {
                var link = event.target.closest('a[href]');
                if (!link || link.target === '_blank' || link.hasAttribute('download') || terminal) return;
                if (!ownsLock) return;
                var url = link.href;
                if (!url || url.indexOf('javascript:') === 0 || url.indexOf('#') === url.length - 1) return;
                event.preventDefault();
                leaveTo(url);
            });

            global.addEventListener('beforeunload', function (event) {
                if (!ownsLock && !authenticationFailed) return;
                var state = options.coordinator.getState();
                if (!state.dirty && !state.requestInFlight && !lastSaveFailed) return;
                event.preventDefault();
                event.returnValue = '';
            });

            global.addEventListener('pagehide', function () {
                if (!ownsLock || options.coordinator.getState().dirty || lastSaveFailed) return;
                release(true);
            });

            if (authenticationFailed) return;

            renewTimer = global.setInterval(renew, 30000);
            stateTimer = global.setInterval(pollState, 10000);
            pollState();
            if (options.editRequested) acquire();
        }

        return {
            attachWriteContract: attachWriteContract,
            canWrite: function () { return ownsLock && !terminal; },
            getRevision: function () { return revision; },
            handleAuthFailure: expireAuthentication,
            handleWriteResult: handleWriteResult,
            setEditingEnabled: function (enabled) { if (ownsLock) setEditable(enabled); },
            start: start
        };
    }

    global.YaqutDraftEditSession = { create: create };
})(window);

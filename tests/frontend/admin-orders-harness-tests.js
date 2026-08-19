(async function () {
    'use strict';

    const state = window.__ordersHarness;
    const failures = [];
    let passes = 0;

    function check(condition, name) {
        if (condition) passes += 1;
        else failures.push(name);
    }

    function flush() {
        return new Promise(function (resolve) { setTimeout(resolve, 0); });
    }

    async function settle() {
        await flush();
        await flush();
    }

    function response(status, contentType, data, parseError) {
        return {
            ok: status >= 200 && status < 300,
            status,
            headers: {
                get: function (name) {
                    return name.toLowerCase() === 'content-type' ? contentType : null;
                }
            },
            json: function () {
                return parseError ? Promise.reject(parseError) : Promise.resolve(data);
            }
        };
    }

    function queueJson(status, data) {
        state.fetchQueue.push(function () {
            return Promise.resolve(response(status, 'application/json; charset=utf-8', data));
        });
    }

    async function changeStatus(select, status) {
        const before = state.fetchCalls.length;
        select.value = status;
        select.dispatchEvent(new Event('change', { bubbles: true }));
        check(select.disabled && select.style.opacity === '0.5', `${status} owns its pending UI state`);
        await settle();
        return state.fetchCalls.length - before;
    }

    async function loadPageModuleAgain() {
        await new Promise(function (resolve, reject) {
            const script = document.createElement('script');
            script.src = '../../wwwroot/js/admin/orders/index.js';
            script.onload = resolve;
            script.onerror = reject;
            document.body.appendChild(script);
        });
    }

    try {
        check(state.popovers.size === 4, 'normal initialization owns all page popovers');
        check(state.popoverConstructions() === 4, 'each production-shaped popover is initialized once');

        await loadPageModuleAgain();
        check(state.popoverConstructions() === 4, 'duplicate page asset inclusion cannot reinitialize popovers');

        const location = state.popovers.get(document.getElementById('locationTrigger'));
        const locationContent = location.options.content();
        check(locationContent.textContent.includes('<img src=x onerror=alert(1)>'), 'HTML tags remain literal text');
        check(locationContent.textContent.includes('<svg onload=alert(2)>'), 'SVG event payload remains literal text');
        check(locationContent.textContent.includes('"\' 123'), 'quotes remain literal text');
        check(locationContent.textContent.includes('صنعاء & عدن'), 'Arabic and special characters survive dataset decoding');
        check(locationContent.textContent.includes('xxxxxxxxxxxxxxxxxxxxxxxx'), 'long customer text remains available');
        check(locationContent.querySelectorAll('img, svg, script').length === 0, 'customer text does not become active DOM');

        const payment = state.popovers.get(document.getElementById('paymentTrigger'));
        const paymentContent = payment.options.content();
        check(paymentContent.textContent.includes('<img src=x onerror=alert(1)>'), 'payment text remains literal');
        check(paymentContent.querySelectorAll('img').length === 0, 'payment text does not become active HTML');

        document.body.click();
        check(Array.from(state.popovers.values()).every(function (item) { return item.hideCalls === 0; }), 'missing optional open popover is tolerated');

        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        check(Array.from(state.popovers.values()).every(function (item) { return item.hideCalls === 1; }), 'Escape closes owned popovers');

        const select = document.getElementById('statusSelect');
        const badge = document.querySelector('[data-order-badge="42"]');
        queueJson(200, { success: true, message: 'ok' });
        check(await changeStatus(select, 'Delivered') === 1, 'one successful change issues one request');
        check(state.fetchCalls[0].url === '/Admin/Orders/UpdateStatus', 'DOM endpoint contract is used');
        check(state.fetchCalls[0].options.method === 'POST', 'POST method is preserved');
        check(state.fetchCalls[0].options.headers['Content-Type'] === 'application/x-www-form-urlencoded', 'form content type is preserved');
        check(state.fetchCalls[0].options.headers.Accept === 'application/json', 'JSON response contract is requested');
        check(state.fetchCalls[0].options.headers['X-Requested-With'] === 'XMLHttpRequest', 'AJAX header is preserved');
        check(state.fetchCalls[0].options.headers.RequestVerificationToken === 'test-token', 'antiforgery header is preserved');
        check(state.fetchCalls[0].options.body === 'id=42&status=Delivered', 'payload contract is preserved');
        check(!select.disabled && select.style.opacity === '1' && select.dataset.originalStatus === 'Delivered', 'success releases control and advances state');
        check(badge.textContent === 'تم التوصيل' && badge.classList.contains('yq-orders-badge--delivered'), 'success updates badge safely');

        queueJson(200, { success: false, message: '<img src=x onerror=alert(3)>' });
        check(await changeStatus(select, 'Shipped') === 1, 'success=false issues one request');
        check(select.value === 'Delivered' && !select.disabled, 'success=false reverts and releases control');
        const alertArea = document.getElementById('yqOrdersAlertArea');
        check(alertArea.textContent.includes('<img src=x onerror=alert(3)>') && alertArea.querySelectorAll('img').length === 0, 'server failure message is inert text');

        for (const statusCode of [400, 401, 403, 404, 409, 500]) {
            queueJson(statusCode, { success: false, message: `status ${statusCode}` });
            check(await changeStatus(select, 'Shipped') === 1, `${statusCode} issues one request`);
            check(select.value === 'Delivered' && !select.disabled && select.style.opacity === '1', `${statusCode} reverts and restores UI`);
            const expected = statusCode === 401
                ? 'انتهت جلسة تسجيل الدخول'
                : statusCode === 403
                    ? 'ليس لديك صلاحية'
                    : statusCode === 500
                        ? 'حدث خطأ في الخادم'
                        : `status ${statusCode}`;
            check(alertArea.textContent.includes(expected), `${statusCode} shows its controlled error category`);
        }

        state.fetchQueue.push(function () { return Promise.reject(new Error('network')); });
        check(await changeStatus(select, 'Shipped') === 1, 'network rejection issues one request');
        check(select.value === 'Delivered' && !select.disabled, 'network rejection reverts and releases control');
        check(alertArea.textContent.includes('تعذر الاتصال بالخادم'), 'network rejection has a controlled network message');

        state.fetchQueue.push(function () {
            return Promise.resolve(response(200, 'text/html; charset=utf-8', null));
        });
        check(await changeStatus(select, 'Shipped') === 1, 'unexpected HTML response issues one request');
        check(select.value === 'Delivered' && !select.disabled, 'unexpected HTML response reverts and releases control');
        check(alertArea.textContent.includes('استجابة غير متوقعة'), 'unexpected HTML has a controlled content-type message');

        state.fetchQueue.push(function () {
            return Promise.resolve(response(200, 'application/json', null, new SyntaxError('malformed')));
        });
        check(await changeStatus(select, 'Shipped') === 1, 'malformed JSON issues one request');
        check(select.value === 'Delivered' && !select.disabled, 'malformed JSON reverts and releases control');
        check(alertArea.textContent.includes('تعذر قراءة استجابة الخادم'), 'malformed JSON has a controlled parse message');

        let resolveSlow;
        state.fetchQueue.push(function () {
            return new Promise(function (resolve) { resolveSlow = resolve; });
        });
        const beforeSlow = state.fetchCalls.length;
        select.value = 'Shipped';
        select.dispatchEvent(new Event('change', { bubbles: true }));
        check(select.disabled && state.fetchCalls.length - beforeSlow === 1, 'slow response keeps one owned request pending');
        resolveSlow(response(200, 'application/json', { success: true }));
        await settle();
        check(!select.disabled && select.dataset.originalStatus === 'Shipped', 'slow success releases control for a later change');

        queueJson(200, { success: true });
        check(await changeStatus(select, 'Delivered') === 1, 'a new change remains possible after settlement');

        const receipt = state.popovers.get(document.getElementById('receiptTrigger')).options.content();
        const popoverHost = document.createElement('div');
        popoverHost.className = 'popover show';
        popoverHost.appendChild(receipt);
        document.body.appendChild(popoverHost);
        receipt.querySelector('.js-zoom-receipt').click();
        check(state.modalShows() === 1, 'visible delegated receipt action opens one modal');
        check(document.getElementById('modalOrderId').textContent === '42', 'receipt order ID is preserved');
        check(document.getElementById('modalReceiptImg').getAttribute('src') === '/Admin/Orders/Receipt/42', 'protected receipt URL is preserved');
        receipt.querySelector('.js-zoom-receipt').click();
        check(state.modalShows() === 2, 'repeated visible receipt action remains usable');
        popoverHost.classList.remove('show');
        receipt.querySelector('.js-zoom-receipt').click();
        check(state.modalShows() === 2, 'stale hidden popover action cannot reopen the modal');

        const emptyReceipt = state.popovers.get(document.getElementById('emptyReceiptTrigger')).options.content();
        check(emptyReceipt.classList.contains('yq-popover-empty') && !emptyReceipt.querySelector('.js-zoom-receipt'), 'missing receipt URL renders a non-actionable empty state');

        check(document.documentElement.scrollWidth <= window.innerWidth, 'page-owned styles do not create viewport overflow');
        check(state.consoleErrors.length === 0, 'console error channel remains clear');
        check(state.pageErrors.length === 0, 'page error channel remains clear');
        check(state.unhandledRejections.length === 0, 'unhandled rejection channel remains clear');
    } catch (error) {
        failures.push('unhandled harness error: ' + error.message);
    }

    const result = document.getElementById('harnessResult');
    result.textContent = failures.length === 0 ? `PASS ${passes}` : `FAIL ${failures.join(' | ')}`;
    result.dataset.result = failures.length === 0 ? 'pass' : 'fail';
    result.dataset.width = String(window.innerWidth);
})();

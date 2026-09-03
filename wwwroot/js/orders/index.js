(function (window, document) {
    'use strict';

    var root = document.querySelector('[data-yq-orders-root]');
    if (!root) return;

    var abortController = null;
    var latestRequestId = 0;
    var reviewDialogShown = false;

    function checkReviewPopup() {
        if (reviewDialogShown) return;
        var count = parseInt(root.getAttribute('data-yq-review-count') || '0', 10);
        if (count <= 0) return;
        reviewDialogShown = true;

        if (count === 1) {
            window.YaqutOperationDialog.confirm({
                title: "طلب يحتاج مراجعتك",
                message: "يوجد طلب يحتاج إلى مراجعتك قبل استكمال المعالجة.",
                confirmText: "مراجعة الطلب",
                cancelText: "إغلاق",
                confirmStyle: "primary"
            }).then(function (result) {
                if (result) {
                    var url = root.getAttribute('data-yq-review-url');
                    if (url) window.location.assign(url);
                }
            });
        } else {
            window.YaqutOperationDialog.confirm({
                title: "طلبات تحتاج مراجعتك",
                message: "لديك " + count + " طلبات تحتاج إلى مراجعتك لاتخاذ الإجراء المناسب.",
                confirmText: "عرض الطلبات",
                cancelText: "إغلاق",
                confirmStyle: "primary"
            }).then(function (result) {
                // Confirmation simply closes the dialog and stays on the Orders list.
            });
        }
    }

    function hydrateThumbnails() {
        root.querySelectorAll('.yq-order-card__thumb img').forEach(function (img) {
            if (img.complete && img.naturalWidth > 0) {
                img.classList.add('is-loaded');
                return;
            }
            if (img.complete && img.naturalWidth === 0) {
                var initialFallback = img.getAttribute('data-yaqut-fallback');
                if (initialFallback && img.getAttribute('src') !== initialFallback) img.setAttribute('src', initialFallback);
            }
            img.addEventListener('load', function () { img.classList.add('is-loaded'); }, { once: true });
            img.addEventListener('error', function () {
                var fallback = img.getAttribute('data-yaqut-fallback');
                if (fallback && img.getAttribute('src') !== fallback) img.setAttribute('src', fallback);
                else img.classList.add('is-loaded');
            }, { once: true });
        });
    }

    async function loadOrders(url, historyMode) {
        if (abortController) abortController.abort();
        abortController = new AbortController();
        var requestId = ++latestRequestId;
        root.setAttribute('aria-busy', 'true');

        try {
            var response = await window.fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                signal: abortController.signal
            });
            if (!response.ok) throw new Error('Orders request failed.');

            var html = await response.text();
            if (requestId !== latestRequestId) return;
            var parsed = new window.DOMParser().parseFromString(html, 'text/html');
            var nextRoot = parsed.querySelector('[data-yq-orders-root]');
            if (!nextRoot) throw new Error('Orders response is incomplete.');
            nextRoot.querySelectorAll('.yq-reveal').forEach(function (element) {
                element.classList.add('is-visible');
            });

            root.replaceChildren.apply(root, Array.prototype.map.call(nextRoot.childNodes, function (node) {
                return document.importNode(node, true);
            }));
            root.dataset.yqOrdersUrl = nextRoot.dataset.yqOrdersUrl || root.dataset.yqOrdersUrl;
            if (parsed.title) document.title = parsed.title;
            if (historyMode === 'push') window.history.pushState({ yqOrders: true }, '', url);
            else if (historyMode === 'replace') window.history.replaceState({ yqOrders: true }, '', url);
            hydrateThumbnails();
        } catch (error) {
            if (error.name !== 'AbortError' && requestId === latestRequestId) {
                window.location.assign(url);
            }
        } finally {
            if (requestId === latestRequestId) root.removeAttribute('aria-busy');
        }
    }

    function searchUrl(form) {
        var url = new URL(form.action || root.dataset.yqOrdersUrl || window.location.href, window.location.origin);
        var data = new FormData(form);
        data.forEach(function (value, key) {
            if (value) url.searchParams.set(key, value);
            else url.searchParams.delete(key);
        });
        url.searchParams.set('page', '1');
        return url.toString();
    }

    root.addEventListener('click', function (event) {
        var link = event.target.closest('[data-yq-order-filter], [data-yq-orders-pager] a[href], [data-yq-orders-reset]');
        if (!link || !root.contains(link)) return;
        event.preventDefault();
        if (link.getAttribute('aria-disabled') === 'true') return;
        loadOrders(link.href, 'push');
    });

    root.addEventListener('submit', function (event) {
        var form = event.target.closest('[data-yq-orders-search]');
        if (!form) return;
        event.preventDefault();
        loadOrders(searchUrl(form), 'push');
    });

    window.addEventListener('popstate', function () {
        loadOrders(window.location.href, null);
    });

    function init() {
        hydrateThumbnails();
        checkReviewPopup();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})(window, document);

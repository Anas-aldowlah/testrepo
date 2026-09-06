(function (window, document) {
    'use strict';

    var root = document.querySelector('[data-yq-orders-root]');
    if (!root) return;

    var abortController = null;
    var latestRequestId = 0;
    var latestRequestedUrl = new URL(window.location.href).toString();
    var latestHistoryMode = null;
    var navigationInFlight = false;
    var pageSizeMedia = window.matchMedia('(max-width: 767.98px)');

    function requiredPageSize() {
        return pageSizeMedia.matches ? 6 : 9;
    }

    function normalizeResponsiveUrl(candidate, requiredSize) {
        var url = new URL(candidate, window.location.origin);
        var originalUrl = url.toString();
        var rawPageSize = url.searchParams.get('pageSize');
        var hasValidPageSize = rawPageSize === '6' || rawPageSize === '9';
        var oldPageSize = hasValidPageSize ? Number(rawPageSize) : 9;
        var rawPage = url.searchParams.get('page');
        var oldPage = rawPage === null ? 1 : Number(rawPage);

        if (!Number.isSafeInteger(oldPage) || oldPage < 1 ||
            !Number.isSafeInteger((oldPage - 1) * oldPageSize)) {
            oldPage = 1;
        }

        var pageSizeChanged = oldPageSize !== requiredSize;
        if (pageSizeChanged) {
            var offset = (oldPage - 1) * oldPageSize;
            var newPage = Math.floor(offset / requiredSize) + 1;
            url.searchParams.set('page', String(newPage));
            url.searchParams.set('pageSize', String(requiredSize));
        } else if (rawPageSize !== null && !hasValidPageSize) {
            url.searchParams.set('pageSize', String(requiredSize));
        }

        return {
            url: url.toString(),
            pageSizeChanged: pageSizeChanged,
            urlChanged: url.toString() !== originalUrl
        };
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
        latestRequestedUrl = new URL(url, window.location.origin).toString();
        latestHistoryMode = historyMode;
        navigationInFlight = true;

        if (abortController) abortController.abort();
        abortController = new AbortController();
        var requestId = ++latestRequestId;
        root.setAttribute('aria-busy', 'true');

        try {
            var response = await window.fetch(latestRequestedUrl, {
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
            if (historyMode === 'push') window.history.pushState({ yqOrders: true }, '', latestRequestedUrl);
            else if (historyMode === 'replace') window.history.replaceState({ yqOrders: true }, '', latestRequestedUrl);
            hydrateThumbnails();
        } catch (error) {
            if (error.name !== 'AbortError' && requestId === latestRequestId) {
                window.location.assign(latestRequestedUrl);
            }
        } finally {
            if (requestId === latestRequestId) {
                navigationInFlight = false;
                root.removeAttribute('aria-busy');
            }
        }
    }

    function navigateOrders(candidate, historyMode) {
        var normalized = normalizeResponsiveUrl(candidate, requiredPageSize());
        loadOrders(normalized.url, historyMode);
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

    function filterUrl(link) {
        var url = new URL(link.href, window.location.origin);
        var searchInput = root.querySelector('#yqOrdersSearchInput');
        if (searchInput) {
            var searchVal = searchInput.value.trim();
            if (searchVal) {
                url.searchParams.set('search', searchVal);
            } else {
                url.searchParams.delete('search');
            }
        }
        url.searchParams.set('page', '1');
        return url.toString();
    }

    root.addEventListener('click', function (event) {
        var filterLink = event.target.closest('[data-yq-order-filter]');
        if (filterLink && root.contains(filterLink)) {
            event.preventDefault();
            if (filterLink.getAttribute('aria-disabled') === 'true') return;
            navigateOrders(filterUrl(filterLink), 'push');
            return;
        }

        var link = event.target.closest('[data-yq-orders-pager] a[href], [data-yq-orders-reset]');
        if (!link || !root.contains(link)) return;
        event.preventDefault();
        if (link.getAttribute('aria-disabled') === 'true') return;
        navigateOrders(link.href, 'push');
    });

    root.addEventListener('submit', function (event) {
        var form = event.target.closest('[data-yq-orders-search]');
        if (!form) return;
        event.preventDefault();
        navigateOrders(searchUrl(form), 'push');
    });

    window.addEventListener('popstate', function () {
        var normalized = normalizeResponsiveUrl(window.location.href, requiredPageSize());
        loadOrders(normalized.url, normalized.urlChanged ? 'replace' : null);
    });

    function reconcileBreakpoint() {
        var candidate = navigationInFlight ? latestRequestedUrl : window.location.href;
        var normalized = normalizeResponsiveUrl(candidate, requiredPageSize());
        if (!normalized.pageSizeChanged) return;

        var historyMode = navigationInFlight ? latestHistoryMode : 'replace';
        loadOrders(normalized.url, historyMode);
    }

    function reconcileInitialPageSize() {
        var normalized = normalizeResponsiveUrl(window.location.href, requiredPageSize());
        if (normalized.pageSizeChanged) {
            loadOrders(normalized.url, 'replace');
        } else if (normalized.urlChanged) {
            latestRequestedUrl = normalized.url;
            window.history.replaceState({ yqOrders: true }, '', normalized.url);
        }
    }

    function init() {
        hydrateThumbnails();
        pageSizeMedia.addEventListener('change', reconcileBreakpoint);
        reconcileInitialPageSize();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})(window, document);

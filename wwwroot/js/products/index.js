/**
 * ياقوت — Products / Index page interactions
 * Owns catalog URL/form state and server-page loading only.
 */
(function (window, document) {
    'use strict';

    var filterForm = document.getElementById('yqCatalogFilters');
    var categoryInput = document.getElementById('yqCategoryId');
    var loadMoreBtn = document.getElementById('yqLoadMoreBtn');
    var grid = document.getElementById('yaqutProductsGrid');
    var status = document.getElementById('yqCatalogStatus');
    var isLoading = false;

    /* ── Recent Searches (client-side, storefront-wide within this page) ── */
    (function () {
        var STORAGE_KEY = 'yaqut-recent-searches';
        var MAX_RECENT = 5;
        var root = document.querySelector('[data-yq-search-term]');
        if (!root) return;
        var currentTerm = root.getAttribute('data-yq-search-term');

        function getRecent() {
            try {
                var raw = localStorage.getItem(STORAGE_KEY);
                var parsed = raw ? JSON.parse(raw) : [];
                if (!Array.isArray(parsed)) return [];
                return parsed.filter(function (term) {
                    return typeof term === 'string' && term.length > 0;
                }).slice(0, MAX_RECENT);
            } catch (error) {
                return [];
            }
        }

        function saveRecent(list) {
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
            } catch (error) { /* Storage unavailable. */ }
        }

        if (currentTerm) {
            var tracked = getRecent().filter(function (term) {
                return term.toLowerCase() !== currentTerm.toLowerCase();
            });
            tracked.unshift(currentTerm);
            saveRecent(tracked.slice(0, MAX_RECENT));
        }

        var wrapper = document.getElementById('yqRecentSearches');
        var list = document.getElementById('yqRecentSearchesList');
        if (!wrapper || !list) return;
        var recent = getRecent().filter(function (term) {
            return term.toLowerCase() !== (currentTerm || '').toLowerCase();
        });
        if (!recent.length) return;

        recent.forEach(function (term) {
            var chip = document.createElement('a');
            chip.className = 'yq-recent-search-chip';
            chip.href = '/Products?search=' + encodeURIComponent(term);
            var icon = document.createElement('i');
            icon.className = 'bi bi-clock-history';
            icon.setAttribute('aria-hidden', 'true');
            chip.append(icon, document.createTextNode(term));
            list.appendChild(chip);
        });
        wrapper.hidden = false;
    })();

    function submitFilters() {
        if (filterForm) filterForm.requestSubmit();
    }

    document.querySelectorAll('[data-yq-category]').forEach(function (button) {
        button.addEventListener('click', function () {
            if (!categoryInput) return;
            categoryInput.value = button.getAttribute('data-yq-category') || '';
            submitFilters();
        });
    });

    document.querySelectorAll('[data-yq-auto-submit]').forEach(function (control) {
        control.addEventListener('change', submitFilters);
    });

    if (loadMoreBtn && grid) {
        loadMoreBtn.addEventListener('click', async function () {
            if (isLoading) return;
            isLoading = true;
            loadMoreBtn.disabled = true;
            if (status) status.textContent = 'جارٍ تحميل المزيد من المنتجات';

            try {
                var currentPage = parseInt(loadMoreBtn.getAttribute('data-current-page'), 10) || 1;
                var nextUrl = new URL(window.location.href);
                nextUrl.searchParams.set('page', String(currentPage + 1));
                var response = await window.fetch(nextUrl.toString(), {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (!response.ok) throw new Error('Catalog page request failed.');

                var html = await response.text();
                var parsed = new window.DOMParser().parseFromString(html, 'text/html');
                var nextGrid = parsed.getElementById('yaqutProductsGrid');
                if (!nextGrid) throw new Error('Catalog page response is incomplete.');

                var existingIds = new Set(Array.prototype.map.call(
                    grid.querySelectorAll('[data-product-id]'),
                    function (item) { return item.getAttribute('data-product-id'); }
                ));
                Array.prototype.forEach.call(nextGrid.children, function (item) {
                    var productId = item.getAttribute('data-product-id');
                    if (productId && !existingIds.has(productId)) {
                        existingIds.add(productId);
                        grid.appendChild(document.importNode(item, true));
                    }
                });

                var nextButton = parsed.getElementById('yqLoadMoreBtn');
                loadMoreBtn.setAttribute('data-current-page', String(currentPage + 1));
                if (!nextButton || nextButton.hidden) {
                    loadMoreBtn.hidden = true;
                } else {
                    var remaining = nextButton.querySelector('[data-yq-remaining]');
                    var currentRemaining = loadMoreBtn.querySelector('[data-yq-remaining]');
                    if (remaining && currentRemaining) currentRemaining.textContent = remaining.textContent;
                }
                if (status) status.textContent = 'تم تحميل المزيد من المنتجات';
            } catch (error) {
                if (status) status.textContent = 'تعذر تحميل المزيد من المنتجات. حاول مرة أخرى.';
            } finally {
                isLoading = false;
                loadMoreBtn.disabled = false;
            }
        });
    }
})(window, document);

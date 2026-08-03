/**
 * ياقوت — Products / Index page interactions
 * (Sort + client-side pagination. Filtering/search stays in yaqut-main.js)
 */
(function (window, document) {
    'use strict';

    var PAGE_SIZE = 12;

    var grid = document.getElementById('yaqutProductsGrid');
    if (!grid) return;

    var loadMoreBtn = document.getElementById('yqLoadMoreBtn');
    var sortSelect = document.getElementById('yqSortSelect');
    var filtersRoot = document.getElementById('yaqutFilters');
    var filtersClose = document.getElementById('yqFiltersClose');
    var searchInput = document.getElementById('yaqutSearchInput');

    var paginationActive = true;
    var visibleCount = PAGE_SIZE;

    function getItems() {
        return Array.prototype.slice.call(grid.querySelectorAll('.yq-col-item'));
    }

    function updateLoadMoreVisibility(remaining) {
        if (!loadMoreBtn) return;
        var show = paginationActive && remaining > 0;
        loadMoreBtn.style.display = show ? '' : 'none';
        if (show) {
            var counter = loadMoreBtn.querySelector('[data-yq-remaining]');
            if (counter) counter.textContent = remaining;
        }
    }
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
                return raw ? JSON.parse(raw) : [];
            } catch (e) {
                return [];
            }
        }

        function saveRecent(list) {
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
            } catch (e) { /* storage unavailable, ignore silently */ }
        }

        function trackSearch(term) {
            if (!term) return;
            var list = getRecent().filter(function (t) {
                return t.toLowerCase() !== term.toLowerCase();
            });
            list.unshift(term);
            saveRecent(list.slice(0, MAX_RECENT));
        }

        function renderRecent() {
            var wrapper = document.getElementById('yqRecentSearches');
            var list = document.getElementById('yqRecentSearchesList');
            if (!wrapper || !list) return;

            var recent = getRecent().filter(function (t) {
                return t.toLowerCase() !== (currentTerm || '').toLowerCase();
            });
            if (!recent.length) return;

            list.innerHTML = '';
            recent.forEach(function (term) {
                var chip = document.createElement('a');
                chip.className = 'yq-recent-search-chip';
                chip.href = '/Products?search=' + encodeURIComponent(term);
                chip.innerHTML = '<i class="bi bi-clock-history"></i>' + term;
                list.appendChild(chip);
            });

            wrapper.hidden = false;
        }

        if (currentTerm) trackSearch(currentTerm);
        renderRecent();
    })();
    function applyPagination() {
        var items = getItems();

        if (!paginationActive) {
            items.forEach(function (item) { item.classList.remove('yq-page-hidden'); });
            updateLoadMoreVisibility(0);
            return;
        }

        items.forEach(function (item, idx) {
            item.classList.toggle('yq-page-hidden', idx >= visibleCount);
        });
        updateLoadMoreVisibility(items.length - visibleCount);
    }

    function disablePagination() {
        if (!paginationActive) return;
        paginationActive = false;
        applyPagination();
    }

    function applySort(value) {
        var items = getItems();
        var compare;

        if (value === 'price-asc') {
            compare = function (a, b) { return parseFloat(a.dataset.price) - parseFloat(b.dataset.price); };
        } else if (value === 'price-desc') {
            compare = function (a, b) { return parseFloat(b.dataset.price) - parseFloat(a.dataset.price); };
        } else if (value === 'name-asc') {
            compare = function (a, b) { return a.dataset.name.localeCompare(b.dataset.name, 'ar'); };
        } else {
            compare = function (a, b) { return parseFloat(b.dataset.created) - parseFloat(a.dataset.created); };
        }

        items.sort(compare).forEach(function (item) { grid.appendChild(item); });

        visibleCount = PAGE_SIZE;
        applyPagination();
    }

    if (loadMoreBtn) {
        loadMoreBtn.addEventListener('click', function () {
            visibleCount += PAGE_SIZE;
            applyPagination();
        });
    }

    if (sortSelect) {
        sortSelect.addEventListener('change', function () {
            applySort(sortSelect.value);
        });
    }

    /* ── Brand filter (client-side) ── */
    function applyBrandFilter() {
        var brandCheckboxes = filtersRoot
            ? filtersRoot.querySelectorAll('input[name="brand"]:checked')
            : [];
        var selectedBrands = Array.prototype.slice.call(brandCheckboxes).map(function (cb) {
            return cb.value.toLowerCase();
        });

        var items = getItems();
        var visibleAfterFilter = 0;

        items.forEach(function (item) {
            if (selectedBrands.length === 0) {
                item.style.display = '';
                visibleAfterFilter++;
            } else {
                var itemBrand = (item.dataset.brand || '').toLowerCase();
                var matches = selectedBrands.indexOf(itemBrand) !== -1;
                item.style.display = matches ? '' : 'none';
                if (matches) visibleAfterFilter++;
            }
        });

        // Update results count
        var countEl = document.getElementById('yaqutResultsCount');
        if (countEl) {
            countEl.textContent = visibleAfterFilter + ' عطر';
        }
    }

    if (filtersRoot) {
        filtersRoot.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
            cb.addEventListener('change', function () {
                disablePagination();
                if (cb.name === 'brand') {
                    applyBrandFilter();
                }
            });
        });
    }

    if (searchInput) {
        searchInput.addEventListener('input', disablePagination);
    }

    if (filtersClose && filtersRoot) {
        filtersClose.addEventListener('click', function () {
            filtersRoot.classList.remove('is-open');
        });
    }

    applyPagination();
})(window, document);
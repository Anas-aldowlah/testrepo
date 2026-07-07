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

    if (filtersRoot) {
        filtersRoot.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
            cb.addEventListener('change', disablePagination);
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
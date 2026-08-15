/**
 * ياقوت — Products / Index page interactions
 * Single owner for filtering, sorting, result count, empty state, and pagination.
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

    var visibleCount = PAGE_SIZE;
    var filterState = {
        search: '',
        brands: [],
        families: [],
        sizes: [],
        concentrations: [],
        sort: sortSelect ? sortSelect.value : 'newest'
    };

    function getItems() {
        return Array.prototype.slice.call(grid.querySelectorAll('.yq-col-item'));
    }

    function updateLoadMoreVisibility(remaining) {
        if (!loadMoreBtn) return;
        var show = remaining > 0;
        loadMoreBtn.style.display = show ? '' : 'none';
        var counter = loadMoreBtn.querySelector('[data-yq-remaining]');
        if (counter) counter.textContent = Math.max(0, remaining);
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
                var parsed = raw ? JSON.parse(raw) : [];
                if (!Array.isArray(parsed)) return [];
                return parsed.filter(function (term) {
                    return typeof term === 'string' && term.length > 0;
                }).slice(0, MAX_RECENT);
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

            list.replaceChildren();
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
        }

        if (currentTerm) trackSearch(currentTerm);
        renderRecent();
    })();
    function getChecked(name) {
        if (!filtersRoot) return [];
        return Array.prototype.slice.call(filtersRoot.querySelectorAll('input[name="' + name + '"]:checked'))
            .map(function (checkbox) { return checkbox.value.toLowerCase(); });
    }

    function readFilterState() {
        filterState.search = (searchInput ? searchInput.value : '').toLowerCase().trim();
        filterState.brands = getChecked('brand');
        filterState.families = getChecked('family');
        filterState.sizes = getChecked('size');
        filterState.concentrations = getChecked('concentration');
        filterState.sort = sortSelect ? sortSelect.value : 'newest';
    }

    function getCard(item) {
        return item.querySelector('[data-yaqut-product]');
    }

    function includesValue(selected, value) {
        return selected.length === 0 || selected.indexOf(value) !== -1;
    }

    function matchesFilters(item) {
        var card = getCard(item);
        if (!card) return false;

        var name = (card.getAttribute('data-name') || '').toLowerCase();
        var brand = (item.getAttribute('data-brand') || '').toLowerCase();
        var family = (card.getAttribute('data-family') || '').toLowerCase();
        var size = (card.getAttribute('data-size') || 'all').toLowerCase();
        var concentration = (card.getAttribute('data-concentration') || 'all').toLowerCase();

        return (!filterState.search || name.indexOf(filterState.search) !== -1)
            && includesValue(filterState.brands, brand)
            && includesValue(filterState.families, family)
            && includesValue(filterState.sizes, size)
            && includesValue(filterState.concentrations, concentration);
    }

    function getComparer(value) {
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

        return compare;
    }

    function applyProductState(resetPage) {
        if (resetPage) visibleCount = PAGE_SIZE;
        readFilterState();

        var items = getItems().sort(getComparer(filterState.sort));
        items.forEach(function (item) { grid.appendChild(item); });

        var filteredItems = items.filter(matchesFilters);
        var filteredSet = new Set(filteredItems);
        filteredItems.forEach(function (item, index) {
            item.style.display = '';
            item.classList.toggle('yq-page-hidden', index >= visibleCount);
        });
        items.forEach(function (item) {
            if (!filteredSet.has(item)) {
                item.style.display = 'none';
                item.classList.remove('yq-page-hidden');
            }
        });

        var countEl = document.getElementById('yaqutResultsCount');
        if (countEl) countEl.textContent = filteredItems.length + ' عطر';

        var noResults = document.getElementById('yqClientNoResults');
        if (noResults) noResults.hidden = filteredItems.length !== 0;

        updateLoadMoreVisibility(Math.max(0, filteredItems.length - visibleCount));
    }

    if (loadMoreBtn) {
        loadMoreBtn.addEventListener('click', function () {
            visibleCount += PAGE_SIZE;
            applyProductState(false);
        });
    }

    if (sortSelect) {
        sortSelect.addEventListener('change', function () {
            applyProductState(true);
        });
    }

    if (filtersRoot) {
        filtersRoot.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
            cb.addEventListener('change', function () {
                applyProductState(true);
            });
        });
    }

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            applyProductState(true);
        });
    }

    if (filtersClose && filtersRoot) {
        filtersClose.addEventListener('click', function () {
            filtersRoot.classList.remove('is-open');
        });
    }

    applyProductState(true);
})(window, document);

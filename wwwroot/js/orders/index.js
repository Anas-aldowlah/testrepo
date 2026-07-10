(function () {
    'use strict';

    function init() {
        var root = document.querySelector('[data-yq-orders-root]');
        if (!root) return;

        var filterButtons = Array.prototype.slice.call(root.querySelectorAll('[data-yq-order-filter]'));
        var searchInput = document.getElementById('yqOrdersSearchInput');
        var grid = document.getElementById('yqOrdersGrid');
        var noResults = document.getElementById('yqOrdersNoResults');
        var resetBtn = document.getElementById('yqOrdersResetBtn');
        var cards = grid ? Array.prototype.slice.call(grid.querySelectorAll('[data-yq-order-card]')) : [];

        var activeStatus = 'all';

        function applyFilters() {
            var query = (searchInput && searchInput.value.trim().toLowerCase()) || '';
            var visibleCount = 0;

            cards.forEach(function (card) {
                var matchesStatus = activeStatus === 'all' || card.getAttribute('data-status') === activeStatus;
                var matchesSearch = !query || (card.getAttribute('data-search') || '').indexOf(query) !== -1;
                var isVisible = matchesStatus && matchesSearch;

                card.hidden = !isVisible;
                if (isVisible) visibleCount++;
            });

            if (noResults) {
                noResults.hidden = visibleCount !== 0;
            }
        }

        filterButtons.forEach(function (btn) {
            btn.addEventListener('click', function () {
                filterButtons.forEach(function (b) {
                    b.classList.remove('is-active');
                    b.setAttribute('aria-selected', 'false');
                });
                btn.classList.add('is-active');
                btn.setAttribute('aria-selected', 'true');
                activeStatus = btn.getAttribute('data-yq-order-filter');
                applyFilters();
            });
        });

        if (searchInput) {
            var debounceTimer;
            searchInput.addEventListener('input', function () {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(applyFilters, 150);
            });
        }

        if (resetBtn) {
            resetBtn.addEventListener('click', function () {
                activeStatus = 'all';
                if (searchInput) searchInput.value = '';
                filterButtons.forEach(function (b) {
                    var isAll = b.getAttribute('data-yq-order-filter') === 'all';
                    b.classList.toggle('is-active', isAll);
                    b.setAttribute('aria-selected', isAll ? 'true' : 'false');
                });
                applyFilters();
            });
        }

        // Thumbnail loading state: fade in once loaded, keep fallback behavior intact
        var thumbImages = root.querySelectorAll('.yq-order-card__thumb img');
        thumbImages.forEach(function (img) {
            if (img.complete && img.naturalWidth > 0) {
                img.classList.add('is-loaded');
                return;
            }
            img.addEventListener('load', function () {
                img.classList.add('is-loaded');
            });
            img.addEventListener('error', function () {
                img.classList.add('is-loaded');
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
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
    var abortController = null;
    var currentRequestId = 0;

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

    async function submitFilters(e, isPopState, popUrl) {
        if (e) e.preventDefault();
        if (!filterForm) return;

        if (typeof window.fetch !== 'function' || !window.history.pushState) {
            if (!isPopState) filterForm.submit();
            return;
        }

        var nextUrl;
        if (isPopState && popUrl) {
            nextUrl = new URL(popUrl);
        } else {
            nextUrl = new URL(filterForm.action || window.location.href);
            var fd = new FormData(filterForm);
            var params = new URLSearchParams();
            fd.forEach(function (val, key) {
                if (val !== '') params.append(key, val);
            });
            nextUrl.search = params.toString();
        }

        var nextUrlString = nextUrl.toString();

        if (!isPopState) {
            if (window.location.href !== nextUrlString) {
                window.history.pushState({ path: nextUrlString }, '', nextUrlString);
            }
        }

        if (abortController) {
            abortController.abort();
        }
        abortController = new AbortController();

        var requestId = ++currentRequestId;

        if (status) status.textContent = 'جارٍ تحديث المنتجات';
        isLoading = true;

        try {
            var response = await window.fetch(nextUrlString, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                signal: abortController.signal
            });

            if (!response.ok) throw new Error('Filter request failed.');

            var html = await response.text();

            if (requestId !== currentRequestId) return;

            var parsed = new window.DOMParser().parseFromString(html, 'text/html');

            var newShop = parsed.querySelector('.yq-shop');
            var oldShop = document.querySelector('.yq-shop');

            if (newShop && oldShop) {
                // Regions that are direct children of .yq-shop, in canonical order.
                var directRegions = [
                    '.yq-shop-header',
                    '.yq-shop-pills',
                    '.yq-shop-toolbar'
                ];

                directRegions.forEach(function (selector, index) {
                    var oldEl = oldShop.querySelector(selector);
                    var newEl = newShop.querySelector(selector);
                    if (newEl && newEl.classList.contains('yq-reveal')) {
                        // Scroll reveal observes only the initial document nodes. Replaced
                        // server-rendered regions must remain visible after async updates.
                        newEl.classList.add('is-visible');
                    }
                    if (selector === '.yq-shop-toolbar' && oldEl && newEl) {
                        var oldFilterToggle = oldEl.querySelector('#yaqutFilterToggle');
                        var newFilterToggle = newEl.querySelector('#yaqutFilterToggle');
                        if (oldFilterToggle && newFilterToggle) {
                            // The shared drawer initializer binds this control once.
                            // Preserve the live node when synchronizing its toolbar.
                            newFilterToggle.parentNode.replaceChild(oldFilterToggle, newFilterToggle);
                        }
                    }
                    if (oldEl && newEl) {
                        oldEl.parentNode.replaceChild(newEl, oldEl);
                    } else if (newEl && !oldEl) {
                        // Region was absent from old DOM but present in SSR response.
                        // Insert at canonical position by finding the nearest preceding
                        // sibling from the direct regions list that exists in the DOM.
                        var inserted = false;
                        for (var i = index - 1; i >= 0; i--) {
                            var precedingSibling = oldShop.querySelector(directRegions[i]);
                            if (precedingSibling) {
                                precedingSibling.parentNode.insertBefore(newEl, precedingSibling.nextSibling);
                                inserted = true;
                                break;
                            }
                        }
                        if (!inserted) {
                            oldShop.insertBefore(newEl, oldShop.firstChild);
                        }
                    } else if (oldEl && !newEl) {
                        oldEl.parentNode.removeChild(oldEl);
                    }
                });

                // .yq-shop-main lives inside .yq-shop-layout, not directly under .yq-shop.
                // Replace it within its actual parent to preserve the layout structure.
                var oldMain = oldShop.querySelector('.yq-shop-main');
                var newMain = newShop.querySelector('.yq-shop-main');
                if (oldMain && newMain) {
                    oldMain.parentNode.replaceChild(newMain, oldMain);
                } else if (newMain && !oldMain) {
                    var layout = oldShop.querySelector('.yq-shop-layout');
                    if (layout) {
                        layout.appendChild(newMain);
                    }
                } else if (oldMain && !newMain) {
                    oldMain.parentNode.removeChild(oldMain);
                }

                if (isPopState) {
                    var newForm = parsed.getElementById('yqCatalogFilters');
                    if (newForm && filterForm) {
                        filterForm.innerHTML = newForm.innerHTML;
                    }
                }

                grid = document.getElementById('yaqutProductsGrid');
                status = document.getElementById('yqCatalogStatus');
                loadMoreBtn = document.getElementById('yqLoadMoreBtn');

                if (status) status.textContent = 'تم تحديث المنتجات';
                if (parsed.title) document.title = parsed.title;
            }
        } catch (error) {
            if (requestId !== currentRequestId) return;
            if (error.name !== 'AbortError') {
                if (status) status.textContent = 'تعذر تحديث المنتجات. حاول مرة أخرى.';
                window.location.href = nextUrlString;
            }
        } finally {
            if (requestId === currentRequestId) {
                isLoading = false;
            }
        }
    }

    function triggerSubmit() {
        submitFilters();
    }

    // Event delegation for category pills and auto-submit controls
    document.addEventListener('click', function(e) {
        var categoryBtn = e.target.closest('[data-yq-category]');
        if (categoryBtn) {
            categoryInput = document.getElementById('yqCategoryId');
            if (categoryInput) {
                categoryInput.value = categoryBtn.getAttribute('data-yq-category') || '';
                triggerSubmit();
            }
        }

        var clearBtn = e.target.closest('.yq-shop-clear-link, .yq-search-banner__clear');
        if (clearBtn && clearBtn.tagName === 'A') {
            e.preventDefault();
            window.history.pushState({ path: clearBtn.href }, '', clearBtn.href);
            submitFilters(null, true, clearBtn.href);
        }

        // Load more logic
        if (e.target.closest('#yqLoadMoreBtn')) {
            var btn = document.getElementById('yqLoadMoreBtn');
            if (!btn || isLoading || !grid) return;
            isLoading = true;
            btn.disabled = true;
            if (status) status.textContent = 'جارٍ تحميل المزيد من المنتجات';

            if (abortController) {
                abortController.abort();
            }
            abortController = new AbortController();

            var requestId = ++currentRequestId;

            (async function() {
                try {
                    var currentPage = parseInt(btn.getAttribute('data-current-page'), 10) || 1;
                    var nextUrl = new URL(window.location.href);
                    nextUrl.searchParams.set('page', String(currentPage + 1));
                    var response = await window.fetch(nextUrl.toString(), {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' },
                        signal: abortController.signal
                    });
                    if (!response.ok) throw new Error('Catalog page request failed.');

                    var html = await response.text();

                    if (requestId !== currentRequestId) return;

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
                    btn.setAttribute('data-current-page', String(currentPage + 1));
                    if (!nextButton || nextButton.hidden) {
                        btn.hidden = true;
                    } else {
                        var remaining = nextButton.querySelector('[data-yq-remaining]');
                        var currentRemaining = btn.querySelector('[data-yq-remaining]');
                        if (remaining && currentRemaining) currentRemaining.textContent = remaining.textContent;
                    }
                    if (status) status.textContent = 'تم تحميل المزيد من المنتجات';
                } catch (error) {
                    if (requestId !== currentRequestId) return;
                    if (error.name !== 'AbortError') {
                        if (status) status.textContent = 'تعذر تحميل المزيد من المنتجات. حاول مرة أخرى.';
                    }
                } finally {
                    if (requestId === currentRequestId) {
                        isLoading = false;
                        btn.disabled = false;
                    }
                }
            })();
        }
    });

    document.addEventListener('change', function(e) {
        if (e.target.closest('[data-yq-auto-submit]')) {
            triggerSubmit();
        }
    });

    if (filterForm) {
        filterForm.addEventListener('submit', function (e) {
            submitFilters(e);
        });
    }

    window.addEventListener('popstate', function (e) {
        submitFilters(null, true, window.location.href);
    });

})(window, document);

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
    var suppressNextPopState = false;

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

    var lastCommittedFormData = null;
    function syncRetailSizeDependency() {
        if (!filterForm) return;
        var sizeGroup = document.getElementById('yqRetailSizeGroup');
        if (!sizeGroup) return;
        var selectedRetail = filterForm.querySelector('input[name="retail"]:checked');
        var retailIsUnavailable = selectedRetail && selectedRetail.value === 'no';
        sizeGroup.style.display = retailIsUnavailable ? 'none' : 'block';
        if (retailIsUnavailable) {
            sizeGroup.querySelectorAll('input[type="checkbox"]').forEach(function (checkbox) {
                checkbox.checked = false;
            });
        }
    }
    function saveCommittedState() {
        if (filterForm) {
            lastCommittedFormData = new FormData(filterForm);
        }
    }
    function restoreCommittedState() {
        if (!filterForm || !lastCommittedFormData) return;
        var checkboxes = filterForm.querySelectorAll('input[type="checkbox"], input[type="radio"]');
        checkboxes.forEach(function(el) { el.checked = false; });
        var textInputs = filterForm.querySelectorAll('input[type="number"], input[type="text"], select');
        textInputs.forEach(function(el) { el.value = ''; });

        for (var pair of lastCommittedFormData.entries()) {
            var el = filterForm.querySelector('[name="' + pair[0] + '"][value="' + pair[1] + '"]');
            if (el && (el.type === 'checkbox' || el.type === 'radio')) el.checked = true;
            else {
                el = filterForm.querySelector('[name="' + pair[0] + '"]');
                if (el) el.value = pair[1];
            }
        }
        syncRetailSizeDependency();
    }

    if (filterForm) {
        saveCommittedState();
        var filtersPanel = document.getElementById('yaqutFilters');
        if (filtersPanel) {
            var observer = new MutationObserver(function(mutations) {
                var closedFromOpen = mutations.some(function (mutation) {
                    return mutation.attributeName === 'class'
                        && (' ' + (mutation.oldValue || '') + ' ').indexOf(' is-open ') !== -1;
                });
                if (!closedFromOpen || filtersPanel.classList.contains('is-open')) return;
                restoreCommittedState();
                if (window.history.state && window.history.state.yqPanel) {
                    suppressNextPopState = true;
                    window.history.back();
                }
            });
            observer.observe(filtersPanel, { attributes: true, attributeOldValue: true, attributeFilter: ['class'] });
        }
    }

    async function submitFilters(e, isPopState, popUrl) {
        if (e) e.preventDefault();
        if (!filterForm) return;

        var filters = document.getElementById('yaqutFilters');
        var isMobileDrawerApply = window.innerWidth < 992
            && !isPopState
            && !!e
            && filters
            && filters.classList.contains('is-open');

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
                if (key !== 'mobileCategory' && val !== '') params.append(key, val);
            });
            nextUrl.search = params.toString();
        }

        var nextUrlString = nextUrl.toString();

        if (abortController) {
            abortController.abort();
        }
        abortController = new AbortController();

        var requestId = ++currentRequestId;

                if (status) {
            status.textContent = 'جارٍ تحديث المنتجات...';
            status.classList.remove('visually-hidden');
        }
        if (grid) {
            grid.setAttribute('aria-busy', 'true');
            grid.style.opacity = '0.6';
            grid.style.transition = 'opacity 0.15s ease-in-out';
        }
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

            if (!newShop || !oldShop) {
                throw new Error('Filter response is incomplete.');
            }

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
                            oldFilterToggle.replaceChildren.apply(
                                oldFilterToggle,
                                Array.prototype.map.call(newFilterToggle.childNodes, function (node) {
                                    return document.importNode(node, true);
                                }));
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

                var newForm = parsed.getElementById('yqCatalogFilters');
                if (newForm && filterForm) {
                    filterForm.innerHTML = newForm.innerHTML;
                    syncRetailSizeDependency();
                }

                grid = document.getElementById('yaqutProductsGrid');
                status = document.getElementById('yqCatalogStatus');
                loadMoreBtn = document.getElementById('yqLoadMoreBtn');

                if (parsed.title) document.title = parsed.title;
            }

            if (!isPopState && window.location.href !== nextUrlString) {
                if (isMobileDrawerApply && window.history.state && window.history.state.yqPanel) {
                    window.history.replaceState({ path: nextUrlString }, '', nextUrlString);
                } else {
                    window.history.pushState({ path: nextUrlString }, '', nextUrlString);
                }
            }

            saveCommittedState();

            if (isMobileDrawerApply && filters) {
                filters.classList.remove('is-open');
                document.documentElement.removeAttribute('data-yq-drawer-open');
                var filterToggle = document.getElementById('yaqutFilterToggle');
                if (filterToggle) filterToggle.focus();
            }
        } catch (error) {
            if (requestId !== currentRequestId) return;
            if (error.name !== 'AbortError') {
                if (!window.catalogHarness) {
                    window.location.assign(nextUrlString);
                }
            }
        } finally {
            if (requestId === currentRequestId) {
                isLoading = false;
                if (status) {
                    status.classList.add('visually-hidden');
                }
                if (grid) {
                    grid.removeAttribute('aria-busy');
                    grid.style.opacity = '';
                }
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

        var removeChip = e.target.closest('.yq-chip-remove');
        if (removeChip && filterForm) {
            var name = removeChip.getAttribute('data-remove-name');
            var val = removeChip.getAttribute('data-remove-val');

            if (name === 'price') {
                var minP = filterForm.querySelector('[name="minPrice"]');
                var maxP = filterForm.querySelector('[name="maxPrice"]');
                if (minP) minP.value = '';
                if (maxP) maxP.value = '';
            } else if (name === 'retail') {
                var radio = filterForm.querySelector('input[type="radio"][name="' + name + '"][value="' + val + '"]');
                if (radio) radio.checked = true;
            } else if (name === 'retailSize' || name === 'brand') {
                var cb = filterForm.querySelector('input[type="checkbox"][name="' + name + '"][value="' + val + '"]');
                if (cb) cb.checked = false;
            } else if (name === 'categoryId') {
                var category = filterForm.querySelector('[name="categoryId"]');
                if (category) category.value = '';
            }
            triggerSubmit();
        }

        var mobileCancel = e.target.closest('#yqMobileCancel');
        if (mobileCancel) {
            e.preventDefault();
            var closeButton = document.getElementById('yqFiltersClose');
            if (closeButton) {
                closeButton.click();
            } else {
                var filters = document.getElementById('yaqutFilters');
                if (filters) {
                    filters.classList.remove('is-open');
                    document.documentElement.removeAttribute('data-yq-drawer-open');
                }
            }
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
        if (e.target.matches('[data-yq-mobile-category]')) {
            categoryInput = document.getElementById('yqCategoryId');
            if (categoryInput) categoryInput.value = e.target.value;
        }

        if (e.target.name === 'retail') {
            syncRetailSizeDependency();
        }

        if (e.target.closest('[data-yq-auto-submit]')) {
            if (window.innerWidth < 992) return;
            triggerSubmit();
        }
    });

    if (filterForm) {
        filterForm.addEventListener('submit', function (e) {
            submitFilters(e);
        });
    }

    window.addEventListener('popstate', function (e) {
        if (suppressNextPopState) {
            suppressNextPopState = false;
            return;
        }
        var filters = document.getElementById('yaqutFilters');
        if (filters && filters.classList.contains('is-open')) return;
        submitFilters(null, true, window.location.href);
    });

})(window, document);

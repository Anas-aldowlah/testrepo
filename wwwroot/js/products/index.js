/**
 * ياقوت — Products / Index page interactions
 * Owns catalog URL/form state and server-page loading only.
 */
(function (window, document) {
    'use strict';

    var filterForm = document.getElementById('yqCatalogFilters');
    var categoryInput = document.getElementById('yqCategoryId');
    var grid = document.getElementById('yaqutProductsGrid');
    var status = document.getElementById('yqCatalogStatus');
    var isLoading = false;
    var abortController = null;
    var currentRequestId = 0;
    var suppressNextPopState = false;

    function bindImageFallbacks(scope) {
        (scope || document).querySelectorAll('[data-yaqut-fallback]').forEach(function (img) {
            if (img.dataset.yqFallbackBound) return;
            img.dataset.yqFallbackBound = 'true';
            img.addEventListener('error', function () {
                var fallback = img.getAttribute('data-yaqut-fallback');
                if (fallback && img.getAttribute('src') !== fallback) img.setAttribute('src', fallback);
            });
            if (img.complete && img.naturalWidth === 0) {
                var fallback = img.getAttribute('data-yaqut-fallback');
                if (fallback && img.getAttribute('src') !== fallback) img.setAttribute('src', fallback);
            }
        });
    }

    bindImageFallbacks(document);

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

    var currentFetchPromise = null;
    var lastAppliedQuery = canonicalizeParams(new URLSearchParams(window.location.search));
    var lastAppliedActionQuery = getCanonicalQuery();
    var defaultQuery = getDefaultQuery();
    var inFlightQuery = null;
    var pendingSubmitTimer = null;
    var priceQuietUntil = 0;

    function canonicalizeParams(source) {
        var valuesByKey = {};
        source.forEach(function (value, key) {
            value = String(value);
            if (key !== 'mobileCategory' && value !== '') {
                if (!valuesByKey[key]) valuesByKey[key] = [];
                valuesByKey[key].push(value);
            }
        });

        var params = new URLSearchParams();
        Object.keys(valuesByKey).sort().forEach(function (key) {
            valuesByKey[key].sort().forEach(function (value) {
                params.append(key, value);
            });
        });
        return params.toString();
    }

    function getCanonicalQuery() {
        if (!filterForm) return '';
        return canonicalizeParams(new FormData(filterForm));
    }

    function getDefaultQuery() {
        if (!filterForm) return '';
        var fd = new FormData(filterForm);
        [
            'availability',
            'retail',
            'categoryId',
            'filterCategoryRadio',
            'brand',
            'retailSize',
            'minPrice',
            'maxPrice'
        ].forEach(function (key) {
            fd.delete(key);
        });
        fd.append('availability', 'all');
        fd.append('retail', 'all');
        return canonicalizeParams(fd);
    }

    function isPriceRangeValid() {
        var minEl = document.getElementById('yqMinPriceInput');
        var maxEl = document.getElementById('yqMaxPriceInput');
        if (!minEl || !maxEl) return true;
        var minVal = parseFloat(minEl.value);
        var maxVal = parseFloat(maxEl.value);
        return isNaN(minVal) || isNaN(maxVal) || minVal <= maxVal;
    }

    function updateFilterActionState() {
        var primaryBtn = document.getElementById('yqPrimaryActionBtn');
        var resetBtn = document.getElementById('yqFiltersReset');
        if (!primaryBtn && !resetBtn) return;

        var currentQuery = getCanonicalQuery();
        defaultQuery = getDefaultQuery();
        var isDirty = currentQuery !== lastAppliedActionQuery;
        var isDefault = currentQuery === defaultQuery;
        var priceIsValid = isPriceRangeValid();

        if (primaryBtn) {
            primaryBtn.disabled = isLoading || !priceIsValid || !isDirty;
        }
        if (resetBtn) {
            resetBtn.disabled = isLoading || (!isDirty && isDefault);
        }
    }


    function validatePrice() {
        var minEl = document.getElementById('yqMinPriceInput');
        var maxEl = document.getElementById('yqMaxPriceInput');
        if (!minEl || !maxEl) return true;
        var errorEl = document.getElementById('yqPriceError');
        var isValid = isPriceRangeValid();

        if (errorEl) errorEl.classList.toggle('d-none', isValid);
        minEl.setAttribute('aria-invalid', !isValid ? 'true' : 'false');
        maxEl.setAttribute('aria-invalid', !isValid ? 'true' : 'false');
        return isValid;
    }


    function scheduleUpdate(delay) {
        if (pendingSubmitTimer) {
            clearTimeout(pendingSubmitTimer);
            pendingSubmitTimer = null;
        }
        var now = Date.now();
        var timeUntilPriceQuiet = Math.max(0, priceQuietUntil - now);
        var effectiveDelay = Math.max(delay, timeUntilPriceQuiet);

        pendingSubmitTimer = setTimeout(function() {
            pendingSubmitTimer = null;
            if (validatePrice()) {
                var currentQuery = getCanonicalQuery();
                if (currentQuery !== lastAppliedActionQuery) {
                    submitFilters();
                }
            }
            updateFilterActionState();
        }, effectiveDelay);
        updateFilterActionState();
    }

    function syncRetailSizeDependency() {
        if (!filterForm) return;
        var sizeGroup = document.getElementById('yqRetailSizeGroup');
        if (!sizeGroup) return;
        var selectedRetail = filterForm.querySelector('input[name="retail"]:checked');
        var retailIsUnavailable = selectedRetail && selectedRetail.value === 'no';
        sizeGroup.classList.toggle('d-none', retailIsUnavailable);
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
        validatePrice();
        updateFilterBadgesAndSelections();
        updateFilterActionState();
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
        updateFilterActionState();
    }

    function submitFilters(e, isPopState, popUrl) {
        if (e) e.preventDefault();
        if (!filterForm) return Promise.resolve({ applied: false, query: '' });

        var currentQuery = '';
        var nextUrlString = '';
        var isMobileDrawerApply = false;

        if (isPopState && popUrl) {
            var tempUrl = new URL(popUrl);
            currentQuery = canonicalizeParams(new URLSearchParams(tempUrl.search));
            nextUrlString = popUrl;
        } else {
            currentQuery = getCanonicalQuery();
            var nextUrl = new URL(filterForm.action || window.location.href);
            nextUrl.search = currentQuery;
            nextUrlString = nextUrl.toString();
            var filters = document.getElementById('yaqutFilters');
            isMobileDrawerApply = window.innerWidth < 992
                && !isPopState
                && !!e
                && filters
                && filters.classList.contains('is-open');
        }

        if (typeof window.fetch !== 'function' || !window.history.pushState) {
            if (!isPopState) filterForm.submit();
            return Promise.resolve({ applied: false, query: currentQuery });
        }

        if (currentQuery === lastAppliedQuery) {
            updateFilterActionState();
            return Promise.resolve({ applied: true, query: currentQuery });
        }

        if (inFlightQuery === currentQuery && currentFetchPromise) {
            return currentFetchPromise;
        }

        if (abortController) {
            abortController.abort();
        }
        var requestAbortController = new AbortController();
        abortController = requestAbortController;

        var requestId = ++currentRequestId;
        inFlightQuery = currentQuery;

        isLoading = true;
        currentFetchPromise = (async function() {

                if (status) {
            status.textContent = 'جارٍ تحديث المنتجات...';
            status.classList.remove('visually-hidden');
        }
        var primaryBtn = document.getElementById('yqPrimaryActionBtn');
        if (primaryBtn) {
            var primarySpan = primaryBtn.querySelector('span');
            if (primarySpan) {
                if (!primarySpan.dataset.originalText) {
                    primarySpan.dataset.originalText = primarySpan.textContent;
                }
                primarySpan.textContent = 'جاري التحديث...';
            }
        }
        updateFilterActionState();
        if (grid) {
            grid.setAttribute('aria-busy', 'true');
            grid.style.opacity = '0.6';
            grid.style.transition = 'opacity 0.15s ease-in-out';
        }
        try {
            var response = await window.fetch(nextUrlString, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                signal: requestAbortController.signal
            });

            if (!response.ok) {
                if (response.status === 400) {
                    var contentType = response.headers.get('content-type');
                    if (contentType && contentType.indexOf('application/json') !== -1) {
                        throw new Error('Validation error');
                    }
                }
                throw new Error('Filter request failed.');
            }

            var html = await response.text();

            if (requestId !== currentRequestId) {
                return { applied: false, query: currentQuery };
            }

            var parsed = new window.DOMParser().parseFromString(html, 'text/html');


            var activeEl = document.activeElement;
            var focusId = activeEl ? activeEl.id : null;
            var focusName = activeEl ? activeEl.name : null;
            var focusValue = activeEl ? activeEl.value : null;
            var selectionStart = null;
            var selectionEnd = null;
            if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA') && activeEl.type !== 'radio' && activeEl.type !== 'checkbox') {
                try {
                    selectionStart = activeEl.selectionStart;
                    selectionEnd = activeEl.selectionEnd;
                } catch(e) {}
            }
            var filtersPanel = document.getElementById('yaqutFilters');
            var drawerScrollTop = filtersPanel ? filtersPanel.scrollTop : 0;
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
                    updateFilterActionState();
                }

                grid = document.getElementById('yaqutProductsGrid');
                status = document.getElementById('yqCatalogStatus');

                if (parsed.title) document.title = parsed.title;
                bindImageFallbacks(oldShop);

                if (focusId) {
                    var el = document.getElementById(focusId);
                    if (!el && focusName) {
                        el = filterForm.querySelector('[name="' + focusName + '"][value="' + focusValue + '"]');
                        if (!el) el = filterForm.querySelector('[name="' + focusName + '"]');
                    }
                    if (el) {
                        el.focus();
                        if (selectionStart !== null) {
                            try {
                                el.setSelectionRange(selectionStart, selectionEnd);
                            } catch(e) {}
                        }
                    }
                }
                if (filtersPanel) {
                    filtersPanel.scrollTop = drawerScrollTop;
                }

            }

            if (!isPopState && window.location.href !== nextUrlString) {
                if (isMobileDrawerApply && window.history.state && window.history.state.yqPanel) {
                    window.history.replaceState({ path: nextUrlString }, '', nextUrlString);
                } else {
                    window.history.pushState({ path: nextUrlString }, '', nextUrlString);
                }
            }

            saveCommittedState();
            var appliedQuery = getCanonicalQuery();
            lastAppliedQuery = appliedQuery;
            lastAppliedActionQuery = appliedQuery;
            updateFilterActionState();

            if (isMobileDrawerApply && filters) {
                filters.classList.remove('is-open');
                document.documentElement.removeAttribute('data-yq-drawer-open');
                var filterToggle = document.getElementById('yaqutFilterToggle');
                if (filterToggle) filterToggle.focus();
            }
            return { applied: true, query: appliedQuery };
        } catch (error) {
            if (requestId !== currentRequestId) {
                return { applied: false, query: currentQuery };
            }
            if (error.name !== 'AbortError') {
                if (!window.catalogHarness && error.message !== 'Validation error') {
                    window.location.assign(nextUrlString);
                }
            }
            return { applied: false, query: currentQuery };
        } finally {
            if (requestId === currentRequestId) {
                isLoading = false;
                var primaryBtn = document.getElementById('yqPrimaryActionBtn');
                if (primaryBtn) {
                    var primarySpan = primaryBtn.querySelector('span');
                    if (primarySpan && primarySpan.dataset.originalText) {
                        primarySpan.textContent = primarySpan.dataset.originalText;
                    }
                }
                if (status) {
                    status.classList.add('visually-hidden');
                }
                if (grid) {
                    grid.removeAttribute('aria-busy');
                    grid.style.opacity = '';
                }
                inFlightQuery = null;
                currentFetchPromise = null;
                abortController = null;
                updateFilterActionState();
            }
        }
        })();
        return currentFetchPromise;
    }

    function updateFilterBadgesAndSelections() {
        if (!filterForm) return;
        filterForm.querySelectorAll('.yq-filter-item').forEach(function (item) {
            var input = item.querySelector('.yq-control-input');
            if (input) {
                item.classList.toggle('is-selected', !!input.checked);
            }
        });
    }


    function triggerSubmit() {
        if (!validatePrice()) return Promise.resolve();
        return submitFilters();
    }


    // Event delegation for category pills, filter cards, and auto-submit controls
    document.addEventListener('click', function(e) {
        var primaryBtn = e.target.closest('#yqPrimaryActionBtn');
        if (primaryBtn) {
            e.preventDefault();
            if (primaryBtn.disabled || isLoading) return;
            if (!validatePrice()) {
                if (pendingSubmitTimer) {
                    clearTimeout(pendingSubmitTimer);
                    pendingSubmitTimer = null;
                }
                return;
            }

            (async function() {
                if (pendingSubmitTimer) {
                    clearTimeout(pendingSubmitTimer);
                    pendingSubmitTimer = null;
                }

                var currentQuery = getCanonicalQuery();

                var result = { applied: true, query: currentQuery };
                if (currentQuery !== lastAppliedActionQuery) {
                    if (currentQuery === inFlightQuery && currentFetchPromise) {
                        result = await currentFetchPromise;
                    } else {
                        result = await submitFilters();
                    }
                }

                if (!result.applied || lastAppliedQuery !== getCanonicalQuery()) return;

                if (window.innerWidth < 992) {
                    var closeButton = document.getElementById('yqFiltersClose');
                    if (closeButton) closeButton.click();
                } else {
                    var mainArea = document.querySelector('.yq-shop-main');
                    if (mainArea) {
                        mainArea.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                }
            })().catch(function () {});
            return;
        }

        var toggleBtn = e.target.closest('[data-filter-toggle]');
        if (toggleBtn) {
            e.preventDefault();
            var card = toggleBtn.closest('.yq-filter-card');
            if (card) {
                var isCollapsed = card.classList.toggle('is-collapsed');
                toggleBtn.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
            }
            return;
        }

        var moreBtn = e.target.closest('[data-toggle-more]');
        if (moreBtn) {
            e.preventDefault();
            var cardMore = moreBtn.closest('.yq-filter-card');
            if (cardMore) {
                var isExpanded = moreBtn.classList.toggle('is-expanded');
                cardMore.querySelectorAll('.is-extra-item').forEach(function(item) {
                    item.classList.toggle('d-none', !isExpanded);
                });
                var span = moreBtn.querySelector('span');
                if (span) span.textContent = isExpanded ? 'عرض أقل' : 'عرض المزيد';
            }
            return;
        }

        var resetBtn = e.target.closest('#yqFiltersReset');
        if (resetBtn) {
            e.preventDefault();
            if (resetBtn.disabled || isLoading) return;
            if (filterForm) {
                categoryInput = document.getElementById('yqCategoryId');
                if (categoryInput) categoryInput.value = '';
                var catAllRadio = filterForm.querySelector('[data-yq-category-radio][value=""]');
                if (catAllRadio) catAllRadio.checked = true;

                filterForm.querySelectorAll('input[type="radio"]').forEach(function(r) {
                    if (r.value === 'all' || r.value === '') r.checked = true;
                    else r.checked = false;
                });

                filterForm.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
                    cb.checked = false;
                });

                var minP = filterForm.querySelector('[name="minPrice"]');
                var maxP = filterForm.querySelector('[name="maxPrice"]');
                if (minP) minP.value = '';
                if (maxP) maxP.value = '';

                if (pendingSubmitTimer) {
                    clearTimeout(pendingSubmitTimer);
                    pendingSubmitTimer = null;
                }
                priceQuietUntil = 0;
                validatePrice();
                syncRetailSizeDependency();
                updateFilterBadgesAndSelections();
                updateFilterActionState();
                var currentQuery = getCanonicalQuery();
                if (currentQuery !== lastAppliedQuery) {
                    submitFilters();
                }
            }
            return;
        }

        var categoryBtn = e.target.closest('[data-yq-category]');
        if (categoryBtn) {
            categoryInput = document.getElementById('yqCategoryId');
            if (categoryInput) {
                categoryInput.value = categoryBtn.getAttribute('data-yq-category') || '';
                scheduleUpdate(350);
            }
        }

        var clearBtn = e.target.closest('.yq-shop-clear-link, .yq-search-banner__clear, #yqFilterClearAll');
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
                var minPrice = filterForm.querySelector('[name="minPrice"]');
                var maxPrice = filterForm.querySelector('[name="maxPrice"]');
                if (minPrice) minPrice.value = '';
                if (maxPrice) maxPrice.value = '';
            } else if (name === 'retail' || name === 'availability') {
                var radio = filterForm.querySelector('input[type="radio"][name="' + name + '"][value="' + val + '"]');
                if (radio) radio.checked = true;
            } else if (name === 'retailSize' || name === 'brand') {
                var cb = filterForm.querySelector('input[type="checkbox"][name="' + name + '"][value="' + val + '"]');
                if (cb) cb.checked = false;
            } else if (name === 'categoryId') {
                var category = filterForm.querySelector('[name="categoryId"]');
                if (category) category.value = '';
            }
            scheduleUpdate(350);
        }

        var filtersCancel = e.target.closest('#yqFiltersCancel');
        if (filtersCancel) {
            e.preventDefault();
            restoreCommittedState();
            var filters = document.getElementById('yaqutFilters');
            if (filters && filters.classList.contains('is-open')) {
                var closeButton = document.getElementById('yqFiltersClose');
                if (closeButton) closeButton.click();
            }
        }

        var pageLink = e.target.closest('[data-yq-catalog-pager] a[href]');
        if (pageLink) {
            e.preventDefault();
            if (pageLink.getAttribute('aria-disabled') === 'true' || isLoading) return;
            window.history.pushState({ path: pageLink.href }, '', pageLink.href);
            submitFilters(null, true, pageLink.href);
        }
    });

    document.addEventListener('change', function(e) {
        if (filterForm && filterForm.contains(e.target) && (e.target.type === 'radio' || e.target.type === 'checkbox')) {
            updateFilterBadgesAndSelections();
            if (validatePrice()) {
                scheduleUpdate(350);
            }
        }
        if (e.target.matches('[data-yq-category-radio]')) {
            categoryInput = document.getElementById('yqCategoryId');
            if (categoryInput) {
                categoryInput.value = e.target.value;
            }
        }

        if (e.target.matches('[data-yq-mobile-category]')) {
            categoryInput = document.getElementById('yqCategoryId');
            if (categoryInput) categoryInput.value = e.target.value;
        }

        if (e.target.name === 'retail') {
            syncRetailSizeDependency();
        }

        updateFilterBadgesAndSelections();
        updateFilterActionState();

        if (e.target.closest('[data-yq-auto-submit]')) {
            scheduleUpdate(350);
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


    document.addEventListener('input', function(e) {
        if (e.target.id === 'yqMinPriceInput' || e.target.id === 'yqMaxPriceInput') {
            var isValid = validatePrice();
            if (!isValid) {
                if (pendingSubmitTimer) {
                    clearTimeout(pendingSubmitTimer);
                    pendingSubmitTimer = null;
                }
                updateFilterActionState();
                return;
            }
            priceQuietUntil = Date.now() + 600;
            scheduleUpdate(600);
            updateFilterActionState();
        }
    });

})(window, document);

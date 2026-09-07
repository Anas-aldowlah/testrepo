/**
 * ياقوت — Products / Details page interactions
 * Quantity, retail choice, wishlist, and recently viewed interactions
 */
(function (window, document) {
    'use strict';

    var RECENT_KEY = 'yaqut-recently-viewed';
    var MAX_RECENT = 6;

    function readQuantityMax(input) {
        var max = parseInt(input.max, 10);
        return Number.isFinite(max) && max >= 0 ? max : Infinity;
    }

    function initQtyStepper(root) {
        var input = root.querySelector('#yqQtyInput');
        var select = root.querySelector('#yqQtySelect');
        if (!input || !select) return;

        function syncControls() {
            var max = readQuantityMax(input);
            var isAvailable = max > 0;
            var currentVal = parseInt(input.value || '1', 10);
            
            select.disabled = !isAvailable;
            input.disabled = !isAvailable;
            
            if (!isAvailable) {
                input.value = '1';
                select.replaceChildren();
                return;
            }

            var presetLimit = Math.min(max, 10);
            
            var needsRebuild = false;
            var options = Array.from(select.options);
            var lastVal = options.length > 0 ? options[options.length - 1].value : null;
            var hasCustom = lastVal === 'custom';
            var expectedOptionsCount = presetLimit + (max > 10 ? 1 : 0);
            
            if (options.length !== expectedOptionsCount || (max > 10 && !hasCustom)) {
                needsRebuild = true;
            }
            
            if (needsRebuild) {
                select.replaceChildren();
                for (var i = 1; i <= presetLimit; i++) {
                    var opt = document.createElement('option');
                    opt.value = String(i);
                    opt.textContent = String(i);
                    select.appendChild(opt);
                }
                if (max > 10) {
                    var customOpt = document.createElement('option');
                    customOpt.value = 'custom';
                    customOpt.textContent = 'أكثر من 10 (10+)';
                    select.appendChild(customOpt);
                }
            }

            if (currentVal <= 10) {
                select.value = String(currentVal);
                select.classList.remove('d-none');
                input.classList.add('d-none');
            } else {
                select.value = 'custom';
                select.classList.add('d-none');
                input.value = String(currentVal);
                input.classList.remove('d-none');
            }
        }

        select.addEventListener('change', function () {
            if (select.value === 'custom') {
                var current = parseInt(input.value, 10) || 1;
                input.value = String(Math.max(11, current));
                select.classList.add('d-none');
                input.classList.remove('d-none');
                input.focus();
                input.select();
                return;
            }
            
            input.value = select.value;
        });

        input.addEventListener('blur', function () {
            var val = parseInt(input.value || '1', 10);
            var min = parseInt(input.min, 10) || 1;
            var max = readQuantityMax(input);
            
            if (!Number.isFinite(val) || val < min) val = min;
            
            if (val > max) {
                if (window.YaqutOperationDialog) {
                    window.YaqutOperationDialog.show({
                        title: 'الكمية غير متوفرة',
                        message: max === 1
                            ? 'المتوفر حاليًا ' + (input.dataset.yqUnitOne || 'وحدة واحدة') + ' فقط.'
                            : 'المتوفر حاليًا ' + max + ' فقط.'
                    });
                }
                val = max;
            }
            
            input.value = String(val);
            syncControls();
        });

        input.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                input.blur();
            }
        });

        input.addEventListener('yq:quantity-availability-changed', syncControls);

        syncControls();
    }

    function getRecentList() {
        try {
            var raw = localStorage.getItem(RECENT_KEY);
            var parsed = raw ? JSON.parse(raw) : [];
            if (!Array.isArray(parsed)) return [];
            return parsed.filter(function (product) {
                return product && typeof product === 'object' && typeof product.id === 'string';
            });
        } catch (e) {
            return [];
        }
    }

    function saveRecentList(list) {
        try {
            localStorage.setItem(RECENT_KEY, JSON.stringify(list));
        } catch (e) { /* storage unavailable, ignore silently */ }
    }

    function trackCurrentProduct(root) {
        var product = {
            id: root.dataset.id,
            name: root.dataset.name,
            price: root.dataset.price,
            image: root.dataset.image
        };
        if (!product.id) return;

        var list = getRecentList().filter(function (p) { return p.id !== product.id; });
        list.unshift(product);
        saveRecentList(list.slice(0, MAX_RECENT));
    }

    function bindImageFallbacks(root) {
        if (!root) return;
        root.querySelectorAll('[data-yaqut-fallback]').forEach(function (img) {
            if (img.dataset.yqFallbackBound === 'true') return;
            img.dataset.yqFallbackBound = 'true';

            function applyFallback() {
                var fallback = img.getAttribute('data-yaqut-fallback');
                if (fallback && img.getAttribute('src') !== fallback) {
                    img.setAttribute('src', fallback);
                }
            }

            img.addEventListener('error', applyFallback, { once: true });

            if (img.complete && img.naturalWidth === 0) {
                applyFallback();
            }
        });
    }

    function renderRecentlyViewed(root) {
        var section = root.querySelector('#yqRecentlyViewed');
        var grid = root.querySelector('#yqRecentlyViewedGrid');
        if (!section || !grid) return;

        var list = getRecentList().filter(function (p) { return p.id !== root.dataset.id; });
        if (!list.length) return;

        var params = new URLSearchParams();
        list.forEach(function (p) { params.append('ids', p.id); });

        fetch('/Products/RecentlyViewed?' + params.toString())
            .then(function (response) {
                if (!response.ok) throw new Error('Network response was not ok');
                return response.text();
            })
            .then(function (html) {
                var parser = new DOMParser();
                var doc = parser.parseFromString(html, 'text/html');
                var cards = doc.querySelectorAll('.yq-pdp-recent__card');
                
                if (cards.length > 0) {
                    grid.replaceChildren();
                    cards.forEach(function(card) {
                        grid.appendChild(card.cloneNode(true));
                    });
                    bindImageFallbacks(grid);
                    section.hidden = false;
                } else {
                    section.hidden = true;
                }
            })
            .catch(function (error) {
                // Fail gracefully
            });
    }

    function initRecentlyViewed(root) {
        renderRecentlyViewed(root);
        trackCurrentProduct(root);
    }

    function initRetailChoice(root) {
        var choices = root.querySelectorAll('input[name="retailChoice"]');
        var retailInput = root.querySelector('#yqRetailPriceId');
        var qtyInput = root.querySelector('#yqQtyInput');
        var priceBox = root.querySelector('.yq-pdp-price');
        var form = root.querySelector('.yq-pdp-purchase');
        var addButton = form ? form.querySelector('[data-yq-add-button]') : null;
        var stockBadge = root.querySelector('[data-yq-stock-badge]');
        var stockIcon = stockBadge ? stockBadge.querySelector('[data-yq-stock-icon]') : null;
        var stockLabel = stockBadge ? stockBadge.querySelector('[data-yq-stock-label]') : null;
        var stockStateText = root.querySelector('[data-yq-stock-state-text]');
        var lowStockMessage = root.querySelector('[data-yq-low-stock-message]');
        var detailsPriceBox = root.querySelector('[data-yq-details-price]');
        var stickyPrice = root.querySelector('[data-yq-sticky-price]');
        if (!choices.length || !retailInput || !qtyInput || !priceBox || !form || !addButton) return;

        function formatAvailableUnits(max, selected) {
            if (max === 0) return 'غير متوفر';
            if (max === 1) return selected.dataset.unitOne || 'وحدة واحدة';
            if (max === 2) return selected.dataset.unitTwo || 'وحدتان';
            if (max >= 3 && max <= 10) return max + ' ' + (selected.dataset.unitFew || 'وحدات');
            return max + ' ' + (selected.dataset.unitMany || 'وحدة');
        }

        function syncRetailChoice(selected) {
            retailInput.value = selected.value || '';
            var parsedMax = parseInt(selected.dataset.max, 10);
            var max = Number.isFinite(parsedMax) && parsedMax >= 0 ? parsedMax : 0;
            var isAvailable = max > 0;
            var currentQuantity = parseInt(qtyInput.value || '1', 10) || 1;

            qtyInput.max = String(max);
            qtyInput.disabled = !isAvailable;
            qtyInput.value = isAvailable ? String(Math.min(Math.max(currentQuantity, 1), max)) : '1';
            qtyInput.dataset.yqUnitOne = selected.dataset.unitOne || 'وحدة واحدة';
            qtyInput.dispatchEvent(new Event('yq:quantity-availability-changed'));

            form.dataset.yqStockUnavailable = isAvailable ? 'false' : 'true';
            addButton.disabled = !isAvailable;
            addButton.setAttribute('aria-disabled', isAvailable ? 'false' : 'true');

            var price = parseFloat(selected.dataset.price || '0') || 0;
            priceBox.textContent = window.Yaqut.formatPrice(price);
            if (detailsPriceBox) {
                detailsPriceBox.textContent = window.Yaqut.formatPrice(price);
            }
            if (stickyPrice) {
                stickyPrice.textContent = window.Yaqut.formatPrice(price);
            }

            var availableText = formatAvailableUnits(max, selected);
            root.querySelectorAll('.yq-available-stock-text').forEach(function (stockText) {
                stockText.textContent = availableText;
            });

            if (stockBadge) {
                stockBadge.classList.toggle('yq-pdp-stock__badge--in', isAvailable);
                stockBadge.classList.toggle('yq-pdp-stock__badge--out', !isAvailable);
            }
            if (stockIcon) stockIcon.className = isAvailable ? 'bi bi-check-circle-fill' : 'bi bi-x-circle-fill';
            if (stockLabel) stockLabel.textContent = isAvailable ? 'متوفر في المخزون' : 'غير متوفر حالياً';
            if (stockStateText) {
                stockStateText.classList.toggle('text-green', isAvailable);
                stockStateText.classList.toggle('text-red', !isAvailable);
                stockStateText.textContent = isAvailable ? 'متوفر' : 'نفذت الكمية';
            }

            if (lowStockMessage) {
                lowStockMessage.hidden = !isAvailable || max > 5;
                var messageText = lowStockMessage.querySelector('span');
                if (messageText) messageText.textContent = 'المتوفر حاليًا ' + availableText + ' فقط';
            }
        }

        choices.forEach(function (choice) {
            choice.addEventListener('change', function () {
                if (choice.checked) syncRetailChoice(choice);
            });
        });

        var checkedChoice = root.querySelector('input[name="retailChoice"]:checked');
        if (checkedChoice) syncRetailChoice(checkedChoice);
    }

    function initWishlist(root) {
        var button = root.querySelector('#yqWishlistBtn');
        var icon = root.querySelector('#yqWishlistIcon');
        if (!button || !icon) return;

        var productId = button.dataset.productId;
        var key = 'yq_wishlist';

        function getWishlist() {
            try {
                var parsed = JSON.parse(window.localStorage.getItem(key) || '[]');
                return Array.isArray(parsed)
                    ? parsed.filter(function (id) { return typeof id === 'string'; })
                    : [];
            } catch (error) {
                return [];
            }
        }

        function saveWishlist(list) {
            try {
                window.localStorage.setItem(key, JSON.stringify(list));
                return true;
            } catch (error) {
                return false;
            }
        }

        function updateUi(isWishlisted) {
            icon.className = isWishlisted ? 'bi bi-heart-fill' : 'bi bi-heart';
            button.classList.toggle('is-wishlisted', isWishlisted);
            button.setAttribute('aria-label', isWishlisted ? 'إزالة من المفضلة' : 'إضافة إلى المفضلة');
            button.title = isWishlisted ? 'إزالة من المفضلة' : 'إضافة إلى المفضلة';
        }

        updateUi(getWishlist().includes(productId));

        button.addEventListener('click', function () {
            var list = getWishlist();
            var index = list.indexOf(productId);
            if (index === -1) {
                list.push(productId);
            } else {
                list.splice(index, 1);
            }
            if (!saveWishlist(list)) return;

            updateUi(list.includes(productId));
            button.classList.add('yq-pdp-wishlist--pop');
            button.addEventListener('animationend', function () {
                button.classList.remove('yq-pdp-wishlist--pop');
            }, { once: true });
        });
    }

    function initStickyBar(root) {
        var stickyBar = root.querySelector('[data-yq-pdp-sticky-bar]');
        var stickyBtn = root.querySelector('[data-yq-sticky-add-btn]');
        var primaryBtn = root.querySelector('.yq-pdp-add-btn');
        var form = root.querySelector('.yq-pdp-purchase');
        if (!stickyBar || !stickyBtn || !primaryBtn || !form) return;

        var mql = typeof window.matchMedia === 'function' ? window.matchMedia('(max-width: 991.98px)') : null;
        var isStickyVisible = false;

        function isMobile() {
            return !mql || mql.matches;
        }

        function showSticky() {
            if (isStickyVisible || !isMobile()) return;
            isStickyVisible = true;
            stickyBar.classList.add('is-visible');
            stickyBar.setAttribute('aria-hidden', 'false');
            stickyBtn.setAttribute('tabindex', '0');
            document.body.classList.add('yq-pdp-sticky-active');
        }

        function hideSticky() {
            if (!isStickyVisible) return;
            isStickyVisible = false;
            stickyBar.classList.remove('is-visible');
            stickyBar.setAttribute('aria-hidden', 'true');
            stickyBtn.setAttribute('tabindex', '-1');
            document.body.classList.remove('yq-pdp-sticky-active');
            if (document.activeElement === stickyBtn && typeof primaryBtn.focus === 'function') {
                primaryBtn.focus({ preventScroll: true });
            }
        }

        function syncButtonState() {
            var isPrimaryDisabled = primaryBtn.disabled || primaryBtn.getAttribute('aria-disabled') === 'true';
            var isLoading = primaryBtn.classList.contains('is-loading');
            stickyBtn.disabled = isPrimaryDisabled;
            stickyBtn.setAttribute('aria-disabled', isPrimaryDisabled ? 'true' : 'false');
            stickyBtn.classList.toggle('is-loading', isLoading);
            var stickyLabel = stickyBtn.querySelector('span');
            var primaryLabel = primaryBtn.querySelector('[data-yq-add-label]');
            if (stickyLabel && primaryLabel) {
                var text = primaryLabel.textContent ? primaryLabel.textContent.trim() : '';
                if (text) stickyLabel.textContent = text;
            }
        }

        stickyBtn.addEventListener('click', function (e) {
            e.preventDefault();
            if (stickyBtn.disabled || primaryBtn.disabled || form.dataset.yqBusy === 'true') return;
            primaryBtn.click();
        });

        if (typeof window.MutationObserver === 'function') {
            var observer = new MutationObserver(function () {
                syncButtonState();
            });
            observer.observe(primaryBtn, {
                attributes: true,
                attributeFilter: ['disabled', 'aria-disabled', 'class']
            });

            var drawer = document.querySelector('[data-yq-cart-drawer]');
            if (drawer) {
                var drawerObserver = new MutationObserver(function () {
                    if (drawer.classList.contains('is-open')) {
                        hideSticky();
                    } else if (primaryBtn.getBoundingClientRect().top < 0) {
                        showSticky();
                    }
                });
                drawerObserver.observe(drawer, {
                    attributes: true,
                    attributeFilter: ['class', 'aria-hidden']
                });
            }
        }

        if (typeof window.IntersectionObserver === 'function') {
            var io = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        hideSticky();
                    } else if (entry.boundingClientRect.top < 0) {
                        showSticky();
                    } else {
                        hideSticky();
                    }
                });
            }, {
                root: null,
                threshold: 0
            });
            io.observe(primaryBtn);
        }

        if (mql) {
            var onMediaChange = function () {
                if (!isMobile()) hideSticky();
            };
            if (typeof mql.addEventListener === 'function') {
                mql.addEventListener('change', onMediaChange);
            } else if (typeof mql.addListener === 'function') {
                mql.addListener(onMediaChange);
            }
        }

        syncButtonState();
    }

    function init() {
        var root = document.querySelector('[data-yq-product-details]');
        if (!root) return;

        initQtyStepper(root);
        initRetailChoice(root);
        initWishlist(root);
        initRecentlyViewed(root);
        initStickyBar(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

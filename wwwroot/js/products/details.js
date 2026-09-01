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
        var decreaseBtn = root.querySelector('[data-yq-qty-decrease]');
        var increaseBtn = root.querySelector('[data-yq-qty-increase]');
        if (!input || !decreaseBtn || !increaseBtn) return;

        function clamp(value) {
            var min = parseInt(input.min, 10) || 1;
            var max = readQuantityMax(input);
            return Math.min(Math.max(value, min), max);
        }

        function syncButtons() {
            var value = parseInt(input.value || '1', 10);
            var max = readQuantityMax(input);
            var unavailable = input.disabled || max === 0;
            decreaseBtn.disabled = unavailable || value <= (parseInt(input.min, 10) || 1);
            increaseBtn.disabled = unavailable || value >= max;
        }

        input.addEventListener('change', function () {
            var val = parseInt(input.value || '1', 10);
            var max = readQuantityMax(input);
            if (max === 0) {
                input.value = '1';
                syncButtons();
                return;
            }
            if (val > max) {
                if (window.YaqutOperationDialog) {
                    window.YaqutOperationDialog.show({
                        title: 'الكمية غير متوفرة',
                        message: max === 1
                            ? 'المتوفر حاليًا ' + (input.dataset.yqUnitOne || 'وحدة واحدة') + ' فقط.'
                            : 'المتوفر حاليًا ' + max + ' فقط.'
                    });
                }
                input.value = max;
            } else {
                input.value = clamp(val);
            }
            syncButtons();
        });

        decreaseBtn.addEventListener('click', function () {
            input.value = clamp((parseInt(input.value || '1', 10) - 1));
            syncButtons();
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });

        increaseBtn.addEventListener('click', function () {
            var val = parseInt(input.value || '1', 10);
            var max = readQuantityMax(input);
            if (val >= max) return;
            input.value = clamp(val + 1);
            syncButtons();
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });

        input.addEventListener('yq:quantity-availability-changed', syncButtons);

        syncButtons();
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

    function init() {
        var root = document.querySelector('[data-yq-product-details]');
        if (!root) return;

        initQtyStepper(root);
        initRetailChoice(root);
        initWishlist(root);
        initRecentlyViewed(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

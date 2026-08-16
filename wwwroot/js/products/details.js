/**
 * ياقوت — Products / Details page interactions
 * Quantity, retail choice, wishlist, and recently viewed interactions
 */
(function (window, document) {
    'use strict';

    var RECENT_KEY = 'yaqut-recently-viewed';
    var MAX_RECENT = 6;

    function initQtyStepper(root) {
        var input = root.querySelector('#yqQtyInput');
        var decreaseBtn = root.querySelector('[data-yq-qty-decrease]');
        var increaseBtn = root.querySelector('[data-yq-qty-increase]');
        if (!input || !decreaseBtn || !increaseBtn) return;

        function clamp(value) {
            var min = parseInt(input.min, 10) || 1;
            var max = parseInt(input.max, 10) || Infinity;
            return Math.min(Math.max(value, min), max);
        }

        decreaseBtn.addEventListener('click', function () {
            input.value = clamp((parseInt(input.value, 10) || 1) - 1);
        });

        increaseBtn.addEventListener('click', function () {
            input.value = clamp((parseInt(input.value, 10) || 1) + 1);
        });

        input.addEventListener('change', function () {
            input.value = clamp(parseInt(input.value, 10) || 1);
        });
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

        grid.replaceChildren();
        list.forEach(function (product) {
            var card = document.createElement('a');
            card.className = 'yq-pdp-recent__card';
            card.href = '/Products/Details/' + encodeURIComponent(product.id);

            var media = document.createElement('div');
            media.className = 'yq-pdp-recent__media';
            var img = document.createElement('img');
            img.src = product.image || '/images/placeholder-product.svg';
            img.alt = product.name || '';
            img.loading = 'lazy';
            img.decoding = 'async';
            img.addEventListener('error', function () {
                img.src = '/images/placeholder-product.svg';
            }, { once: true });
            media.appendChild(img);

            var name = document.createElement('span');
            name.className = 'yq-pdp-recent__name';
            name.textContent = product.name || '';

            var price = document.createElement('span');
            price.className = 'yq-pdp-recent__price';
            var priceNum = parseFloat(product.price);
            price.textContent = (isNaN(priceNum) ? '' : priceNum.toLocaleString('ar-SA')) + ' ر.س';

            card.appendChild(media);
            card.appendChild(name);
            card.appendChild(price);
            grid.appendChild(card);
        });

        section.hidden = false;
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
        if (!choices.length || !retailInput || !qtyInput || !priceBox) return;

        function syncRetailChoice(selected) {
            retailInput.value = selected.value || '';
            var max = parseInt(selected.dataset.max || '1', 10);
            qtyInput.max = Math.max(1, max).toString();
            if ((parseInt(qtyInput.value || '1', 10) || 1) > max) {
                qtyInput.value = Math.max(1, max);
            }

            var price = parseFloat(selected.dataset.price || '0') || 0;
            var currency = document.createElement('small');
            currency.textContent = 'ر.س';
            priceBox.replaceChildren(
                document.createTextNode(price.toLocaleString('ar-SA', { maximumFractionDigits: 0 }) + ' '),
                currency
            );

            var label = (max >= 3 && max <= 10) ? 'عبوات' : 'عبوة';
            root.querySelectorAll('.yq-available-stock-text').forEach(function (stockText) {
                stockText.textContent = max + ' ' + label;
            });
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

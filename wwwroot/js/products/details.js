/**
 * ياقوت — Products / Details page interactions
 * Quantity stepper, detail tabs, recently viewed (client-side only)
 */
(function (window, document) {
    'use strict';

    var RECENT_KEY = 'yaqut-recently-viewed';
    var MAX_RECENT = 6;

    function initQtyStepper() {
        var input = document.getElementById('yqQtyInput');
        var decreaseBtn = document.querySelector('[data-yq-qty-decrease]');
        var increaseBtn = document.querySelector('[data-yq-qty-increase]');
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

    function initTabs() {
        var tabs = document.querySelectorAll('.yq-pdp-tab');
        var panels = document.querySelectorAll('.yq-pdp-tab-panel');
        if (!tabs.length) return;

        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                var target = tab.getAttribute('data-yq-tab');

                tabs.forEach(function (t) {
                    var isActive = t === tab;
                    t.classList.toggle('is-active', isActive);
                    t.setAttribute('aria-selected', isActive ? 'true' : 'false');
                });

                panels.forEach(function (panel) {
                    var isActive = panel.getAttribute('data-yq-panel') === target;
                    panel.classList.toggle('is-active', isActive);
                    panel.hidden = !isActive;
                });
            });
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
        var section = document.getElementById('yqRecentlyViewed');
        var grid = document.getElementById('yqRecentlyViewedGrid');
        if (!section || !grid) return;

        var list = getRecentList().filter(function (p) { return p.id !== root.dataset.id; });
        if (!list.length) return;

        grid.innerHTML = '';
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

    function initRecentlyViewed() {
        var root = document.querySelector('[data-yq-recent-item]');
        if (!root) return;

        renderRecentlyViewed(root);
        trackCurrentProduct(root);
    }

    function init() {
        initQtyStepper();
        initTabs();
        initRecentlyViewed();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

/**
 * Yaqut - Cart / Index page interactions.
 * Enhances quantity updates, checkout auth guard, and remove transitions.
 */
(function (window, document) {
    'use strict';

    function toArray(list) {
        return Array.prototype.slice.call(list || []);
    }

    function normalizeDigits(value) {
        var arabic = '٠١٢٣٤٥٦٧٨٩';
        var persian = '۰۱۲۳۴۵۶۷۸۹';
        return String(value || '').replace(/[٠-٩۰-۹]/g, function (digit) {
            var arabicIndex = arabic.indexOf(digit);
            if (arabicIndex > -1) return arabicIndex;
            return persian.indexOf(digit);
        });
    }

    function quantityLabel(count) {
        if (count <= 0) return 'السلة فارغة';
        if (count === 1) return 'منتج واحد في السلة';
        if (count === 2) return 'منتجان في السلة';
        if (count <= 10) return count + ' منتجات في السلة';
        return count + ' منتجاً في السلة';
    }

    function announce(message) {
        var live = document.querySelector('[data-yq-cart-live]');
        if (!live || !message) return;
        live.textContent = '';
        window.setTimeout(function () {
            live.textContent = message;
        }, 20);
    }

    function updateHeaderBadge() {
        var badge = document.querySelector('[data-yq-cart-count]');
        if (!badge) return;

        var total = toArray(document.querySelectorAll('[data-yq-cart-page] input[name="quantity"]'))
            .reduce(function (sum, input) {
                var value = parseInt(normalizeDigits(input.value), 10);
                return sum + (Number.isFinite(value) ? value : 0);
            }, 0);

        if (total > 0) {
            badge.hidden = false;
            badge.textContent = total > 99 ? '99+' : String(total);
            badge.setAttribute('aria-label', quantityLabel(total));
        } else {
            badge.hidden = true;
            badge.textContent = '0';
            badge.setAttribute('aria-label', 'السلة فارغة');
        }
    }

    function initRemoveTransition(root) {
        root.querySelectorAll('[data-yq-cart-item]').forEach(function (item) {
            var form = item.querySelector('.yq-cart-item__remove-form');
            if (!form || form.dataset.yqRemoveBound) return;
            form.dataset.yqRemoveBound = '1';

            form.addEventListener('submit', function (event) {
                if (item.classList.contains('is-removing')) return;
                event.preventDefault();
                item.classList.add('is-removing');
                window.setTimeout(function () {
                    form.submit();
                }, 280);
            });
        });
    }

    function clampQuantity(input) {
        if (!input) return 1;
        var min = parseInt(input.getAttribute('min') || '1', 10);
        var max = parseInt(input.getAttribute('max') || '', 10);
        var value = parseInt(normalizeDigits(input.value), 10);

        if (!Number.isFinite(min) || min < 1) min = 1;
        if (!Number.isFinite(value) || value < min) value = min;
        if (Number.isFinite(max) && max >= min && value > max) value = max;

        input.value = String(value);
        return value;
    }

    function updateStepState(form) {
        var input = form.querySelector('input[name="quantity"]');
        var value = clampQuantity(input);
        var max = parseInt(input.getAttribute('max') || '', 10);
        var minus = form.querySelector('[data-yq-cart-qty-step="-1"]');
        var plus = form.querySelector('[data-yq-cart-qty-step="1"]');
        var isUpdating = form.classList.contains('is-updating');

        if (minus) minus.disabled = isUpdating || value <= 1;
        if (plus) plus.disabled = isUpdating || (Number.isFinite(max) && value >= max);
    }

    function setFormUpdating(form, isUpdating) {
        var item = form.closest('[data-yq-cart-item]');
        form.classList.toggle('is-updating', isUpdating);
        if (item) item.classList.toggle('is-updating', isUpdating);

        toArray(form.querySelectorAll('button, input[name="quantity"]')).forEach(function (control) {
            control.disabled = isUpdating;
        });

        if (!isUpdating) updateStepState(form);
    }

    function refreshCartFromHtml(html, message) {
        var doc = new window.DOMParser().parseFromString(html, 'text/html');
        var nextCart = doc.querySelector('[data-yq-cart-page]');
        var currentCart = document.querySelector('[data-yq-cart-page]');

        if (!nextCart || !currentCart) {
            window.location.reload();
            return;
        }

        currentCart.innerHTML = nextCart.innerHTML;
        updateHeaderBadge();
        initCartPage();
        announce(message || 'تم تحديث السلة.');
    }

    function submitQuantity(form, message) {
        if (!form || form.classList.contains('is-updating')) return;
        var input = form.querySelector('input[name="quantity"]');
        var nextValue = clampQuantity(input);
        var previousValue = parseInt(form.getAttribute('data-yq-last-qty') || input.defaultValue || '1', 10);
        var payload = new window.FormData(form);

        updateStepState(form);
        if (Number.isFinite(previousValue) && nextValue === previousValue) return;

        setFormUpdating(form, true);

        if (!window.fetch || !window.DOMParser) {
            form.submit();
            return;
        }

        window.fetch(form.action, {
            method: 'POST',
            body: payload,
            credentials: 'same-origin',
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        }).then(function (response) {
            if (!response.ok) throw new Error('cart update failed');
            return response.text();
        }).then(function (html) {
            refreshCartFromHtml(html, message || 'تم تحديث الكمية.');
        }).catch(function () {
            setFormUpdating(form, false);
            announce('تعذّر تحديث الكمية الآن. حاول مرة أخرى.');
        });
    }

    function initQuantityControls(root) {
        root.querySelectorAll('[data-yq-cart-qty-form]').forEach(function (form) {
            if (form.dataset.yqQtyBound) return;
            form.dataset.yqQtyBound = '1';

            var input = form.querySelector('input[name="quantity"]');
            if (!input) return;

            clampQuantity(input);
            form.setAttribute('data-yq-last-qty', input.value);
            updateStepState(form);

            form.addEventListener('submit', function (event) {
                event.preventDefault();
                submitQuantity(form, 'تم تحديث الكمية.');
            });

            form.querySelectorAll('[data-yq-cart-qty-step]').forEach(function (button) {
                button.addEventListener('click', function () {
                    if (form.classList.contains('is-updating')) return;
                    var step = parseInt(button.getAttribute('data-yq-cart-qty-step'), 10);
                    var current = clampQuantity(input);
                    input.value = String(current + (Number.isFinite(step) ? step : 0));
                    clampQuantity(input);
                    updateStepState(form);
                    submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
                });
            });

            input.addEventListener('change', function () {
                clampQuantity(input);
                updateStepState(form);
                submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
            });

            input.addEventListener('input', function () {
                if (normalizeDigits(input.value) === '0') input.value = '1';
                updateStepState(form);
            });

            input.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
                }
            });
        });
    }

    function showAuthModal(modalId) {
        if (!modalId) return;
        var modalElement = document.getElementById(modalId);
        if (!modalElement || !window.bootstrap || !window.bootstrap.Modal) return;
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    function initCheckoutGuard(root) {
        var link = root.querySelector('[data-yq-cart-checkout-link]');
        if (!link || link.dataset.yqCheckoutBound) return;
        link.dataset.yqCheckoutBound = '1';

        link.addEventListener('click', function (event) {
            if (link.getAttribute('data-yq-auth-required') !== 'true') return;
            event.preventDefault();
            showAuthModal(link.getAttribute('data-yq-auth-modal'));
            announce('سجّل دخولك أو أنشئ حساباً جديداً لإتمام الطلب.');
        });
    }

    function initCartPage() {
        var root = document.querySelector('[data-yq-cart-page]');
        if (!root) return;

        initRemoveTransition(root);
        initQuantityControls(root);
        initCheckoutGuard(root);
        updateHeaderBadge();
    }

    function init() {
        initCartPage();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

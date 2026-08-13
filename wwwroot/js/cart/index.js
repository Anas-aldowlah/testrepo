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

    function setHeaderBadge(total) {
        var badge = document.querySelector('[data-yq-cart-count]');
        if (!badge) return;

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

    function updateHeaderBadge() {
        var total = toArray(document.querySelectorAll('[data-yq-cart-page] [data-yq-cart-qty-value]'))
            .reduce(function (sum, input) {
                var value = parseInt(normalizeDigits(input.value), 10);
                return sum + (Number.isFinite(value) ? value : 0);
            }, 0);
        setHeaderBadge(total);
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
                
                // Optimistically remove from UI
                item.style.display = 'none';
                calculateAndUpdateTotals();

                // Send request in background
                var payload = new window.FormData(form);
                window.fetch(form.action, {
                    method: 'POST',
                    body: payload,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                }).then(function(res) {
                    if(!res.ok) throw new Error();
                    item.remove();
                    // If cart is empty now, we might want to reload to show empty state
                    if (document.querySelectorAll('[data-yq-cart-item]:not(.is-removing)').length === 0) {
                        window.location.reload();
                    }
                }).catch(function() {
                    item.style.display = '';
                    item.classList.remove('is-removing');
                    calculateAndUpdateTotals();
                    announce('تعذّر الحذف الآن.');
                });
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

    function setQuantityValue(form, quantity) {
        var valueInput = form.querySelector('[data-yq-cart-qty-value]');
        if (valueInput) valueInput.value = String(quantity);
    }

    function syncQuantitySelector(form, quantity) {
        var select = form.querySelector('[data-yq-cart-qty-select]');
        var customInput = form.querySelector('[data-yq-cart-qty-custom]');
        if (!select || !customInput) return;

        setQuantityValue(form, quantity);
        if (quantity <= 5) {
            select.value = String(quantity);
            select.classList.remove('d-none');
            customInput.classList.add('d-none');
        } else {
            select.value = 'custom';
            select.classList.add('d-none');
            customInput.value = String(quantity);
            customInput.classList.remove('d-none');
        }
    }

    function setFormUpdating(form, isUpdating) {
        var item = form.closest('[data-yq-cart-item]');
        var spinner = form.querySelector('[data-yq-cart-qty-spinner]');
        form.classList.toggle('is-updating', isUpdating);
        form.setAttribute('aria-busy', isUpdating ? 'true' : 'false');
        if (item) item.classList.toggle('is-updating', isUpdating);
        if (spinner) spinner.hidden = !isUpdating;
    }

    function refreshCartFromHtml(html, message) {
        var doc = new window.DOMParser().parseFromString(html, 'text/html');
        var nextCart = doc.querySelector('[data-yq-cart-page]');
        var currentCart = document.querySelector('[data-yq-cart-page]');

        if (!nextCart || !currentCart) {
            window.location.reload();
            return;
        }

        // Instead of replacing the whole innerHTML, we just update what might be out of sync
        // if we are doing optimistic updates. Actually, for a fully optimistic approach,
        // we can just silently succeed. Let's just update the badge just in case.
        updateHeaderBadge();
        
        // If we want to show a toast message:
        // announce(message || 'تم تحديث السلة.');
    }

    function calculateAndUpdateTotals() {
        var items = toArray(document.querySelectorAll('[data-yq-cart-item]'));
        var subtotal = 0;
        
        items.forEach(function(item) {
            if (item.classList.contains('is-removing')) return;
            var input = item.querySelector('[data-yq-cart-qty-value]');
            var qty = parseInt(normalizeDigits(input.value), 10) || 0;
            var priceEl = item.querySelector('.yq-cart-item__unit-price');
            // Extract numeric price from text like "250 ر.س / للقطعة"
            var priceText = priceEl ? priceEl.textContent.replace(/[^\d]/g, '') : '0';
            var price = parseFloat(priceEl ? priceEl.dataset.unitPrice : '0') || 0;
            
            var lineTotal = qty * price;
            subtotal += lineTotal;
            
            var lineTotalEl = item.querySelector('.yq-cart-item__line-total');
            if (lineTotalEl) {
                lineTotalEl.innerHTML = lineTotal.toLocaleString('en-US', { maximumFractionDigits: 0 }) + ' <small>ر.س</small>';
            }
        });

        // Update summary
        var summaryTotalEl = document.querySelector('[data-yq-summary-total]');
        if (summaryTotalEl) {
            summaryTotalEl.textContent = subtotal.toLocaleString('en-US', { maximumFractionDigits: 0 });
        }
        
        updateHeaderBadge();
    }

    function applyCartState(state, form) {
        if (!state) return;

        if (Number.isFinite(Number(state.totalQuantity))) {
            setHeaderBadge(Number(state.totalQuantity));
        }

        var summaryTotalEl = document.querySelector('[data-yq-summary-total]');
        if (summaryTotalEl && Number.isFinite(Number(state.subtotal))) {
            summaryTotalEl.textContent = Number(state.subtotal).toLocaleString('en-US', { maximumFractionDigits: 0 });
        }

        var item = form.closest('[data-yq-cart-item]');
        var lineTotalEl = item ? item.querySelector('.yq-cart-item__line-total') : null;
        if (lineTotalEl && state.item && Number.isFinite(Number(state.item.lineTotal))) {
            lineTotalEl.innerHTML = Number(state.item.lineTotal).toLocaleString('en-US', { maximumFractionDigits: 0 }) + ' <small>ر.س</small>';
        }
    }

    function submitQuantity(form, message) {
        if (!form) return;
        var valueInput = form.querySelector('[data-yq-cart-qty-value]');
        var nextValue = parseInt(normalizeDigits(valueInput ? valueInput.value : '1'), 10);
        var previousValue = parseInt(form.getAttribute('data-yq-last-qty') || '1', 10);

        if (!Number.isFinite(nextValue) || nextValue < 1) nextValue = 1;
        setQuantityValue(form, nextValue);
        calculateAndUpdateTotals();

        if (form.classList.contains('is-updating')) {
            form.setAttribute('data-yq-pending-qty', String(nextValue));
            return;
        }

        if (Number.isFinite(previousValue) && nextValue === previousValue) return;

        if (!window.fetch) {
            form.submit();
            return;
        }

        var payload = new window.FormData(form);
        form.removeAttribute('data-yq-pending-qty');
        setFormUpdating(form, true);

        window.fetch(form.action, {
            method: 'POST',
            body: payload,
            credentials: 'same-origin',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            }
        }).then(function (response) {
            return response.json().then(function (state) {
                if (!response.ok || !state.success) {
                    var error = new Error('cart update failed');
                    error.state = state;
                    throw error;
                }
                return state;
            });
        }).then(function (state) {
            var savedQuantity = parseInt(state.item ? state.item.quantity : nextValue, 10);

            if (!Number.isFinite(savedQuantity) || savedQuantity < 1) savedQuantity = nextValue;
            form.setAttribute('data-yq-last-qty', String(savedQuantity));
            var expectedInput = form.querySelector('[data-yq-cart-qty-expected]');
            if (expectedInput) expectedInput.value = String(savedQuantity);

            var pendingQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
            if (Number.isFinite(pendingQuantity) && pendingQuantity !== savedQuantity) {
                setQuantityValue(form, pendingQuantity);
                calculateAndUpdateTotals();
            } else {
                form.removeAttribute('data-yq-pending-qty');
                syncQuantitySelector(form, savedQuantity);
                applyCartState(state, form);
            }
            announce(state.message || message);
        }).catch(function (error) {
            var state = error && error.state;
            var serverQuantity = state && state.item ? parseInt(state.item.quantity, 10) : NaN;

            if (state && state.conflict && Number.isFinite(serverQuantity) && serverQuantity > 0) {
                var retryQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
                if (!Number.isFinite(retryQuantity)) retryQuantity = nextValue;
                form.setAttribute('data-yq-last-qty', String(serverQuantity));
                var expectedInput = form.querySelector('[data-yq-cart-qty-expected]');
                if (expectedInput) expectedInput.value = String(serverQuantity);
                form.setAttribute('data-yq-pending-qty', String(retryQuantity));
                setQuantityValue(form, retryQuantity);
                applyCartState(state, form);
                calculateAndUpdateTotals();
                return;
            }

            form.removeAttribute('data-yq-pending-qty');
            syncQuantitySelector(form, previousValue);
            calculateAndUpdateTotals();
            announce((state && (state.message || state.detail)) || 'تعذّر تحديث الكمية الآن. حاول مرة أخرى.');
        }).finally(function () {
            setFormUpdating(form, false);

            var pendingQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
            var savedQuantity = parseInt(form.getAttribute('data-yq-last-qty') || '1', 10);
            if (Number.isFinite(pendingQuantity)) {
                form.removeAttribute('data-yq-pending-qty');
                setQuantityValue(form, pendingQuantity);
                if (pendingQuantity !== savedQuantity) {
                    window.setTimeout(function () {
                        submitQuantity(form, message);
                    }, 0);
                }
            }
        });
    }

    function initQuantityControls(root) {
        root.querySelectorAll('[data-yq-cart-qty-form]').forEach(function (form) {
            if (form.dataset.yqQtyBound) return;
            form.dataset.yqQtyBound = '1';

            var valueInput = form.querySelector('[data-yq-cart-qty-value]');
            var select = form.querySelector('[data-yq-cart-qty-select]');
            var customInput = form.querySelector('[data-yq-cart-qty-custom]');
            if (!valueInput || !select || !customInput) return;

            var initialQuantity = parseInt(valueInput.value, 10) || 1;
            form.setAttribute('data-yq-last-qty', String(initialQuantity));
            syncQuantitySelector(form, initialQuantity);

            form.addEventListener('submit', function (event) {
                event.preventDefault();
                submitQuantity(form, 'تم تحديث الكمية.');
            });

            select.addEventListener('change', function () {
                if (select.value === 'custom') {
                    var current = parseInt(valueInput.value, 10) || 1;
                    customInput.value = String(Math.max(6, current));
                    select.classList.add('d-none');
                    customInput.classList.remove('d-none');
                    customInput.focus();
                    customInput.select();
                    return;
                }

                setQuantityValue(form, parseInt(select.value, 10) || 1);
                submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
            });

            customInput.addEventListener('blur', function () {
                var quantity = clampQuantity(customInput);
                setQuantityValue(form, quantity);
                submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
            });

            customInput.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    customInput.blur();
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
        
        // Add staggered animation delay
        root.querySelectorAll('[data-yq-cart-item]').forEach(function (item, index) {
            item.style.animationDelay = (index * 0.1) + 's';
        });
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

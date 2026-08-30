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

    function notifyCartReconciliation(authoritativeState, options) {
        window.dispatchEvent(new window.CustomEvent('yq:cart-reconcile', {
            detail: {
                authoritativeState: authoritativeState,
                options: options || {}
            }
        }));
    }

    function activateRevealState(root) {
        if (root.matches('.yq-reveal')) root.classList.add('is-visible');
        root.querySelectorAll('.yq-reveal').forEach(function (element) {
            element.classList.add('is-visible');
        });
    }

    function cartFeedback(root) {
        var alert = root ? root.querySelector('.yaqut-alert') : null;
        return {
            element: alert,
            message: alert ? alert.textContent.replace(/\s+/g, ' ').trim() : ''
        };
    }

    function isConfirmedRemoveSuccess(message) {
        return message === 'تم حذف العنصر بنجاح' || message === 'تم حذف العنصر بنجاح.';
    }

    function showOperationDialog(options) {
        if (!window.YaqutOperationDialog || typeof window.YaqutOperationDialog.show !== 'function') return;
        window.YaqutOperationDialog.show(options);
    }

    function removeFailureMessage(response) {
        var fallback = 'تعذّر حذف المنتج من السلة حالياً. يرجى المحاولة مرة أخرى.';
        return response.text().then(function (body) {
            if (!body || !body.trim()) return fallback;

            var contentType = response.headers.get('content-type') || '';
            if (contentType.toLowerCase().indexOf('json') === -1 && body.trim().charAt(0) !== '{') {
                return fallback;
            }

            try {
                var problem = JSON.parse(body);
                var detail = typeof problem.detail === 'string' ? problem.detail.trim() : '';
                return detail && /[\u0600-\u06ff]/.test(detail) ? detail : fallback;
            } catch (error) {
                return fallback;
            }
        }, function () {
            return fallback;
        });
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
                if (typeof window.fetch !== 'function') return;
                event.preventDefault();
                item.classList.add('is-removing');
                
                var payload = new window.FormData(form);
                window.fetch(form.action, {
                    method: 'POST',
                    body: payload,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                }).then(function (response) {
                    if (!response.ok) {
                        return removeFailureMessage(response).then(function (message) {
                            var error = new Error('cart remove failed');
                            error.userMessage = message;
                            throw error;
                        });
                    }
                    return response.text();
                }).then(function (html) {
                    var doc = new window.DOMParser().parseFromString(html, 'text/html');
                    var nextRoot = doc.querySelector('[data-yq-cart-page]');
                    if (!nextRoot) throw new Error('cart response invalid');
                    var feedback = cartFeedback(nextRoot);
                    activateRevealState(nextRoot);
                    notifyCartReconciliation(doc);
                    if (feedback.element) feedback.element.remove();
                    root.replaceWith(nextRoot);
                    initCartPage();

                    if (isConfirmedRemoveSuccess(feedback.message)) {
                        var successMessage = 'تم حذف المنتج من السلة بنجاح.';
                        announce(successMessage);
                        showOperationDialog({
                            title: 'تم حذف المنتج',
                            message: successMessage,
                            confirmText: 'حسنًا',
                            kind: 'success'
                        });
                    } else {
                        var resultMessage = feedback.message || 'تعذّر تأكيد نتيجة حذف المنتج من السلة.';
                        announce(resultMessage);
                        showOperationDialog({
                            title: 'لم يتم حذف المنتج',
                            message: resultMessage,
                            confirmText: 'حسنًا',
                            kind: 'info'
                        });
                    }
                }).catch(function (error) {
                    item.classList.remove('is-removing');
                    var message = error && error.userMessage
                        ? error.userMessage
                        : 'تعذّر حذف المنتج من السلة حالياً. يرجى المحاولة مرة أخرى.';
                    announce(message);
                    showOperationDialog({
                        title: 'تعذّر حذف المنتج',
                        message: message,
                        confirmText: 'حسنًا',
                        kind: 'error'
                    });
                });
            });
        });
    }

    function normalizeQuantity(input) {
        if (!input) return 1;
        var min = parseInt(input.getAttribute('min') || '1', 10);
        var value = parseInt(normalizeDigits(input.value), 10);

        if (!Number.isFinite(min) || min < 1) min = 1;
        if (!Number.isFinite(value) || value < min) value = min;

        input.value = String(value);
        return value;
    }

    function stockMessage(maximum) {
        return 'الكمية المطلوبة غير متوفرة، المتوفر حاليًا ' + maximum + ' فقط.';
    }

    function showStockFeedback(form, maximum, message) {
        var feedback = form.querySelector('[data-yq-cart-stock-feedback]');
        if (!feedback) return;
        feedback.textContent = message || stockMessage(maximum);
        feedback.hidden = false;
    }

    function clearStockFeedback(form) {
        var feedback = form.querySelector('[data-yq-cart-stock-feedback]');
        if (!feedback) return;
        feedback.textContent = '';
        feedback.hidden = true;
    }

    function setAvailabilityLimit(form, maximum) {
        if (!Number.isFinite(maximum) || maximum < 0) return;
        form.setAttribute('data-yq-cart-max-units', String(maximum));
        var presetLimit = Math.min(maximum, 10);
        form.setAttribute('data-yq-cart-preset-limit', String(presetLimit));
        var customInput = form.querySelector('[data-yq-cart-qty-custom]');
        var select = form.querySelector('[data-yq-cart-qty-select]');
        if (customInput) customInput.setAttribute('max', String(maximum));
        if (!select) return;
        select.replaceChildren();
        for (var quantity = 1; quantity <= presetLimit; quantity++) {
            var option = document.createElement('option');
            option.value = String(quantity);
            option.textContent = String(quantity);
            select.appendChild(option);
        }
        if (maximum > 10) {
            var customOption = document.createElement('option');
            customOption.value = 'custom';
            customOption.textContent = 'أكثر من 10 (10+)';
            select.appendChild(customOption);
        }
    }

    function setQuantityValue(form, quantity) {
        var valueInput = form.querySelector('[data-yq-cart-qty-value]');
        if (valueInput) valueInput.value = String(quantity);
    }

    function syncQuantitySelector(form, quantity) {
        var select = form.querySelector('[data-yq-cart-qty-select]');
        var customInput = form.querySelector('[data-yq-cart-qty-custom]');
        if (!select || !customInput) return;
        var presetLimit = parseInt(form.getAttribute('data-yq-cart-preset-limit') || '10', 10);

        setQuantityValue(form, quantity);
        if (quantity <= presetLimit) {
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

        if (state.warningCode === 'InsufficientStock' && Number.isFinite(Number(state.availableQuantity))) {
            var availableQuantity = Number(state.availableQuantity);
            setAvailabilityLimit(form, availableQuantity);
            showStockFeedback(form, availableQuantity, state.message);
        } else {
            clearStockFeedback(form);
        }

        notifyCartReconciliation(state);
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

            if (state && state.warningCode === 'InsufficientStock' && Number.isFinite(serverQuantity) && serverQuantity > 0) {
                form.removeAttribute('data-yq-pending-qty');
                form.setAttribute('data-yq-last-qty', String(serverQuantity));
                var stockExpectedInput = form.querySelector('[data-yq-cart-qty-expected]');
                if (stockExpectedInput) stockExpectedInput.value = String(serverQuantity);
                applyCartState(state, form);
                syncQuantitySelector(form, serverQuantity);
                calculateAndUpdateTotals();
                announce(state.message);
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
            setAvailabilityLimit(form, parseInt(form.getAttribute('data-yq-cart-max-units') || '', 10));
            syncQuantitySelector(form, initialQuantity);

            form.addEventListener('submit', function (event) {
                event.preventDefault();
                submitQuantity(form, 'تم تحديث الكمية.');
            });

            select.addEventListener('change', function () {
                if (select.value === 'custom') {
                    var current = parseInt(valueInput.value, 10) || 1;
                    customInput.value = String(Math.max(11, current));
                    select.classList.add('d-none');
                    customInput.classList.remove('d-none');
                    customInput.focus();
                    customInput.select();
                    return;
                }

                clearStockFeedback(form);
                setQuantityValue(form, parseInt(select.value, 10) || 1);
                submitQuantity(form, 'تم تحديث الكمية تلقائياً.');
            });

            customInput.addEventListener('blur', function () {
                var quantity = normalizeQuantity(customInput);
                var maximum = parseInt(form.getAttribute('data-yq-cart-max-units') || customInput.getAttribute('max') || '', 10);
                if (Number.isFinite(maximum) && quantity > maximum) {
                    var persistedQuantity = parseInt(form.getAttribute('data-yq-last-qty') || '1', 10);
                    showStockFeedback(form, maximum);
                    announce(stockMessage(maximum));
                    syncQuantitySelector(form, persistedQuantity);
                    calculateAndUpdateTotals();
                    return;
                }
                clearStockFeedback(form);
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

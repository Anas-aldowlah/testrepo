/**
 * ياقوت — Orders / Checkout page interactions
 */
(function (window, document) {
    'use strict';

    function initPaymentSelection() {
        var inputs = document.querySelectorAll('.yq-checkout-payment__option input[type="radio"]');
        if (!inputs.length) return;

        function syncSelection() {
            inputs.forEach(function (input) {
                var option = input.closest('.yq-checkout-payment__option');
                if (!option) return;
                option.classList.toggle('is-selected', input.checked);
            });
        }

        inputs.forEach(function (input) {
            input.addEventListener('change', syncSelection);
        });

        syncSelection();
    }

    function initPaymentPagination() {
        var list = document.querySelector('[data-yq-payment-list]');
        var options = Array.prototype.slice.call(document.querySelectorAll('[data-yq-payment-option]'));
        var nav = document.querySelector('[data-yq-payment-nav]');
        if (!list || !options.length || options.length <= 4 || !nav) return;

        var prevButton = nav.querySelector('[data-yq-payment-prev]');
        var nextButton = nav.querySelector('[data-yq-payment-next]');
        var status = nav.querySelector('[data-yq-payment-status]');
        var totalPages = Math.ceil(options.length / 4);
        var currentPage = Number(list.getAttribute('data-yq-payment-page') || 0);

        function render(direction) {
            var start = currentPage * 4;
            var end = start + 4;

            options.forEach(function (option, index) {
                var isVisible = index >= start && index < end;
                option.classList.toggle('is-page-hidden', !isVisible);
                option.classList.remove('is-slide-left', 'is-slide-right');
                if (isVisible && direction) {
                    option.classList.add(direction === 'next' ? 'is-slide-left' : 'is-slide-right');
                }
            });

            if (prevButton) prevButton.disabled = currentPage === 0;
            if (nextButton) nextButton.disabled = currentPage >= totalPages - 1;
            if (status) {
                var visibleEnd = Math.min(end, options.length);
                status.textContent = 'طرق الدفع ' + (start + 1) + '-' + visibleEnd + ' من ' + options.length;
            }
        }

        if (prevButton) {
            prevButton.addEventListener('click', function () {
                if (currentPage <= 0) return;
                currentPage -= 1;
                render('prev');
            });
        }

        if (nextButton) {
            nextButton.addEventListener('click', function () {
                if (currentPage >= totalPages - 1) return;
                currentPage += 1;
                render('next');
            });
        }

        render();
    }

    function initPlaceOrderGuard() {
        var form = document.getElementById('yqCheckoutForm');
        var buttons = [
            document.getElementById('confirmOrderBottom'),
            document.getElementById('yqPlaceOrderBtn')
        ].filter(Boolean);
        if (!form || !buttons.length) return;

        var isSubmitting = false;

        buttons.forEach(function (button) {
            button.dataset.yqOriginalHtml = button.innerHTML;
        });

        function setSubmitting(submitting) {
            isSubmitting = submitting;
            buttons.forEach(function (button) {
                button.disabled = submitting;
                button.classList.toggle('is-submitting', submitting);
                button.innerHTML = submitting
                    ? '<i class="bi bi-arrow-repeat yq-spin" aria-hidden="true"></i> جارٍ تأكيد الطلب...'
                    : button.dataset.yqOriginalHtml;
            });
        }

        form.querySelectorAll('input[type="tel"]').forEach(function (input) {
            input.addEventListener('input', function () {
                validatePhoneField(input);
            });
        });

        form.addEventListener('invalid', function () {
            setSubmitting(false);
        }, true);

        form.addEventListener('submit', function (event) {
            if (isSubmitting) {
                event.preventDefault();
                event.stopImmediatePropagation();
                return;
            }

            if (!validateForm(form)) {
                event.preventDefault();
                event.stopImmediatePropagation();
                setSubmitting(false);
                return;
            }

            var authRequired = form.getAttribute('data-yq-auth-required') === 'true';
            if (authRequired) {
                event.preventDefault();
                event.stopImmediatePropagation();
                setSubmitting(false);
                showAuthModal(form.getAttribute('data-yq-auth-modal'));
                return;
            }

            setSubmitting(true);
            try {
                window.localStorage.removeItem('yq-checkout-draft');
            } catch (error) {
                // Storage availability must never block checkout.
            }

            // If a later submit listener cancels the request, restore the controls.
            window.setTimeout(function () {
                if (event.defaultPrevented) setSubmitting(false);
            }, 0);
        });

        window.addEventListener('pageshow', function () {
            setSubmitting(false);
        });

        // Reset state if a jQuery-based AJAX submitter is introduced or enabled.
        if (window.jQuery) {
            window.jQuery(document).on('ajaxError.yqCheckout', function (_event, _xhr, settings) {
                var failedUrl = settings && settings.url
                    ? new URL(settings.url, window.location.href).href
                    : form.action;
                if (failedUrl === form.action) {
                    setSubmitting(false);
                }
            });
        }
    }

    function validateForm(form) {
        var phonesValid = true;
        form.querySelectorAll('input[type="tel"]').forEach(function (input) {
            if (!validatePhoneField(input)) phonesValid = false;
        });

        var nativeValid = typeof form.checkValidity !== 'function' || form.checkValidity();
        var jqueryValid = true;
        if (window.jQuery && window.jQuery.fn && typeof window.jQuery.fn.valid === 'function') {
            jqueryValid = window.jQuery(form).valid();
        }

        var isValid = phonesValid && nativeValid && jqueryValid;
        if (!isValid) {
            if (!nativeValid && typeof form.reportValidity === 'function') {
                form.reportValidity();
            }
            focusFirstInvalidField(form, true);
        }

        return isValid;
    }

    function validatePhoneField(input) {
        if (!input || input.disabled) return true;

        // Account details are read-only here; validate the editable recipient phone.
        if (input.readOnly) {
            input.setCustomValidity('');
            return true;
        }

        var value = String(input.value || '').trim();
        var isRecipientPhone = input.name === 'Street';
        var regex = isRecipientPhone
            ? /^7[01378][0-9]{7}$/
            : /^(?:(?:\+?967|00967))?7[01378][0-9]{7}$/;
        var message = isRecipientPhone
            ? 'رقم الجوال يجب أن يتكون من 9 أرقام ويبدأ بـ 70 أو 71 أو 73 أو 77 أو 78.'
            : 'رقم الجوال غير صالح.';
        var valid = !value || regex.test(value);

        input.setCustomValidity(valid ? '' : message);
        return valid;
    }

    function focusFirstInvalidField(form, includeNativeInvalid) {
        var firstInvalid = form.querySelector('.input-validation-error, .is-invalid, [aria-invalid="true"]');
        if (!firstInvalid && includeNativeInvalid && typeof form.querySelector === 'function') {
            firstInvalid = form.querySelector(':invalid');
        }

        if (firstInvalid && typeof firstInvalid.focus === 'function') {
            firstInvalid.focus({ preventScroll: false });
            if (typeof firstInvalid.scrollIntoView === 'function') {
                firstInvalid.scrollIntoView({ block: 'center', behavior: 'smooth' });
            }
        }
    }

    function showAuthModal(modalId) {
        if (!modalId) return;

        var modalElement = document.getElementById(modalId);
        if (!modalElement || !window.bootstrap || !window.bootstrap.Modal) return;

        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    function initAuthModalAutoShow() {
        var modalElement = document.getElementById('yqCheckoutAuthModal');
        if (!modalElement) return;

        if (modalElement.getAttribute('data-yq-show-on-load') === 'true') {
            showAuthModal(modalElement.id);
        }
    }

    function init() {
        initPaymentSelection();
        initPaymentPagination();
        initPlaceOrderGuard();
        initAuthModalAutoShow();
        focusFirstInvalidField(document.getElementById('yqCheckoutForm') || document, false);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

// === Checkout draft persistence ===
(function() {
    const form = document.getElementById('yqCheckoutForm');
    if (!form) return;

    // === Auto-save form data ===
    const STORAGE_KEY = 'yq-checkout-draft';
    const formInputs = form.querySelectorAll(
        'input:not([type="hidden"]):not([type="file"]):not([type="password"]):not([readonly]):not([disabled]), ' +
        'select:not([disabled]), textarea:not([readonly]):not([disabled])'
    );
    
    function saveFormData() {
        const data = {};
        formInputs.forEach(input => {
            if (!input.name) return;

            if (input.type === 'radio') {
                if (input.checked) data[input.name] = input.value;
                return;
            }

            if (input.type === 'checkbox') {
                if (!Array.isArray(data[input.name])) data[input.name] = [];
                if (input.checked) data[input.name].push(input.value);
                return;
            }

            data[input.name] = input.value;
        });
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(data)); } catch(e) {}
    }

    function restoreFormData() {
        try {
            const saved = localStorage.getItem(STORAGE_KEY);
            if (!saved) return;
            const data = JSON.parse(saved);
            formInputs.forEach(input => {
                if (!input.name || data[input.name] === undefined) return;

                if (input.type === 'radio') {
                    input.checked = input.value === data[input.name];
                } else if (input.type === 'checkbox') {
                    input.checked = Array.isArray(data[input.name]) && data[input.name].indexOf(input.value) !== -1;
                } else if (input.tagName === 'SELECT' || !input.value) {
                    input.value = data[input.name];
                } else {
                    return;
                }

                const eventName = input.type === 'radio' || input.type === 'checkbox' || input.tagName === 'SELECT'
                    ? 'change'
                    : 'input';
                input.dispatchEvent(new Event(eventName, { bubbles: true }));
            });
        } catch(e) {}
    }

    restoreFormData();
    setInterval(saveFormData, 30000);
    formInputs.forEach(input => input.addEventListener('change', saveFormData));
})();

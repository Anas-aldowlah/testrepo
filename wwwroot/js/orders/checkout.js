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

    function initPlaceOrderGuard() {
        var form = document.getElementById('yqCheckoutForm');
        var btn = document.getElementById('yqPlaceOrderBtn');
        if (!form || !btn) return;

        form.addEventListener('submit', function (event) {
            if (!validateForm(form)) {
                event.preventDefault();
                return;
            }

            var authRequired = form.getAttribute('data-yq-auth-required') === 'true';
            if (authRequired) {
                event.preventDefault();
                showAuthModal(form.getAttribute('data-yq-auth-modal'));
                return;
            }

            btn.disabled = true;
            btn.innerHTML = '<i class="bi bi-arrow-repeat"></i> جارِ تأكيد الطلب...';
        });
    }

    function validateForm(form) {
        if (window.jQuery && window.jQuery.fn && typeof window.jQuery.fn.valid === 'function') {
            var isValid = window.jQuery(form).valid();
            if (!isValid) {
                focusFirstInvalidField(form, false);
            }
            return isValid;
        }

        if (typeof form.checkValidity === 'function') {
            var nativeValid = form.checkValidity();
            if (!nativeValid) {
                focusFirstInvalidField(form, true);
            }
            return nativeValid;
        }

        return true;
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

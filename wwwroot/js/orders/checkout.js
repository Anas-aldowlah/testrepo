/**
 * ياقوت — Orders / Checkout page interactions
 */
(function (window, document) {
    'use strict';

    function initPaymentSelection() {
        var options = document.querySelectorAll('.yq-checkout-payment__option:not(.is-disabled)');
        options.forEach(function (option) {
            function selectOption() {
                options.forEach(function (o) { o.classList.remove('is-selected'); });
                option.classList.add('is-selected');
                var input = option.querySelector('input[type="radio"]');
                if (input) input.checked = true;
            }

            option.addEventListener('click', selectOption);

            var input = option.querySelector('input[type="radio"]');
            if (input) {
                input.addEventListener('change', selectOption);
            }
        });
    }

    function initPlaceOrderGuard() {
        var form = document.getElementById('yqCheckoutForm');
        var btn = document.getElementById('yqPlaceOrderBtn');
        if (!form || !btn) return;

        form.addEventListener('submit', function (event) {
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
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

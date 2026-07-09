/**
 * ياقوت — Orders / Checkout page interactions
 */
(function (window, document) {
    'use strict';

    function initPaymentSelection() {
        var options = document.querySelectorAll('.yq-checkout-payment__option:not(.is-disabled)');
        options.forEach(function (option) {
            option.addEventListener('click', function () {
                options.forEach(function (o) { o.classList.remove('is-selected'); });
                option.classList.add('is-selected');
                var input = option.querySelector('input[type="radio"]');
                if (input) input.checked = true;
            });
        });
    }

    function initPlaceOrderGuard() {
        var form = document.getElementById('yqCheckoutForm');
        var btn = document.getElementById('yqPlaceOrderBtn');
        if (!form || !btn) return;

        form.addEventListener('submit', function () {
            btn.disabled = true;
            btn.innerHTML = '<i class="bi bi-arrow-repeat"></i> جارِ تأكيد الطلب...';
        });
    }

    function init() {
        initPaymentSelection();
        initPlaceOrderGuard();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
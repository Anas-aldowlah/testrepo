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

// === Linked Submit Buttons ===
(function() {
    const form = document.querySelector('form[method="post"]');
    const btnTop = document.getElementById('confirmOrderBottom');
    const btnSidebar = document.querySelector('.yq-checkout-summary button[type="submit"], .yq-checkout-aside button[type="submit"]');
    const allBtns = [btnTop, btnSidebar].filter(Boolean);
    let isSubmitting = false;

    if (form) {
        form.addEventListener('submit', function(e) {
            if (isSubmitting) { e.preventDefault(); return; }
            isSubmitting = true;
            allBtns.forEach(btn => {
                btn.disabled = true;
                btn.innerHTML = '<i class="bi bi-arrow-repeat yq-spin" aria-hidden="true"></i> جاري تأكيد الطلب...';
            });
            // Clear saved data on submit
            localStorage.removeItem('yq-checkout-draft');
        });
    }

    // === Auto-save form data ===
    const STORAGE_KEY = 'yq-checkout-draft';
    const formInputs = document.querySelectorAll('.yq-checkout-form input:not([type="hidden"]):not([type="file"]), .yq-checkout-form select, .yq-checkout-form textarea');
    
    function saveFormData() {
        const data = {};
        formInputs.forEach(input => {
            if (input.name && input.type !== 'file') {
                data[input.name] = input.value;
            }
        });
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(data)); } catch(e) {}
    }

    function restoreFormData() {
        try {
            const saved = localStorage.getItem(STORAGE_KEY);
            if (!saved) return;
            const data = JSON.parse(saved);
            formInputs.forEach(input => {
                if (input.name && data[input.name] !== undefined && !input.value) {
                    input.value = data[input.name];
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                }
            });
        } catch(e) {}
    }

    restoreFormData();
    setInterval(saveFormData, 30000);
    formInputs.forEach(input => input.addEventListener('change', saveFormData));
})();

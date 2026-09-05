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
        var submitButton = document.getElementById('yqPlaceOrderBtn');
        if (!form || !submitButton) return;

        var isSubmitting = false;
        var confirmationAccepted = false;

        submitButton.dataset.yqOriginalHtml = submitButton.innerHTML;

        function setSubmitting(submitting) {
            isSubmitting = submitting;
            submitButton.classList.toggle('is-submitting', submitting);
            submitButton.innerHTML = submitting
                ? '<i class="bi bi-arrow-repeat yq-spin" aria-hidden="true"></i> جارٍ تأكيد الطلب...'
                : submitButton.dataset.yqOriginalHtml;
            submitButton.disabled = submitting;
        }

        form.querySelectorAll('input[type="tel"]').forEach(function (input) {
            input.addEventListener('input', function () {
                validatePhoneField(input);
            });
        });

        form.addEventListener('invalid', function () {
            setSubmitting(false);
        }, true);

        form.addEventListener('submit', async function (event) {
            if (isSubmitting) {
                event.preventDefault();
                event.stopImmediatePropagation();
                return;
            }

            if (confirmationAccepted) {
                setSubmitting(true);
                return;
            }

            if (!validateForm(form, true)) {
                event.preventDefault();
                event.stopImmediatePropagation();
                setSubmitting(false);
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            var confirmed = true;
            if (window.YaqutOperationDialog) {
                confirmed = await window.YaqutOperationDialog.confirm({
                    title: 'تأكيد الطلب',
                    message: 'هل تريد إرسال الطلب بهذه البيانات؟',
                    confirmText: 'تأكيد الطلب',
                    cancelText: 'مراجعة البيانات'
                });
            }
            if (!confirmed) {
                setSubmitting(false);
                return;
            }

            confirmationAccepted = true;
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit(submitButton);
            } else {
                setSubmitting(true);
                form.submit();
            }

            // If a later submit listener cancels the request, restore the controls.
            window.setTimeout(function () {
                if (event.defaultPrevented) setSubmitting(false);
            }, 0);
        });

        window.addEventListener('pageshow', function () {
            confirmationAccepted = false;
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

    function validateForm(form, showFeedback) {
        var phonesValid = true;
        form.querySelectorAll('input[type="tel"]').forEach(function (input) {
            if (!validatePhoneField(input)) phonesValid = false;
        });

        var nativeValid = typeof form.checkValidity !== 'function' || form.checkValidity();
        var jqueryValid = true;
        if (window.jQuery && window.jQuery.fn && typeof window.jQuery.fn.valid === 'function') {
            var validator = window.jQuery(form).data('validator');
            jqueryValid = showFeedback
                ? window.jQuery(form).valid()
                : !validator || typeof validator.checkForm !== 'function' || validator.checkForm();
        }

        var isValid = phonesValid && nativeValid && jqueryValid;
        if (!isValid && showFeedback) {
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

    function initInputFilters() {
        var form = document.getElementById('yqCheckoutForm');
        if (!form) return;

        var govInput = form.querySelector('[name="Governorate"]');
        var cityInput = form.querySelector('[name="City"]');
        var distInput = form.querySelector('[name="District"]');
        var streetInput = form.querySelector('[name="Street"]');

        if (govInput) {
            govInput.addEventListener('input', function () {
                this.value = this.value.replace(/[0-9]/g, '');
                if (typeof window.updateCities === 'function') window.updateCities();
            });
        }
        if (cityInput) {
            cityInput.addEventListener('input', function () {
                this.value = this.value.replace(/[0-9]/g, '');
            });
        }
        if (distInput) {
            distInput.addEventListener('input', function () {
                this.value = this.value.replace(/[0-9]/g, '');
            });
        }
        if (streetInput) {
            streetInput.addEventListener('input', function () {
                this.value = this.value.replace(/[^0-9]/g, '');
            });
        }
    }

    function initReceiptUploadFeedback() {
        var input = document.getElementById('yqReceiptInput');
        var feedback = document.getElementById('yqReceiptSelectedFeedback');
        var fileNameElem = document.getElementById('yqReceiptFileName');
        var fileSizeElem = document.getElementById('yqReceiptFileSize');
        var clearBtn = document.getElementById('yqReceiptClearBtn');
        var errorElem = document.getElementById('yqReceiptError');
        if (!input || !feedback || !fileNameElem || !fileSizeElem || !clearBtn) return;

        var box = input.closest('.yq-receipt-box');
        var MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
        var ALLOWED_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp'];

        function formatFileSize(bytes) {
            if (bytes < 1024) return bytes + ' بايت';
            if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' كيلوبايت';
            return (bytes / (1024 * 1024)).toFixed(1) + ' ميجابايت';
        }

        function isExtensionAllowed(fileName) {
            var lower = String(fileName || '').toLowerCase();
            return ALLOWED_EXTENSIONS.some(function (ext) {
                return lower.endsWith(ext);
            });
        }

        function clearFile(clearErrorMessage) {
            input.value = '';
            feedback.hidden = true;
            if (box) box.classList.remove('has-file');
            fileNameElem.textContent = '';
            fileSizeElem.textContent = '';
            if (clearErrorMessage && errorElem) {
                errorElem.textContent = '';
            }
        }

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (!file) {
                clearFile(false);
                return;
            }

            if (!isExtensionAllowed(file.name)) {
                clearFile(false);
                if (errorElem) {
                    errorElem.textContent = 'صيغة الملف غير مدعومة. يرجى اختيار صورة بصيغة JPG أو PNG أو WEBP.';
                }
                return;
            }

            if (file.size > MAX_FILE_SIZE) {
                clearFile(false);
                if (errorElem) {
                    errorElem.textContent = 'حجم صورة الإيصال يجب ألا يتجاوز 5 ميجابايت.';
                }
                return;
            }

            if (errorElem) {
                errorElem.textContent = '';
            }

            fileNameElem.textContent = file.name;
            fileSizeElem.textContent = formatFileSize(file.size);
            feedback.hidden = false;
            if (box) box.classList.add('has-file');
        });

        clearBtn.addEventListener('click', function (event) {
            event.preventDefault();
            clearFile(true);
            input.focus();
        });
    }

    function initSummaryCollapse() {
        var summary = document.getElementById('yqCheckoutSummary');
        var toggle = document.getElementById('yqCheckoutSummaryToggle');
        var body = document.getElementById('yqCheckoutSummaryBody');
        if (!summary || !toggle || !body) return;

        var toggleText = toggle.querySelector('.yq-checkout-summary__toggle-text');
        var isMobileOrTablet = function () {
            return window.matchMedia ? window.matchMedia('(max-width: 991.98px)').matches : window.innerWidth < 992;
        };

        function setExpanded(expanded) {
            toggle.setAttribute('aria-expanded', String(expanded));
            body.hidden = !expanded;
            summary.classList.toggle('is-expanded', expanded);
            if (toggleText) {
                toggleText.textContent = expanded ? 'إخفاء التفاصيل' : 'عرض التفاصيل';
            }
            toggle.setAttribute('aria-label', expanded ? 'إخفاء تفاصيل ملخص الطلب' : 'عرض تفاصيل ملخص الطلب');
        }

        // Default state: collapsed on mobile/tablet (<992px), open on desktop (>=992px)
        if (isMobileOrTablet()) {
            setExpanded(false);
        } else {
            setExpanded(true);
        }

        toggle.addEventListener('click', function (event) {
            if (!isMobileOrTablet()) return;
            event.preventDefault();
            var currentlyExpanded = toggle.getAttribute('aria-expanded') === 'true';
            setExpanded(!currentlyExpanded);
        });

        var mql = window.matchMedia ? window.matchMedia('(max-width: 991.98px)') : null;
        if (mql && typeof mql.addEventListener === 'function') {
            mql.addEventListener('change', function (e) {
                if (!e.matches) {
                    setExpanded(true);
                }
            });
        } else {
            window.addEventListener('resize', function () {
                if (!isMobileOrTablet()) {
                    setExpanded(true);
                }
            });
        }
    }

    function init() {
        initSummaryCollapse();
        initInputFilters();
        initPaymentSelection();
        initPaymentPagination();
        initReceiptUploadFeedback();
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

// === Checkout draft recovery UI ===
(function (window, document) {
    'use strict';

    const form = document.getElementById('yqCheckoutForm');
    const draftStorage = window.YaqutCheckoutDraft;
    if (!form || !draftStorage) return;

    const userId = form.getAttribute('data-yq-checkout-user');
    const recovery = document.querySelector('[data-yq-checkout-draft-recovery]');
    const restoreButton = recovery && recovery.querySelector('[data-yq-checkout-draft-restore]');
    const freshButton = recovery && recovery.querySelector('[data-yq-checkout-draft-fresh]');
    const draftIdInput = form.querySelector('[data-yq-checkout-draft-id]');
    const fields = draftStorage.allowedFields.map(name => form.elements.namedItem(name)).filter(Boolean);
    let recoveryPending = false;
    let saveTimer = 0;
    const draft = draftStorage.read(userId);
    let draftId = draft ? draft.draftId : draftStorage.createDraftId();

    function syncDraftIdInput() {
        if (draftIdInput) draftIdInput.value = draftId || '';
    }

    function fieldValues() {
        const values = {};
        fields.forEach(field => { values[field.name] = String(field.value || ''); });
        return values;
    }

    function hasDeliveryValues() {
        return fields.some(field => String(field.value || '').trim().length > 0);
    }

    function saveFormData() {
        if (recoveryPending) return;
        if (!hasDeliveryValues()) {
            draftStorage.clear();
            return;
        }
        if (!draftId) draftId = draftStorage.createDraftId();
        syncDraftIdInput();
        draftStorage.write(userId, draftId, fieldValues());
    }

    function scheduleSave() {
        window.clearTimeout(saveTimer);
        saveTimer = window.setTimeout(saveFormData, 500);
    }

    function hideRecovery() {
        recoveryPending = false;
        if (recovery) recovery.hidden = true;
    }

    function showRecovery() {
        recoveryPending = true;
        if (recovery) recovery.hidden = false;
    }

    function restoreFormData() {
        const record = draftStorage.read(userId);
        if (!record) {
            hideRecovery();
            return;
        }

        fields.forEach(field => {
            field.value = record.fields[field.name];
            field.dispatchEvent(new Event('input', { bubbles: true }));
            field.dispatchEvent(new Event('change', { bubbles: true }));
        });
        hideRecovery();
        if (typeof window.updateCities === 'function') window.updateCities();
    }

    function startFresh() {
        draftStorage.clear();
        draftId = draftStorage.createDraftId();
        syncDraftIdInput();
        hideRecovery();
    }

    syncDraftIdInput();
    if (draft && !hasDeliveryValues() && recovery && restoreButton && freshButton) showRecovery();

    fields.forEach(field => {
        field.addEventListener('input', scheduleSave);
        field.addEventListener('change', scheduleSave);
    });
    if (restoreButton) restoreButton.addEventListener('click', restoreFormData);
    if (freshButton) freshButton.addEventListener('click', startFresh);

    form.addEventListener('submit', saveFormData, true);
    window.addEventListener('pagehide', saveFormData);
    window.addEventListener('storage', function (event) {
        if (event.key === draftStorage.key && !event.newValue) hideRecovery();
    });
})(window, document);

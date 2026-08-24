(function () {
    'use strict';

    const editForm = document.querySelector('[data-product-edit-form]');
    const stockUnit = document.querySelector('[data-stock-unit]');
    const retailToggle = document.getElementById('Product_IsRetailEnabled');
    const retailPrices = document.querySelector('[data-retail-prices]');
    const retailList = document.querySelector('[data-retail-price-list]');
    const addRetailButton = document.querySelector('[data-add-retail-price]');
    const volumeField = document.querySelector('[data-volume-field]');
    const volumeInput = document.getElementById('Product_VolumeMl');
    const retailGroupValidation = document.querySelector('[data-retail-group-validation]');
    const editSaveButton = editForm?.querySelector('[data-edit-save]');
    const retailPriceRequiredMessage = 'يجب إضافة سعر تجزئة واحد على الأقل عند تفعيل البيع بالتجزئة.';
    let retailIndex = retailList?.querySelectorAll('.yq-retail-price-row').length ?? 0;

    function syncProductFields() {
        const isMl = stockUnit?.value === 'Ml';
        if (volumeField) volumeField.hidden = !isMl;
        if (retailToggle) {
            retailToggle.closest('.yq-retail-toggle').hidden = !isMl;
            if (!isMl) retailToggle.checked = false;
        }
        if (retailPrices) retailPrices.hidden = !(isMl && retailToggle?.checked);
    }

    function refreshUnobtrusiveValidation() {
        const jQuery = window.jQuery;
        if (!editForm || !jQuery?.validator?.unobtrusive) return;

        const form = jQuery(editForm);
        form.data('validator')?.destroy();
        form.removeData('unobtrusiveValidation');
        jQuery.validator.unobtrusive.parse(editForm);
    }

    function addRetailRow() {
        if (!retailList) return;

        const prefix = `Product.RetailPrices[${retailIndex}]`;
        const sizeName = `${prefix}.SizeMl`;
        const priceName = `${prefix}.Price`;
        const row = document.createElement('div');
        row.className = 'yq-retail-price-row';
        row.innerHTML = `
            <div class="yq-retail-controls">
                <div class="yaqut-form-group yq-product-field">
                    <label class="yaqut-form-label" for="Product_RetailPrices_${retailIndex}__SizeMl">الحجم ml</label>
                    <input id="Product_RetailPrices_${retailIndex}__SizeMl" name="${sizeName}" type="number" min="1" required
                           class="yaqut-form-input" data-val="true" data-val-required="حجم التجزئة مطلوب."
                           data-val-range="يجب أن يكون حجم التجزئة أكبر من صفر." data-val-range-min="1" data-val-range-max="1000000">
                    <span class="yq-product-validation field-validation-valid" data-valmsg-for="${sizeName}" data-valmsg-replace="true"></span>
                </div>
                <div class="yaqut-form-group yq-product-field">
                    <label class="yaqut-form-label" for="Product_RetailPrices_${retailIndex}__Price">السعر</label>
                    <input id="Product_RetailPrices_${retailIndex}__Price" name="${priceName}" type="number" step="0.01" min="0.01" required
                           class="yaqut-form-input" data-val="true" data-val-required="سعر التجزئة مطلوب."
                           data-val-range="يجب أن يكون سعر التجزئة أكبر من صفر." data-val-range-min="0.01" data-val-range-max="1000000">
                    <span class="yq-product-validation field-validation-valid" data-valmsg-for="${priceName}" data-valmsg-replace="true"></span>
                </div>
                <div class="yq-retail-active-field">
                    <span class="yaqut-form-label">الحالة</span>
                    <label class="yq-retail-active"><input type="checkbox" name="${prefix}.IsActive" value="true" checked> <span data-retail-active-label>نشط</span></label>
                    <input type="hidden" name="${prefix}.IsActive" value="false">
                </div>
                <div class="yq-retail-action-field">
                    <span class="yaqut-form-label">الإجراء</span>
                    <button type="button" class="yq-retail-remove" data-remove-retail-price aria-label="حذف سعر التجزئة">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </div>`;
        retailList.appendChild(row);
        retailIndex += 1;

        refreshUnobtrusiveValidation();
        syncRetailSaveState();
    }

    function reindexRetailRows() {
        if (!retailList) return;

        const rows = retailList.querySelectorAll('.yq-retail-price-row');
        rows.forEach((row, index) => {
            row.querySelectorAll('[name]').forEach((input) => {
                input.name = input.name.replace(/Product\.RetailPrices\[\d+\]/, `Product.RetailPrices[${index}]`);
                if (!input.id) return;
                const fieldName = input.name.slice(input.name.lastIndexOf('.') + 1);
                const oldId = input.id;
                input.id = `Product_RetailPrices_${index}__${fieldName}`;
                row.querySelector(`label[for="${CSS.escape(oldId)}"]`)?.setAttribute('for', input.id);
            });
            row.querySelectorAll('[data-valmsg-for]').forEach((message) => {
                message.dataset.valmsgFor = message.dataset.valmsgFor.replace(
                    /Product\.RetailPrices\[\d+\]/,
                    `Product.RetailPrices[${index}]`
                );
            });
        });
        retailIndex = rows.length;
        refreshUnobtrusiveValidation();
    }

    function showFieldError(input, message) {
        const messageElement = input?.form?.querySelector(`[data-valmsg-for="${CSS.escape(input.name)}"]`);
        input?.classList.toggle('input-validation-error', Boolean(message));
        if (!messageElement) return;
        messageElement.textContent = message;
        messageElement.classList.toggle('field-validation-error', Boolean(message));
        messageElement.classList.toggle('field-validation-valid', !message);
    }

    function showRetailGroupError(message) {
        if (!retailGroupValidation) return;
        retailGroupValidation.textContent = message;
        retailGroupValidation.classList.toggle('field-validation-error', Boolean(message));
        retailGroupValidation.classList.toggle('field-validation-valid', !message);
    }

    function evaluateRetailRows(showErrors) {
        if (stockUnit?.value !== 'Ml' || !retailToggle?.checked || !retailList) {
            if (showErrors) showRetailGroupError('');
            return { allRowsValid: true, validRows: 0, firstInvalid: null };
        }

        const baseSize = Number(volumeInput?.value);
        const hasValidBaseSize = Number.isFinite(baseSize) && baseSize > 0;
        const rows = [...retailList.querySelectorAll('.yq-retail-price-row')];
        const sizeCounts = new Map();
        rows.forEach((row) => {
            const size = Number(row.querySelector('input[name$=".SizeMl"]')?.value);
            if (Number.isFinite(size) && size > 0) sizeCounts.set(size, (sizeCounts.get(size) ?? 0) + 1);
        });
        let firstInvalid = null;
        let validRows = 0;

        rows.forEach((row) => {
            const sizeInput = row.querySelector('input[name$=".SizeMl"]');
            const priceInput = row.querySelector('input[name$=".Price"]');
            const sizeValue = sizeInput?.value.trim() ?? '';
            const priceValue = priceInput?.value.trim() ?? '';
            const size = Number(sizeValue);
            const price = Number(priceValue);
            let sizeError = '';
            let priceError = '';

            if (!sizeValue) sizeError = 'حجم التجزئة مطلوب.';
            else if (!Number.isFinite(size) || size <= 0) sizeError = 'يجب أن يكون حجم التجزئة أكبر من صفر.';
            else if (size > 1000000) sizeError = 'يجب أن يكون حجم التجزئة بين 1 و1,000,000 مل.';
            else if (!hasValidBaseSize || size >= baseSize) sizeError = 'يجب أن يكون حجم التجزئة أصغر من حجم العبوة الأساسي.';
            else if ((sizeCounts.get(size) ?? 0) > 1) sizeError = 'لا يمكن تكرار حجم التجزئة للمنتج نفسه.';

            if (!priceValue) priceError = 'سعر التجزئة مطلوب.';
            else if (!Number.isFinite(price) || price <= 0) priceError = 'يجب أن يكون سعر التجزئة أكبر من صفر.';
            else if (price > 1000000) priceError = 'يجب أن يكون سعر التجزئة بين 0.01 و1,000,000.';

            const isActive = row.querySelector('input[name$=".IsActive"][type="checkbox"]')?.checked !== false;

            if (showErrors) {
                showFieldError(sizeInput, sizeError);
                showFieldError(priceInput, priceError);
            }
            firstInvalid ??= sizeError ? sizeInput : priceError ? priceInput : null;
            if (!sizeError && !priceError && isActive) validRows += 1;
        });

        const groupError = validRows === 0 ? retailPriceRequiredMessage : '';
        if (showErrors) showRetailGroupError(groupError);
        return { allRowsValid: firstInvalid === null && !groupError, validRows, firstInvalid };
    }

    function validateRetailRows() {
        const result = evaluateRetailRows(true);
        const groupError = result.validRows === 0 && stockUnit?.value === 'Ml' && retailToggle?.checked;
        const focusTarget = result.firstInvalid ?? (groupError ? retailGroupValidation : null);
        focusTarget?.focus({ preventScroll: true });
        focusTarget?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        return result.allRowsValid;
    }

    function syncRetailSaveState() {
        if (!editSaveButton) return;
        const retailNeedsRow = stockUnit?.value === 'Ml' && retailToggle?.checked;
        const result = evaluateRetailRows(false);
        editSaveButton.disabled = Boolean(retailNeedsRow && result.validRows === 0);
        showRetailGroupError(retailNeedsRow && result.validRows === 0 ? retailPriceRequiredMessage : '');
    }

    function refreshRetailValidation() {
        evaluateRetailRows(true);
        syncRetailSaveState();
    }

    function syncActiveRowState(input) {
        if (input.type !== 'checkbox') return;
        const row = input.closest('.yq-retail-price-row');
        row?.classList.toggle('is-inactive', !input.checked);
        const label = row?.querySelector('[data-retail-active-label]');
        if (label) label.textContent = input.checked ? 'نشط' : 'غير نشط';
    }

    stockUnit?.addEventListener('change', function () {
        syncProductFields();
        refreshRetailValidation();
    });
    retailToggle?.addEventListener('change', function () {
        syncProductFields();
        refreshRetailValidation();
    });
    volumeInput?.addEventListener('input', refreshRetailValidation);
    addRetailButton?.addEventListener('click', addRetailRow);
    retailList?.addEventListener('input', refreshRetailValidation);
    retailList?.addEventListener('change', function (event) {
        if (event.target.matches('input[name$=".IsActive"]')) syncActiveRowState(event.target);
        refreshRetailValidation();
    });
    retailList?.addEventListener('click', function (event) {
        const removeButton = event.target.closest('[data-remove-retail-price]');
        if (!removeButton) return;
        removeButton.closest('.yq-retail-price-row')?.remove();
        reindexRetailRows();
        if (retailToggle?.checked) validateRetailRows();
        syncRetailSaveState();
    });
    editForm?.addEventListener('submit', function (event) {
        reindexRetailRows();
        if (!validateRetailRows()) event.preventDefault();
    });
    syncProductFields();
    refreshUnobtrusiveValidation();
    retailList?.querySelectorAll('input[name$=".IsActive"]').forEach(syncActiveRowState);
    syncRetailSaveState();

    const firstServerError = editForm?.querySelector('.field-validation-error');
    if (firstServerError) {
        const owningField = firstServerError.closest('.yq-product-field');
        const target = owningField?.querySelector('input, select, textarea') ?? firstServerError;
        target.focus({ preventScroll: true });
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    const adjustmentForm = document.querySelector('[data-stock-adjustment]');
    if (!adjustmentForm) return;

    const operationInput = adjustmentForm.querySelector('[data-adjustment-operation]');
    const sizeInput = adjustmentForm.querySelector('[data-adjustment-size]');
    const customSizeField = adjustmentForm.querySelector('[data-custom-size-field]');
    const customSizeInput = adjustmentForm.querySelector('[data-custom-size]');
    const quantityInput = adjustmentForm.querySelector('[data-adjustment-quantity]');
    const calculationOutput = adjustmentForm.querySelector('[data-preview-calculation]');
    const changeOutput = adjustmentForm.querySelector('[data-preview-change]');
    const balanceOutput = adjustmentForm.querySelector('[data-preview-balance]');
    const currentStock = Number(adjustmentForm.dataset.currentStock);
    const isMl = adjustmentForm.dataset.isMl === 'true';
    const baseSize = isMl ? Number(adjustmentForm.dataset.baseSize) : 1;
    const numberFormatter = new Intl.NumberFormat('ar', { maximumFractionDigits: 3 });

    function updateStockPreview() {
        const isCustom = sizeInput?.value === 'Custom';
        if (customSizeField) customSizeField.hidden = !isCustom;
        if (customSizeInput) customSizeInput.required = isCustom;

        const selectedSize = isMl
            ? isCustom ? Number(customSizeInput?.value) : Number(sizeInput?.selectedOptions[0]?.dataset.sizeMl)
            : 1;
        const quantity = Number(quantityInput?.value);
        const valid = Number.isFinite(selectedSize) && selectedSize > 0 && Number.isInteger(quantity) && quantity > 0;

        if (!valid || !Number.isFinite(currentStock) || baseSize <= 0) {
            calculationOutput.textContent = '—';
            changeOutput.textContent = '—';
            balanceOutput.textContent = '—';
            return;
        }

        const netChange = selectedSize * quantity;
        const isSubtract = operationInput?.value === 'Subtract';
        const signedChange = isSubtract ? -netChange : netChange;
        const expectedStock = currentStock + signedChange;
        const unitLabel = isMl ? 'مل' : 'قطعة';
        const packageLabel = isMl ? 'عبوة أساسية' : 'قطعة';

        calculationOutput.textContent = `${numberFormatter.format(selectedSize)} ${unitLabel} × ${numberFormatter.format(quantity)}`;
        changeOutput.textContent = `${isSubtract ? '−' : '+'}${numberFormatter.format(netChange / baseSize)} ${packageLabel}`;
        balanceOutput.textContent = expectedStock < 0
            ? 'الرصيد غير كافٍ'
            : `${numberFormatter.format(expectedStock / baseSize)} ${packageLabel}`;
    }

    operationInput?.addEventListener('change', updateStockPreview);
    sizeInput?.addEventListener('change', updateStockPreview);
    customSizeInput?.addEventListener('input', updateStockPreview);
    quantityInput?.addEventListener('input', updateStockPreview);
    updateStockPreview();
}());

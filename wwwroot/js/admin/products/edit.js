(function () {
    'use strict';

    const editForm = document.querySelector('form[action$="/Edit"]');
    const stockUnit = document.querySelector('[data-stock-unit]');
    const retailToggle = document.getElementById('Product_IsRetailEnabled');
    const retailPrices = document.querySelector('[data-retail-prices]');
    const retailList = document.querySelector('[data-retail-price-list]');
    const addRetailButton = document.querySelector('[data-add-retail-price]');
    const volumeField = document.querySelector('[data-volume-field]');
    const volumeInput = document.getElementById('Product_VolumeMl');
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

    function addRetailRow() {
        if (!retailList) return;

        const prefix = `Product.RetailPrices[${retailIndex}]`;
        const sizeName = `${prefix}.SizeMl`;
        const priceName = `${prefix}.Price`;
        const row = document.createElement('div');
        row.className = 'yq-retail-price-row';
        row.innerHTML = `
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
                <label class="yq-retail-active"><input type="checkbox" name="${prefix}.IsActive" value="true" checked> نشط</label>
                <input type="hidden" name="${prefix}.IsActive" value="false">
            </div>
            <button type="button" class="yq-retail-remove" data-remove-retail-price aria-label="حذف سعر التجزئة">
                <i class="bi bi-trash"></i>
            </button>`;
        retailList.appendChild(row);
        retailIndex += 1;

        if (window.jQuery?.validator?.unobtrusive) {
            window.jQuery.validator.unobtrusive.parse(row);
        }
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
    }

    function showFieldError(input, message) {
        const messageElement = input?.form?.querySelector(`[data-valmsg-for="${CSS.escape(input.name)}"]`);
        input?.classList.toggle('input-validation-error', Boolean(message));
        if (!messageElement) return;
        messageElement.textContent = message;
        messageElement.classList.toggle('field-validation-error', Boolean(message));
        messageElement.classList.toggle('field-validation-valid', !message);
    }

    function validateRetailRows() {
        if (stockUnit?.value !== 'Ml' || !retailToggle?.checked || !retailList) return true;

        const baseSize = Number(volumeInput?.value);
        const seenSizes = new Set();
        let firstInvalid = null;

        retailList.querySelectorAll('.yq-retail-price-row').forEach((row) => {
            const sizeInput = row.querySelector('input[name$=".SizeMl"]');
            const priceInput = row.querySelector('input[name$=".Price"]');
            const size = Number(sizeInput?.value);
            const price = Number(priceInput?.value);
            let sizeError = '';
            let priceError = '';

            if (!Number.isFinite(size) || size <= 0) sizeError = 'حجم التجزئة مطلوب ويجب أن يكون أكبر من صفر.';
            else if (Number.isFinite(baseSize) && baseSize > 0 && size >= baseSize) sizeError = 'يجب أن يكون حجم التجزئة أصغر من حجم العبوة الأساسي.';
            else if (seenSizes.has(size)) sizeError = 'لا يمكن تكرار حجم التجزئة للمنتج نفسه.';
            else seenSizes.add(size);

            if (!Number.isFinite(price) || price <= 0) priceError = 'سعر التجزئة مطلوب ويجب أن يكون أكبر من صفر.';

            showFieldError(sizeInput, sizeError);
            showFieldError(priceInput, priceError);
            firstInvalid ??= sizeError ? sizeInput : priceError ? priceInput : null;
        });

        firstInvalid?.focus();
        return firstInvalid === null;
    }

    stockUnit?.addEventListener('change', syncProductFields);
    retailToggle?.addEventListener('change', syncProductFields);
    addRetailButton?.addEventListener('click', addRetailRow);
    retailList?.addEventListener('click', function (event) {
        const removeButton = event.target.closest('[data-remove-retail-price]');
        if (!removeButton) return;
        removeButton.closest('.yq-retail-price-row')?.remove();
        reindexRetailRows();
    });
    editForm?.addEventListener('submit', function (event) {
        reindexRetailRows();
        if (!validateRetailRows()) event.preventDefault();
    });
    syncProductFields();

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

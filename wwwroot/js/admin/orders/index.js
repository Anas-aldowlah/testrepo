(function () {
    'use strict';

    const root = document.querySelector('[data-yq-orders-page]');
    if (!root) return;
    if (root.dataset.yqOrdersInitialized === 'true') return;
    root.dataset.yqOrdersInitialized = 'true';

    const updateStatusUrl = root.dataset.yqUpdateStatusUrl;
    const popoverTriggerList = Array.from(root.querySelectorAll('[data-bs-toggle="popover"]'));

    function createPopoverTitle(iconClass, text) {
        const title = document.createElement('span');
        const icon = document.createElement('i');
        icon.className = iconClass;
        icon.setAttribute('aria-hidden', 'true');
        title.append(icon, document.createTextNode(' ' + text));
        return title;
    }

    function appendPopoverRow(card, label, value, direction) {
        const row = document.createElement('div');
        row.className = 'yq-popover-row';
        const labelElement = document.createElement('span');
        labelElement.textContent = label;
        const valueElement = document.createElement('strong');
        valueElement.textContent = value == null ? '' : String(value);
        if (direction) valueElement.dir = direction;
        row.append(labelElement, document.createTextNode(' '), valueElement);
        card.appendChild(row);
    }

    function appendPopoverLink(card, href, text) {
        const footer = document.createElement('div');
        footer.className = 'yq-popover-footer';
        const link = document.createElement('a');
        link.className = 'yq-popover-link';
        link.href = href;
        const icon = document.createElement('i');
        icon.className = 'bi bi-box-arrow-up-right';
        icon.setAttribute('aria-hidden', 'true');
        link.append(icon, document.createTextNode(' ' + text));
        footer.appendChild(link);
        card.appendChild(footer);
    }

    function buildPopoverContent(trigger) {
        const type = trigger.dataset.yqPopoverType;
        if (type === 'receipt') {
            const imageSrc = trigger.dataset.yqReceiptImage;
            if (!imageSrc) {
                const empty = document.createElement('div');
                empty.className = 'yq-popover-empty';
                const icon = document.createElement('i');
                icon.className = 'bi bi-image-alt';
                icon.setAttribute('aria-hidden', 'true');
                const text = document.createElement('span');
                text.textContent = 'لا يوجد إيصال مرفق لهذا الطلب';
                empty.append(icon, text);
                return empty;
            }

            const receipt = document.createElement('div');
            receipt.className = 'yq-popover-receipt';
            const thumb = document.createElement('div');
            thumb.className = 'yq-popover-thumb';
            const image = document.createElement('img');
            image.src = imageSrc;
            image.alt = 'إيصال الدفع';
            thumb.appendChild(image);

            const actions = document.createElement('div');
            actions.className = 'yq-popover-actions';
            const zoomButton = document.createElement('button');
            zoomButton.type = 'button';
            zoomButton.className = 'yq-popover-btn js-zoom-receipt';
            zoomButton.id = 'btn-receipt-' + trigger.dataset.yqOrderId;
            const zoomIcon = document.createElement('i');
            zoomIcon.className = 'bi bi-zoom-in';
            zoomIcon.setAttribute('aria-hidden', 'true');
            zoomButton.append(zoomIcon, document.createTextNode(' عرض الصورة مكبرة'));

            const openLink = document.createElement('a');
            openLink.href = imageSrc;
            openLink.target = '_blank';
            openLink.rel = 'noopener';
            openLink.className = 'yq-popover-btn yq-popover-btn--outline';
            const openIcon = document.createElement('i');
            openIcon.className = 'bi bi-box-arrow-up-right';
            openIcon.setAttribute('aria-hidden', 'true');
            openLink.append(openIcon, document.createTextNode(' فتح في تبويب جديد'));
            actions.append(zoomButton, openLink);
            receipt.append(thumb, actions);
            return receipt;
        }

        const card = document.createElement('div');
        card.className = 'yq-popover-card';
        if (type === 'payment') {
            appendPopoverRow(card, 'وسيلة الدفع:', trigger.dataset.yqPaymentMethod);
            appendPopoverRow(card, 'حالة الطلب:', trigger.dataset.yqStatusLabel);
            appendPopoverLink(card, trigger.dataset.yqDetailsUrl, 'عرض تفاصيل الدفع الكاملة');
            return card;
        }

        appendPopoverRow(card, 'اسم العميل:', trigger.dataset.yqCustomerName);
        appendPopoverRow(card, 'اسم المستلم:', trigger.dataset.yqRecipientName);
        appendPopoverRow(card, 'رقم الجوال:', trigger.dataset.yqRecipientPhone, 'ltr');
        appendPopoverRow(card, 'المحافظة:', trigger.dataset.yqGovernorate);
        appendPopoverRow(card, 'المنطقة:', trigger.dataset.yqRegion);
        appendPopoverLink(card, trigger.dataset.yqDetailsUrl, 'فتح التفاصيل الكاملة');
        return card;
    }

    popoverTriggerList.forEach(function (element) {
        const type = element.dataset.yqPopoverType;
        const isReceipt = type === 'receipt';
        const isPayment = type === 'payment';
        new bootstrap.Popover(element, {
            container: 'body',
            html: true,
            title: function () {
                return createPopoverTitle(
                    isReceipt ? 'bi bi-receipt-cutoff' : isPayment ? 'bi bi-credit-card-fill' : 'bi bi-geo-alt-fill',
                    isReceipt ? 'إيصال الدفع' : isPayment ? 'معلومات الدفع' : 'معلومات التوصيل والمستلم'
                );
            },
            content: function () { return buildPopoverContent(element); }
        });
    });

    document.addEventListener('click', function (event) {
        popoverTriggerList.forEach(function (element) {
            const instance = bootstrap.Popover.getInstance(element);
            if (instance && !element.contains(event.target)) {
                const openPopover = document.querySelector('.popover.show');
                if (openPopover && !openPopover.contains(event.target)) instance.hide();
            }
        });

        const button = event.target.closest('.popover.show .js-zoom-receipt');
        if (!button) return;

        const orderId = button.id.replace('btn-receipt-', '');
        const link = button.nextElementSibling;
        const imageSrc = link ? link.getAttribute('href') : '';
        if (imageSrc) openReceiptModal(imageSrc, orderId);
    });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') return;
        popoverTriggerList.forEach(function (element) {
            const instance = bootstrap.Popover.getInstance(element);
            if (instance) instance.hide();
        });
    });

    const statusLabels = {
        Pending: 'قيد الانتظار',
        Processed: 'تم الدفع',
        Shipped: 'تم الشحن',
        Delivered: 'تم التوصيل',
        Cancelled: 'ملغي',
        Refunded: 'مرتجع'
    };
    const statusClasses = {
        Pending: 'yq-orders-badge--pending',
        Processed: 'yq-orders-badge--processed',
        Shipped: 'yq-orders-badge--shipped',
        Delivered: 'yq-orders-badge--delivered',
        Cancelled: 'yq-orders-badge--cancelled',
        Refunded: 'yq-orders-badge--cancelled'
    };

    root.querySelectorAll('.yq-ajax-status-form select').forEach(function (select) {
        select.addEventListener('change', async function () {
            const form = this.closest('.yq-ajax-status-form');
            const orderId = form.dataset.orderId;
            const newStatus = this.value;
            const token = form.querySelector('input[name="__RequestVerificationToken"]').value;

            this.disabled = true;
            this.style.opacity = '0.5';

            try {
                const response = await fetch(updateStatusUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest',
                        'RequestVerificationToken': token
                    },
                    body: `id=${orderId}&status=${encodeURIComponent(newStatus)}`
                });
                const data = await window.YaqutAdminAjax.readJson(response);
                if (data.success) {
                    const persistedStatus = data.status;
                    if (!persistedStatus) throw new Error('Missing persisted order status.');
                    const badge = root.querySelector(`[data-order-badge="${orderId}"]`);
                    if (badge) {
                        badge.className = 'yq-orders-badge ' + (statusClasses[persistedStatus] || 'yq-orders-badge--neutral');
                        badge.textContent = statusLabels[persistedStatus] || persistedStatus;
                    }
                    this.value = persistedStatus;
                    this.dataset.originalStatus = persistedStatus;
                    showOrderAlert('success', data.message || 'تم تحديث حالة الطلب بنجاح.');
                } else {
                    this.value = this.dataset.originalStatus;
                    showOrderAlert('danger', data.message || 'حدث خطأ أثناء تحديث الحالة');
                }
            } catch (value) {
                const error = window.YaqutAdminAjax.normalizeError(value);
                this.value = this.dataset.originalStatus;
                showOrderAlert('danger', error.message);
            } finally {
                this.disabled = false;
                this.style.opacity = '1';
            }
        });
    });

    function showOrderAlert(type, message) {
        const area = root.querySelector('#yqOrdersAlertArea');
        if (!area) return;

        const alert = document.createElement('div');
        alert.className = `yaqut-alert yq-admin-operation-feedback ${type === 'success' ? 'yaqut-alert--success' : 'yaqut-alert--danger'}`;
        alert.setAttribute('role', type === 'success' ? 'status' : 'alert');
        const icon = document.createElement('i');
        icon.className = type === 'success' ? 'bi bi-check-circle-fill' : 'bi bi-exclamation-octagon-fill';
        icon.setAttribute('aria-hidden', 'true');
        const text = document.createElement('span');
        text.textContent = message;
        alert.append(icon, text);
        area.replaceChildren(alert);
    }

    function openReceiptModal(imageSrc, orderId) {
        popoverTriggerList.forEach(function (element) {
            const instance = bootstrap.Popover.getInstance(element);
            if (instance) instance.hide();
        });

        document.getElementById('modalOrderId').textContent = orderId;
        document.getElementById('modalReceiptImg').src = imageSrc;
        document.getElementById('modalReceiptDownload').href = imageSrc;
        new bootstrap.Modal(document.getElementById('receiptModal')).show();
    }
})();

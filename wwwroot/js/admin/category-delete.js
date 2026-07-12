(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var forms = document.querySelectorAll('[data-category-delete-form]');
        var dialog = document.querySelector('[data-category-delete-dialog]');

        if (!forms.length) return;

        if (!dialog || typeof dialog.showModal !== 'function') {
            forms.forEach(function (form) {
                form.addEventListener('submit', function (event) {
                    var name = form.getAttribute('data-category-name') || '';
                    var message = name
                        ? 'هل أنت متأكد من حذف التصنيف ' + name + '؟'
                        : 'هل أنت متأكد من حذف هذا التصنيف؟';

                    if (!window.confirm(message)) {
                        event.preventDefault();
                    }
                });
            });
            return;
        }

        var activeForm = null;
        var lastTrigger = null;
        var image = dialog.querySelector('[data-category-delete-image]');
        var placeholder = dialog.querySelector('[data-category-delete-placeholder]');
        var nameTarget = dialog.querySelector('[data-category-delete-name]');
        var idTarget = dialog.querySelector('[data-category-delete-id]');
        var statusTarget = dialog.querySelector('[data-category-delete-status]');
        var helpTarget = dialog.querySelector('[data-category-delete-help]');
        var warning = dialog.querySelector('[data-category-delete-warning]');
        var confirmButton = dialog.querySelector('[data-category-delete-confirm]');
        var cancelButtons = dialog.querySelectorAll('[data-category-delete-cancel]');

        function productLabel(count) {
            if (count === 0) return 'لا توجد منتجات مرتبطة';
            if (count === 1) return 'منتج واحد مرتبط';
            if (count === 2) return 'منتجان مرتبطان';
            return count + ' منتجات مرتبطة';
        }

        function setImage(imageUrl, categoryName) {
            if (imageUrl && image) {
                image.src = imageUrl;
                image.alt = categoryName ? 'صورة تصنيف ' + categoryName : 'صورة التصنيف';
                image.hidden = false;
                if (placeholder) placeholder.hidden = true;
                return;
            }

            if (image) {
                image.removeAttribute('src');
                image.alt = '';
                image.hidden = true;
            }
            if (placeholder) placeholder.hidden = false;
        }

        function openDialog(form, trigger) {
            var categoryName = form.getAttribute('data-category-name') || 'هذا التصنيف';
            var categoryId = form.getAttribute('data-category-id') || '';
            var imageUrl = form.getAttribute('data-category-image') || '';
            var products = Number.parseInt(form.getAttribute('data-category-products') || '0', 10);
            var hasProducts = products > 0;

            activeForm = form;
            lastTrigger = trigger || null;

            if (nameTarget) nameTarget.textContent = categoryName;
            if (idTarget) idTarget.textContent = categoryId ? '#' + categoryId : '';
            setImage(imageUrl, categoryName);

            if (warning) {
                warning.classList.toggle('is-blocked', hasProducts);
            }
            if (statusTarget) {
                statusTarget.textContent = productLabel(products);
            }
            if (helpTarget) {
                helpTarget.textContent = hasProducts
                    ? 'قد يرفض النظام الحذف لأن التصنيف يحتوي على منتجات. انقل المنتجات أو عدلها قبل محاولة الحذف.'
                    : 'لا تظهر منتجات مرتبطة بهذا التصنيف في البيانات الحالية، لكن الخادم سيعيد التحقق عند الإرسال.';
            }

            dialog.showModal();
            var cancelButton = dialog.querySelector('[data-category-delete-cancel]');
            if (cancelButton) cancelButton.focus();
        }

        function closeDialog() {
            if (dialog.open) {
                dialog.close();
            }
            if (lastTrigger) {
                lastTrigger.focus();
            }
            activeForm = null;
            lastTrigger = null;
        }

        forms.forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (form.dataset.categoryDeleteConfirmed === 'true') {
                    delete form.dataset.categoryDeleteConfirmed;
                    return;
                }

                event.preventDefault();
                openDialog(form, event.submitter || document.activeElement);
            });
        });

        cancelButtons.forEach(function (button) {
            button.addEventListener('click', closeDialog);
        });

        dialog.addEventListener('click', function (event) {
            if (event.target === dialog) {
                closeDialog();
            }
        });

        dialog.addEventListener('cancel', function (event) {
            event.preventDefault();
            closeDialog();
        });

        if (confirmButton) {
            confirmButton.addEventListener('click', function () {
                if (!activeForm) return;
                activeForm.dataset.categoryDeleteConfirmed = 'true';
                activeForm.requestSubmit();
            });
        }
    });
})();

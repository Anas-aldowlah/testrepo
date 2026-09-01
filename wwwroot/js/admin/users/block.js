(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var forms = document.querySelectorAll('[data-yq-user-block-form]');
        
        forms.forEach(function (form) {
            form.addEventListener('submit', async function (event) {
                if (form.dataset.yqConfirmed === "true") {
                    delete form.dataset.yqConfirmed;
                    return;
                }

                event.preventDefault();
                event.stopImmediatePropagation();

                if (!window.YaqutOperationDialog) {
                    form.dataset.yqConfirmed = "true";
                    form.requestSubmit(event.submitter);
                    return;
                }

                var action = form.getAttribute('data-yq-block-action');
                var isBlock = action === 'block';

                var confirmed = await window.YaqutOperationDialog.confirm({
                    title: isBlock ? "تأكيد حظر المستخدم" : "تأكيد إلغاء الحظر",
                    message: isBlock ? "هل أنت متأكد من حظر هذا الحساب؟" : "هل أنت متأكد من إلغاء حظر هذا الحساب؟",
                    kind: isBlock ? "destructive" : "primary"
                });

                if (confirmed) {
                    form.dataset.yqConfirmed = "true";
                    form.requestSubmit(event.submitter);
                }
            });
        });
    });
})();

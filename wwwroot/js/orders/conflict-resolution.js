(() => {
    "use strict";

    const dialog = document.querySelector("[data-yq-conflict-dialog]");
    const form = dialog?.querySelector("[data-yq-conflict-resolution]");
    if (!dialog || !form) return;

    const unexpectedMessage = "تعذر تنفيذ العملية. حاول مرة أخرى.";
    const validResultKeys = new Set(["Continued", "Removed", "RemovedMultiple", "Mixed", "Cancelled"]);
    const feedback = form.querySelector("[data-yq-conflict-feedback]");
    const saveButton = form.querySelector('[data-yq-resolution-submit="continue"]');
    const decisionFields = [...form.querySelectorAll("[data-yq-conflict-decision]")];
    const openButton = document.querySelector("[data-yq-conflict-open]");
    const closeButton = dialog.querySelector("[data-yq-conflict-close]");

    const openDialog = () => {
        if (!dialog.open) window.YaqutDialog.showModal(dialog);
    };

    const hasCompleteDecision = () => decisionFields.length > 0 &&
        decisionFields.every(fieldset => fieldset.querySelector("input[type='radio']:checked"));

    const syncSaveState = () => {
        if (saveButton) saveButton.disabled = form.getAttribute("aria-busy") === "true" || !hasCompleteDecision();
    };

    const showFeedback = (message, kind = "info") => {
        if (!feedback) return;
        feedback.textContent = message;
        feedback.dataset.kind = kind;
        feedback.hidden = !message;
    };

    const setBusy = busy => {
        if (busy) form.setAttribute("aria-busy", "true");
        else form.removeAttribute("aria-busy");
        form.querySelectorAll("button").forEach(button => { button.disabled = busy; });
        if (!busy) syncSaveState();
    };

    openButton?.addEventListener("click", openDialog);
    closeButton?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        dialog.close();
    });
    dialog.addEventListener("click", event => {
        if (event.target === dialog) dialog.close();
    });

    form.addEventListener("change", event => {
        if (!event.target.matches("input[type='radio']")) return;
        showFeedback("", "info");
        syncSaveState();
    });
    syncSaveState();

    form.addEventListener("submit", async event => {
        event.preventDefault();
        const submitter = event.submitter;
        const isCancel = submitter?.dataset.yqResolutionSubmit === "cancel";
        const incompleteFieldset = !isCancel
            ? decisionFields.find(fieldset => !fieldset.querySelector("input[type='radio']:checked"))
            : null;
        if (incompleteFieldset) {
            showFeedback("اختر قراراً لكل منتج متأثر.", "error");
            incompleteFieldset.querySelector("input[type='radio']")?.focus();
            return;
        }

        if (isCancel) {
            dialog.close();
            const confirmed = await window.YaqutOperationDialog.confirm({
                title: "إلغاء الطلب بالكامل",
                message: "هل تريد إلغاء الطلب بالكامل؟\nبعد الإلغاء لن يمكن إعادة فتحه.",
                confirmText: "إلغاء الطلب بالكامل",
                cancelText: "تراجع",
                kind: "destructive"
            });
            if (!confirmed) {
                openDialog();
                return;
            }
        }

        const cancelInput = form.querySelector("[data-yq-cancel-entire]");
        cancelInput.value = isCancel ? "true" : "false";
        showFeedback(isCancel ? "جارٍ إلغاء الطلب..." : "جارٍ حفظ قرارك...", "info");
        setBusy(true);

        const body = new FormData(form);
        if (isCancel) {
            [...body.keys()].filter(key => key.startsWith("Decisions[")).forEach(key => body.delete(key));
        }

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok || !payload.success) {
                const serverError = new Error(payload.message || "تعذر حفظ القرار.");
                serverError.userSafe = true;
                serverError.reload = payload.reload === true;
                throw serverError;
            }
            if (!validResultKeys.has(payload.resultKey)) throw new Error("Unknown conflict result.");

            if (dialog.open) dialog.close();
            showFeedback("", "info");
            await window.YaqutOperationDialog.show({
                title: payload.title,
                message: payload.message,
                whatsAppUrl: form.dataset.yqConflictWhatsappUrl,
                whatsAppText: "إشعار الإدارة عبر واتساب",
                whatsAppNote: "يمكنك فتح رسالة جاهزة لإشعار الإدارة بتحديث الطلب.",
                confirmText: "حسنًا",
                confirmStyle: "secondary",
                kind: "success"
            });
            window.location.assign(payload.redirectUrl || window.location.href);
        } catch (error) {
            cancelInput.value = "false";
            setBusy(false);
            if (!dialog.open) openDialog();
            showFeedback(error?.userSafe && error.message ? error.message : unexpectedMessage, "error");
            if (error?.reload) window.setTimeout(() => window.location.reload(), 1100);
        }
    });

    openDialog();
})();

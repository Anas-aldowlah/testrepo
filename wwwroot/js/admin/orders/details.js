(() => {
    "use strict";
    const root = document.querySelector("[data-yq-order-details]");
    if (!root) return;
    const statusForm = root.querySelector("[data-yq-status-form]");
    const status = statusForm?.querySelector('select[name="Status"]');
    const save = statusForm?.querySelector("[data-yq-save-status]");
    const verifyForm = root.querySelector("[data-yq-verify-payment]");
    const verifyOrderId = verifyForm?.querySelector('input[name="id"]')?.value;
    const conflictMarkerKey = verifyOrderId ? `yq:admin-payment-conflict:${verifyOrderId}` : null;
    const unexpectedMessage = "تعذر تنفيذ العملية. حاول مرة أخرى.";

    function userSafeError(message) {
        const error = new Error(message);
        error.userSafe = true;
        return error;
    }

    function displayError(error) {
        return error?.userSafe && error.message ? error.message : unexpectedMessage;
    }

    function isTerminal(value) {
        return value === "Cancelled" || value === "Delivered" || value === "Refunded";
    }

    function storeConflictMarker() {
        if (!conflictMarkerKey) return;
        try {
            sessionStorage.setItem(conflictMarkerKey, "1");
        } catch {
            // The authoritative reload must still complete when storage is unavailable.
        }
    }

    function showPendingConflictFeedback() {
        if (!conflictMarkerKey) return;

        let hasMarker = false;
        try {
            hasMarker = sessionStorage.getItem(conflictMarkerKey) === "1";
        } catch {
            return;
        }
        if (!hasMarker) return;

        const conflictState = root.querySelector("[data-yq-conflict-info]");
        try {
            sessionStorage.removeItem(conflictMarkerKey);
        } catch {
            return;
        }
        if (!conflictState) return;

        YaqutOperationDialog.show({
            title: "تم التحقق من الدفع",
            message: "تم التحقق من الدفع، لكن يوجد تعارض في المخزون. يجب أن يراجع العميل طلبه أولًا.",
            whatsAppUrl: conflictState.dataset.yqWhatsappUrl || null,
            whatsAppText: "إشعار العميل عبر واتساب",
            whatsAppNote: "يمكنك إشعار العميل عبر واتساب لمراجعة الطلب.",
            confirmText: "حسنًا",
            kind: "info"
        });
    }

    function syncSaveState() {
        if (!status || !save) return;
        const permanentlyDisabled = statusForm.dataset.yqTerminal === "true" || status.disabled || isTerminal(status.dataset.originalStatus);
        save.disabled = permanentlyDisabled || status.value === status.dataset.originalStatus;
    }

    status?.addEventListener("change", syncSaveState);
    syncSaveState();
    statusForm?.addEventListener("submit", async event => {
        event.preventDefault();
        if (save.disabled) return;
        if (status.value === "Cancelled" && !await YaqutOperationDialog.confirm({
            title: "إلغاء الطلب",
            message: "هل تريد إلغاء الطلب؟\nبعد الإلغاء لن يمكن إعادة فتحه.",
            confirmText: "إلغاء الطلب",
            cancelText: "تراجع",
            kind: "destructive"
        })) {
            status.value = status.dataset.originalStatus;
            syncSaveState();
            return;
        }
        save.disabled = true;
        try {
            const response = await fetch(root.dataset.yqUpdateStatusUrl, { method: "POST", body: new FormData(statusForm), headers: { "X-Requested-With": "XMLHttpRequest" } });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || !data.success) throw userSafeError(data.message || "تعذر تحديث الحالة.");
            status.dataset.originalStatus = data.status;
            status.replaceChildren(...[data.status, ...(data.allowedTargets || [])].map(value => new Option(value, value, value === data.status, value === data.status)));
            if (isTerminal(data.status)) {
                statusForm.dataset.yqTerminal = "true";
                status.disabled = true;
                const verifyButton = root.querySelector("[data-yq-verify-payment] button");
                if (verifyButton) verifyButton.disabled = true;
            }
            syncSaveState();
            const customerResolvedConflict = statusForm.dataset.yqCustomerResolvedConflict === "true";
            await YaqutOperationDialog.show({
                title: data.status === "Cancelled" ? "تم إلغاء الطلب" : customerResolvedConflict ? "حدّث العميل طلبه" : "تم التحديث",
                message: data.status === "Cancelled"
                    ? data.message
                    : customerResolvedConflict
                        ? "اكتمل قرار العميل، ويمكنك الآن متابعة تجهيز الطلب."
                        : data.message,
                whatsAppUrl: data.whatsAppUrl,
                kind: "success"
            });
            statusForm.dataset.yqCustomerResolvedConflict = "false";
            location.reload();
        } catch (error) {
            status.value = status.dataset.originalStatus;
            syncSaveState();
            await YaqutOperationDialog.show({ title: "تعذر التحديث", message: displayError(error), kind: "error" });
        }
    });

    verifyForm?.addEventListener("submit", async event => {
        event.preventDefault();
        const form = event.currentTarget;
        if (!await YaqutOperationDialog.confirm({ title: "تأكيد التحقق من الدفع", message: "هل تريد تأكيد مراجعة الدفع لهذا الطلب؟", confirmText: "تأكيد", cancelText: "رجوع" })) return;
        const button = form.querySelector("button");
        button.disabled = true;
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest" } });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || !data.success) throw userSafeError(data.message || "تعذر التحقق من الدفع.");
            const conflict = data.outcome === "Conflict";
            if (conflict) {
                storeConflictMarker();
                location.reload();
                return;
            }
            await YaqutOperationDialog.show({
                title: "تم التحقق من الدفع بنجاح",
                message: data.message,
                whatsAppUrl: data.whatsAppUrl,
                kind: "success"
            });
            location.reload();
        } catch (error) {
            button.disabled = false;
            await YaqutOperationDialog.show({ title: "تعذر التحقق", message: displayError(error), kind: "error" });
        }
    });

    root.querySelector("[data-yq-conflict-info]")?.addEventListener("click", event => {
        const button = event.currentTarget;
        YaqutOperationDialog.show({
            title: button.dataset.yqConflictTitle,
            message: button.dataset.yqConflictMessage,
            lines: (button.dataset.yqConflictLines || "").split("||"),
            whatsAppUrl: button.dataset.yqWhatsappUrl
        });
    });

    showPendingConflictFeedback();
})();

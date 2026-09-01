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
        let adminNote = null;
        if (status.value === "Shipped") {
            const promptResult = await YaqutOperationDialog.prompt({
                title: "إضافة ملاحظة الشحن",
                message: "يمكنك كتابة ملاحظة إدارية مع شحن هذا الطلب (اختياري):",
                placeholder: "أدخل ملاحظة الشحن هنا...",
                confirmText: "أضف الملاحظة",
                cancelText: "تراجع",
                maxLength: 500
            });
            if (!promptResult || !promptResult.confirmed) {
                status.value = status.dataset.originalStatus;
                syncSaveState();
                return;
            }
            adminNote = promptResult.value;
        }

        save.disabled = true;
        try {
            const formData = new FormData(statusForm);
            if (adminNote !== null) {
                formData.set("adminNote", adminNote);
            }
            const response = await fetch(root.dataset.yqUpdateStatusUrl, { method: "POST", body: formData, headers: { "X-Requested-With": "XMLHttpRequest" } });
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
            const statusText = status.options[status.selectedIndex].text;
            const dialogOptions = {
                title: "تم تحديث الطلب بنجاح",
                message: `تم تحديث حالة الطلب إلى "${statusText}".`,
                whatsAppUrl: data.whatsAppUrl,
                confirmText: "حسنًا",
                kind: "success"
            };
            if (data.whatsAppUrl) {
                dialogOptions.whatsAppNote = "هل تود إشعار العميل عبر واتساب؟";
                dialogOptions.whatsAppText = "إشعار العميل عبر واتساب";
            }
            await YaqutOperationDialog.show(dialogOptions);
            statusForm.dataset.yqCustomerResolvedConflict = "false";
            location.reload();
        } catch (error) {
            status.value = status.dataset.originalStatus;
            syncSaveState();
            await YaqutOperationDialog.show({ title: "تعذر التحديث", message: displayError(error), kind: "error" });
        }
    });

    const editNoteBtn = root.querySelector("[data-yq-edit-admin-note]");
    editNoteBtn?.addEventListener("click", async () => {
        const orderId = editNoteBtn.dataset.orderId;
        const currentNote = editNoteBtn.dataset.currentNote || "";
        const promptResult = await YaqutOperationDialog.prompt({
            title: currentNote ? "تعديل ملاحظة الإدارة" : "إضافة ملاحظة الإدارة",
            message: "أدخل ملاحظة الإدارة لهذا الطلب (أو اتركها فارغة لحذفها):",
            initialValue: currentNote,
            placeholder: "أدخل ملاحظة الإدارة هنا...",
            confirmText: "حفظ الملاحظة",
            cancelText: "إلغاء",
            maxLength: 500
        });
        if (!promptResult || !promptResult.confirmed) return;

        editNoteBtn.disabled = true;
        try {
            const updateUrl = root.dataset.yqUpdateAdminNoteUrl || "/Admin/Orders/UpdateAdminNote";
            const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value ||
                          document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append("id", orderId);
            if (promptResult.value !== null) {
                formData.append("adminNote", promptResult.value);
            }
            const response = await fetch(updateUrl, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token ? { "RequestVerificationToken": token } : {})
                }
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || !data.success) throw userSafeError(data.message || "تعذر حفظ ملاحظة الإدارة.");

            const newNote = data.adminNote;
            editNoteBtn.dataset.currentNote = newNote || "";
            const btnSpan = editNoteBtn.querySelector("span");
            const btnIcon = editNoteBtn.querySelector("i");
            if (btnSpan) btnSpan.textContent = newNote ? "تعديل الملاحظة" : "إضافة ملاحظة";
            if (btnIcon) btnIcon.className = `bi ${newNote ? "bi-pencil-square" : "bi-plus-lg"}`;

            const noteDisplay = root.querySelector("[data-yq-admin-note-display]");
            if (noteDisplay) {
                if (newNote) {
                    noteDisplay.innerHTML = `
                        <div class="yq-order-details-state-note yq-order-details-state-note--admin">
                            <i class="bi bi-shield-check" aria-hidden="true"></i>
                            <span data-yq-admin-note-text>${newNote}</span>
                        </div>`;
                } else {
                    noteDisplay.innerHTML = `
                        <div class="yq-order-details-state-note">
                            <i class="bi bi-shield" aria-hidden="true"></i>
                            <span data-yq-admin-note-text>لا توجد ملاحظة مسجلة من الإدارة.</span>
                        </div>`;
                }
            }

            await YaqutOperationDialog.show({
                title: "تم الحفظ بنجاح",
                message: data.message,
                kind: "success"
            });
        } catch (error) {
            await YaqutOperationDialog.show({
                title: "تعذر الحفظ",
                message: displayError(error),
                kind: "error"
            });
        } finally {
            editNoteBtn.disabled = false;
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

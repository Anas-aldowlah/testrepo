(() => {
    "use strict";
    const root = document.querySelector("[data-yq-orders-page]");
    if (!root) return;
    let latest = 0;
    let controller;
    let popovers = [];
    const unexpectedMessage = "تعذر تنفيذ العملية. حاول مرة أخرى.";
    const labels = { Pending: "قيد الانتظار", Paid: "تم الدفع", Processed: "قيد التجهيز", Shipped: "تم الشحن", Delivered: "تم التوصيل", Cancelled: "ملغي", Refunded: "مرتجع" };
    const classes = { Pending: "yq-orders-badge--pending", Paid: "yq-orders-badge--processed", Processed: "yq-orders-badge--processed", Shipped: "yq-orders-badge--shipped", Delivered: "yq-orders-badge--delivered", Cancelled: "yq-orders-badge--cancelled", Refunded: "yq-orders-badge--cancelled" };

    function userSafeError(message) {
        const error = new Error(message);
        error.userSafe = true;
        return error;
    }

    function displayError(error, fallback = unexpectedMessage) {
        return error?.userSafe && error.message ? error.message : fallback;
    }

    async function json(response) {
        const data = await response.json().catch(() => ({}));
        if (!response.ok || data.success === false) throw userSafeError(data.message || "تعذر تنفيذ الطلب.");
        return data;
    }

    function disposePopovers() {
        const ownedPopovers = popovers;
        popovers = [];
        ownedPopovers.forEach(item => item.dispose());
    }

    function initPopovers() {
        if (!window.bootstrap?.Popover) return;
        root.querySelectorAll('[data-bs-toggle="popover"]').forEach(trigger => {
            popovers.push(new bootstrap.Popover(trigger, {
                container: "body",
                html: false,
                title: trigger.dataset.yqPopoverType === "receipt" ? "إيصال الدفع" : "معلومات الطلب",
                content: trigger.dataset.yqPopoverType === "receipt"
                    ? (trigger.dataset.yqReceiptImage ? "افتح تفاصيل الطلب لعرض الإيصال." : "لا يوجد إيصال مرفق.")
                    : [trigger.dataset.yqCustomerName, trigger.dataset.yqRecipientPhone, trigger.dataset.yqPaymentMethod].filter(Boolean).join(" — ")
            }));
        });
    }

    async function load(url, push = true) {
        controller?.abort();
        controller = new AbortController();
        const request = ++latest;
        root.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" }, signal: controller.signal });
            if (!response.ok) throw userSafeError("تعذر تحميل الطلبات.");
            const next = new DOMParser().parseFromString(await response.text(), "text/html").querySelector("[data-yq-orders-page]");
            if (!next) throw userSafeError("تعذر تحديث النتائج.");
            if (request !== latest) return;
            disposePopovers();
            root.innerHTML = next.innerHTML;
            root.dataset.yqUpdateStatusUrl = next.dataset.yqUpdateStatusUrl;
            root.dataset.yqVerifyPaymentUrl = next.dataset.yqVerifyPaymentUrl;
            if (push) {
                history.pushState({}, "", url);
            }
            root.querySelectorAll(".yq-ajax-status-form").forEach(syncStatusForm);
            initPopovers();
        } catch (error) {
            if (error.name !== "AbortError") await YaqutOperationDialog.show({ title: "تعذر التحديث", message: displayError(error), kind: "error" });
        } finally {
            if (request === latest) root.removeAttribute("aria-busy");
        }
    }

    function isTerminalStatus(status) {
        return status === "Cancelled" || status === "Delivered" || status === "Refunded";
    }

    function syncStatusForm(form) {
        const select = form?.querySelector('select[name="Status"]');
        const save = form?.querySelector("[data-yq-save-status]");
        if (!select || !save) return;
        const terminal = isTerminalStatus(select.dataset.originalStatus);
        const unavailable = terminal || select.disabled;
        save.disabled = unavailable || select.value === select.dataset.originalStatus;
    }

    root.querySelectorAll(".yq-ajax-status-form").forEach(syncStatusForm);

    root.addEventListener("submit", async event => {
        const search = event.target.closest("[data-yq-orders-search-form]");
        if (search) {
            event.preventDefault();
            const url = new URL(search.action || location.href, location.href);
            url.search = new URLSearchParams(new FormData(search)).toString();
            await load(url.href);
            return;
        }

        const statusForm = event.target.closest(".yq-ajax-status-form");
        if (statusForm) {
            event.preventDefault();
            const select = statusForm.querySelector('select[name="Status"]');
            const save = statusForm.querySelector("[data-yq-save-status]");
            if (!select || !save || save.disabled) return;
            const original = select.dataset.originalStatus;
            if (select.value === "Cancelled" && !await YaqutOperationDialog.confirm({
                title: "إلغاء الطلب",
                message: "هل تريد إلغاء الطلب؟\nبعد الإلغاء لن يمكن إعادة فتحه.",
                confirmText: "إلغاء الطلب",
                cancelText: "تراجع",
                kind: "destructive"
            })) {
                select.value = original;
                syncStatusForm(statusForm);
                return;
            }

            select.disabled = true;
            save.disabled = true;
            try {
                const data = await json(await fetch(root.dataset.yqUpdateStatusUrl, {
                    method: "POST",
                    body: new URLSearchParams({ id: statusForm.dataset.orderId, status: select.value }),
                    headers: { "X-Requested-With": "XMLHttpRequest", "RequestVerificationToken": statusForm.querySelector('[name="__RequestVerificationToken"]').value }
                }));
                const badge = root.querySelector(`[data-order-badge="${statusForm.dataset.orderId}"]`);
                if (badge) { badge.className = `yq-orders-badge ${classes[data.status] || "yq-orders-badge--neutral"}`; badge.textContent = labels[data.status] || data.status; }
                select.replaceChildren(...[data.status, ...(data.allowedTargets || [])].map(status => new Option(labels[status] || status, status, status === data.status, status === data.status)));
                select.dataset.originalStatus = data.status;
                const terminal = isTerminalStatus(data.status);
                select.disabled = terminal;
                if (terminal) {
                    const verify = statusForm.closest("tr")?.querySelector("[data-yq-verify-payment] button");
                    if (verify) verify.disabled = true;
                }
                syncStatusForm(statusForm);
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
            } catch (error) {
                select.value = original;
                select.disabled = isTerminalStatus(original);
                syncStatusForm(statusForm);
                await YaqutOperationDialog.show({ title: "تعذر التحديث", message: displayError(error), kind: "error" });
            }
            return;
        }

        const form = event.target.closest("[data-yq-verify-payment]");
        if (!form) return;
        event.preventDefault();
        if (!await YaqutOperationDialog.confirm({ title: "تأكيد التحقق من الدفع", message: "هل تريد تأكيد مراجعة الدفع لهذا الطلب؟", confirmText: "تأكيد", cancelText: "رجوع" })) return;
        const button = form.querySelector("button");
        button.disabled = true;
        try {
            const data = await json(await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest" } }));
            const conflict = data.outcome === "Conflict";
            await YaqutOperationDialog.show({
                title: conflict ? "تمت مراجعة الدفع" : "تم التحقق من الدفع بنجاح",
                message: data.message,
                whatsAppUrl: data.whatsAppUrl,
                kind: "success"
            });
            await load(location.href, false);
        } catch (error) {
            button.disabled = false;
            await YaqutOperationDialog.show({ title: "تعذر التحقق", message: displayError(error), kind: "error" });
        }
    });

    root.addEventListener("change", event => {
        const select = event.target.closest(".yq-ajax-status-form select");
        if (select) syncStatusForm(select.closest("form"));
    });

    root.addEventListener("click", event => {
        const conflict = event.target.closest("[data-yq-conflict-info]");
        if (conflict) {
            YaqutOperationDialog.show({
                title: conflict.dataset.yqConflictTitle,
                message: conflict.dataset.yqConflictMessage,
                lines: (conflict.dataset.yqConflictLines || "").split("||"),
                whatsAppUrl: conflict.dataset.yqWhatsappUrl
            });
        }
        const link = event.target.closest(".yq-admin-pager a:not(.is-disabled), .yq-orders-clear-filter");
        if (link) { event.preventDefault(); load(link.href); }
    });

    addEventListener("popstate", () => load(location.href, false));
    initPopovers();
})();

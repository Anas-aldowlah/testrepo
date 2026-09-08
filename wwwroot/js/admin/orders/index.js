(() => {
    "use strict";
    const root = document.querySelector("[data-yq-orders-page]");
    if (!root) return;
    let latest = 0;
    let controller;
    let popovers = [];
    const unexpectedMessage = "تعذر تنفيذ العملية. حاول مرة أخرى.";
    const labels = {
        Pending: "قيد الانتظار",
        Paid: "تم الدفع",
        Processed: "قيد التجهيز",
        Shipped: "تم الشحن",
        Delivered: "تم التوصيل",
        Cancelled: "ملغي",
        Refunded: "مرتجع"
    };
    const classes = {
        Pending: "yq-orders-badge--pending",
        Paid: "yq-orders-badge--processed",
        Processed: "yq-orders-badge--processed",
        Shipped: "yq-orders-badge--shipped",
        Delivered: "yq-orders-badge--delivered",
        Cancelled: "yq-orders-badge--cancelled",
        Refunded: "yq-orders-badge--cancelled"
    };

    const FILTER_DEBOUNCE_MS = 400;
    let filterDebounceTimer = null;

    function userSafeError(message) {
        const error = new Error(message);
        error.userSafe = true;
        return error;
    }

    function displayError(error, fallback = unexpectedMessage) {
        return error?.userSafe && error.message ? error.message : fallback;
    }

    function escapeHtml(text) {
        if (!text) return "";
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
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

    function buildFilterUrl() {
        const form = root.querySelector("[data-yq-orders-search-form]");
        if (!form) return location.href;
        const url = new URL(form.action || location.href, location.href);
        const formData = new FormData(form);
        const params = new URLSearchParams();

        const pageSize = formData.get("pageSize");
        if (pageSize) {
            params.set("pageSize", pageSize.toString());
        }

        const searchVal = (formData.get("search") || "").toString().trim();
        if (searchVal) {
            params.set("search", searchVal);
        }

        const statuses = formData.getAll("status");
        for (const st of statuses) {
            const trimmed = (st || "").toString().trim();
            if (trimmed && trimmed.toLowerCase() !== "refunded") {
                params.append("status", trimmed);
            }
        }

        url.search = params.toString();
        return url.href;
    }

    function executeFilterLoad() {
        clearTimeout(filterDebounceTimer);
        const url = buildFilterUrl();
        load(url, true);
    }

    function scheduleFilterLoad() {
        clearTimeout(filterDebounceTimer);
        filterDebounceTimer = setTimeout(() => {
            executeFilterLoad();
        }, FILTER_DEBOUNCE_MS);
    }

    function updateSelectedCount() {
        const dropdown = root.querySelector("[data-yq-filter-dropdown]");
        if (!dropdown) return;
        const checkedBoxes = dropdown.querySelectorAll('.yq-status-checkbox:checked');
        const countBadge = dropdown.querySelector("[data-yq-selected-count]");
        if (countBadge) {
            const count = checkedBoxes.length;
            if (count > 0) {
                countBadge.textContent = `${count} محددة`;
                countBadge.classList.remove("d-none");
            } else {
                countBadge.textContent = "";
                countBadge.classList.add("d-none");
            }
        }
    }

    function syncChipsFromForm() {
        const chipsContainer = root.querySelector("[data-yq-active-chips]");
        if (!chipsContainer) return;
        const searchInput = root.querySelector("#ordersSearch");
        const searchValue = searchInput ? searchInput.value.trim() : "";
        const checkedBoxes = Array.from(root.querySelectorAll('.yq-status-checkbox:checked'));

        const hasFilters = searchValue.length > 0 || checkedBoxes.length > 0;
        if (!hasFilters) {
            chipsContainer.innerHTML = "";
            chipsContainer.classList.add("d-none");
            return;
        }

        chipsContainer.classList.remove("d-none");
        chipsContainer.innerHTML = "";

        if (searchValue) {
            const searchChip = document.createElement("span");
            searchChip.className = "yq-filter-chip yq-filter-chip--search";
            searchChip.setAttribute("data-chip-search", "");
            searchChip.innerHTML = `
                <i class="bi bi-search" aria-hidden="true"></i>
                <span class="yq-filter-chip__label">البحث: ${escapeHtml(searchValue)}</span>
                <button type="button" class="yq-filter-chip__remove" data-remove-search aria-label="إزالة تصفية البحث: ${escapeHtml(searchValue)}">
                    <i class="bi bi-x-lg" aria-hidden="true"></i>
                </button>
            `;
            chipsContainer.appendChild(searchChip);
        }

        for (const cb of checkedBoxes) {
            const statusVal = cb.value;
            if (statusVal.toLowerCase() === "refunded") continue;
            const statusLabel = labels[statusVal] || statusVal;
            const statusChip = document.createElement("span");
            statusChip.className = "yq-filter-chip yq-filter-chip--status";
            statusChip.setAttribute("data-chip-status", statusVal);
            statusChip.innerHTML = `
                <span class="yq-status-dot yq-status-dot--${statusVal.toLowerCase()}" aria-hidden="true"></span>
                <span class="yq-filter-chip__label">${escapeHtml(statusLabel)}</span>
                <button type="button" class="yq-filter-chip__remove" data-remove-status="${escapeHtml(statusVal)}" aria-label="إزالة تصفية: ${escapeHtml(statusLabel)}">
                    <i class="bi bi-x-lg" aria-hidden="true"></i>
                </button>
            `;
            chipsContainer.appendChild(statusChip);
        }
    }

    function toggleStatusDropdown(force) {
        const dropdown = root.querySelector("[data-yq-filter-dropdown]");
        const trigger = root.querySelector("#statusDropdownTrigger");
        if (!dropdown || !trigger) return;
        const isOpen = typeof force === "boolean" ? force : !dropdown.classList.contains("is-open");
        dropdown.classList.toggle("is-open", isOpen);
        trigger.setAttribute("aria-expanded", String(isOpen));
    }

    function closeStatusDropdown(focusTrigger = false) {
        toggleStatusDropdown(false);
        if (focusTrigger) {
            root.querySelector("#statusDropdownTrigger")?.focus();
        }
    }

    document.addEventListener("click", event => {
        const dropdown = root.querySelector("[data-yq-filter-dropdown]");
        if (dropdown && dropdown.classList.contains("is-open")) {
            if (!dropdown.contains(event.target)) {
                closeStatusDropdown(false);
            }
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            const dropdown = root.querySelector("[data-yq-filter-dropdown]");
            if (dropdown && dropdown.classList.contains("is-open")) {
                closeStatusDropdown(true);
            }
        }
    });

    async function load(url, push = true) {
        controller?.abort();
        controller = new AbortController();
        const request = ++latest;
        root.setAttribute("aria-busy", "true");
        const wasDropdownOpen = Boolean(root.querySelector("[data-yq-filter-dropdown].is-open"));

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
            if (wasDropdownOpen) {
                toggleStatusDropdown(true);
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
            clearTimeout(filterDebounceTimer);
            executeFilterLoad();
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

            let adminNote = null;
            if (select.value === "Shipped") {
                const promptResult = await YaqutOperationDialog.prompt({
                    title: "إضافة ملاحظة الشحن",
                    message: "يمكنك كتابة ملاحظة إدارية مع شحن هذا الطلب (اختياري):",
                    placeholder: "أدخل ملاحظة الشحن هنا...",
                    confirmText: "أضف الملاحظة",
                    cancelText: "تراجع",
                    maxLength: 500
                });
                if (!promptResult || !promptResult.confirmed) {
                    select.value = original;
                    syncStatusForm(statusForm);
                    return;
                }
                adminNote = promptResult.value;
            }

            select.disabled = true;
            save.disabled = true;
            try {
                const payload = { id: statusForm.dataset.orderId, status: select.value };
                if (adminNote !== null) payload.adminNote = adminNote;
                const data = await json(await fetch(root.dataset.yqUpdateStatusUrl, {
                    method: "POST",
                    body: new URLSearchParams(payload),
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

    root.addEventListener("input", event => {
        const searchInput = event.target.closest("#ordersSearch");
        if (searchInput) {
            syncChipsFromForm();
            scheduleFilterLoad();
        }
    });

    root.addEventListener("change", event => {
        const statusCheckbox = event.target.closest(".yq-status-checkbox");
        if (statusCheckbox) {
            updateSelectedCount();
            syncChipsFromForm();
            scheduleFilterLoad();
            return;
        }

        const select = event.target.closest(".yq-ajax-status-form select");
        if (select) syncStatusForm(select.closest("form"));
    });

    root.addEventListener("click", event => {
        const trigger = event.target.closest("#statusDropdownTrigger");
        if (trigger) {
            event.preventDefault();
            toggleStatusDropdown();
            return;
        }

        const removeStatus = event.target.closest("[data-remove-status]");
        if (removeStatus) {
            event.preventDefault();
            const statusVal = removeStatus.dataset.removeStatus;
            const cb = root.querySelector(`.yq-status-checkbox[value="${statusVal}"]`);
            if (cb) {
                cb.checked = false;
            }
            updateSelectedCount();
            syncChipsFromForm();
            scheduleFilterLoad();
            return;
        }

        const removeSearch = event.target.closest("[data-remove-search]");
        if (removeSearch) {
            event.preventDefault();
            const searchInput = root.querySelector("#ordersSearch");
            if (searchInput) {
                searchInput.value = "";
            }
            syncChipsFromForm();
            scheduleFilterLoad();
            return;
        }

        const conflict = event.target.closest("[data-yq-conflict-info]");
        if (conflict) {
            YaqutOperationDialog.show({
                title: conflict.dataset.yqConflictTitle,
                message: conflict.dataset.yqConflictMessage,
                lines: (conflict.dataset.yqConflictLines || "").split("||"),
                whatsAppUrl: conflict.dataset.yqWhatsappUrl,
                confirmStyle: "secondary"
            });
        }
        const link = event.target.closest(".yq-admin-pager a:not(.is-disabled), .yq-orders-clear-filter");
        if (link) { event.preventDefault(); load(link.href); }
    });

    addEventListener("popstate", () => {
        clearTimeout(filterDebounceTimer);
        load(location.href, false);
    });
    initPopovers();
})();

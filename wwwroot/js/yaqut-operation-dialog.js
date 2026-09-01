(() => {
    "use strict";

    const dialog = document.querySelector("[data-yq-operation-dialog]");
    if (!dialog) return;

    const title = dialog.querySelector("[data-yq-dialog-title]");
    const message = dialog.querySelector("[data-yq-dialog-message]");
    const lines = dialog.querySelector("[data-yq-dialog-lines]");
    const inputWrap = dialog.querySelector("[data-yq-dialog-input-wrap]");
    const textarea = dialog.querySelector("[data-yq-dialog-input]");
    const counter = dialog.querySelector("[data-yq-dialog-counter]");
    const whatsApp = dialog.querySelector("[data-yq-dialog-whatsapp]");
    const whatsAppText = dialog.querySelector("[data-yq-dialog-whatsapp-text]");
    const whatsAppNote = dialog.querySelector("[data-yq-dialog-whatsapp-note]");
    const confirmButton = dialog.querySelector("[data-yq-dialog-confirm]");
    const confirmText = dialog.querySelector("[data-yq-dialog-confirm-text]");
    const cancelButton = dialog.querySelector("[data-yq-dialog-cancel]");
    const cancelText = dialog.querySelector("[data-yq-dialog-cancel-text]");
    const closeButton = dialog.querySelector("[data-yq-dialog-close]");
    const confirmVariants = [
        "yq-operation-dialog__button--primary",
        "yq-operation-dialog__button--secondary",
        "yq-operation-dialog__button--destructive",
        "yq-paired-action--primary",
        "yq-paired-action--secondary",
        "yq-paired-action--destructive"
    ];
    const defaults = {
        whatsAppText: "إشعار العميل عبر واتساب",
        whatsAppNote: "يمكنك فتح رسالة جاهزة لإشعار العميل بالتحديث."
    };
    let resolver = null;
    let currentOptions = null;

    const finish = value => {
        if (dialog.open) dialog.close();
        const current = resolver;
        const opts = currentOptions;
        resolver = null;
        currentOptions = null;
        if (current) {
            if (opts?.input) {
                if (value === true) {
                    const raw = textarea?.value ?? "";
                    const trimmed = raw.trim();
                    current({ confirmed: true, value: trimmed.length > 0 ? trimmed : null });
                } else {
                    current({ confirmed: false, value: null });
                }
            } else {
                current(value);
            }
        }
    };

    const updateCounter = () => {
        if (!textarea || !counter) return;
        const max = textarea.maxLength > 0 ? textarea.maxLength : 500;
        counter.textContent = `${textarea.value.length} / ${max}`;
    };

    textarea?.addEventListener("input", updateCounter);

    const open = options => {
        if (dialog.open) dialog.close();
        currentOptions = options;
        title.textContent = options.title || "تنبيه";
        message.textContent = options.message || "";
        const entries = Array.isArray(options.lines) ? options.lines.filter(Boolean) : [];
        lines.replaceChildren(...entries.map(entry => {
            const item = document.createElement("li");
            item.textContent = entry;
            return item;
        }));
        lines.hidden = entries.length === 0;

        if (inputWrap && textarea) {
            if (options.input) {
                inputWrap.hidden = false;
                const max = options.maxLength || 500;
                textarea.maxLength = max;
                textarea.value = options.initialValue || "";
                textarea.placeholder = options.placeholder || "أدخل الملاحظة هنا...";
                updateCounter();
            } else {
                inputWrap.hidden = true;
                textarea.value = "";
            }
        }

        whatsApp.hidden = !options.whatsAppUrl;
        whatsAppNote.hidden = !options.whatsAppUrl;
        whatsAppText.textContent = options.whatsAppText || defaults.whatsAppText;
        whatsAppNote.textContent = options.whatsAppNote || defaults.whatsAppNote;
        if (options.whatsAppUrl) whatsApp.href = options.whatsAppUrl;
        else whatsApp.removeAttribute("href");
        confirmText.textContent = options.confirmText || (options.input ? "أضف الملاحظة" : "حسنًا");
        cancelText.textContent = options.cancelText || "تراجع";
        cancelButton.hidden = !options.confirm;
        dialog.dataset.kind = options.kind || "info";
        confirmButton.classList.remove(...confirmVariants);
        const confirmStyle = options.confirmStyle || (options.kind === "destructive" ? "destructive" : "primary");
        confirmButton.classList.add(`yq-operation-dialog__button--${confirmStyle}`);
        confirmButton.classList.add(`yq-paired-action--${confirmStyle}`);
        dialog.showModal();
        requestAnimationFrame(() => {
            if (dialog.open) {
                if (options.input && textarea) {
                    textarea.focus({ preventScroll: true });
                } else {
                    confirmButton.focus({ preventScroll: true });
                }
            }
        });
    };

    confirmButton.addEventListener("click", () => finish(true));
    cancelButton.addEventListener("click", () => finish(false));
    closeButton.addEventListener("click", () => finish(false));
    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        finish(false);
    });
    dialog.addEventListener("click", event => {
        if (event.target === dialog) finish(false);
    });
    window.addEventListener("popstate", () => finish(false));

    window.YaqutOperationDialog = {
        show(options = {}) {
            return new Promise(resolve => {
                resolver = () => resolve(true);
                open(options);
            });
        },
        confirm(options = {}) {
            return new Promise(resolve => {
                resolver = resolve;
                open({ ...options, confirm: true });
            });
        },
        prompt(options = {}) {
            return new Promise(resolve => {
                resolver = resolve;
                open({ ...options, confirm: true, input: true });
            });
        }
    };

    if (dialog.dataset.yqInitialMessage) {
        open({
            title: dialog.dataset.yqInitialKind === "error" ? "تعذر التنفيذ" : "تم",
            message: dialog.dataset.yqInitialMessage,
            kind: dialog.dataset.yqInitialKind
        });
    }
})();

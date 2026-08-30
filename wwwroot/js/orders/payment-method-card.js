(() => {
    "use strict";

    const writeClipboard = async value => {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
            return;
        }

        const input = document.createElement("textarea");
        input.value = value;
        input.setAttribute("readonly", "");
        input.style.position = "fixed";
        input.style.opacity = "0";
        document.body.appendChild(input);
        input.select();
        const copied = document.execCommand("copy");
        input.remove();
        if (!copied) throw new Error("Copy failed.");
    };

    document.addEventListener("click", async event => {
        const target = event.target instanceof Element
            ? event.target.closest("[data-yq-copy-payment-number]")
            : null;
        if (!target) return;

        const card = target.closest(".yq-payment-card");
        const number = card?.querySelector(".yq-payment-card__number")?.textContent?.trim();
        const status = card?.querySelector("[data-yq-payment-copy-status]");
        if (!number || !status) return;

        try {
            await writeClipboard(number);
            status.textContent = "تم نسخ رقم الحساب.";
        } catch {
            status.textContent = "تعذر نسخ رقم الحساب.";
        }
    });
})();

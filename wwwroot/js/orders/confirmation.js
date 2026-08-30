/**
 * ياقوت — Orders / Confirmation page interactions
 */
(function (window, document) {
    'use strict';

    function copyWithSelection(text) {
        var temp = null;
        try {
            temp = document.createElement('textarea');
            temp.value = text;
            temp.style.position = 'fixed';
            temp.style.opacity = '0';
            document.body.appendChild(temp);
            if (typeof temp.select !== 'function') return false;
            temp.select();
            return typeof document.execCommand === 'function' && document.execCommand('copy');
        } catch (error) {
            return false;
        } finally {
            if (temp && temp.parentNode) temp.parentNode.removeChild(temp);
        }
    }

    function initCopyTracking() {
        var btn = document.getElementById('yqCopyTrackingBtn');
        var value = document.getElementById('yqTrackingNumber');
        if (!btn || !value) return;

        btn.addEventListener('click', function () {
            var text = value.textContent.trim();
            if (!text) return;

            var icon = btn.querySelector('i');

            function showCopied() {
                btn.classList.add('is-copied');
                if (icon) icon.className = 'bi bi-check-lg';
                setTimeout(function () {
                    btn.classList.remove('is-copied');
                    if (icon) icon.className = 'bi bi-clipboard';
                }, 1800);
            }

            if (navigator.clipboard && navigator.clipboard.writeText) {
                Promise.resolve().then(function () {
                    return navigator.clipboard.writeText(text);
                }).then(showCopied).catch(function () {
                    if (copyWithSelection(text)) showCopied();
                });
            } else if (copyWithSelection(text)) showCopied();
        });
    }

    function clearSuccessfulCheckoutDraft() {
        var page = document.querySelector('[data-yq-confirmation-page]');
        var draftId = page && page.getAttribute('data-yq-clear-checkout-draft-id');
        if (!draftId || !window.YaqutCheckoutDraft) return;
        window.YaqutCheckoutDraft.remove(draftId);
    }

    function initArrivalDialog() {
        var dialog = document.querySelector('[data-yq-confirm-arrival]');
        if (!dialog || typeof dialog.showModal !== 'function') return;

        var close = function () {
            if (dialog.open) dialog.close();
        };
        var closeButton = dialog.querySelector('[data-yq-confirm-arrival-close]');
        if (closeButton) closeButton.addEventListener('click', close);
        dialog.addEventListener('click', function (event) {
            if (event.target === dialog) close();
        });
        dialog.showModal();
    }

    function init() {
        clearSuccessfulCheckoutDraft();
        initCopyTracking();
        initArrivalDialog();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

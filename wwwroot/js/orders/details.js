(function () {
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

    function init() {
        var root = document.querySelector('[data-yq-order-details]');
        if (!root) return;

        var copyBtn = document.getElementById('yqDetailsCopyTrackingBtn');
        var trackingEl = document.getElementById('yqDetailsTrackingNumber');
        var statusEl = document.getElementById('yqDetailsCopyStatus');
        if (!copyBtn || !trackingEl) return;

        copyBtn.addEventListener('click', function () {
            var text = trackingEl.textContent.trim();
            if (!text) return;

            var markCopied = function () {
                var icon = copyBtn.querySelector('i');
                copyBtn.classList.add('is-copied');
                if (icon) icon.className = 'bi bi-check-lg';
                if (statusEl) statusEl.textContent = 'تم نسخ رقم التتبع بنجاح';
                setTimeout(function () {
                    copyBtn.classList.remove('is-copied');
                    if (icon) icon.className = 'bi bi-clipboard';
                    if (statusEl) statusEl.textContent = '';
                }, 1800);
            };

            var markFailed = function () {
                if (statusEl) statusEl.textContent = 'تعذر نسخ رقم التتبع';
                setTimeout(function () {
                    if (statusEl) statusEl.textContent = '';
                }, 1800);
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                Promise.resolve().then(function () {
                    return navigator.clipboard.writeText(text);
                }).then(markCopied).catch(function () {
                    if (copyWithSelection(text)) markCopied();
                    else markFailed();
                });
            } else if (copyWithSelection(text)) {
                markCopied();
            } else {
                markFailed();
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

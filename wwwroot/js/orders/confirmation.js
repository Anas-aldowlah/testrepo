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

    function init() {
        initCopyTracking();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

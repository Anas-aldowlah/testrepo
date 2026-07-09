/**
 * ياقوت — Orders / Confirmation page interactions
 */
(function (window, document) {
    'use strict';

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
                navigator.clipboard.writeText(text).then(showCopied);
            } else {
                var temp = document.createElement('textarea');
                temp.value = text;
                document.body.appendChild(temp);
                temp.select();
                try { document.execCommand('copy'); } catch (e) { /* clipboard unavailable */ }
                document.body.removeChild(temp);
                showCopied();
            }
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
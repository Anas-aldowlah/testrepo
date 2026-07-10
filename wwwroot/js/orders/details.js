(function () {
    'use strict';

    function init() {
        var copyBtn = document.getElementById('yqDetailsCopyTrackingBtn');
        var trackingEl = document.getElementById('yqDetailsTrackingNumber');
        if (!copyBtn || !trackingEl) return;

        copyBtn.addEventListener('click', function () {
            var text = trackingEl.textContent.trim();
            if (!text) return;

            var markCopied = function () {
                var icon = copyBtn.querySelector('i');
                copyBtn.classList.add('is-copied');
                if (icon) icon.className = 'bi bi-check-lg';
                setTimeout(function () {
                    copyBtn.classList.remove('is-copied');
                    if (icon) icon.className = 'bi bi-clipboard';
                }, 1800);
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text).then(markCopied).catch(function () { });
            } else {
                try {
                    var temp = document.createElement('textarea');
                    temp.value = text;
                    temp.style.position = 'fixed';
                    temp.style.opacity = '0';
                    document.body.appendChild(temp);
                    temp.select();
                    document.execCommand('copy');
                    document.body.removeChild(temp);
                    markCopied();
                } catch (e) { }
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
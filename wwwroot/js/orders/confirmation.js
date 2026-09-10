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
        var status = document.getElementById('yqTrackingCopyStatus');
        if (!btn || !value) return;

        var copyAttempt = 0;
        var visualTimer = null;
        var statusTimer = null;
        var announcementFrame = null;

        function announce(message, attemptId) {
            if (!status) return;

            if (statusTimer) {
                clearTimeout(statusTimer);
                statusTimer = null;
            }
            if (announcementFrame) {
                if (typeof window.cancelAnimationFrame === 'function') {
                    window.cancelAnimationFrame(announcementFrame);
                } else {
                    clearTimeout(announcementFrame);
                }
                announcementFrame = null;
            }

            status.textContent = '';

            var schedule = typeof window.requestAnimationFrame === 'function'
                ? window.requestAnimationFrame
                : function (cb) { return setTimeout(cb, 16); };

            announcementFrame = schedule(function () {
                if (attemptId !== copyAttempt) {
                    announcementFrame = null;
                    return;
                }

                status.textContent = message;
                announcementFrame = null;
                statusTimer = setTimeout(function () {
                    if (attemptId !== copyAttempt) {
                        statusTimer = null;
                        return;
                    }
                    status.textContent = '';
                    statusTimer = null;
                }, 1800);
            });
        }

        btn.addEventListener('click', function () {
            var text = value.textContent.trim();
            if (!text) return;

            var attemptId = ++copyAttempt;
            var icon = btn.querySelector('i');

            function showCopied() {
                if (attemptId !== copyAttempt) return;

                if (visualTimer) {
                    clearTimeout(visualTimer);
                    visualTimer = null;
                }
                btn.classList.add('is-copied');
                if (icon) icon.className = 'bi bi-check-lg';
                visualTimer = setTimeout(function () {
                    btn.classList.remove('is-copied');
                    if (icon) icon.className = 'bi bi-clipboard';
                    visualTimer = null;
                }, 1800);

                announce('تم نسخ رقم التتبع.', attemptId);
            }

            function showFailed() {
                if (attemptId !== copyAttempt) return;

                if (visualTimer) {
                    clearTimeout(visualTimer);
                    visualTimer = null;
                }
                btn.classList.remove('is-copied');
                if (icon) icon.className = 'bi bi-clipboard';

                announce('تعذر نسخ رقم التتبع.', attemptId);
            }

            if (navigator.clipboard && navigator.clipboard.writeText) {
                Promise.resolve().then(function () {
                    return navigator.clipboard.writeText(text);
                }).then(function () {
                    showCopied();
                }).catch(function () {
                    if (attemptId !== copyAttempt) return;
                    if (copyWithSelection(text)) {
                        showCopied();
                    } else {
                        showFailed();
                    }
                });
            } else if (copyWithSelection(text)) {
                showCopied();
            } else {
                showFailed();
            }
        });
    }

    function initOrderSuccessModal() {
        var modalElement = document.getElementById('yqOrderSuccessModal');
        if (!modalElement || !window.bootstrap || !window.bootstrap.Modal) return;

        var pageElement = document.querySelector('[data-yq-confirmation-page]');
        var orderId = pageElement ? pageElement.getAttribute('data-yq-order-id') : null;
        var orderStatus = pageElement ? pageElement.getAttribute('data-yq-order-status') : null;
        var isJustConfirmed = pageElement && pageElement.getAttribute('data-yq-just-confirmed') === 'true';

        // Suppress pop-up for non-initial states (e.g. Cancelled, Shipped, Delivered)
        if (orderStatus && orderStatus !== 'Pending') {
            return;
        }

        var storageKey = orderId ? ('yq_order_confirmed_popup_' + orderId) : null;
        var alreadyShown = storageKey ? (window.sessionStorage.getItem(storageKey) === 'true') : false;

        var urlParams = new URLSearchParams(window.location.search);
        var forceShow = urlParams.get('showSuccessModal') === 'true' || urlParams.get('justConfirmed') === 'true';

        var shouldShow = forceShow || isJustConfirmed || !alreadyShown;

        if (shouldShow && (!alreadyShown || forceShow || isJustConfirmed)) {
            if (storageKey) {
                try {
                    window.sessionStorage.setItem(storageKey, 'true');
                } catch (e) {
                    // Ignore quota or disabled sessionStorage
                }
            }

            // Smooth entrance delay after DOM paint
            setTimeout(function () {
                try {
                    var modalInstance = window.bootstrap.Modal.getOrCreateInstance(modalElement);
                    modalInstance.show();
                } catch (err) {
                    // Fail gracefully if modal unmounted
                }
            }, 350);
        }
    }

    function init() {
        initCopyTracking();
        initOrderSuccessModal();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);


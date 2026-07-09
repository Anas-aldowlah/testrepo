/**
 * ياقوت — Cart / Index page interactions
 * Adds a smooth removal transition before the existing form submits.
 */
(function (window, document) {
    'use strict';

    function initRemoveTransition() {
        document.querySelectorAll('[data-yq-cart-item]').forEach(function (item) {
            var form = item.querySelector('.yq-cart-item__remove-form');
            if (!form) return;

            form.addEventListener('submit', function (e) {
                if (item.classList.contains('is-removing')) return;
                e.preventDefault();
                item.classList.add('is-removing');
                setTimeout(function () {
                    form.submit();
                }, 280);
            });
        });
    }

    function init() {
        initRemoveTransition();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
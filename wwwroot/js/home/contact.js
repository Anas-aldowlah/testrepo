/**
 * ياقوت — Home / Contact page interactions
 * Form validation reuses the shared data-yq-validate pattern from yaqut-main.js.
 * This file only handles the success-state scroll-into-view.
 */
(function (window, document) {
    'use strict';

    function scrollToSuccess() {
        var success = document.getElementById('yqContactSuccess');
        if (!success) return;
        success.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    function init() {
        scrollToSuccess();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
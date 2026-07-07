/**
 * ياقوت — Home / About page interactions
 * Staggered reveal for value & highlight cards (shared .yq-reveal observer from yaqut-main.js).
 */
(function (window, document) {
    'use strict';

    function initStaggeredReveal() {
        document.querySelectorAll('.yq-about-values .yq-about-value').forEach(function (el, i) {
            el.style.transitionDelay = Math.min(i * 70, 350) + 'ms';
        });
        document.querySelectorAll('.yq-about-highlights .yq-about-highlight').forEach(function (el, i) {
            el.style.transitionDelay = Math.min(i * 70, 350) + 'ms';
        });
    }

    function init() {
        initStaggeredReveal();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
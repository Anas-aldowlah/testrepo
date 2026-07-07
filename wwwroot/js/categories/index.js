/**
 * ياقوت — Categories / Index page interactions
 * Staggered reveal for category tiles (uses shared .yq-reveal IntersectionObserver in yaqut-main.js).
 */
(function (window, document) {
    'use strict';

    function initStaggeredReveal() {
        var tiles = document.querySelectorAll('.yq-cat-tile');
        tiles.forEach(function (tile, index) {
            tile.style.transitionDelay = Math.min(index * 60, 400) + 'ms';
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
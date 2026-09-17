/**
 * Yaqut — Storefront Live Promotions Countdown Timer Engine
 * Automatically ticks every second for all elements containing [data-yq-countdown]
 */
(function (window, document) {
    'use strict';

    function parseDate(dateStr) {
        if (!dateStr) return null;
        var date = new Date(dateStr);
        return isNaN(date.getTime()) ? null : date;
    }

    function formatTimeRemaining(ms) {
        if (ms <= 0) return null;

        var totalSec = Math.floor(ms / 1000);
        var days = Math.floor(totalSec / 86400);
        var hours = Math.floor((totalSec % 86400) / 3600);
        var mins = Math.floor((totalSec % 3600) / 60);
        var secs = totalSec % 60;

        var parts = [];
        if (days > 0) parts.push(days + ' يوم');
        if (hours > 0 || days > 0) parts.push((hours < 10 ? '0' + hours : hours) + ' ساعة');
        parts.push((mins < 10 ? '0' + mins : mins) + ' دقيقة');
        parts.push((secs < 10 ? '0' + secs : secs) + ' ثانية');

        return parts.join(' ');
    }

    function renderTiles(container, days, hours, mins, secs) {
        var pad = function(n) { return n < 10 ? '0' + n : '' + n; };
        var dStr = pad(days);
        var hStr = pad(hours);
        var mStr = pad(mins);
        var sStr = pad(secs);

        var numEls = container.querySelectorAll('.yq-timer-num');
        if (numEls && numEls.length === 4) {
            numEls[0].textContent = dStr;
            numEls[1].textContent = hStr;
            numEls[2].textContent = mStr;
            numEls[3].textContent = sStr;
        } else {
            container.innerHTML =
                '<div class="yq-timer-tiles">' +
                    '<div class="yq-timer-tile"><span class="yq-timer-num">' + dStr + '</span><span class="yq-timer-unit">يوم</span></div>' +
                    '<span class="yq-timer-sep">:</span>' +
                    '<div class="yq-timer-tile"><span class="yq-timer-num">' + hStr + '</span><span class="yq-timer-unit">ساعة</span></div>' +
                    '<span class="yq-timer-sep">:</span>' +
                    '<div class="yq-timer-tile"><span class="yq-timer-num">' + mStr + '</span><span class="yq-timer-unit">دقيقة</span></div>' +
                    '<span class="yq-timer-sep">:</span>' +
                    '<div class="yq-timer-tile"><span class="yq-timer-num">' + sStr + '</span><span class="yq-timer-unit">ثانية</span></div>' +
                '</div>';
        }
    }

    function updateTimers() {
        var elements = document.querySelectorAll('[data-yq-countdown]');
        if (!elements.length) return;

        var now = new Date().getTime();

        elements.forEach(function (el) {
            var targetStr = el.getAttribute('data-yq-countdown');
            var startDateStr = el.getAttribute('data-yq-startdate');
            
            var targetDate = parseDate(targetStr);
            var startDate = parseDate(startDateStr);

            if (!targetDate) return;

            var targetMs = targetDate.getTime();
            var startMs = startDate ? startDate.getTime() : 0;

            var displayEl = el.querySelector('[data-yq-timer-display]') || el;
            var isTilesMode = el.hasAttribute('data-yq-timer-tiles') || el.classList.contains('yq-promo-timer--tiles');

            // If promotion hasn't started yet
            if (startMs > now) {
                var upcomingDiff = startMs - now;
                var totalSec = Math.floor(upcomingDiff / 1000);
                var days = Math.floor(totalSec / 86400);
                var hours = Math.floor((totalSec % 86400) / 3600);
                var mins = Math.floor((totalSec % 3600) / 60);
                var secs = totalSec % 60;

                if (isTilesMode) {
                    renderTiles(displayEl, days, hours, mins, secs);
                } else {
                    displayEl.textContent = 'يبدأ خلال: ' + formatTimeRemaining(upcomingDiff);
                }
                return;
            }

            var diff = targetMs - now;
            if (diff <= 0) {
                displayEl.innerHTML = '<span class="yq-timer-expired">انتهى العرض</span>';
                el.classList.add('is-expired');
                var parentCard = el.closest('.yq-col-item, .yq-pdp-promo-box');
                if (parentCard && el.getAttribute('data-yq-autohide') === 'true') {
                    parentCard.style.display = 'none';
                }
            } else {
                var totalSec = Math.floor(diff / 1000);
                var days = Math.floor(totalSec / 86400);
                var hours = Math.floor((totalSec % 86400) / 3600);
                var mins = Math.floor((totalSec % 3600) / 60);
                var secs = totalSec % 60;

                if (isTilesMode) {
                    renderTiles(displayEl, days, hours, mins, secs);
                } else {
                    displayEl.textContent = 'ينتهي خلال: ' + formatTimeRemaining(diff);
                }
            }
        });
    }

    function schedulePromotionTransitionRefresh() {
        var transition = parseDate(document.body.getAttribute('data-yq-next-promotion-transition'));
        if (!transition) return;

        var maxTimeout = 2147483647;
        var refreshAtTransition = function () {
            var remaining = transition.getTime() - Date.now();
            if (remaining <= 0) {
                window.location.reload();
                return;
            }

            window.setTimeout(refreshAtTransition, Math.min(remaining + 250, maxTimeout));
        };

        refreshAtTransition();
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && transition.getTime() <= Date.now()) {
                window.location.reload();
            }
        });
    }

    function init() {
        updateTimers();
        setInterval(updateTimers, 1000);
        schedulePromotionTransitionRefresh();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

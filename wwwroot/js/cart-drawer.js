/**
 * Yaqoot — Premium Cart Peek
 * Enhances the existing server-rendered cart flow without changing cart business logic.
 */
(function (window, document) {
    'use strict';

    var drawer = document.querySelector('[data-yq-cart-drawer]');
    if (!drawer) return;

    var body = drawer.querySelector('[data-yq-cart-drawer-body]');
    var footer = drawer.querySelector('[data-yq-cart-drawer-footer]');
    var titleEl = drawer.querySelector('#yqCartDrawerTitle');
    var countLabel = drawer.querySelector('[data-yq-cart-drawer-count]');
    var notice = drawer.querySelector('[data-yq-cart-drawer-notice]');
    var totalEl = drawer.querySelector('[data-yq-cart-drawer-total]');
    var shipping = drawer.querySelector('[data-yq-cart-drawer-shipping]');
    var shippingLabel = drawer.querySelector('[data-yq-cart-shipping-label]');
    var shippingValue = drawer.querySelector('[data-yq-cart-shipping-value]');
    var shippingProgress = drawer.querySelector('[data-yq-cart-shipping-progress]');
    var peekTimer = drawer.querySelector('[data-yq-cart-peek-timer]');
    var live = drawer.querySelector('[data-yq-cart-live]');
    var badge = document.querySelector('[data-yq-cart-count]');
    var checkoutLink = drawer.querySelector('[data-yq-cart-checkout]');
    var reduceMotion = typeof window.matchMedia === 'function'
        ? window.matchMedia('(prefers-reduced-motion: reduce)')
        : { matches: false };
    var restoreFocusEl = null;
    var drawerTransitionVersion = 0;
    var cartRefreshVersion = 0;
    var cachedCartDoc = null;
    var peekAutoCloseTimer = null;
    var peekAutoCloseVersion = 0;
    var peekOpenedAt = 0;
    var isSettingInitialFocus = false;
    var flightLayer = null;
    var toastEl = null;
    var toastAutoHideTimer = null;
    var PEEK_AUTO_CLOSE_DELAY = 4000;

    function requestFrame(callback) {
        if (typeof window.requestAnimationFrame === 'function') {
            window.requestAnimationFrame(callback);
        } else {
            window.setTimeout(callback, 0);
        }
    }

    function toArray(list) {
        return Array.prototype.slice.call(list || []);
    }

    function normalizeDigits(value) {
        var arabic = '٠١٢٣٤٥٦٧٨٩';
        var persian = '۰۱۲۳۴۵۶۷۸۹';
        return String(value || '').replace(/[٠-٩۰-۹]/g, function (digit) {
            var arabicIndex = arabic.indexOf(digit);
            if (arabicIndex > -1) return arabicIndex;
            return persian.indexOf(digit);
        });
    }

    function escapeHtml(value) {
        return String(value || '').replace(/[&<>"']/g, function (char) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[char];
        });
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/`/g, '&#96;');
    }

    function bindImageFallbacks(root) {
        if (!root) return;
        root.querySelectorAll('[data-yaqut-fallback]').forEach(function (img) {
            img.addEventListener('error', function () {
                var fallback = img.getAttribute('data-yaqut-fallback');
                if (fallback && img.getAttribute('src') !== fallback) img.src = fallback;
            }, { once: true });
        });
    }

    function parseMoney(text) {
        var normalized = normalizeDigits(text).replace(/[^\d.,]/g, '').replace(/,/g, '');
        var value = parseFloat(normalized);
        return Number.isFinite(value) ? value : 0;
    }

    function productCountLabel(count) {
        if (count <= 0) return 'السلة فارغة حالياً';
        if (count === 1) return 'منتج واحد في السلة';
        if (count === 2) return 'منتجان في السلة';
        if (count <= 10) return count + ' منتجات في السلة';
        return count + ' منتجاً في السلة';
    }

    function quantityLabel(count) {
        if (count <= 0) return '0 منتجات';
        if (count === 1) return 'منتج واحد';
        if (count === 2) return 'منتجان';
        if (count <= 10) return count + ' منتجات';
        return count + ' منتجاً';
    }

    function announce(message) {
        if (!message || !live) return;
        live.textContent = '';
        window.setTimeout(function () {
            live.textContent = message;
        }, 20);
    }

    function ensureFlightLayer() {
        if (flightLayer) return flightLayer;
        flightLayer = document.createElement('div');
        flightLayer.className = 'yq-cart-flight-layer';
        flightLayer.setAttribute('aria-hidden', 'true');
        document.body.appendChild(flightLayer);
        return flightLayer;
    }

    function ensureToastEl() {
        if (toastEl) return toastEl;
        toastEl = document.createElement('div');
        toastEl.className = 'yq-cart-toast';
        toastEl.setAttribute('role', 'status');
        toastEl.setAttribute('aria-live', 'polite');
        document.body.appendChild(toastEl);
        return toastEl;
    }

    function hideCartToast() {
        if (toastAutoHideTimer) {
            window.clearTimeout(toastAutoHideTimer);
            toastAutoHideTimer = null;
        }
        if (!toastEl) return;
        toastEl.classList.add('is-hiding');
        toastEl.classList.remove('is-visible');
        window.setTimeout(function () {
            if (toastEl) toastEl.classList.remove('is-hiding');
        }, 350);
    }

    function showCartToast(snapshot, totalText) {
        var el = ensureToastEl();
        var autoHideDuration = 3500;
        var imgSrc = escapeAttr(snapshot.imageSrc || '/images/placeholder-product.svg');
        var name = escapeHtml(snapshot.name || 'منتج');
        var total = escapeHtml(totalText || '');

        el.innerHTML = [
            '<img class="yq-cart-toast__img" src="' + imgSrc + '" alt="" data-yaqut-fallback="/images/placeholder-product.svg" decoding="async" />',
            '<div class="yq-cart-toast__body">',
            '  <p class="yq-cart-toast__title">',
            '    <i class="bi bi-check-circle-fill" aria-hidden="true"></i>',
            '    <span class="yq-cart-toast__name">' + name + '</span>',
            '  </p>',
            '  <div class="yq-cart-toast__meta">',
            '    <span>أُضيف إلى السلة</span>',
            total ? '    <span class="yq-cart-toast__total">الإجمالي: ' + total + '</span>' : '',
            '  </div>',
            '</div>',
            '<button type="button" class="yq-cart-toast__close" aria-label="إغلاق"><i class="bi bi-x" aria-hidden="true"></i></button>',
            '<span class="yq-cart-toast__progress"></span>'
        ].join('');
        bindImageFallbacks(el);
        var progressEl = el.querySelector('.yq-cart-toast__progress');
        if (progressEl) progressEl.style.transitionDuration = autoHideDuration + 'ms';

        // close button
        var closeBtn = el.querySelector('.yq-cart-toast__close');
        if (closeBtn) {
            closeBtn.onclick = function () { hideCartToast(); };
        }

        // reset and show
        el.classList.remove('is-hiding', 'is-visible');
        requestFrame(function () {
            el.classList.add('is-visible');
        });

        // auto hide
        if (toastAutoHideTimer) window.clearTimeout(toastAutoHideTimer);
        toastAutoHideTimer = window.setTimeout(function () {
            hideCartToast();
        }, autoHideDuration);
    }

    function clearPeekAutoCloseTimer() {
        peekAutoCloseVersion += 1;
        if (peekAutoCloseTimer) {
            window.clearTimeout(peekAutoCloseTimer);
            peekAutoCloseTimer = null;
        }
        if (peekTimer) {
            peekTimer.classList.remove('is-animating');
            peekTimer.style.removeProperty('--yq-peek-duration');
        }
    }

    function schedulePeekAutoClose(delay) {
        clearPeekAutoCloseTimer();
        if (!Number.isFinite(delay) || delay < 0) delay = 0;
        var timerVersion = peekAutoCloseVersion;
        peekAutoCloseTimer = window.setTimeout(function () {
            if (timerVersion !== peekAutoCloseVersion) return;
            closeDrawer();
        }, delay);
        if (peekTimer) {
            peekTimer.style.setProperty('--yq-peek-duration', delay + 'ms');
            requestFrame(function () {
                if (!peekTimer || !peekAutoCloseTimer || timerVersion !== peekAutoCloseVersion) return;
                peekTimer.classList.add('is-animating');
            });
        }
    }

    function showAuthModal(modalId) {
        if (!modalId) return;
        var modalElement = document.getElementById(modalId);
        if (!modalElement || !window.bootstrap || !window.bootstrap.Modal) return;
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    function showNotice(message, isError) {
        if (!notice) return;
        if (!message) {
            notice.hidden = true;
            notice.textContent = '';
            notice.classList.remove('is-error');
            return;
        }

        notice.hidden = false;
        notice.textContent = message;
        notice.classList.toggle('is-error', Boolean(isError));
    }

    function setPeekTitle(title, helperText) {
        if (titleEl) titleEl.textContent = title || 'سلة التسوق';
        if (countLabel) countLabel.textContent = helperText || '';
    }

    function setCheckoutAvailable(isAvailable) {
        if (!checkoutLink) return;
        checkoutLink.classList.toggle('is-disabled', !isAvailable);
        checkoutLink.setAttribute('aria-disabled', isAvailable ? 'false' : 'true');
        checkoutLink.tabIndex = isAvailable ? 0 : -1;
    }

    function isErrorMessage(message) {
        if (!message) return false;
        return /غير متوفر|غير موجود|فشلت|تعذّر|تعذر/i.test(message);
    }

    function setBadge(totalQuantity) {
        if (!badge) return;
        if (totalQuantity > 0) {
            badge.hidden = false;
            badge.textContent = totalQuantity > 99 ? '99+' : String(totalQuantity);
            badge.setAttribute('aria-label', quantityLabel(totalQuantity) + ' في السلة');
            badge.classList.remove('is-updated');
            requestFrame(function () {
                badge.classList.add('is-updated');
            });
        } else {
            badge.hidden = true;
            badge.textContent = '0';
            badge.setAttribute('aria-label', 'السلة فارغة');
        }
    }

    function animateBadgeNudge() {
        if (!badge) return;
        badge.classList.remove('is-updated');
        requestFrame(function () {
            badge.classList.add('is-updated');
        });
    }

    function parseCartDoc(html) {
        var doc = new window.DOMParser().parseFromString(html, 'text/html');
        if (!doc.querySelector('[data-yq-cart-page]')) {
            throw new Error('cart-response-invalid');
        }
        return doc;
    }

    function getCartMessage(doc) {
        var alert = doc.querySelector('.yq-cart .yaqut-alert');
        return alert ? alert.textContent.replace(/\s+/g, ' ').trim() : '';
    }

    function getSummary(doc) {
        var summary = doc.querySelector('.yq-cart-summary');
        var totalText = '';
        var subtotalText = '';
        var shippingText = '';
        var qualifiesForFreeShipping = false;

        if (summary) {
            // Try the new cart structure first: data-yq-summary-total + sibling <small>
            var summaryTotalEl = summary.querySelector('[data-yq-summary-total]');
            if (summaryTotalEl) {
                var totalBox = summaryTotalEl.closest('.yq-cart-summary__total-value, .yq-cart-summary__total-box');
                if (totalBox) {
                    // Grab the number + the currency text from <small>
                    var numText = summaryTotalEl.textContent.trim();
                    var smallEl = totalBox.querySelector('small');
                    var currency = smallEl ? smallEl.textContent.trim() : 'ر.س';
                    totalText = numText + ' ' + currency;
                } else {
                    totalText = summaryTotalEl.textContent.trim();
                }
                subtotalText = totalText;
            }

            // Fallback: old drawer structure using rows
            if (!totalText) {
                var rows = toArray(summary.querySelectorAll('.yq-cart-summary__row'));
                if (rows[0]) {
                    var subtotalNode = rows[0].querySelector('span:last-child');
                    subtotalText = subtotalNode ? subtotalNode.textContent.trim() : '';
                }
                var totalNode = summary.querySelector('.yq-cart-summary__row--total span:last-child');
                totalText = totalNode ? totalNode.textContent.trim() : subtotalText;
            }

            var shippingHint = summary.querySelector('.yq-cart-summary__shipping-hint');
            if (shippingHint) {
                shippingText = shippingHint.textContent.replace(/\s+/g, ' ').trim();
                qualifiesForFreeShipping = shippingHint.classList.contains('yq-cart-summary__shipping-hint--success');
            }
        }

        return {
            subtotal: parseMoney(totalText || subtotalText),
            totalText: totalText,
            shippingText: shippingText,
            qualifiesForFreeShipping: qualifiesForFreeShipping
        };
    }

    function formHiddenHtml(form) {
        if (!form) return '';
        return toArray(form.querySelectorAll('input[type="hidden"], input[name="__RequestVerificationToken"]'))
            .filter(function (input) { return input.name !== 'quantity'; })
            .map(function (input) { return input.outerHTML; })
            .join('');
    }

    function extractItems(doc) {
        return toArray(doc.querySelectorAll('.yq-cart-item')).map(function (item) {
            var nameLink = item.querySelector('.yq-cart-item__name');
            var category = item.querySelector('.yq-cart-item__category');
            var media = item.querySelector('.yq-cart-item__media');
            var image = item.querySelector('.yq-cart-item__media img');
            var qtyForm = item.querySelector('.yq-cart-item__qty-block');
            var removeForm = item.querySelector('.yq-cart-item__remove-form');
            var qtyInput = qtyForm ? qtyForm.querySelector('[data-yq-cart-qty-value]') : null;
            var customQtyInput = qtyForm ? qtyForm.querySelector('[data-yq-cart-qty-custom]') : null;
            var productIdInput = qtyForm ? qtyForm.querySelector('input[name="productId"]') : null;
            var lineTotal = item.querySelector('.yq-cart-item__line-total');
            var unitPrice = item.querySelector('.yq-cart-item__unit-price');
            var quantity = qtyInput ? parseInt(normalizeDigits(qtyInput.value), 10) : 1;

            return {
                productId: productIdInput ? productIdInput.value : '',
                name: nameLink ? nameLink.textContent.trim() : 'منتج من ياقوت',
                href: nameLink ? nameLink.href : (media ? media.href : '#'),
                category: category ? category.textContent.trim() : '',
                imageSrc: image ? image.getAttribute('src') : '/images/placeholder-product.svg',
                imageAlt: image ? image.getAttribute('alt') : '',
                quantity: Number.isFinite(quantity) ? quantity : 1,
                max: customQtyInput ? customQtyInput.getAttribute('max') : '',
                unitPrice: unitPrice ? unitPrice.textContent.replace(/\s+/g, ' ').trim() : '',
                lineTotal: lineTotal ? lineTotal.textContent.replace(/\s+/g, ' ').trim() : '',
                qtyAction: qtyForm ? qtyForm.action : '',
                removeAction: removeForm ? removeForm.action : '',
                qtyHidden: formHiddenHtml(qtyForm),
                removeHidden: formHiddenHtml(removeForm)
            };
        });
    }

    function renderEmpty() {
        setPeekTitle('سلة التسوق', 'السلة فارغة حالياً');
        body.innerHTML = [
            '<div class="yq-cart-drawer__empty" role="status">',
            '<span class="yq-cart-drawer__empty-icon"><i class="bi bi-bag-heart" aria-hidden="true"></i></span>',
            '<h3>سلتك فارغة</h3>',
            '<p>ابدأ باختيار عطرك المفضل، وسنبقي السلة قريبة منك أثناء التسوق.</p>',
            '</div>'
        ].join('');
        if (footer) footer.hidden = false;
        setCheckoutAvailable(false);
        if (totalEl) totalEl.textContent = '0 ر.س';
        if (shipping) shipping.hidden = true;
    }

    function getProductSnapshot(form) {
        var card = form.closest('[data-yq-product-card]');
        var productId = form.querySelector('input[name="productId"]');
        var retailPriceId = form.querySelector('input[name="retailPriceId"]');
        var quantityInput = form.querySelector('input[name="quantity"]');
        var image = card ? card.querySelector('.yq-pcard__img, .yaqut-card-img, img') : document.querySelector('.yq-pdp-gallery__img');
        var nameLink = card ? card.querySelector('.yq-pcard__name-link, .yaqut-product-card__name-link') : null;
        var price = card ? card.querySelector('.yq-pcard__price, .yaqut-product-card__price') : document.querySelector('.yq-pdp-price');
        var category = card ? card.querySelector('.yq-pcard__family, .yaqut-product-card__family') : document.querySelector('.yq-pdp-category');
        var href = nameLink ? nameLink.href : window.location.href;
        var name = form.getAttribute('data-product-name') || (card ? card.getAttribute('data-product-name') : '') || (nameLink ? nameLink.textContent.trim() : document.title);
        var quantity = quantityInput ? parseInt(normalizeDigits(quantityInput.value), 10) : 1;

        return {
            productId: productId ? productId.value : '',
            retailPriceId: retailPriceId ? retailPriceId.value : '',
            name: name || 'عطر من ياقوت',
            href: href,
            category: category ? category.textContent.trim() : '',
            imageSrc: image ? image.getAttribute('src') : '/images/placeholder-product.svg',
            imageAlt: image ? image.getAttribute('alt') : name,
            sourceImageEl: image || null,
            sourceCardEl: card || null,
            quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : 1,
            priceText: price ? price.textContent.replace(/\s+/g, ' ').trim() : ''
        };
    }

    function animateProductFlight(snapshot) {
        if (reduceMotion.matches || !snapshot || !snapshot.sourceImageEl || !badge) return;

        var sourceRect = snapshot.sourceImageEl.getBoundingClientRect();
        var cartLink = badge.closest('.yaqut-cart-link');
        var badgeRect = badge.hidden || !badge.getBoundingClientRect().width ? (cartLink ? cartLink.getBoundingClientRect() : badge.getBoundingClientRect()) : badge.getBoundingClientRect();
        if (!sourceRect.width || !sourceRect.height || !badgeRect.width || !badgeRect.height) return;

        var layer = ensureFlightLayer();
        var ghost = document.createElement('div');
        ghost.className = 'yq-cart-flight';
        ghost.style.left = sourceRect.left + 'px';
        ghost.style.top = sourceRect.top + 'px';
        ghost.style.width = sourceRect.width + 'px';
        ghost.style.height = sourceRect.height + 'px';
        ghost.innerHTML = [
            '<img src="' + escapeAttr(snapshot.imageSrc) + '" alt="" aria-hidden="true" decoding="async" />',
            '<span>+' + snapshot.quantity + '</span>'
        ].join('');
        layer.appendChild(ghost);

        var dx = (badgeRect.left + badgeRect.width * 0.5) - (sourceRect.left + sourceRect.width * 0.5);
        var dy = (badgeRect.top + badgeRect.height * 0.5) - (sourceRect.top + sourceRect.height * 0.5);
        var scale = Math.max(0.16, Math.min(0.48, badgeRect.width / Math.max(sourceRect.width, 1) * 0.55));

        requestFrame(function () {
            ghost.style.transform = 'translate(' + dx + 'px, ' + dy + 'px) scale(' + scale + ')';
            ghost.style.opacity = '0';
        });

        window.setTimeout(function () {
            ghost.remove();
        }, 820);
    }

    function renderItem(item, highlightProductId) {
        var highlighted = highlightProductId && String(item.productId) === String(highlightProductId);
        var disabledMinus = item.quantity <= 1 ? ' disabled' : '';
        var max = item.max ? ' max="' + escapeAttr(item.max) + '"' : '';
        var itemName = escapeHtml(item.name);
        var itemNameAttr = escapeAttr(item.name);
        var itemHref = escapeAttr(item.href);
        var itemImage = escapeAttr(item.imageSrc);
        var itemAlt = escapeAttr(item.imageAlt || item.name);
        var itemCategory = escapeHtml(item.category);
        var productId = escapeAttr(item.productId);
        var qtyAction = escapeAttr(item.qtyAction);
        var removeAction = escapeAttr(item.removeAction);

        return [
            '<li class="yq-cart-drawer__item' + (highlighted ? ' is-highlighted' : '') + '" data-yq-drawer-item data-product-id="' + productId + '">',
            '<a class="yq-cart-drawer__media" href="' + itemHref + '">',
            '<img src="' + itemImage + '" alt="' + itemAlt + '" data-yaqut-fallback="/images/placeholder-product.svg" loading="lazy" decoding="async" />',
            '</a>',
            '<div class="yq-cart-drawer__item-body">',
            '<div class="yq-cart-drawer__item-head">',
            '<div>',
            item.category ? '<span class="yq-cart-drawer__category">' + itemCategory + '</span>' : '',
            '<a class="yq-cart-drawer__name" href="' + itemHref + '">' + itemName + '</a>',
            '</div>',
            '<form class="yq-cart-drawer__remove-form" action="' + removeAction + '" method="post" data-yq-drawer-remove-form>',
            item.removeHidden,
            '<button type="submit" class="yq-cart-drawer__remove" aria-label="إزالة ' + itemNameAttr + ' من السلة"><i class="bi bi-trash3" aria-hidden="true"></i></button>',
            '</form>',
            '</div>',
            '<div class="yq-cart-drawer__row">',
            '<form class="yq-cart-drawer__qty-form" action="' + qtyAction + '" method="post" data-yq-drawer-qty-form>',
            item.qtyHidden,
            '<div class="yq-cart-drawer__qty" aria-label="تحديث كمية ' + itemNameAttr + '">',
            '<button type="button" class="yq-cart-drawer__qty-btn" data-yq-qty-step="-1" aria-label="تقليل الكمية"' + disabledMinus + '><i class="bi bi-dash" aria-hidden="true"></i></button>',
            '<input class="yq-cart-drawer__qty-input" type="number" name="quantity" min="1"' + max + ' value="' + item.quantity + '" aria-label="كمية ' + itemNameAttr + '" />',
            '<button type="button" class="yq-cart-drawer__qty-btn" data-yq-qty-step="1" aria-label="زيادة الكمية"><i class="bi bi-plus" aria-hidden="true"></i></button>',
            '</div>',
            '</form>',
            '<div class="yq-cart-drawer__prices">',
            item.unitPrice ? '<span class="yq-cart-drawer__unit">' + escapeHtml(item.unitPrice) + '</span>' : '',
            '<strong class="yq-cart-drawer__line-total">' + escapeHtml(item.lineTotal) + '</strong>',
            '</div>',
            '</div>',
            '</div>',
            '</li>'
        ].join('');
    }

    function updateShipping(summary) {
        if (!shipping || !shippingProgress || !shippingLabel || !shippingValue) return;

        var threshold = parseMoney(drawer.getAttribute('data-free-shipping-threshold'));
        if (!threshold || summary.subtotal <= 0) {
            shipping.hidden = true;
            return;
        }

        shipping.hidden = false;
    }

    function renderCart(doc, options) {
        var items = extractItems(doc);
        var uniqueCount = items.length;
        var totalQuantity = items.reduce(function (sum, item) { return sum + item.quantity; }, 0);
        var summary = getSummary(doc);
        var message = getCartMessage(doc);
        var hasError = isErrorMessage(message);

        cachedCartDoc = doc;
        setPeekTitle(items.length ? 'سلة التسوق' : 'سلة التسوق', productCountLabel(uniqueCount));
        setBadge(totalQuantity);
        if (countLabel) countLabel.textContent = productCountLabel(uniqueCount);
        showNotice(options && options.notice ? options.notice : message, hasError);

        if (!items.length) {
            renderEmpty();
        } else {
            body.innerHTML = '<ul class="yq-cart-drawer__list">' + items.map(function (item) {
                return renderItem(item, options && options.highlightProductId);
            }).join('') + '</ul>';
            bindImageFallbacks(body);

            if (footer) footer.hidden = false;
            setCheckoutAvailable(true);
            if (totalEl) totalEl.textContent = summary.totalText || '0 ر.س';
            updateShipping(summary);
        }

        announce(options && options.announcement ? options.announcement : message);
        return {
            items: items,
            totalQuantity: totalQuantity,
            totalText: summary.totalText,
            message: message,
            hasError: hasError
        };
    }

    function renderRefreshError() {
        setPeekTitle('سلة التسوق', 'تعذّر تحديث السلة');
        body.replaceChildren();

        var state = document.createElement('div');
        state.className = 'yq-cart-drawer__empty';
        state.setAttribute('role', 'alert');

        var title = document.createElement('h3');
        title.textContent = 'تعذّر تحديث السلة';
        var message = document.createElement('p');
        message.textContent = 'تحقق من الاتصال ثم أعد فتح السلة، أو انتقل إلى صفحة السلة.';
        var fallback = document.createElement('a');
        fallback.className = 'btn-yaqut-outline';
        fallback.href = drawer.getAttribute('data-cart-url') || '/Cart';
        fallback.textContent = 'فتح صفحة السلة';

        state.append(title, message, fallback);
        body.appendChild(state);
        if (footer) footer.hidden = false;
        setCheckoutAvailable(false);
        if (shipping) shipping.hidden = true;
    }

    function openDrawer(trigger, origin) {
        var transitionVersion = ++drawerTransitionVersion;
        var shouldAutoClose = origin === 'add-to-cart';
        restoreFocusEl = trigger || document.activeElement;
        drawer.hidden = false;
        drawer.inert = false;
        drawer.setAttribute('aria-hidden', 'false');
        clearPeekAutoCloseTimer();
        var autoCloseVersion = peekAutoCloseVersion;

        requestFrame(function () {
            if (transitionVersion !== drawerTransitionVersion) return;
            drawer.classList.add('is-open');
            var initialFocus = drawer.querySelector('[data-yq-cart-close]:not([tabindex="-1"])') || drawer.querySelector('.yq-cart-drawer__panel');
            if (initialFocus && typeof initialFocus.focus === 'function') {
                isSettingInitialFocus = true;
                try {
                    initialFocus.focus({ preventScroll: true });
                } finally {
                    isSettingInitialFocus = false;
                }
            }
            if (shouldAutoClose && autoCloseVersion === peekAutoCloseVersion) {
                schedulePeekAutoClose(PEEK_AUTO_CLOSE_DELAY);
            }
        });

    }

    function isDrawerOpen() {
        return drawer.getAttribute('aria-hidden') === 'false';
    }

    function closeDrawer() {
        var transitionVersion = ++drawerTransitionVersion;
        cartRefreshVersion += 1;
        clearPeekAutoCloseTimer();
        peekOpenedAt = 0;
        if (peekTimer) {
            peekTimer.classList.remove('is-animating');
            peekTimer.style.removeProperty('--yq-peek-duration');
        }
        drawer.classList.remove('is-open');
        if (restoreFocusEl && document.contains(restoreFocusEl) && drawer.contains(document.activeElement)) {
            restoreFocusEl.focus({ preventScroll: true });
        }
        if (drawer.contains(document.activeElement) && typeof document.activeElement.blur === 'function') {
            document.activeElement.blur();
        }
        drawer.inert = true;
        drawer.setAttribute('aria-hidden', 'true');

        window.setTimeout(function () {
            if (transitionVersion !== drawerTransitionVersion) return;
            drawer.hidden = true;
        }, reduceMotion.matches ? 0 : 240);
    }

    function fetchCartPage() {
        var cartUrl = drawer.getAttribute('data-cart-url') || '/Cart';
        return window.fetch(cartUrl, {
            method: 'GET',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) throw new Error('cart-refresh-failed');
            return response.text();
        }).then(parseCartDoc);
    }

    function submitForm(form, expectsJson) {
        var headers = { 'X-Requested-With': 'XMLHttpRequest' };
        if (expectsJson) headers.Accept = 'application/json';

        return window.fetch(form.action, {
            method: (form.method || 'post').toUpperCase(),
            body: new window.FormData(form),
            credentials: 'same-origin',
            headers: headers
        }).then(function (response) {
            if (expectsJson) {
                return response.json().then(function (state) {
                    if (!response.ok || !state.success) {
                        var error = new Error('cart-submit-failed');
                        error.state = state;
                        throw error;
                    }
                    return state;
                });
            }
            if (!response.ok) throw new Error('cart-submit-failed');
            return response.text();
        }).then(function (payload) {
            return expectsJson ? payload : parseCartDoc(payload);
        });
    }

    function refreshCart(options) {
        var refreshVersion = ++cartRefreshVersion;
        return fetchCartPage()
            .then(function (doc) {
                if (refreshVersion !== cartRefreshVersion) return null;
                return reconcileCart(doc, options || {});
            })
            .catch(function (error) {
                if (refreshVersion !== cartRefreshVersion) return;
                if (!(options && options.silent)) {
                    if (cachedCartDoc) renderCart(cachedCartDoc, {});
                    else renderRefreshError();
                    showNotice('تعذّر تحديث السلة الآن. حاول مرة أخرى.', true);
                    announce('تعذّر تحديث السلة الآن. حاول مرة أخرى.');
                }
                if (options && options.throwOnError) throw error;
                return null;
            });
    }

    function setAddButtonState(form, state, message) {
        var button = form.querySelector('[data-yq-add-button], button[type="submit"]');
        var label = button ? button.querySelector('[data-yq-add-label]') : null;
        var status = form.closest('[data-yq-product-card]') ? form.closest('[data-yq-product-card]').querySelector('[data-yq-add-status]') : null;
        var stockUnavailable = form.dataset.yqStockUnavailable === 'true';

        if (!button) return;
        if (!button.dataset.originalHtml) button.dataset.originalHtml = button.innerHTML;

        button.classList.remove('is-loading', 'is-success');
        if (status) {
            status.classList.remove('is-error');
            status.textContent = '';
        }

        if (state === 'loading') {
            button.disabled = true;
            button.classList.add('is-loading');
            if (label) label.textContent = 'جارٍ الإضافة...';
            else button.innerHTML = '<i class="bi bi-arrow-repeat" aria-hidden="true"></i> جارٍ الإضافة...';
        } else if (state === 'success') {
            button.disabled = stockUnavailable;
            button.classList.add('is-success');
            if (label) label.textContent = message || 'تمت الإضافة';
            else button.innerHTML = '<i class="bi bi-check2" aria-hidden="true"></i> ' + (message || 'تمت الإضافة');
            if (status) status.textContent = message || 'تمت الإضافة إلى السلة';
            window.setTimeout(function () {
                button.classList.remove('is-success');
                button.innerHTML = button.dataset.originalHtml;
                if (status) status.textContent = '';
            }, reduceMotion.matches ? 400 : 1500);
        } else if (state === 'error') {
            button.disabled = stockUnavailable;
            button.innerHTML = button.dataset.originalHtml;
            if (status) {
                status.classList.add('is-error');
                status.textContent = message || 'تعذّرت الإضافة. حاول مرة أخرى.';
            }
        } else {
            button.disabled = stockUnavailable;
            button.innerHTML = button.dataset.originalHtml;
        }
        button.setAttribute('aria-disabled', button.disabled ? 'true' : 'false');
    }

    function handleAdd(form) {
        if (form.dataset.yqBusy === 'true') return;
        var snapshot = getProductSnapshot(form);

        form.dataset.yqBusy = 'true';
        setAddButtonState(form, 'loading');

        submitForm(form, true)
            .then(function (state) {
                return refreshCart({
                    silent: false,
                    throwOnError: true,
                    highlightProductId: snapshot.productId,
                    notice: state.message,
                    announcement: state.message
                }).then(function (result) {
                    return { state: state, result: result };
                });
            })
            .then(function (payload) {
                if (!payload || !payload.result) throw new Error('cart-reconciliation-failed');

                var state = payload.state;
                var result = payload.result;
                var isWarning = state.warningCode === 'InsufficientStock';
                if (isErrorMessage(state.message) && !isWarning) {
                    setAddButtonState(form, 'error', state.message);
                    hideCartToast();
                    announce(state.message);
                    return;
                }

                var authoritativeItem = result.items.find(function (item) {
                    var itemRetailPriceId = item.retailPriceId === null || item.retailPriceId === undefined
                        ? ''
                        : String(item.retailPriceId);
                    return String(item.productId) === String(snapshot.productId) &&
                        itemRetailPriceId === String(snapshot.retailPriceId || '');
                });
                if (!authoritativeItem) throw new Error('cart-added-item-missing');

                animateProductFlight(snapshot);
                animateBadgeNudge();
                setAddButtonState(form, 'success', state.message && !isWarning ? 'تم تحديث السلة' : 'تمت الإضافة');
                showCartToast(authoritativeItem, result.totalText, isWarning ? state.message : null);
                if (isWarning && window.YaqutOperationDialog) {
                    window.YaqutOperationDialog.show({ title: 'الكمية غير متوفرة', message: state.message });
                }
                openDrawer(form.querySelector('[data-yq-add-button], button[type="submit"]'), 'add-to-cart');
            })
            .catch(function (error) {
                setAddButtonState(form, 'error', (error.state && (error.state.message || error.state.detail)) || 'تعذّرت الإضافة. حاول مرة أخرى.');
                hideCartToast();
                announce('تعذّرت الإضافة. حاول مرة أخرى.');
            })
            .finally(function () {
                form.dataset.yqBusy = 'false';
            });
    }

    function handleDrawerMutation(form, noticeText) {
        if (form.dataset.yqBusy === 'true') return;
        if (document.documentElement.dataset.yqCartBusy === 'true') {
            showNotice('السلة قيد التحديث حالياً. يرجى المحاولة بعد قليل.', true);
            announce('السلة قيد التحديث حالياً. يرجى المحاولة بعد قليل.');
            return;
        }
        var item = form.closest('[data-yq-drawer-item]');
        form.dataset.yqBusy = 'true';
        document.documentElement.dataset.yqCartBusy = 'true';
        if (item) item.classList.add('is-updating');

        submitForm(form, false)
            .then(function (doc) {
                var result = reconcileCart(doc, {
                    notice: getCartMessage(doc) || noticeText || 'تم تحديث السلة',
                    announcement: getCartMessage(doc) || noticeText || 'تم تحديث السلة'
                });
                if (!result.hasError && !getCartMessage(doc)) {
                    showNotice(noticeText || 'تم تحديث السلة', false);
                }
            })
            .catch(function () {
                showNotice('تعذّر تحديث السلة. حاول مرة أخرى.', true);
                announce('تعذّر تحديث السلة. حاول مرة أخرى.');
                if (cachedCartDoc) renderCart(cachedCartDoc, {});
            })
            .finally(function () {
                form.dataset.yqBusy = 'false';
                delete document.documentElement.dataset.yqCartBusy;
                if (item) item.classList.remove('is-updating');
            });
    }

    function applyDrawerQuantityState(form, state, noticeText) {
        var item = form.closest('[data-yq-drawer-item]');
        var input = form.querySelector('input[name="quantity"]');
        var expectedInput = form.querySelector('input[name="expectedQuantity"]');
        var savedQuantity = state.item ? parseInt(state.item.quantity, 10) : NaN;

        cachedCartDoc = null;
        setBadge(Number(state.totalQuantity) || 0);
        if (countLabel) countLabel.textContent = productCountLabel(Number(state.uniqueItemCount) || 0);
        if (totalEl && state.subtotalText) totalEl.textContent = state.subtotalText;
        setCheckoutAvailable(Number(state.uniqueItemCount) > 0);
        updateShipping({ subtotal: Number(state.subtotal) || 0 });

        if (Number.isFinite(savedQuantity) && savedQuantity > 0) {
            form.setAttribute('data-yq-last-qty', String(savedQuantity));
            if (input) input.value = String(savedQuantity);
            if (expectedInput) expectedInput.value = String(savedQuantity);

            var max = input ? parseInt(input.getAttribute('max'), 10) : NaN;
            if (state.warningCode === 'InsufficientStock' && Number.isFinite(state.availableQuantity)) {
                max = state.availableQuantity;
                if (input) input.setAttribute('max', String(max));
            }

            var minus = form.querySelector('[data-yq-qty-step="-1"]');
            var plus = form.querySelector('[data-yq-qty-step="1"]');
            if (minus) minus.disabled = savedQuantity <= 1;
            if (plus) plus.disabled = Number.isFinite(max) && savedQuantity >= max;

            var lineTotal = item ? item.querySelector('.yq-cart-drawer__line-total') : null;
            if (lineTotal && state.item.lineTotalText) lineTotal.textContent = state.item.lineTotalText;
        }

        if (state.warningCode === 'InsufficientStock' && window.YaqutOperationDialog) {
            window.YaqutOperationDialog.show({ title: 'الكمية غير متوفرة', message: state.message });
        }

        showNotice(state.message || noticeText, false);
        announce(state.message || noticeText);
        return savedQuantity;
    }

    function findDrawerQuantityForm(state) {
        if (!state || !state.item) return null;
        return toArray(drawer.querySelectorAll('[data-yq-drawer-qty-form]')).find(function (form) {
            var cartItemId = form.querySelector('input[name="cartItemId"]');
            var productId = form.querySelector('input[name="productId"]');
            var retailPriceId = form.querySelector('input[name="retailPriceId"]');
            return (state.item.cartItemId && cartItemId && String(cartItemId.value) === String(state.item.cartItemId)) ||
                (productId && String(productId.value) === String(state.item.productId) &&
                    String(retailPriceId ? retailPriceId.value : '') === String(state.item.retailPriceId || ''));
        }) || null;
    }

    function reconcileCart(authoritativeState, options) {
        options = options || {};
        if (authoritativeState && authoritativeState.nodeType === 9) {
            return renderCart(authoritativeState, options);
        }

        if (authoritativeState && typeof authoritativeState === 'object') {
            var form = options.form || findDrawerQuantityForm(authoritativeState);
            if (form) {
                return applyDrawerQuantityState(form, authoritativeState, options.noticeText || authoritativeState.message);
            }
            return refreshCart({ silent: false });
        }

        throw new Error('cart-state-invalid');
    }

    function handleDrawerQuantity(form, noticeText) {
        var input = form ? form.querySelector('input[name="quantity"]') : null;
        if (!form || !input) return;

        var requestedQuantity = parseInt(normalizeDigits(input.value), 10);
        if (!Number.isFinite(requestedQuantity) || requestedQuantity < 1) requestedQuantity = 1;
        input.value = String(requestedQuantity);

        var expectedInput = form.querySelector('input[name="expectedQuantity"]');
        var previousQuantity = parseInt(
            form.getAttribute('data-yq-last-qty') || (expectedInput ? expectedInput.value : '1'),
            10
        );

        if (form.dataset.yqBusy === 'true') {
            form.setAttribute('data-yq-pending-qty', String(requestedQuantity));
            return;
        }

        if (document.documentElement.dataset.yqCartBusy === 'true') {
            input.value = String(previousQuantity);
            showNotice('السلة قيد التحديث حالياً. يرجى المحاولة بعد قليل.', true);
            announce('السلة قيد التحديث حالياً. يرجى المحاولة بعد قليل.');
            return;
        }

        if (requestedQuantity === previousQuantity) return;

        var item = form.closest('[data-yq-drawer-item]');
        form.dataset.yqBusy = 'true';
        document.documentElement.dataset.yqCartBusy = 'true';
        form.removeAttribute('data-yq-pending-qty');
        if (item) item.classList.add('is-updating');

        submitForm(form, true)
            .then(function (state) {
                var savedQuantity = reconcileCart(state, { form: form, noticeText: noticeText });
                var pendingQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
                if (Number.isFinite(pendingQuantity) && pendingQuantity !== savedQuantity) {
                    input.value = String(pendingQuantity);
                } else {
                    form.removeAttribute('data-yq-pending-qty');
                }
            })
            .catch(function (error) {
                var state = error && error.state;
                if (state && state.conflict && state.item) {
                    var retryQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
                    if (!Number.isFinite(retryQuantity)) retryQuantity = requestedQuantity;
                    reconcileCart(state, { form: form, noticeText: noticeText });
                    form.setAttribute('data-yq-pending-qty', String(retryQuantity));
                    input.value = String(retryQuantity);
                    return;
                }

                form.removeAttribute('data-yq-pending-qty');
                input.value = String(previousQuantity);
                showNotice((state && (state.message || state.detail)) || 'تعذّر تحديث السلة. حاول مرة أخرى.', true);
                announce((state && (state.message || state.detail)) || 'تعذّر تحديث السلة. حاول مرة أخرى.');
            })
            .finally(function () {
                form.dataset.yqBusy = 'false';
                delete document.documentElement.dataset.yqCartBusy;
                if (item) item.classList.remove('is-updating');

                var pendingQuantity = parseInt(form.getAttribute('data-yq-pending-qty') || '', 10);
                var savedQuantity = parseInt(form.getAttribute('data-yq-last-qty') || '1', 10);
                if (Number.isFinite(pendingQuantity)) {
                    form.removeAttribute('data-yq-pending-qty');
                    input.value = String(pendingQuantity);
                    if (pendingQuantity !== savedQuantity) {
                        window.setTimeout(function () {
                            handleDrawerQuantity(form, noticeText);
                        }, 0);
                    }
                }
            });
    }

    function onDocumentSubmit(event) {
        var form = event.target;
        if (!(form instanceof window.HTMLFormElement)) return;

        if (form.matches('[data-yq-add-to-cart-form], .yq-pdp-purchase')) {
            if (typeof window.fetch !== 'function') return;
            event.preventDefault();
            handleAdd(form);
            return;
        }

        if (form.matches('[data-yq-drawer-qty-form]')) {
            if (typeof window.fetch !== 'function') return;
            event.preventDefault();
            handleDrawerQuantity(form, 'تم تحديث الكمية');
            return;
        }

        if (form.matches('[data-yq-drawer-remove-form]')) {
            if (typeof window.fetch !== 'function') return;
            event.preventDefault();
            handleDrawerMutation(form, 'تمت إزالة المنتج من السلة');
        }
    }

    function onDocumentClick(event) {
        var close = event.target.closest('[data-yq-cart-close]');
        if (close) {
            event.preventDefault();
            closeDrawer();
            return;
        }

        var checkout = event.target.closest('[data-yq-cart-checkout]');
        if (checkout && checkout.getAttribute('data-yq-auth-required') === 'true') {
            event.preventDefault();
            showAuthModal(checkout.getAttribute('data-yq-auth-modal'));
            announce('سجّل دخولك أو أنشئ حساباً جديداً لإتمام الطلب.');
            return;
        }

        var disabledCheckout = event.target.closest('[data-yq-cart-checkout][aria-disabled="true"]');
        if (disabledCheckout) {
            event.preventDefault();
            showNotice('أضف عطراً للسلة أولاً، ثم يمكنك إتمام الطلب.', true);
            announce('أضف عطراً للسلة أولاً، ثم يمكنك إتمام الطلب.');
            return;
        }

        var opener = event.target.closest('[data-yq-cart-open]');
        if (opener && typeof window.fetch === 'function' && !event.metaKey && !event.ctrlKey && !event.shiftKey && event.button !== 1) {
            event.preventDefault();
            if (isDrawerOpen()) closeDrawer();
            else openDrawer(opener);
            return;
        }

        var step = event.target.closest('[data-yq-qty-step]');
        if (step && drawer.contains(step)) {
            var form = step.closest('[data-yq-drawer-qty-form]');
            var input = form ? form.querySelector('input[name="quantity"]') : null;
            if (!form || !input) return;
            var delta = parseInt(step.getAttribute('data-yq-qty-step'), 10) || 0;
            var current = parseInt(normalizeDigits(input.value), 10) || 1;
            var min = parseInt(input.getAttribute('min'), 10) || 1;
            var max = parseInt(input.getAttribute('max'), 10);
            var next = current + delta;

            if (Number.isFinite(max)) next = Math.min(next, max);
            next = Math.max(min, next);
            if (next === current) return;
            input.value = next;
            handleDrawerQuantity(form, 'تم تحديث الكمية');
        }
    }

    function cancelPeekAutoCloseOnInteraction(event) {
        if (event.type === 'focusin' && isSettingInitialFocus) return;
        clearPeekAutoCloseTimer();
    }

    function onDocumentChange(event) {
        var input = event.target.closest('.yq-cart-drawer__qty-input');
        if (!input) return;
        var form = input.closest('[data-yq-drawer-qty-form]');
        var value = parseInt(normalizeDigits(input.value), 10);
        var min = parseInt(input.getAttribute('min'), 10) || 1;
        var max = parseInt(input.getAttribute('max'), 10);

        if (!Number.isFinite(value) || value < min) value = min;
        if (Number.isFinite(max)) value = Math.min(value, max);
        input.value = value;
        if (form) handleDrawerQuantity(form, 'تم تحديث الكمية');
    }

    function onKeydown(event) {
        if (drawer.hidden) return;
        if (drawer.contains(event.target)) clearPeekAutoCloseTimer();
        if (event.key === 'Escape') {
            event.preventDefault();
            closeDrawer();
            return;
        }

        if (event.key === 'Tab') {
            var candidates = Array.prototype.slice.call(drawer.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            ));
            var focusable = candidates.filter(function (element) {
                var style = window.getComputedStyle(element);
                return !element.closest('[hidden]') && style.display !== 'none' && style.visibility !== 'hidden';
            });
            if (!focusable.length) {
                event.preventDefault();
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    }

    function init() {
        drawer.addEventListener('pointerdown', cancelPeekAutoCloseOnInteraction);
        drawer.addEventListener('focusin', cancelPeekAutoCloseOnInteraction);
        document.addEventListener('submit', onDocumentSubmit);
        document.addEventListener('click', onDocumentClick);
        document.addEventListener('change', onDocumentChange);
        document.addEventListener('keydown', onKeydown);
        window.addEventListener('yq:cart-reconcile', function (event) {
            if (event.detail && event.detail.authoritativeState) {
                reconcileCart(event.detail.authoritativeState, event.detail.options || {});
            }
        });
        refreshCart({ silent: false });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

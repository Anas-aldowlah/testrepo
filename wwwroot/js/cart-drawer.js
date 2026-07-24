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
    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    var restoreFocusEl = null;
    var cachedCartDoc = null;
    var peekAutoCloseTimer = null;
    var peekOpenedAt = 0;
    var flightLayer = null;

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

    function clearPeekAutoCloseTimer() {
        if (peekAutoCloseTimer) {
            window.clearTimeout(peekAutoCloseTimer);
            peekAutoCloseTimer = null;
        }
    }

    function schedulePeekAutoClose(delay) {
        clearPeekAutoCloseTimer();
        if (!Number.isFinite(delay) || delay < 0) delay = 0;
        if (peekTimer) {
            peekTimer.classList.remove('is-animating');
            peekTimer.style.setProperty('--yq-peek-duration', delay + 'ms');
            window.requestAnimationFrame(function () {
                if (!peekTimer) return;
                peekTimer.classList.add('is-animating');
            });
        }
        peekAutoCloseTimer = window.setTimeout(function () {
            closeDrawer();
        }, delay);
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
            window.requestAnimationFrame(function () {
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
        window.requestAnimationFrame(function () {
            badge.classList.add('is-updated');
        });
    }

    function getBadgeCount() {
        if (!badge || badge.hidden) return 0;
        var count = parseInt(normalizeDigits(badge.textContent), 10);
        return Number.isFinite(count) ? count : 0;
    }

    function parseCartDoc(html) {
        return new window.DOMParser().parseFromString(html, 'text/html');
    }

    function getCartMessage(doc) {
        var alert = doc.querySelector('.yq-cart .yaqut-alert');
        return alert ? alert.textContent.replace(/\s+/g, ' ').trim() : '';
    }

    function getSummary(doc) {
        var summary = doc.querySelector('.yq-cart-summary');
        var totalText = '0 ر.س';
        var subtotalText = '0 ر.س';
        var shippingText = '';
        var qualifiesForFreeShipping = false;

        if (summary) {
            var rows = toArray(summary.querySelectorAll('.yq-cart-summary__row'));
            if (rows[0]) {
                var subtotalNode = rows[0].querySelector('span:last-child');
                subtotalText = subtotalNode ? subtotalNode.textContent.trim() : subtotalText;
            }

            var totalNode = summary.querySelector('.yq-cart-summary__row--total span:last-child');
            totalText = totalNode ? totalNode.textContent.trim() : subtotalText;

            var shippingHint = summary.querySelector('.yq-cart-summary__shipping-hint');
            if (shippingHint) {
                shippingText = shippingHint.textContent.replace(/\s+/g, ' ').trim();
                qualifiesForFreeShipping = shippingHint.classList.contains('yq-cart-summary__shipping-hint--success');
            }
        }

        return {
            subtotal: parseMoney(subtotalText),
            totalText: totalText,
            shippingText: shippingText,
            qualifiesForFreeShipping: qualifiesForFreeShipping
        };
    }

    function formHiddenHtml(form) {
        if (!form) return '';
        return toArray(form.querySelectorAll('input[type="hidden"], input[name="__RequestVerificationToken"]'))
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
            var qtyInput = qtyForm ? qtyForm.querySelector('input[name="quantity"]') : null;
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
                max: qtyInput ? qtyInput.getAttribute('max') : '',
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
        var quantityInput = form.querySelector('input[name="quantity"]');
        var image = card ? card.querySelector('.yaqut-card-img, img') : document.querySelector('.yq-pdp-gallery__img');
        var nameLink = card ? card.querySelector('.yaqut-product-card__name-link') : null;
        var price = card ? card.querySelector('.yaqut-product-card__price') : document.querySelector('.yq-pdp-price');
        var category = card ? card.querySelector('.yaqut-product-card__family') : document.querySelector('.yq-pdp-category');
        var href = nameLink ? nameLink.href : window.location.href;
        var name = form.getAttribute('data-product-name') || (card ? card.getAttribute('data-product-name') : '') || (nameLink ? nameLink.textContent.trim() : document.title);
        var quantity = quantityInput ? parseInt(normalizeDigits(quantityInput.value), 10) : 1;

        return {
            productId: productId ? productId.value : '',
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
            '<img src="' + escapeAttr(snapshot.imageSrc) + '" alt="" aria-hidden="true" />',
            '<span>+' + snapshot.quantity + '</span>'
        ].join('');
        layer.appendChild(ghost);

        var dx = (badgeRect.left + badgeRect.width * 0.5) - (sourceRect.left + sourceRect.width * 0.5);
        var dy = (badgeRect.top + badgeRect.height * 0.5) - (sourceRect.top + sourceRect.height * 0.5);
        var scale = Math.max(0.16, Math.min(0.48, badgeRect.width / Math.max(sourceRect.width, 1) * 0.55));

        window.requestAnimationFrame(function () {
            ghost.style.transform = 'translate(' + dx + 'px, ' + dy + 'px) scale(' + scale + ')';
            ghost.style.opacity = '0';
        });

        window.setTimeout(function () {
            ghost.remove();
        }, 820);
    }

    function renderInstantAdd(snapshot) {
        var itemName = escapeHtml(snapshot.name);
        var itemNameAttr = escapeAttr(snapshot.name);
        var itemHref = escapeAttr(snapshot.href);
        var itemImage = escapeAttr(snapshot.imageSrc);
        var itemAlt = escapeAttr(snapshot.imageAlt || snapshot.name);
        var category = escapeHtml(snapshot.category);
        var priceText = escapeHtml(snapshot.priceText);
        var quantityText = snapshot.quantity > 1 ? '<span class="yq-cart-drawer__instant-qty">الكمية ' + snapshot.quantity + '</span>' : '';

        setPeekTitle('تمت الإضافة إلى السلة', 'اختيارك أضيف بنجاح');
        body.innerHTML = [
            '<div class="yq-cart-drawer__instant" role="status">',
            '<div class="yq-cart-drawer__instant-head">',
            '<span class="yq-cart-drawer__instant-check"><i class="bi bi-check2" aria-hidden="true"></i></span>',
            '<div>',
            '<h3>' + itemName + '</h3>',
            '<p>أضيف إلى سلتك. تقدر تكمل التسوق أو تنتقل لإتمام الطلب.</p>',
            '</div>',
            '</div>',
            '<div class="yq-cart-drawer__item is-highlighted">',
            '<a class="yq-cart-drawer__media" href="' + itemHref + '">',
            '<img src="' + itemImage + '" alt="' + itemAlt + '" data-yaqut-fallback="/images/placeholder-product.svg" loading="lazy" />',
            '</a>',
            '<div class="yq-cart-drawer__item-body">',
            '<div>',
            snapshot.category ? '<span class="yq-cart-drawer__category">' + category + '</span>' : '',
            '<a class="yq-cart-drawer__name" href="' + itemHref + '">' + itemName + '</a>',
            '</div>',
            '<div class="yq-cart-drawer__instant-meta">',
            quantityText,
            priceText ? '<strong>' + priceText + '</strong>' : '',
            '</div>',
            '</div>',
            '</div>',
            '</div>'
        ].join('');

        if (footer) footer.hidden = false;
        setCheckoutAvailable(true);
        if (totalEl && snapshot.priceText) totalEl.textContent = snapshot.priceText;
        if (shipping) shipping.hidden = true;
        peekOpenedAt = Date.now();
        schedulePeekAutoClose(3000);
    }

    function renderAddError(message, options) {
        var hasExistingItems = Boolean(options && options.hasItems);
        setPeekTitle('ما تمت الإضافة', 'حاول مرة أخرى');
        body.innerHTML = [
            '<div class="yq-cart-drawer__empty" role="alert">',
            '<span class="yq-cart-drawer__empty-icon"><i class="bi bi-exclamation-circle" aria-hidden="true"></i></span>',
            '<h3>ما تمت الإضافة</h3>',
            '<p>' + escapeHtml(message || 'تعذّرت الإضافة الآن. حاول مرة أخرى.') + '</p>',
            '</div>'
        ].join('');
        if (footer) footer.hidden = false;
        setCheckoutAvailable(hasExistingItems);
        if (totalEl && options && options.totalText) totalEl.textContent = options.totalText;
        else if (totalEl && !hasExistingItems) totalEl.textContent = '0 ر.س';
        if (shipping) shipping.hidden = true;
        clearPeekAutoCloseTimer();
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
            '<img src="' + itemImage + '" alt="' + itemAlt + '" data-yaqut-fallback="/images/placeholder-product.svg" loading="lazy" />',
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
        if (summary.qualifiesForFreeShipping) {
            shippingLabel.textContent = 'طلبك مؤهل للشحن المجاني';
            shippingValue.textContent = 'مكتمل';
            shippingProgress.style.width = '100%';
            return;
        }

        var remaining = Math.max(0, threshold - summary.subtotal);
        var progress = Math.max(0, Math.min(100, (summary.subtotal / threshold) * 100));
        shippingLabel.textContent = 'اقتربت من الشحن المجاني';
        shippingValue.textContent = remaining.toLocaleString('ar-SA', { maximumFractionDigits: 0 }) + ' ر.س';
        shippingProgress.style.width = progress + '%';
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

            if (footer) footer.hidden = false;
            setCheckoutAvailable(true);
            if (totalEl) totalEl.textContent = summary.totalText || '0 ر.س';
            updateShipping(summary);
        }

        announce(options && options.announcement ? options.announcement : message);
        return { items: items, totalQuantity: totalQuantity, message: message, hasError: hasError };
    }

    function syncCartMeta(doc, options) {
        var items = extractItems(doc);
        var uniqueCount = items.length;
        var totalQuantity = items.reduce(function (sum, item) { return sum + item.quantity; }, 0);
        var summary = getSummary(doc);
        var message = getCartMessage(doc);
        var hasError = isErrorMessage(message);

        cachedCartDoc = doc;
        setBadge(totalQuantity);
        setPeekTitle(
            hasError ? 'ما تمت الإضافة' : 'تمت الإضافة إلى السلة',
            hasError ? 'حاول مرة أخرى' : productCountLabel(uniqueCount)
        );
        showNotice(options && options.notice ? options.notice : (hasError ? message : ''), hasError);
        setCheckoutAvailable(items.length > 0);
        if (totalEl) totalEl.textContent = summary.totalText || '0 ر.س';
        if (items.length && !hasError) updateShipping(summary);
        else if (shipping) shipping.hidden = true;
        announce(options && options.announcement ? options.announcement : message);

        if (items.length && !hasError) {
            var elapsed = peekOpenedAt ? Date.now() - peekOpenedAt : 0;
            schedulePeekAutoClose(Math.max(0, 3000 - elapsed));
        } else {
            clearPeekAutoCloseTimer();
        }

        return { items: items, totalQuantity: totalQuantity, message: message, hasError: hasError, summary: summary };
    }

    function setDrawerLoading(allowCheckout) {
        setPeekTitle('سلة التسوق', 'جارٍ تحديث السلة');
        body.innerHTML = [
            '<div class="yq-cart-drawer__loading" role="status">',
            '<span class="yq-cart-drawer__spinner" aria-hidden="true"></span>',
            '<span>جارٍ تحديث السلة...</span>',
            '</div>'
        ].join('');
        if (footer) footer.hidden = false;
        setCheckoutAvailable(Boolean(allowCheckout));
        showNotice('', false);
    }

    function openDrawer(trigger, shouldLoad) {
        restoreFocusEl = trigger || document.activeElement;
        drawer.hidden = false;
        drawer.setAttribute('aria-hidden', 'false');
        clearPeekAutoCloseTimer();

        window.requestAnimationFrame(function () {
            drawer.classList.add('is-open');
        });

        if (shouldLoad) {
            setDrawerLoading(false);
            refreshCart({ silent: false });
        }
    }

    function closeDrawer() {
        clearPeekAutoCloseTimer();
        peekOpenedAt = 0;
        if (peekTimer) {
            peekTimer.classList.remove('is-animating');
            peekTimer.style.removeProperty('--yq-peek-duration');
        }
        drawer.classList.remove('is-open');
        drawer.setAttribute('aria-hidden', 'true');

        window.setTimeout(function () {
            drawer.hidden = true;
            if (restoreFocusEl && document.contains(restoreFocusEl) && drawer.contains(document.activeElement)) {
                restoreFocusEl.focus({ preventScroll: true });
            }
        }, reduceMotion.matches ? 0 : 240);
    }

    function fetchCartPage() {
        return window.fetch('/Cart', {
            method: 'GET',
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) throw new Error('cart-refresh-failed');
            return response.text();
        }).then(parseCartDoc);
    }

    function submitForm(form) {
        return window.fetch(form.action, {
            method: (form.method || 'post').toUpperCase(),
            body: new window.FormData(form),
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) throw new Error('cart-submit-failed');
            return response.text();
        }).then(parseCartDoc);
    }

    function refreshCart(options) {
        return fetchCartPage()
            .then(function (doc) {
                return renderCart(doc, options || {});
            })
            .catch(function () {
                if (!(options && options.silent)) {
                    showNotice('تعذّر تحديث السلة الآن. حاول مرة أخرى.', true);
                    announce('تعذّر تحديث السلة الآن. حاول مرة أخرى.');
                }
            });
    }

    function setAddButtonState(form, state, message) {
        var button = form.querySelector('[data-yq-add-button], button[type="submit"]');
        var label = button ? button.querySelector('[data-yq-add-label]') : null;
        var status = form.closest('[data-yq-product-card]') ? form.closest('[data-yq-product-card]').querySelector('[data-yq-add-status]') : null;

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
            button.disabled = false;
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
            button.disabled = false;
            button.innerHTML = button.dataset.originalHtml;
            if (status) {
                status.classList.add('is-error');
                status.textContent = message || 'تعذّرت الإضافة. حاول مرة أخرى.';
            }
        } else {
            button.disabled = false;
            button.innerHTML = button.dataset.originalHtml;
        }
    }

    function handleAdd(form) {
        if (form.dataset.yqBusy === 'true') return;
        var productName = form.getAttribute('data-product-name') || (form.closest('[data-product-name]') ? form.closest('[data-product-name]').getAttribute('data-product-name') : 'المنتج');
        var snapshot = getProductSnapshot(form);
        var previousBadgeCount = getBadgeCount();

        form.dataset.yqBusy = 'true';
        setAddButtonState(form, 'loading');
        animateProductFlight(snapshot);
        renderInstantAdd(snapshot);
        setBadge(previousBadgeCount + snapshot.quantity);
        animateBadgeNudge();
        showNotice('', false);
        announce('تمت إضافة ' + productName + ' إلى السلة');
        openDrawer(form.querySelector('button[type="submit"]'), false);

        submitForm(form)
            .then(function (doc) {
                var result = syncCartMeta(doc, {
                    announcement: 'تمت إضافة ' + productName + ' إلى السلة'
                });
                var message = result.message;
                var failed = result.hasError;

                if (failed) {
                    renderAddError(message || 'تعذّرت الإضافة. حاول مرة أخرى.', {
                        hasItems: result.items.length > 0,
                        totalText: result.summary ? result.summary.totalText : ''
                    });
                    setAddButtonState(form, 'error', message || 'تعذّرت الإضافة. حاول مرة أخرى.');
                } else {
                    animateBadgeNudge();
                    setAddButtonState(form, 'success', message ? 'تم تحديث السلة' : 'تمت الإضافة');
                }
            })
            .catch(function () {
                setBadge(previousBadgeCount);
                renderAddError('تعذّرت الإضافة. حاول مرة أخرى.', { hasItems: previousBadgeCount > 0 });
                setAddButtonState(form, 'error', 'تعذّرت الإضافة. حاول مرة أخرى.');
                showNotice('تعذّرت الإضافة. حاول مرة أخرى.', true);
                announce('تعذّرت الإضافة. حاول مرة أخرى.');
            })
            .finally(function () {
                form.dataset.yqBusy = 'false';
            });
    }

    function handleDrawerMutation(form, noticeText) {
        if (form.dataset.yqBusy === 'true') return;
        var item = form.closest('[data-yq-drawer-item]');
        form.dataset.yqBusy = 'true';
        if (item) item.classList.add('is-updating');

        submitForm(form)
            .then(function (doc) {
                var result = renderCart(doc, {
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
                if (item) item.classList.remove('is-updating');
            });
    }

    function onDocumentSubmit(event) {
        var form = event.target;
        if (!(form instanceof window.HTMLFormElement)) return;

        if (form.matches('[data-yq-add-to-cart-form], .yq-pdp-purchase')) {
            event.preventDefault();
            handleAdd(form);
            return;
        }

        if (form.matches('[data-yq-drawer-qty-form]')) {
            event.preventDefault();
            handleDrawerMutation(form, 'تم تحديث الكمية');
            return;
        }

        if (form.matches('[data-yq-drawer-remove-form]')) {
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
        if (opener && !event.metaKey && !event.ctrlKey && !event.shiftKey && event.button !== 1) {
            event.preventDefault();
            openDrawer(opener, true);
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
            handleDrawerMutation(form, 'تم تحديث الكمية');
        }
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
        if (form) handleDrawerMutation(form, 'تم تحديث الكمية');
    }

    function onKeydown(event) {
        if (drawer.hidden) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            closeDrawer();
        }
    }

    function init() {
        document.addEventListener('submit', onDocumentSubmit);
        document.addEventListener('click', onDocumentClick);
        document.addEventListener('change', onDocumentChange);
        document.addEventListener('keydown', onKeydown);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);

/**
 * Yaqut checkout draft storage contract.
 * Owns the short-lived, user-bound browser record and logout cleanup.
 */
(function (window, document) {
    'use strict';

    var STORAGE_KEY = 'yq-checkout-draft';
    var ALLOWED_FIELDS = ['Governorate', 'City', 'District', 'Street', 'DeliveryNotes'];

    // Purge legacy PII from localStorage unconditionally
    try {
        window.localStorage.removeItem(STORAGE_KEY);
    } catch (error) {}

    var serverDraft = null;

    function getServerDraft() {
        if (serverDraft !== null) return serverDraft;
        var el = document.getElementById('yq-server-checkout-draft');
        if (el && el.textContent) {
            try {
                serverDraft = JSON.parse(el.textContent);
            } catch (e) {
                serverDraft = {};
            }
        }
        return serverDraft;
    }

    function getEndpointUrl(actionName) {
        var form = document.getElementById('yqCheckoutForm');
        var action = form ? form.getAttribute('action') : '/Orders/Checkout';
        return action.replace(/\/Checkout\/?$/i, '/' + actionName);
    }

    function clear() {
        serverDraft = {};
        if (window.fetch) {
            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            var headers = {};
            if (token) headers['RequestVerificationToken'] = token.value;
            window.fetch(getEndpointUrl('ClearCheckoutDraft'), { method: 'POST', headers: headers }).catch(function(){});
        }
    }

    function createDraftId() {
        if (!window.crypto) return null;
        if (typeof window.crypto.randomUUID === 'function') return window.crypto.randomUUID();
        if (typeof window.crypto.getRandomValues !== 'function') return null;

        var bytes = new Uint8Array(16);
        window.crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        var hex = Array.prototype.map.call(bytes, function (value) {
            return value.toString(16).padStart(2, '0');
        }).join('');
        return hex.slice(0, 8) + '-' + hex.slice(8, 12) + '-' + hex.slice(12, 16) + '-' +
            hex.slice(16, 20) + '-' + hex.slice(20);
    }

    function read(userId) {
        var draft = getServerDraft();
        if (!draft || Object.keys(draft).length === 0) return null;
        return {
            draftId: draft.DraftId,
            userId: String(userId),
            fields: draft
        };
    }

    function normalizeFields(value) {
        if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
        var fields = {};
        for (var i = 0; i < ALLOWED_FIELDS.length; i++) {
            var name = ALLOWED_FIELDS[i];
            if (typeof value[name] === 'string') fields[name] = value[name];
        }
        return fields;
    }

    function write(userId, draftId, fields) {
        var normalized = normalizeFields(fields);
        if (!normalized) return false;

        normalized.DraftId = draftId;
        serverDraft = normalized;

        if (window.fetch) {
            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            var headers = { 'Content-Type': 'application/json' };
            if (token) headers['RequestVerificationToken'] = token.value;
            window.fetch(getEndpointUrl('SaveCheckoutDraft'), {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(normalized)
            }).catch(function(){});
        }
        return true;
    }

    function remove(draftId) {
        var record = read();
        if (record && record.draftId !== draftId) return false;
        clear();
        return true;
    }

    function isLogoutForm(form) {
        if (!form || String(form.method || '').toLowerCase() !== 'post') return false;
        try {
            var pathname = new URL(form.action, window.location.href).pathname.replace(/\/+$/, '');
            return /\/Account\/Logout$/i.test(pathname);
        } catch (error) {
            return false;
        }
    }

    document.addEventListener('submit', function (event) {
        if (isLogoutForm(event.target)) clear();
    }, true);

    window.YaqutCheckoutDraft = Object.freeze({
        key: STORAGE_KEY,
        allowedFields: Object.freeze(ALLOWED_FIELDS.slice()),
        createDraftId: createDraftId,
        read: read,
        write: write,
        remove: remove,
        clear: clear
    });
})(window, document);

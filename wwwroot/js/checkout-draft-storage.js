/**
 * Yaqut checkout draft storage contract.
 * Owns the short-lived, user-bound browser record and logout cleanup.
 */
(function (window, document) {
    'use strict';

    var STORAGE_KEY = 'yq-checkout-draft';
    var SCHEMA_VERSION = 2;
    var TTL_MS = 2 * 60 * 60 * 1000;
    var ALLOWED_FIELDS = ['Governorate', 'City', 'District', 'Street', 'DeliveryNotes'];
    var DRAFT_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

    function clear() {
        try {
            window.localStorage.removeItem(STORAGE_KEY);
        } catch (error) {
            // Storage availability must never block navigation or checkout.
        }
    }

    function isDraftId(value) {
        return typeof value === 'string' && DRAFT_ID_PATTERN.test(value);
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

    function normalizeFields(value) {
        if (!value || typeof value !== 'object' || Array.isArray(value)) return null;

        var fields = {};
        for (var index = 0; index < ALLOWED_FIELDS.length; index += 1) {
            var name = ALLOWED_FIELDS[index];
            if (typeof value[name] !== 'string') return null;
            fields[name] = value[name];
        }

        if (Object.keys(value).some(function (name) { return ALLOWED_FIELDS.indexOf(name) === -1; })) {
            return null;
        }

        return fields;
    }

    function readRecord(now) {
        try {
            var raw = window.localStorage.getItem(STORAGE_KEY);
            if (!raw) return null;

            var record = JSON.parse(raw);
            var fields = record && normalizeFields(record.fields);
            var valid = record &&
                record.version === SCHEMA_VERSION &&
                typeof record.userId === 'string' &&
                record.userId.length > 0 &&
                isDraftId(record.draftId) &&
                Number.isFinite(record.savedAt) &&
                Number.isFinite(record.expiresAt) &&
                record.expiresAt === record.savedAt + TTL_MS &&
                record.savedAt <= now &&
                record.expiresAt > now &&
                fields;

            if (!valid) {
                clear();
                return null;
            }

            record.fields = fields;
            return record;
        } catch (error) {
            clear();
            return null;
        }
    }

    function read(userId) {
        var record = readRecord(Date.now());
        if (!record) return null;

        if (!userId || record.userId !== String(userId)) {
            clear();
            return null;
        }

        return record;
    }

    function write(userId, draftId, fields) {
        var normalizedFields = normalizeFields(fields);
        if (!userId || !isDraftId(draftId) || !normalizedFields) return false;

        var savedAt = Date.now();
        var record = {
            version: SCHEMA_VERSION,
            userId: String(userId),
            draftId: draftId,
            savedAt: savedAt,
            expiresAt: savedAt + TTL_MS,
            fields: normalizedFields
        };

        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(record));
            return true;
        } catch (error) {
            return false;
        }
    }

    function remove(draftId) {
        if (!isDraftId(draftId)) return false;
        var record = readRecord(Date.now());
        if (!record || record.draftId !== draftId) return false;
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

    // Purge malformed, legacy, or expired records as soon as the shared owner loads.
    readRecord(Date.now());

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

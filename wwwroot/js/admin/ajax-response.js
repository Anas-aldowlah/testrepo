(function (global) {
    'use strict';

    function AjaxResponseError(kind, status, message, payload) {
        this.name = 'AjaxResponseError';
        this.kind = kind;
        this.status = status || 0;
        this.message = message;
        this.payload = payload || null;
        if (Error.captureStackTrace) Error.captureStackTrace(this, AjaxResponseError);
    }

    AjaxResponseError.prototype = Object.create(Error.prototype);
    AjaxResponseError.prototype.constructor = AjaxResponseError;

    function error(kind, response, message, payload) {
        return new AjaxResponseError(kind, response && response.status, message, payload);
    }

    async function readJson(response) {
        if (!response) {
            throw new AjaxResponseError('network', 0, 'تعذر الاتصال بالخادم. تحقق من الاتصال ثم حاول مرة أخرى.');
        }

        if (response.status === 401) {
            throw error('session_expired', response, 'انتهت جلسة تسجيل الدخول. لم يتم تنفيذ الطلب. سجّل الدخول مرة أخرى ثم راجع بياناتك غير المحفوظة.');
        }

        if (response.status === 403) {
            throw error('forbidden', response, 'ليس لديك صلاحية لتنفيذ هذا الطلب.');
        }

        if (response.status >= 500) {
            throw error('server', response, 'حدث خطأ في الخادم. لم يتم تنفيذ الطلب.');
        }

        var contentType = response.headers && typeof response.headers.get === 'function'
            ? response.headers.get('content-type') || ''
            : '';
        if (!/(?:application\/json|\+json)(?:\s*;|\s*$)/i.test(contentType)) {
            throw error('unexpected_content', response, 'أرسل الخادم استجابة غير متوقعة. لم يتم تنفيذ الطلب.');
        }

        var payload;
        try {
            payload = await response.json();
        } catch (parseError) {
            throw error('malformed_json', response, 'تعذر قراءة استجابة الخادم. لم يتم تنفيذ الطلب.');
        }

        if (!response.ok) {
            throw error(
                'http',
                response,
                payload && payload.message ? String(payload.message) : 'رفض الخادم الطلب.',
                payload);
        }

        return payload;
    }

    function normalizeError(value) {
        if (value instanceof AjaxResponseError || (value && value.name === 'AbortError')) return value;
        return new AjaxResponseError('network', 0, 'تعذر الاتصال بالخادم. تحقق من الاتصال ثم حاول مرة أخرى.');
    }

    global.YaqutAdminAjax = {
        AjaxResponseError: AjaxResponseError,
        normalizeError: normalizeError,
        readJson: readJson
    };
})(window);

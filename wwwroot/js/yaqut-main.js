/**
 * ياقوت Yaqut — v3 Luxury Store Frontend
 */
(function (window, document) {
    'use strict';

    var STORAGE_KEY = 'yaqut-theme';

    /* ═══ THEME ENGINE ═══ */
    function getPreferredTheme() {
        var stored = localStorage.getItem(STORAGE_KEY);
        if (stored === 'dark' || stored === 'light') return stored;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        var isDark = theme === 'dark';
        var root = document.documentElement;
        var body = document.body;

        // إضافة كلاس الانتقال السلس للحظات
        root.classList.add('yq-transitioning');

        root.setAttribute('data-theme', theme);
        root.classList.toggle('dark-mode', isDark);
        root.classList.toggle('theme-dark', isDark);
        root.classList.toggle('theme-light', !isDark);

        if (body) {
            body.classList.toggle('dark-mode', isDark);
            body.classList.toggle('theme-dark', isDark);
            body.classList.toggle('theme-light', !isDark);
        }

        localStorage.setItem(STORAGE_KEY, theme);
        updateLogos(!isDark);
        updateThemeToggleUI(isDark);

        setTimeout(function () {
            root.classList.remove('yq-transitioning');
        }, 500);
    }

    function updateLogos(isLight) {
        document.querySelectorAll('.yaqut-logo').forEach(function (img) {
            img.classList.toggle('yaqut-logo--on-light', isLight);
            img.classList.toggle('yaqut-logo--on-dark', !isLight);
        });
    }

    function updateThemeToggleUI(isDark) {
        // تحديث الأزرار القديمة
        document.querySelectorAll('.yaqut-theme-toggle').forEach(function (btn) {
            btn.setAttribute('aria-label', isDark ? 'تفعيل الوضع الصباحي' : 'تفعيل الوضع المسائي');
            btn.setAttribute('title', isDark ? 'وضع النهار' : 'وضع الليل');
        });

        // تحديث أزرار الـ Pill الجديدة إن وجدت
        document.querySelectorAll('.yaqut-theme-pill__btn').forEach(function (btn) {
            var opt = btn.getAttribute('data-theme-opt');
            if (opt) {
                var isActive = (opt === 'dark' && isDark) || (opt === 'light' && !isDark);
                btn.classList.toggle('is-active', isActive);
            }
        });
    }

    // تطبيق الثيم فوراً لتجنب الوميض الأبيض (Flicker)
    applyTheme(getPreferredTheme());

    function initThemeToggle() {
        document.querySelectorAll('#yaqutThemeToggle, .yaqut-theme-toggle').forEach(function (btn) {
            if (btn.dataset.yaqutThemeBound) return;
            btn.dataset.yaqutThemeBound = '1';
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var isDark = document.documentElement.classList.contains('dark-mode');
                applyTheme(isDark ? 'light' : 'dark');
            });
        });

        document.querySelectorAll('.yaqut-theme-pill__btn').forEach(function (btn) {
            if (btn.dataset.yaqutThemeBound) return;
            btn.dataset.yaqutThemeBound = '1';
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var opt = btn.getAttribute('data-theme-opt');
                if (opt === 'dark' || opt === 'light') {
                    applyTheme(opt);
                }
            });
        });

        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
            if (!localStorage.getItem(STORAGE_KEY)) {
                applyTheme(e.matches ? 'dark' : 'light');
            }
        });
    }

    /* ═══ NAVIGATION ═══ */
    function toggleNav() {
        var nav = document.querySelector('.yaqut-nav');
        if (nav) nav.classList.toggle('is-open');
    }
    window.toggleNav = toggleNav;

    function toggleAdminNav() {
        var sidebar = document.getElementById('adminSidebar');
        if (sidebar) sidebar.classList.toggle('show');
    }
    window.toggleAdminNav = toggleAdminNav;

    /* ═══ HERO SLIDER ═══ */
    function initHeroSlider() {
        var slider = document.querySelector('.yq-hero__track');
        var dotsContainer = document.querySelector('.yq-hero__dots');
        if (!slider) return;

        var slides = slider.querySelectorAll('.yq-hero__slide');
        if (slides.length < 2) return;

        var index = 0;
        var intervalId = null;

        // تهيئة الشريحة الأولى
        slides.forEach(function (s, i) {
            s.classList.toggle('is-active', i === 0);
        });

        // بناء المؤشرات الرقمية (Dots) ديناميكياً
        if (dotsContainer) {
            dotsContainer.innerHTML = '';
            slides.forEach(function (_, i) {
                var dot = document.createElement('button');
                dot.type = 'button';
                dot.className = 'yq-hero__dot' + (i === 0 ? ' is-active' : '');
                dot.setAttribute('aria-label', 'الذهاب للشريحة ' + (i + 1));
                dot.addEventListener('click', function () {
                    goToSlide(i);
                    restartAutoplay();
                });
                dotsContainer.appendChild(dot);
            });
        }

        function goToSlide(nextIndex) {
            slides[index].classList.remove('is-active');
            var dots = document.querySelectorAll('.yq-hero__dot');
            if (dots[index]) dots[index].classList.remove('is-active');

            index = nextIndex;

            slides[index].classList.add('is-active');
            if (dots[index]) dots[index].classList.add('is-active');
        }

        function startAutoplay() {
            intervalId = setInterval(function () {
                var next = (index + 1) % slides.length;
                goToSlide(next);
            }, 6000);
        }

        function restartAutoplay() {
            if (intervalId) clearInterval(intervalId);
            startAutoplay();
        }

        startAutoplay();
    }

    /* ═══ AUTH TABS ═══ */
    function initAuthPanels() {
        var loginToggle = document.getElementById('loginToggle');
        var registerToggle = document.getElementById('registerToggle');
        var loginPanel = document.getElementById('loginPanel');
        var registerPanel = document.getElementById('registerPanel');
        if (!loginToggle || !registerToggle || !loginPanel || !registerPanel) return;

        function setActive(panel) {
            var isLogin = panel === 'login';
            loginToggle.classList.toggle('is-active', isLogin);
            registerToggle.classList.toggle('is-active', !isLogin);
            loginPanel.classList.toggle('d-none', !isLogin);
            registerPanel.classList.toggle('d-none', isLogin);
            loginPanel.setAttribute('aria-hidden', !isLogin);
            registerPanel.setAttribute('aria-hidden', isLogin);
            
            // إخفاء الأخطاء القديمة عند الانتقال بين التبويبات
            document.querySelectorAll('.yq-field-error').forEach(function(el) {
                el.style.display = 'none';
            });
            document.querySelectorAll('.yaqut-form-input').forEach(function(input) {
                input.classList.remove('is-invalid', 'is-valid');
            });
        }

        loginToggle.addEventListener('click', function () { setActive('login'); });
        registerToggle.addEventListener('click', function () { setActive('register'); });

        var startRegister = !registerPanel.classList.contains('d-none');
        setActive(startRegister ? 'register' : 'login');
    }

    /* ═══ FORM VALIDATION (التحقق الفوري والذكي) ═══ */
    function initFormValidation() {
        var forms = document.querySelectorAll('form[data-yq-validate]');
        
        forms.forEach(function (form) {
            var inputs = form.querySelectorAll('input[required], input[minlength], input[type="tel"]');
            
            // التحقق عند الإرسال
            form.addEventListener('submit', function (e) {
                var isValid = true;
                inputs.forEach(function (input) {
                    if (!validateField(input)) {
                        isValid = false;
                    }
                });
                if (!isValid) {
                    e.preventDefault();
                    // التركيز على أول حقل به خطأ
                    var firstErr = form.querySelector('.is-invalid');
                    if (firstErr) firstErr.focus();
                }
            });

            // التحقق أثناء الكتابة (Real-time)
            inputs.forEach(function (input) {
                input.addEventListener('input', function () {
                    validateField(input);
                });
                input.addEventListener('blur', function () {
                    validateField(input);
                });
            });
        });

        function validateField(input) {
            var val = input.value.trim();
            var name = input.name;

            // 1. التحقق من الحقول المطلوبة الفارغة
            if (input.hasAttribute('required') && !val) {
                showError(input, 'هذا الحقل مطلوب ولا يمكن تركه فارغاً.');
                return false;
            }

            // 2. التحقق من رقم الجوال (يجب أن يتكون من 9 أرقام)
            if (input.type === 'tel' || name.toLowerCase().indexOf('phone') !== -1) {
                var phoneRegex = /^\d{9}$/;
                if (val && !phoneRegex.test(val)) {
                    showError(input, 'رقم الجوال يجب أن يتكون من 9 أرقام بالضبط.');
                    return false;
                }
            }

            // 3. التحقق من طول النص (الاسم الكامل وكلمة المرور)
            var minLen = input.getAttribute('minlength');
            if (minLen && val && val.length < parseInt(minLen)) {
                var fieldName = input.placeholder || 'المدخل';
                showError(input, 'يجب ألا يقل هذا الحقل عن ' + minLen + ' أحرف.');
                return false;
            }

            // 4. التحقق من تطابق كلمتي المرور
            if (input.id === 'regConfirm') {
                var passwordInput = document.getElementById('regPassword');
                if (passwordInput && val !== passwordInput.value) {
                    showError(input, 'كلمة المرور وتأكيدها غير متطابقين.');
                    return false;
                }
            }

            if (input.id === 'regConfirmPhone') {
                var passwordInput = document.getElementById('regPhone');
                if (passwordInput && val !== passwordInput.value) {
                    showError(input, 'رقم الجوال وتأكيدها غير متطابقين.');
                    return false;
                }
            }

            // إذا مرّ الحقل بسلام
            clearError(input);
            return true;
        }

        function showError(input, msg) {
            input.classList.add('is-invalid');
            input.classList.remove('is-valid');
            var errorEl = input.closest('.yaqut-form-group').querySelector('.yq-field-error');
            if (errorEl) {
                errorEl.textContent = msg;
                errorEl.style.display = 'block';
            }
        }

        function clearError(input) {
            input.classList.remove('is-invalid');
            if (input.value.trim()) {
                input.classList.add('is-valid');
            } else {
                input.classList.remove('is-valid');
            }
            var errorEl = input.closest('.yaqut-form-group').querySelector('.yq-field-error');
            if (errorEl) {
                errorEl.textContent = '';
                errorEl.style.display = 'none';
            }
        }
    }

    /* ═══ SCROLL REVEAL (تأثير الظهور الفخم عند التمرير) ═══ */
    function initScrollReveal() {
        var elements = document.querySelectorAll('.yq-reveal');
        if (!elements.length) return;

        if ('IntersectionObserver' in window) {
            var observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('is-visible');
                        observer.unobserve(entry.target);
                    }
                });
            }, {
                threshold: 0.1,
                rootMargin: '0px 0px -50px 0px'
            });

            elements.forEach(function (el) {
                observer.observe(el);
            });
        } else {
            // متصفحات قديمة جداً
            elements.forEach(function (el) {
                el.classList.add('is-visible');
            });
        }
    }

    /* ═══ UTILITIES (تأكيد الحذف والفلاتر) ═══ */
    function initImageFallbacks() {
        document.querySelectorAll('[data-yaqut-fallback]').forEach(function (img) {
            img.addEventListener('error', function () {
                var fb = img.getAttribute('data-yaqut-fallback');
                if (fb && img.src.indexOf(fb) === -1) {
                    img.onerror = null;
                    img.src = fb;
                }
            });
        });
    }

    function initImagePreviews() {
        document.querySelectorAll('[data-yaqut-image-input]').forEach(function (input) {
            var targetId = input.getAttribute('data-yaqut-image-input');
            var preview = document.getElementById(targetId);
            if (!preview) return;
            input.addEventListener('change', function () {
                var file = input.files && input.files[0];
                if (file) preview.src = URL.createObjectURL(file);
            });
        });
    }

    function initProductFilters() {
        var grid = document.getElementById('yaqutProductsGrid');
        var filters = document.getElementById('yaqutFilters');
        if (!grid || !filters) return;

        var cards = grid.querySelectorAll('[data-yaqut-product]');
        var countEl = document.getElementById('yaqutResultsCount');
        var searchInput = document.getElementById('yaqutSearchInput');
        var urlSearch = new URLSearchParams(window.location.search).get('search') || '';
        if (searchInput && urlSearch) searchInput.value = urlSearch;

        function getChecked(name) {
            var vals = [];
            filters.querySelectorAll('input[name="' + name + '"]:checked').forEach(function (cb) {
                vals.push(cb.value);
            });
            return vals;
        }

        function applyFilters() {
            var sizes = getChecked('size');
            var concentrations = getChecked('concentration');
            var families = getChecked('family');
            var search = (searchInput ? searchInput.value : urlSearch).toLowerCase().trim();
            var visible = 0;

            cards.forEach(function (card) {
                var name = (card.getAttribute('data-name') || '').toLowerCase();
                var size = card.getAttribute('data-size') || 'all';
                var conc = card.getAttribute('data-concentration') || 'all';
                var family = card.getAttribute('data-family') || '';

                var matchSize = sizes.length === 0 || sizes.indexOf(size) !== -1;
                var matchConc = concentrations.length === 0 || concentrations.indexOf(conc) !== -1 || conc === 'all';
                var matchFamily = families.length === 0 || families.indexOf(family) !== -1;
                var matchSearch = !search || name.indexOf(search) !== -1;

                if (sizes.length && size === 'all') matchSize = false;
                if (concentrations.length && conc === 'all') matchConc = false;

                var show = matchSize && matchConc && matchFamily && matchSearch;
                card.classList.toggle('yq-hidden', !show);
                var col = card.closest('[class*="col-"]');
                if (col) col.classList.toggle('yq-hidden', !show);
                if (show) visible++;
            });

            if (countEl) countEl.textContent = visible + ' عطر';
        }

        filters.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
            cb.addEventListener('change', applyFilters);
        });
        if (searchInput) searchInput.addEventListener('input', applyFilters);
        applyFilters();
    }

    function initMobileFilters() {
        var toggle = document.getElementById('yaqutFilterToggle');
        var filters = document.getElementById('yaqutFilters');
        if (!toggle || !filters) return;
        toggle.addEventListener('click', function () {
            filters.classList.toggle('is-open');
        });
    }

    function initConfirmForms() {
        document.querySelectorAll('[data-yaqut-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                var msg = form.getAttribute('data-yaqut-confirm');
                if (msg && !window.confirm(msg)) e.preventDefault();
            });
        });
    }

    function initAdminNav() {
        var sidebar = document.getElementById('adminSidebar');
        if (!sidebar) return;
        document.addEventListener('click', function (e) {
            var link = e.target.closest('.yaqut-admin-sidebar a');
            if (link && sidebar.classList.contains('show')) {
                e.preventDefault();
                sidebar.classList.remove('show');
                setTimeout(function () { window.location.href = link.href; }, 200);
            }
        });
    }

    function init() {
        initThemeToggle();
        initHeroSlider();
        initAuthPanels();
        initFormValidation();
        initScrollReveal();
        initImageFallbacks();
        initImagePreviews();
        initProductFilters();
        initMobileFilters();
        initConfirmForms();
        initAdminNav();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
document.addEventListener("DOMContentLoaded", function () {

    const fields = [
        "regName",
        "regPhone",
        "regConfirmPhone"
    ];

    // استرجاع البيانات
    fields.forEach(id => {
        const element = document.getElementById(id);

        if (element) {
            const savedValue = localStorage.getItem(id);

            if (savedValue !== null) {
                element.value = savedValue;
            }

            // حفظ البيانات عند الكتابة
            element.addEventListener("input", function () {
                localStorage.setItem(id, this.value);
            });
        }
    });

});
document.addEventListener("DOMContentLoaded", function () {

    const loginBtn = document.getElementById("loginToggle");
    const registerBtn = document.getElementById("registerToggle");

    const loginPanel = document.getElementById("loginPanel");
    const registerPanel = document.getElementById("registerPanel");
    if (!loginBtn || !registerBtn || !loginPanel || !registerPanel) return;

    function showLogin() {

        loginPanel.classList.remove("d-none");
        registerPanel.classList.add("d-none");

        loginBtn.classList.add("is-active");
        registerBtn.classList.remove("is-active");

        loginBtn.setAttribute("aria-selected", "true");
        registerBtn.setAttribute("aria-selected", "false");

        localStorage.setItem("AuthPanel", "login");
    }

    function showRegister() {

        registerPanel.classList.remove("d-none");
        loginPanel.classList.add("d-none");

        registerBtn.classList.add("is-active");
        loginBtn.classList.remove("is-active");

        registerBtn.setAttribute("aria-selected", "true");
        loginBtn.setAttribute("aria-selected", "false");

        localStorage.setItem("AuthPanel", "register");
    }

    loginBtn.addEventListener("click", showLogin);
    registerBtn.addEventListener("click", showRegister);

    // استرجاع آخر تبويب
    const panel = localStorage.getItem("AuthPanel");

    if (panel === "register") {
        showRegister();
    } else {
        showLogin();
    }

});

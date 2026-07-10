(function () {
    "use strict";

    function init() {
        var shell = document.getElementById("yqAdminShell");
        var sidebar = document.getElementById("yqAdminSidebar");
        var overlay = document.getElementById("yqAdminOverlay");
        var menuBtn = document.getElementById("yqAdminMenuBtn");
        var collapseBtn = document.getElementById("yqSidebarCollapseBtn");
        var userTrigger = document.getElementById("yqAdminUserTrigger");
        var userDropdown = document.getElementById("yqAdminUserDropdown");

        if (!shell || !sidebar) return;

        var STORAGE_KEY = "yq-admin-sidebar-collapsed";

        // Restore collapsed state (desktop only, purely visual preference)
        try {
            if (localStorage.getItem(STORAGE_KEY) === "1") {
                shell.classList.add("is-collapsed");
            }
        } catch (e) {
            /* localStorage unavailable — ignore */
        }

        function openMobileNav() {
            sidebar.classList.add("is-open");
            overlay.classList.add("is-open");
            menuBtn.setAttribute("aria-expanded", "true");
        }

        function closeMobileNav() {
            sidebar.classList.remove("is-open");
            overlay.classList.remove("is-open");
            menuBtn.setAttribute("aria-expanded", "false");
        }

        if (menuBtn) {
            menuBtn.addEventListener("click", function () {
                if (sidebar.classList.contains("is-open")) {
                    closeMobileNav();
                } else {
                    openMobileNav();
                }
            });
        }

        if (overlay) {
            overlay.addEventListener("click", closeMobileNav);
        }

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") closeMobileNav();
        });

        if (collapseBtn) {
            collapseBtn.addEventListener("click", function () {
                var collapsed = shell.classList.toggle("is-collapsed");
                try {
                    localStorage.setItem(STORAGE_KEY, collapsed ? "1" : "0");
                } catch (e) {
                    /* ignore */
                }
            });
        }

        // User dropdown
        if (userTrigger && userDropdown) {
            userTrigger.addEventListener("click", function (e) {
                e.stopPropagation();
                var isOpen = userDropdown.classList.toggle("is-open");
                userTrigger.setAttribute("aria-expanded", isOpen ? "true" : "false");
            });

            document.addEventListener("click", function (e) {
                if (!userDropdown.contains(e.target) && !userTrigger.contains(e.target)) {
                    userDropdown.classList.remove("is-open");
                    userTrigger.setAttribute("aria-expanded", "false");
                }
            });
        }

        // Reset mobile drawer state on breakpoint change
        window.addEventListener("resize", function () {
            if (window.innerWidth > 992) {
                closeMobileNav();
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
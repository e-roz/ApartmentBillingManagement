/**
 * Modern Sidebar JavaScript
 * Handles mobile sidebar toggle and responsive behavior
 */

(function () {
    'use strict';

    // Initialize sidebar functionality when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        initSidebar();
    });

    function initSidebar() {
        // Get sidebar elements
        const sidebar = document.querySelector('.sidebar');
        const sidebarOverlay = document.getElementById('sidebarOverlay');
        const sidebarToggle = document.querySelector('.sidebar-toggle');
        const sidebarClose = document.getElementById('sidebarClose');

        if (!sidebar) return;

        // Create hamburger toggle button if it doesn't exist and we're on mobile
        if (!sidebarToggle && window.innerWidth < 992) {
            createToggleButton();
        }

        // Get or create toggle button
        const toggleBtn = sidebarToggle || document.querySelector('.sidebar-toggle');

        // Toggle sidebar when hamburger button is clicked
        if (toggleBtn) {
            toggleBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                toggleSidebar();
            });
        }

        // Close sidebar when close button is clicked
        if (sidebarClose) {
            sidebarClose.addEventListener('click', function (e) {
                e.stopPropagation();
                closeSidebar();
            });
        }

        // Close sidebar when overlay is clicked
        if (sidebarOverlay) {
            sidebarOverlay.addEventListener('click', function () {
                closeSidebar();
            });
        }

        // Close sidebar when clicking outside on mobile
        document.addEventListener('click', function (e) {
            if (window.innerWidth < 992 && sidebar && sidebar.classList.contains('show')) {
                if (!sidebar.contains(e.target) && !toggleBtn?.contains(e.target)) {
                    closeSidebar();
                }
            }
        });

        // Close sidebar on escape key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && sidebar && sidebar.classList.contains('show')) {
                closeSidebar();
            }
        });

        // Handle window resize
        window.addEventListener('resize', function () {
            if (window.innerWidth >= 992) {
                closeSidebar();
            }
        });

        function toggleSidebar() {
            if (!sidebar || !sidebarOverlay) return;

            const isOpen = sidebar.classList.contains('show');

            if (isOpen) {
                closeSidebar();
            } else {
                openSidebar();
            }
        }

        function openSidebar() {
            if (!sidebar || !sidebarOverlay) return;

            sidebar.classList.add('show');
            sidebarOverlay.classList.add('show');
            document.body.style.overflow = 'hidden'; // Prevent body scroll when sidebar is open
        }

        function closeSidebar() {
            if (!sidebar || !sidebarOverlay) return;

            sidebar.classList.remove('show');
            sidebarOverlay.classList.remove('show');
            document.body.style.overflow = ''; // Restore body scroll
        }

        function createToggleButton() {
            const toggleBtn = document.createElement('button');
            toggleBtn.className = 'sidebar-toggle d-lg-none';
            toggleBtn.type = 'button';
            toggleBtn.setAttribute('aria-label', 'Toggle sidebar');
            toggleBtn.innerHTML = '<i class="bi bi-list"></i>';

            // Insert before body content
            const body = document.body;
            if (body.firstChild) {
                body.insertBefore(toggleBtn, body.firstChild);
            } else {
                body.appendChild(toggleBtn);
            }

            // Add click event listener
            toggleBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                toggleSidebar();
            });
        }
    }
})();


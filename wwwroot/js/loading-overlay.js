/**
 * Loading Overlay Utility
 * Provides consistent loading indicators for long-running operations
 */

(function () {
    'use strict';

    // ========================================
    // Loading Overlay Functions
    // ========================================
    window.showLoading = {
        /**
         * Show loading overlay on a specific element or entire page
         * @param {string|HTMLElement} target - CSS selector or element to show overlay on
         * @param {string} message - Optional loading message
         */
        show: function (target, message = 'Processing...') {
            if (typeof jQuery !== 'undefined' && jQuery.fn.LoadingOverlay) {
                const options = {
                    image: '',
                    imageAutoResize: true,
                    imageColor: '#7c3aed', // Matcha color
                    maxSize: 50,
                    minSize: 20,
                    resizeInterval: 500,
                    size: '50%',
                    text: message,
                    textColor: '#7c3aed',
                    textResizeFactor: 0.8,
                    textClass: 'loading-overlay-text',
                    textOnly: false,
                    textPosition: 'bottom',
                    fade: [200, 200],
                    custom: function (overlay) {
                        overlay.css({
                            'background-color': 'rgba(255, 255, 255, 0.9)',
                            'z-index': '9999'
                        });
                    }
                };

                if (typeof target === 'string') {
                    jQuery(target).LoadingOverlay('show', options);
                } else if (target instanceof HTMLElement) {
                    jQuery(target).LoadingOverlay('show', options);
                } else {
                    jQuery('body').LoadingOverlay('show', options);
                }
            } else {
                // Fallback: Simple spinner
                const overlay = document.createElement('div');
                overlay.id = 'loading-overlay-fallback';
                overlay.style.cssText = `
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(255, 255, 255, 0.9);
                    z-index: 9999;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    flex-direction: column;
                `;
                overlay.innerHTML = `
                    <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-3 text-muted">${message}</p>
                `;
                document.body.appendChild(overlay);
            }
        },

        /**
         * Hide loading overlay
         * @param {string|HTMLElement} target - CSS selector or element to hide overlay on
         */
        hide: function (target) {
            if (typeof jQuery !== 'undefined' && jQuery.fn.LoadingOverlay) {
                if (typeof target === 'string') {
                    jQuery(target).LoadingOverlay('hide');
                } else if (target instanceof HTMLElement) {
                    jQuery(target).LoadingOverlay('hide');
                } else {
                    jQuery('body').LoadingOverlay('hide');
                }
            } else {
                // Fallback: Remove overlay
                const overlay = document.getElementById('loading-overlay-fallback');
                if (overlay) {
                    overlay.remove();
                }
            }
        }
    };

    // ========================================
    // Auto-enhance forms with loading states
    // ========================================
    document.addEventListener('DOMContentLoaded', function () {
        // Enhance forms that should show loading
        document.querySelectorAll('form[data-loading="true"]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                const submitButton = form.querySelector('button[type="submit"], input[type="submit"]');
                if (submitButton) {
                    submitButton.disabled = true;
                    const originalText = submitButton.textContent || submitButton.value;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Processing...';
                    
                    // Show loading overlay
                    window.showLoading.show(form, 'Processing your request...');
                }
            });
        });

        // Enhance buttons with loading class
        document.querySelectorAll('.btn-loading').forEach(function (button) {
            button.addEventListener('click', function () {
                if (button.type === 'submit' || button.getAttribute('data-loading') === 'true') {
                    button.disabled = true;
                    const originalText = button.innerHTML;
                    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Processing...';
                    button.setAttribute('data-original-text', originalText);
                }
            });
        });
    });
})();


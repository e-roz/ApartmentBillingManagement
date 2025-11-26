/**
 * UI Utilities - Common functions for enhanced UI/UX
 * Provides wrapper functions for SweetAlert2, Toastr, DataTables, etc.
 */

(function () {
    'use strict';

    // ========================================
    // Toastr Configuration
    // ========================================
    if (typeof toastr !== 'undefined') {
        toastr.options = {
            closeButton: true,
            debug: false,
            newestOnTop: true,
            progressBar: true,
            positionClass: 'toast-bottom-right',
            preventDuplicates: false,
            onclick: null,
            showDuration: '300',
            hideDuration: '1000',
            timeOut: '5000',
            extendedTimeOut: '1000',
            showEasing: 'swing',
            hideEasing: 'linear',
            showMethod: 'fadeIn',
            hideMethod: 'fadeOut'
        };
    }

    // ========================================
    // Toast Notification Wrappers
    // ========================================
    window.showToast = {
        success: function (message, title = 'Success') {
            if (typeof toastr !== 'undefined') {
                toastr.success(message, title);
            } else {
                console.log('Success:', message);
            }
        },
        error: function (message, title = 'Error') {
            if (typeof toastr !== 'undefined') {
                toastr.error(message, title);
            } else {
                console.error('Error:', message);
            }
        },
        warning: function (message, title = 'Warning') {
            if (typeof toastr !== 'undefined') {
                toastr.warning(message, title);
            } else {
                console.warn('Warning:', message);
            }
        },
        info: function (message, title = 'Info') {
            if (typeof toastr !== 'undefined') {
                toastr.info(message, title);
            } else {
                console.info('Info:', message);
            }
        }
    };

    // ========================================
    // SweetAlert2 Wrappers
    // ========================================
    window.showAlert = {
        success: function (title, message, callback) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    icon: 'success',
                    title: title,
                    text: message,
                    confirmButtonColor: '#9fbf9f',
                    timer: 3000,
                    timerProgressBar: true
                }).then((result) => {
                    if (callback && typeof callback === 'function') {
                        callback(result);
                    }
                });
            } else {
                alert(title + ': ' + message);
                if (callback) callback();
            }
        },
        error: function (title, message, callback) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    icon: 'error',
                    title: title,
                    text: message,
                    confirmButtonColor: '#dc3545'
                }).then((result) => {
                    if (callback && typeof callback === 'function') {
                        callback(result);
                    }
                });
            } else {
                alert(title + ': ' + message);
                if (callback) callback();
            }
        },
        confirm: function (title, message, confirmText = 'Yes', cancelText = 'Cancel') {
            return new Promise((resolve) => {
                if (typeof Swal !== 'undefined') {
                    Swal.fire({
                        title: title,
                        html: message,
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#9fbf9f',
                        cancelButtonColor: '#6c757d',
                        confirmButtonText: confirmText,
                        cancelButtonText: cancelText,
                        buttonsStyling: true,
                        customClass: {
                            confirmButton: 'swal2-confirm',
                            cancelButton: 'swal2-cancel',
                            popup: 'swal2-popup-matcha'
                        },
                        reverseButtons: false,
                        focusCancel: false,
                        focusConfirm: true
                    }).then((result) => {
                        resolve(result.isConfirmed);
                    });
                } else {
                    const confirmed = confirm(title + ': ' + message);
                    resolve(confirmed);
                }
            });
        },
        delete: function (title, message, itemName) {
            return new Promise((resolve) => {
                if (typeof Swal !== 'undefined') {
                    Swal.fire({
                        title: title,
                        html: message,
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#dc3545',
                        cancelButtonColor: '#6c757d',
                        confirmButtonText: 'Yes, delete it!',
                        cancelButtonText: 'Cancel',
                        buttonsStyling: true,
                        customClass: {
                            confirmButton: 'swal2-confirm-danger',
                            cancelButton: 'swal2-cancel',
                            popup: 'swal2-popup-matcha'
                        },
                        reverseButtons: false,
                        focusCancel: false,
                        focusConfirm: false
                    }).then((result) => {
                        resolve(result.isConfirmed);
                    });
                } else {
                    const confirmed = confirm(title + ': ' + message);
                    resolve(confirmed);
                }
            });
        }
    };

    // ========================================
    // Loading Overlay Wrappers
    // ========================================
    window.loadingOverlay = {
        show: function (element = 'body', text = 'Loading...') {
            if (typeof jQuery !== 'undefined' && jQuery.fn.loadingOverlay) {
                jQuery(element).loadingOverlay({
                    text: text,
                    image: '',
                    fontawesome: 'fa fa-spinner fa-spin',
                    custom: '<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>'
                });
            }
        },
        hide: function (element = 'body') {
            if (typeof jQuery !== 'undefined' && jQuery.fn.loadingOverlay) {
                jQuery(element).loadingOverlay('remove');
            }
        }
    };

    // ========================================
    // DataTables Initialization Helper
    // ========================================
    window.initDataTable = function (selector, options = {}) {
        if (typeof jQuery !== 'undefined' && jQuery.fn.DataTable) {
            const defaultOptions = {
                language: {
                    search: 'Search:',
                    lengthMenu: 'Show _MENU_ entries',
                    info: 'Showing _START_ to _END_ of _TOTAL_ entries',
                    infoEmpty: 'Showing 0 to 0 of 0 entries',
                    infoFiltered: '(filtered from _MAX_ total entries)',
                    paginate: {
                        first: 'First',
                        last: 'Last',
                        next: 'Next',
                        previous: 'Previous'
                    }
                },
                pageLength: 10,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                responsive: true,
                order: [],
                dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>rt<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>',
                ...options
            };
            return jQuery(selector).DataTable(defaultOptions);
        }
        return null;
    };

    // ========================================
    // Select2 Initialization Helper
    // ========================================
    window.initSelect2 = function (selector, options = {}) {
        if (typeof jQuery !== 'undefined' && jQuery.fn.select2) {
            const defaultOptions = {
                theme: 'bootstrap-5',
                width: '100%',
                allowClear: false,
                ...options
            };
            return jQuery(selector).select2(defaultOptions);
        }
        return null;
    };

    // ========================================
    // Flatpickr Initialization Helper
    // ========================================
    window.initFlatpickr = function (selector, options = {}) {
        if (typeof flatpickr !== 'undefined') {
            const defaultOptions = {
                dateFormat: 'Y-m-d',
                allowInput: true,
                ...options
            };
            return flatpickr(selector, defaultOptions);
        }
        return null;
    };

    // ========================================
    // Auto-initialize common elements on page load
    // ========================================
    document.addEventListener('DOMContentLoaded', function () {
        // Initialize Select2 on all select elements with class 'select2'
        if (typeof jQuery !== 'undefined' && jQuery.fn.select2) {
            jQuery('select.select2').each(function () {
                window.initSelect2(this);
            });
        }

        // Initialize Flatpickr on all date inputs
        if (typeof flatpickr !== 'undefined') {
            document.querySelectorAll('input[type="date"], input.flatpickr').forEach(function (input) {
                window.initFlatpickr(input);
            });
        }

        // Auto-dismiss Bootstrap alerts after 5 seconds
        setTimeout(function () {
            const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
            alerts.forEach(function (alert) {
                if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                    const bsAlert = new bootstrap.Alert(alert);
                    bsAlert.close();
                }
            });
        }, 5000);
    });

    // ========================================
    // Form Submission Enhancement
    // ========================================
    window.enhanceFormSubmission = function (formSelector, options = {}) {
        const form = document.querySelector(formSelector);
        if (!form) return;

        form.addEventListener('submit', function (e) {
            const submitButton = form.querySelector('button[type="submit"], input[type="submit"]');
            
            if (submitButton && !options.skipLoading) {
                // Show loading state
                submitButton.disabled = true;
                const originalText = submitButton.innerHTML;
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
                
                // Show loading overlay if specified
                if (options.showOverlay) {
                    window.loadingOverlay.show(options.overlayTarget || 'body', options.overlayText || 'Processing...');
                }

                // Re-enable button after timeout (fallback)
                setTimeout(function () {
                    submitButton.disabled = false;
                    submitButton.innerHTML = originalText;
                    if (options.showOverlay) {
                        window.loadingOverlay.hide(options.overlayTarget || 'body');
                    }
                }, 10000);
            }
        });
    };

    // ========================================
    // Auto-enhance forms with class 'enhanced-form'
    // ========================================
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('form.enhanced-form').forEach(function (form) {
            window.enhanceFormSubmission('#' + form.id || form, {
                showOverlay: true,
                overlayText: 'Processing your request...'
            });
        });
    });

})();


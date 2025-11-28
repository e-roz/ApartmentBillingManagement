/**
 * Page Enhancements - Auto-applies UI/UX improvements
 * Handles DataTables, Toastr notifications, form enhancements, etc.
 */

(function () {
    'use strict';

    // ========================================
    // Helper to get cookie by name
    // ========================================
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
    }

    // ========================================
    // Auto-convert Bootstrap alerts to Toastr
    // Only convert dismissible alerts (backend messages), not UI info alerts
    // ========================================
    function convertAlertsToToasts() {
        // Only convert alerts that have alert-dismissible class (these are backend messages)
        // Skip informational UI alerts that should stay on the page
        
        // Find success alerts (only dismissible ones from backend)
        document.querySelectorAll('.alert-success.alert-dismissible[data-auto-toast="true"]').forEach(function(alert) {
            // Skip if it's marked to not convert
            if (alert.classList.contains('no-toast')) return;
            
            const message = alert.textContent.trim().replace(/\s+/g, ' ');
            // Only convert if message is meaningful (not empty, not just whitespace)
            if (message && message.length > 5 && typeof showToast !== 'undefined') {
                // Remove icon and close button text from message
                const cleanMessage = message.replace(/[✓✔]/g, '').replace(/×/g, '').trim();
                if (cleanMessage) {
                    showToast.success(cleanMessage, 'Success');
                }
            }
            // Hide the original alert
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        });

        // Find error alerts (only dismissible ones from backend)
        document.querySelectorAll('.alert-danger.alert-dismissible[data-auto-toast="true"]').forEach(function(alert) {
            if (alert.classList.contains('no-toast')) return;
            
            const message = alert.textContent.trim().replace(/\s+/g, ' ');
            if (message && message.length > 5 && typeof showToast !== 'undefined') {
                const cleanMessage = message.replace(/[⚠✗]/g, '').replace(/×/g, '').trim();
                if (cleanMessage) {
                    showToast.error(cleanMessage, 'Error');
                }
            }
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        });

        // Don't convert warning/info alerts automatically - they're usually UI elements
        // Only convert if explicitly marked with data-toast="true"
        document.querySelectorAll('.alert-warning[data-auto-toast="true"], .alert-info[data-auto-toast="true"]').forEach(function(alert) {
            const message = alert.textContent.trim().replace(/\s+/g, ' ');
            if (message && message.length > 5 && typeof showToast !== 'undefined') {
                if (alert.classList.contains('alert-warning')) {
                    showToast.warning(message, 'Warning');
                } else {
                    showToast.info(message, 'Info');
                }
            }
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        });
    }

    // ========================================
    // Auto-initialize DataTables
    // ========================================
    function initDataTables() {
        if (typeof jQuery === 'undefined' || !jQuery.fn.DataTable) return;

        // Initialize all tables with class 'data-table' or in table-responsive containers
        document.querySelectorAll('.table-responsive table, table.data-table').forEach(function(table) {
            // Skip if already initialized
            if (jQuery(table).hasClass('dataTable')) return;

            // Get table ID or create one
            let tableId = table.id;
            if (!tableId) {
                tableId = 'table_' + Math.random().toString(36).substring(2, 11);
                table.id = tableId;
            }

            // Check if table has data
            const rowCount = table.querySelectorAll('tbody tr').length;
            if (rowCount === 0) return;

            // Initialize DataTable
            initDataTable('#' + tableId, {
                pageLength: 10,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                order: [],
                responsive: true,
                language: {
                    search: 'Search:',
                    lengthMenu: 'Show _MENU_ entries',
                    info: 'Showing _START_ to _END_ of _TOTAL_ entries',
                    infoEmpty: 'No entries to show',
                    infoFiltered: '(filtered from _MAX_ total entries)',
                    zeroRecords: 'No matching records found',
                    paginate: {
                        first: 'First',
                        last: 'Last',
                        next: 'Next',
                        previous: 'Previous'
                    }
                },
                dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>rt<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>'
            });
        });
    }

    // ========================================
    // Enhance all forms with loading states
    // ========================================
    function enhanceForms() {
        document.querySelectorAll('form:not(.no-enhance)').forEach(function(form) {
            // Skip if already enhanced
            if (form.dataset.enhanced === 'true') return;

            form.addEventListener('submit', function(e) {
                // IMPORTANT: Check if another handler already prevented submission
                if (e.defaultPrevented) {
                    return;
                }
                
                const submitButton = e.submitter; // Use the button that triggered the submit
                
                if (submitButton && (submitButton.tagName === 'BUTTON' || (submitButton.tagName === 'INPUT' && submitButton.type === 'submit'))) {
                    
                    const isFileDownload = submitButton.hasAttribute('data-file-download');

                    // Small delay to allow other handlers to prevent submission
                    setTimeout(function() {
                        // Double-check the event wasn't prevented
                        if (e.defaultPrevented) {
                            return;
                        }
                        
                        // Disable button
                        submitButton.disabled = true;
                        const originalText = submitButton.innerHTML || submitButton.value;
                        submitButton.dataset.originalText = originalText;
                        
                        // Add loading spinner
                        if (submitButton.tagName === 'BUTTON') {
                            submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
                        }
                        
                        // Show loading overlay for important forms
                        if (form.classList.contains('important-form') || form.querySelector('[name*="Delete"], [name*="delete"]')) {
                            if (typeof loadingOverlay !== 'undefined') {
                                loadingOverlay.show('body', 'Processing your request...');
                            }
                        }

                        if (isFileDownload) {
                            const interval = setInterval(function() {
                                if (getCookie('fileDownload')) {
                                    // Re-enable button
                                    submitButton.disabled = false;
                                    if (submitButton.tagName === 'BUTTON') {
                                        submitButton.innerHTML = originalText;
                                    }
                                    if (typeof loadingOverlay !== 'undefined') {
                                        loadingOverlay.hide('body');
                                    }
                                    
                                    // Clear the cookie
                                    document.cookie = "fileDownload=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
                                    
                                    clearInterval(interval);
                                }
                            }, 1000);
                        } else {
                            // Re-enable after timeout (fallback)
                            setTimeout(function() {
                                submitButton.disabled = false;
                                if (submitButton.tagName === 'BUTTON') {
                                    submitButton.innerHTML = originalText;
                                }
                                if (typeof loadingOverlay !== 'undefined') {
                                    loadingOverlay.hide('body');
                                }
                            }, 30000);
                        }
                    }, 0);
                }
            });

            form.dataset.enhanced = 'true';
        });
    }

    // ========================================
    // Initialize on page load
    // ========================================
    document.addEventListener('DOMContentLoaded', function() {
        // Convert alerts to toasts (with small delay to ensure alerts are rendered)
        setTimeout(convertAlertsToToasts, 100);

        // Initialize DataTables
        setTimeout(initDataTables, 200);

        // Enhance forms
        enhanceForms();

        // Initialize Select2 on all select elements (except those with no-select2 class)
        if (typeof jQuery !== 'undefined' && jQuery.fn.select2) {
            document.querySelectorAll('select:not(.no-select2)').forEach(function(select) {
                // Skip if already initialized or if it's a simple select with few options
                if (select.classList.contains('select2-hidden-accessible')) return;
                
                // Skip very simple selects (like yes/no, status with 2-3 options)
                const optionCount = select.options.length;
                if (optionCount <= 3 && !select.classList.contains('select2')) return;
                
                // Initialize Select2 for dropdowns with many options or explicitly marked
                if (optionCount > 3 || select.classList.contains('select2')) {
                    initSelect2(select, {
                        theme: 'bootstrap-5',
                        width: '100%',
                        allowClear: false
                    });
                }
            });
        }

        // Initialize Flatpickr on all date inputs
        if (typeof flatpickr !== 'undefined') {
            document.querySelectorAll('input[type="date"]:not(.no-flatpickr)').forEach(function(input) {
                if (!input.classList.contains('flatpickr-input')) {
                    initFlatpickr(input, {
                        dateFormat: 'Y-m-d',
                        allowInput: true
                    });
                }
            });
        }

        // Initialize Bootstrap tooltips
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"], [title]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }
    });

    // ========================================
    // Export functions for manual use
    // ========================================
    window.pageEnhancements = {
        convertAlertsToToasts: convertAlertsToToasts,
        initDataTables: initDataTables,
        enhanceForms: enhanceForms
    };

})();


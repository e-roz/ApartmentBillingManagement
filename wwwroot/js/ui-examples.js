/**
 * UI Enhancement Examples
 * Ready-to-use code snippets for common UI enhancements
 * Copy and adapt these examples to your pages
 */

// ========================================
// Example 1: Enhanced Table with DataTables
// ========================================
function initEnhancedTable() {
    // Add this to your page's Scripts section
    // Make sure your table has an id: <table id="tenantsTable">
    
    initDataTable('#tenantsTable', {
        pageLength: 25,
        order: [[0, 'asc']],
        columnDefs: [
            { targets: [-1], orderable: false } // Last column (Actions) not sortable
        ],
        language: {
            search: 'Search tenants:',
            lengthMenu: 'Show _MENU_ tenants per page'
        }
    });
}

// ========================================
// Example 2: Enhanced Dropdown with Select2
// ========================================
function initEnhancedDropdown() {
    // Add class 'select2' to your select element
    // Or manually initialize:
    
    initSelect2('#tenantSelect', {
        placeholder: 'Select a tenant...',
        allowClear: true,
        width: '100%'
    });
}

// ========================================
// Example 3: Date Picker with Flatpickr
// ========================================
function initDatePicker() {
    // Add class 'flatpickr' to your date input
    // Or manually initialize:
    
    initFlatpickr('#paymentDate', {
        dateFormat: 'Y-m-d',
        minDate: 'today',
        defaultDate: 'today'
    });
}

// ========================================
// Example 4: Form Submission with Loading
// ========================================
function enhancePaymentForm() {
    // Add class 'enhanced-form' to your form
    // Or manually enhance:
    
    enhanceFormSubmission('#paymentForm', {
        showOverlay: true,
        overlayText: 'Processing payment...',
        overlayTarget: 'body'
    });
    
    // Handle form submission
    document.getElementById('paymentForm').addEventListener('submit', function(e) {
        // Form will automatically show loading overlay
        // Your backend code remains unchanged
    });
}

// ========================================
// Example 5: Chart with Chart.js
// ========================================
function initRevenueChart() {
    const ctx = document.getElementById('revenueChart');
    if (!ctx) return;
    
    new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
            datasets: [{
                label: 'Revenue',
                data: [12000, 19000, 15000, 25000, 22000, 28000],
                borderColor: '#9fbf9f',
                backgroundColor: 'rgba(159, 191, 159, 0.1)',
                tension: 0.4,
                fill: true
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            return '₱' + value.toLocaleString();
                        }
                    }
                }
            }
        }
    });
}

// ========================================
// Example 6: Delete Confirmation
// ========================================
function confirmDelete(itemName, deleteCallback) {
    showAlert.delete('Confirm Delete', `Are you sure you want to delete <strong>${itemName}</strong>? This action cannot be undone.`)
        .then((confirmed) => {
            if (confirmed && deleteCallback) {
                deleteCallback();
            }
        });
}

// Usage:
// confirmDelete('John Doe', function() {
//     // Perform delete action
//     window.location.href = '/delete?id=123';
// });

// ========================================
// Example 7: Success Notification After Form Submit
// ========================================
function showSuccessAfterSubmit(message) {
    // Call this after successful form submission
    showToast.success(message, 'Success');
    
    // Or use SweetAlert for more prominent notification
    // showAlert.success('Success', message);
}

// ========================================
// Example 8: AJAX Form Submission with Loading
// ========================================
function submitFormWithAJAX(formId, url, successCallback) {
    const form = document.getElementById(formId);
    if (!form) return;
    
    loadingOverlay.show('body', 'Processing...');
    
    const formData = new FormData(form);
    
    fetch(url, {
        method: 'POST',
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        loadingOverlay.hide('body');
        if (data.success) {
            showToast.success(data.message || 'Operation completed successfully!');
            if (successCallback) successCallback(data);
        } else {
            showToast.error(data.message || 'An error occurred');
        }
    })
    .catch(error => {
        loadingOverlay.hide('body');
        showToast.error('Network error. Please try again.');
        console.error('Error:', error);
    });
}

// ========================================
// Example 9: Sortable List
// ========================================
function initSortableList(listId) {
    const list = document.getElementById(listId);
    if (!list) return;
    
    Sortable.create(list, {
        animation: 150,
        handle: '.drag-handle', // Add this class to drag handle element
        onEnd: function(evt) {
            // Save new order
            const items = Array.from(list.children).map((item, index) => ({
                id: item.dataset.id,
                order: index
            }));
            
            // Send to server
            fetch('/api/reorder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ items: items })
            })
            .then(() => showToast.success('Order updated successfully!'));
        }
    });
}

// ========================================
// Example 10: Auto-Initialize on Page Load
// ========================================
document.addEventListener('DOMContentLoaded', function() {
    // Auto-initialize DataTables on tables with class 'data-table'
    document.querySelectorAll('table.data-table').forEach(function(table) {
        if (table.id) {
            initDataTable('#' + table.id);
        }
    });
    
    // Auto-initialize Select2 on selects with class 'select2'
    // Already handled in ui-utils.js
    
    // Auto-initialize Flatpickr on date inputs
    // Already handled in ui-utils.js
});


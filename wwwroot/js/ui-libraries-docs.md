# UI Enhancement Libraries Documentation

This document provides usage examples for all the JavaScript libraries added to enhance UI/UX.

## 📚 Included Libraries

1. **SweetAlert2** - Beautiful alert modals
2. **Toastr** - Toast notifications
3. **DataTables** - Enhanced tables
4. **Chart.js** - Data visualization
5. **Select2** - Enhanced dropdowns
6. **Flatpickr** - Date/time picker
7. **Loading Overlay** - Loading indicators
8. **SortableJS** - Drag and drop
9. **Animate.css** - CSS animations

---

## 🎯 Quick Usage Examples

### SweetAlert2 - Alert Modals

```javascript
// Success alert
showAlert.success('Payment Recorded', 'Payment has been successfully recorded!');

// Error alert
showAlert.error('Error', 'Something went wrong. Please try again.');

// Confirmation dialog
showAlert.confirm('Delete Payment', 'Are you sure you want to delete this payment?')
    .then((confirmed) => {
        if (confirmed) {
            // User clicked "Yes"
            // Perform delete action
        }
    });

// Delete confirmation
showAlert.delete('Delete Tenant', 'This action cannot be undone!', 'John Doe')
    .then((confirmed) => {
        if (confirmed) {
            // Delete the tenant
        }
    });
```

### Toastr - Toast Notifications

```javascript
// Success toast
showToast.success('Payment recorded successfully!', 'Success');

// Error toast
showToast.error('Failed to process payment', 'Error');

// Warning toast
showToast.warning('Payment amount exceeds balance', 'Warning');

// Info toast
showToast.info('Processing your request...', 'Info');
```

### DataTables - Enhanced Tables

```javascript
// Basic initialization
initDataTable('#myTable');

// With custom options
initDataTable('#myTable', {
    pageLength: 25,
    order: [[0, 'desc']],
    columnDefs: [
        { targets: [0], orderable: true },
        { targets: [-1], orderable: false } // Last column not sortable
    ]
});

// Or use jQuery directly
$('#myTable').DataTable({
    responsive: true,
    pageLength: 10
});
```

### Chart.js - Data Visualization

```javascript
// Line chart example
const ctx = document.getElementById('myChart');
new Chart(ctx, {
    type: 'line',
    data: {
        labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May'],
        datasets: [{
            label: 'Revenue',
            data: [12000, 19000, 15000, 25000, 22000],
            borderColor: '#9fbf9f',
            backgroundColor: 'rgba(159, 191, 159, 0.1)',
            tension: 0.4
        }]
    },
    options: {
        responsive: true,
        plugins: {
            legend: {
                display: true
            }
        }
    }
});
```

### Select2 - Enhanced Dropdowns

```javascript
// Basic initialization
initSelect2('#tenantSelect');

// With custom options
initSelect2('#tenantSelect', {
    placeholder: 'Select a tenant...',
    allowClear: true,
    ajax: {
        url: '/api/tenants',
        dataType: 'json'
    }
});

// Or add class 'select2' to select element (auto-initialized)
<select class="select2">
    <option>Option 1</option>
</select>
```

### Flatpickr - Date Picker

```javascript
// Basic initialization
initFlatpickr('#paymentDate');

// With custom options
initFlatpickr('#paymentDate', {
    dateFormat: 'Y-m-d',
    minDate: 'today',
    maxDate: new Date().fp_incr(365) // 1 year from now
});

// Or add class 'flatpickr' to date input (auto-initialized)
<input type="date" class="flatpickr" />
```

### Loading Overlay

```javascript
// Show loading overlay
loadingOverlay.show('body', 'Processing payment...');

// Hide loading overlay
loadingOverlay.hide('body');

// Show on specific element
loadingOverlay.show('#myForm', 'Saving...');
loadingOverlay.hide('#myForm');
```

### SortableJS - Drag and Drop

```javascript
// Make a list sortable
const sortable = Sortable.create(document.getElementById('myList'), {
    animation: 150,
    handle: '.drag-handle',
    onEnd: function (evt) {
        // Save new order
        console.log('New order:', evt.newIndex);
    }
});
```

### Animate.css - CSS Animations

```html
<!-- Add animation classes to elements -->
<div class="animate__animated animate__fadeIn">
    Content fades in
</div>

<button class="btn animate__animated animate__bounce">
    Bouncing button
</button>
```

---

## 🔧 Integration Examples

### Enhanced Form Submission

```javascript
// Add class 'enhanced-form' to form (auto-enhanced)
<form class="enhanced-form" method="post">
    <!-- form fields -->
</form>

// Or manually enhance
enhanceFormSubmission('#myForm', {
    showOverlay: true,
    overlayText: 'Processing payment...',
    overlayTarget: 'body'
});
```

### Auto-Initialize Elements

Elements are automatically initialized if they have these classes:
- `select.select2` - Select2 dropdowns
- `input.flatpickr` or `input[type="date"]` - Date pickers
- `form.enhanced-form` - Enhanced form submissions

---

## 📝 Notes

- All libraries are loaded from CDN for easy updates
- Utilities are in `wwwroot/js/ui-utils.js`
- Backend code is not affected - these are UI-only enhancements
- All functions have fallbacks if libraries fail to load


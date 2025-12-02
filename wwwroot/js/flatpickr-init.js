document.addEventListener('DOMContentLoaded', function () {
    const leaseStart = document.querySelector("#LeaseInput_LeaseStart");
    if (leaseStart) {
        flatpickr(leaseStart, {
            enableTime: false,
            dateFormat: "Y-m-d",
        });
    }

    const leaseEnd = document.querySelector("#LeaseInput_LeaseEnd");
    if (leaseEnd) {
        flatpickr(leaseEnd, {
            enableTime: false,
            dateFormat: "Y-m-d",
        });
    }
});
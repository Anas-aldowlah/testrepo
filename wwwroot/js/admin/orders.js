document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Popovers
    const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
    const popoverList = [...popoverTriggerList].map(popoverTriggerEl => {
        return new bootstrap.Popover(popoverTriggerEl, {
            trigger: 'focus' // Click to open, click outside to close
        });
    });

    // 2. Handle Order Status Update (AJAX)
    const statusSelects = document.querySelectorAll('.ajax-status-select');
    statusSelects.forEach(select => {
        select.addEventListener('change', function () {
            const orderId = this.getAttribute('data-order-id');
            const url = this.getAttribute('data-url');
            const newStatus = this.value;
            const originalValue = this.getAttribute('data-current') || this.querySelector('option[selected]')?.value;

            // Disable select during request
            this.disabled = true;

            const formData = new FormData();
            formData.append('id', orderId);
            formData.append('status', newStatus);
            // Append anti-forgery token if available on page
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) formData.append('__RequestVerificationToken', token);

            fetch(url, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            })
            .then(response => response.json())
            .then(data => {
                this.disabled = false;
                if (data.success) {
                    this.setAttribute('data-current', newStatus);
                    updateStatusUI(orderId, data.status, data.timeState);
                } else {
                    alert(data.message || 'حدث خطأ أثناء التحديث.');
                    this.value = originalValue; // Revert
                }
            })
            .catch(error => {
                console.error('Error:', error);
                this.disabled = false;
                alert('حدث خطأ في الاتصال بالخادم.');
                this.value = originalValue; // Revert
            });
        });
    });

    // 3. Handle Payment Status Update (AJAX)
    const paymentSelects = document.querySelectorAll('.ajax-payment-select');
    paymentSelects.forEach(select => {
        select.addEventListener('change', function () {
            const orderId = this.getAttribute('data-order-id');
            const url = this.getAttribute('data-url');
            const newStatus = this.value;
            const originalValue = this.getAttribute('data-current') || this.querySelector('option[selected]')?.value;

            this.disabled = true;

            const formData = new FormData();
            formData.append('id', orderId);
            formData.append('paymentStatus', newStatus);
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) formData.append('__RequestVerificationToken', token);

            fetch(url, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            })
            .then(response => response.json())
            .then(data => {
                this.disabled = false;
                if (data.success) {
                    this.setAttribute('data-current', newStatus);
                } else {
                    alert(data.message || 'حدث خطأ أثناء التحديث.');
                    this.value = originalValue;
                }
            })
            .catch(error => {
                console.error('Error:', error);
                this.disabled = false;
                alert('حدث خطأ في الاتصال بالخادم.');
                this.value = originalValue;
            });
        });
    });

    function updateStatusUI(orderId, status, timeState) {
        const badge = document.getElementById(`status-badge-${orderId}`);
        const dateElement = document.getElementById(`status-date-${orderId}`);
        const popoverStatus = document.getElementById(`popover-status-${orderId}`);

        let label = status;
        let className = 'yq-orders-badge--neutral';

        switch (status) {
            case 'Pending': label = 'قيد الانتظار'; className = 'yq-orders-badge--pending'; break;
            case 'Processed': label = 'تم الدفع'; className = 'yq-orders-badge--processed'; break;
            case 'Shipped': label = 'تم الشحن'; className = 'yq-orders-badge--shipped'; break;
            case 'Delivered': label = 'تم التوصيل'; className = 'yq-orders-badge--delivered'; break;
            case 'Cancelled': label = 'ملغي'; className = 'yq-orders-badge--cancelled'; break;
        }

        if (badge) {
            badge.textContent = label;
            badge.className = `yq-orders-badge ${className}`;
        }
        
        if (dateElement && timeState) {
            dateElement.textContent = timeState;
        }

        if (popoverStatus) {
            popoverStatus.textContent = label;
        }
    }
});

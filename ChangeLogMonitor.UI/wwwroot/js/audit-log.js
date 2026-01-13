// Audit Log UI JavaScript

document.addEventListener('DOMContentLoaded', function () {
    initExpandButtons();
    initFilterForm();
});

function initExpandButtons() {
    document.querySelectorAll('.expand-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            this.classList.toggle('expanded');
        });
    });
}

function initFilterForm() {
    const form = document.getElementById('filterForm');
    if (!form) return;

    const clearBtn = document.getElementById('clearFilters');
    if (clearBtn) {
        clearBtn.addEventListener('click', function () {
            form.querySelectorAll('input[type="text"], input[type="datetime-local"]').forEach(function (input) {
                input.value = '';
            });
            form.querySelectorAll('select').forEach(function (select) {
                select.selectedIndex = 0;
            });
            form.submit();
        });
    }
}

function formatDateTime(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString('ru-RU', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
}

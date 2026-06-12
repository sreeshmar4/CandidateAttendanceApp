document.addEventListener('DOMContentLoaded', function () {
    var tabOrder = ['tab1-tab', 'tab2-tab', 'tab3-tab', 'tab4-tab'];
    var highestUnlockedTab = 0; // Only tab1 is unlocked initially

    // Disable all tabs except the first one initially
    function updateTabStates() {
        tabOrder.forEach(function (tabId, index) {
            var tabBtn = document.getElementById(tabId);
            if (index > highestUnlockedTab) {
                tabBtn.classList.add('disabled');
                tabBtn.setAttribute('aria-disabled', 'true');
                tabBtn.style.pointerEvents = 'none';
                tabBtn.style.opacity = '0.5';
            } else {
                tabBtn.classList.remove('disabled');
                tabBtn.removeAttribute('aria-disabled');
                tabBtn.style.pointerEvents = '';
                tabBtn.style.opacity = '';
            }
        });
    }

    updateTabStates();

    // Prevent tab header clicks for locked tabs
    tabOrder.forEach(function (tabId, index) {
        var tabBtn = document.getElementById(tabId);
        tabBtn.addEventListener('click', function (e) {
            if (index > highestUnlockedTab) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    });

    function validateTabPane(pane) {
        var inputs = pane.querySelectorAll('input[required], select[required], textarea[required]');
        var isValid = true;

        // Clear previous custom validation messages
        pane.querySelectorAll('.field-validation-error-custom').forEach(function (el) {
            el.remove();
        });

        inputs.forEach(function (input) {
            input.classList.remove('is-invalid');

            var value = input.value.trim();
            if (!value) {
                isValid = false;
                input.classList.add('is-invalid');

                var existingMsg = input.parentElement.querySelector('.field-validation-error-custom');
                if (!existingMsg) {
                    var label = input.parentElement.querySelector('label');
                    var fieldName = label ? label.textContent.replace('*', '').trim() : 'This field';
                    var msg = document.createElement('span');
                    msg.className = 'text-danger field-validation-error-custom';
                    msg.textContent = fieldName + ' is required.';
                    input.parentElement.appendChild(msg);
                }
            }

            if (input.type === 'email' && value) {
                var emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (!emailPattern.test(value)) {
                    isValid = false;
                    input.classList.add('is-invalid');
                    var existingMsg = input.parentElement.querySelector('.field-validation-error-custom');
                    if (!existingMsg) {
                        var msg = document.createElement('span');
                        msg.className = 'text-danger field-validation-error-custom';
                        msg.textContent = 'Please enter a valid email address.';
                        input.parentElement.appendChild(msg);
                    }
                }
            }

            if (input.name && (input.name === 'PhoneNumber' || input.name === 'ParentNo') && value) {
                var phonePattern = /^\+?\d{10,15}$/;
                if (!phonePattern.test(value.replace(/[\s\-]/g, ''))) {
                    isValid = false;
                    input.classList.add('is-invalid');
                    var existingMsg = input.parentElement.querySelector('.field-validation-error-custom');
                    if (!existingMsg) {
                        var msg = document.createElement('span');
                        msg.className = 'text-danger field-validation-error-custom';
                        msg.textContent = 'Please enter a valid phone number (10-15 digits).';
                        input.parentElement.appendChild(msg);
                    }
                }
            }
        });

        return isValid;
    }

    function validateCurrentTab(button) {
        var currentPane = button.closest('.tab-pane');
        return validateTabPane(currentPane);
    }

    // Clear validation styling on input
    document.querySelectorAll('#registrationForm input, #registrationForm select, #registrationForm textarea').forEach(function (input) {
        input.addEventListener('input', function () {
            this.classList.remove('is-invalid');
            var customMsg = this.parentElement.querySelector('.field-validation-error-custom');
            if (customMsg) customMsg.remove();
        });
        input.addEventListener('change', function () {
            this.classList.remove('is-invalid');
            var customMsg = this.parentElement.querySelector('.field-validation-error-custom');
            if (customMsg) customMsg.remove();
        });
    });

    document.querySelectorAll('.btn-next').forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (!validateCurrentTab(this)) {
                return;
            }
            var nextTabId = this.dataset.next;
            var nextTabIndex = tabOrder.indexOf(nextTabId);
            if (nextTabIndex > highestUnlockedTab) {
                highestUnlockedTab = nextTabIndex;
                updateTabStates();
            }
            var nextTab = document.getElementById(nextTabId);
            if (nextTab) {
                var tab = new bootstrap.Tab(nextTab);
                tab.show();
            }
        });
    });

    document.querySelectorAll('.btn-prev').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var prevTab = document.getElementById(this.dataset.prev);
            if (prevTab) {
                var tab = new bootstrap.Tab(prevTab);
                tab.show();
            }
        });
    });
});

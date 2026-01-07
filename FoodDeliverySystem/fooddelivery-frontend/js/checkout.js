// Логика оформления заказа

class CheckoutManager {
    constructor() {
        this.currentStep = 1;
        this.orderData = {
            deliveryAddress: {},
            paymentMethod: 'card',
            cardDetails: {},
            specialInstructions: ''
        };
        this.cartData = null;
    }

    // Загрузка данных для оформления
    async loadCheckoutData() {
        // Загружаем данные корзины
        const cartManager = new CartManager();
        await cartManager.loadCart();

        this.cartData = cartManager.getCheckoutData();

        if (!this.cartData || this.cartData.items.length === 0) {
            Utils.showNotification('Корзина пуста', 'error');
            setTimeout(() => {
                window.location.href = 'cart.html';
            }, 2000);
            return;
        }

        this.renderOrderPreview();
        this.updateDeliveryTime();
    }

    // Отображение предпросмотра заказа
    renderOrderPreview() {
        const container = document.getElementById('orderItemsPreview');
        if (!container || !this.cartData) return;

        container.innerHTML = this.cartData.items.map(item => `
            <div class="preview-order-item">
                <div class="preview-item-image">
                    <img src="images/dishes/default.jpg" alt="${item.dishName}">
                </div>
                <div class="preview-item-info">
                    <h4>${item.dishName}</h4>
                    <p class="preview-item-quantity">${item.quantity} × ${Utils.formatPrice(item.price)}</p>
                </div>
                <div class="preview-item-total">
                    ${Utils.formatPrice(item.total)}
                </div>
            </div>
        `).join('');

        // Обновляем итоги
        document.getElementById('checkoutSubtotal').textContent = Utils.formatPrice(this.cartData.subtotal);
        document.getElementById('checkoutDelivery').textContent =
            this.cartData.delivery === 0 ? 'Бесплатно' : Utils.formatPrice(this.cartData.delivery);
        document.getElementById('checkoutTotal').textContent = Utils.formatPrice(this.cartData.total);
    }

    // Обновление времени доставки
    updateDeliveryTime() {
        const now = new Date();
        const deliveryTime = new Date(now.getTime() + 45 * 60000); // +45 минут

        const timeString = deliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });

        document.getElementById('deliveryTime').textContent = timeString;
    }

    // Переключение шагов
    goToStep(step) {
        if (step < 1 || step > 3) return;

        // Валидация текущего шага перед переходом
        if (step > this.currentStep) {
            if (!this.validateCurrentStep()) {
                return;
            }
        }

        // Скрываем текущий шаг
        document.querySelectorAll('.checkout-step').forEach(el => {
            el.classList.remove('active');
        });

        // Показываем новый шаг
        document.getElementById(`step${step}`).classList.add('active');

        // Обновляем индикатор шагов
        document.querySelectorAll('.step').forEach(el => {
            el.classList.remove('active');
        });

        for (let i = 1; i <= step; i++) {
            document.querySelector(`.step:nth-child(${i})`).classList.add('active');
        }

        this.currentStep = step;

        // Прокручиваем к началу шага
        document.getElementById(`step${step}`).scrollIntoView({ behavior: 'smooth' });
    }

    // Валидация текущего шага
    validateCurrentStep() {
        switch (this.currentStep) {
            case 1:
                return this.validateAddressStep();
            case 2:
                return this.validatePaymentStep();
            case 3:
                return this.validateConfirmationStep();
            default:
                return true;
        }
    }

    // Валидация шага адреса
    validateAddressStep() {
        const address = document.getElementById('address').value.trim();
        const city = document.getElementById('city').value.trim();

        if (!address || !city) {
            Utils.showNotification('Заполните обязательные поля адреса', 'error');
            return false;
        }

        // Сохраняем данные адреса
        this.orderData.deliveryAddress = {
            address: address,
            city: city,
            postalCode: document.getElementById('postalCode').value.trim(),
            apartment: document.getElementById('apartment').value.trim(),
            entrance: document.getElementById('entrance').value.trim(),
            floor: document.getElementById('floor').value.trim(),
            intercom: document.getElementById('intercom').value.trim()
        };

        this.orderData.specialInstructions = document.getElementById('deliveryInstructions').value.trim();

        return true;
    }

    // Валидация шага оплаты
    validatePaymentStep() {
        const paymentMethod = document.querySelector('.payment-method.active').dataset.method;
        this.orderData.paymentMethod = paymentMethod;

        if (paymentMethod === 'card') {
            const cardNumber = document.getElementById('cardNumber').value.replace(/\s/g, '');
            const cardExpiry = document.getElementById('cardExpiry').value;
            const cardCVC = document.getElementById('cardCVC').value;
            const cardHolder = document.getElementById('cardHolder').value.trim();

            if (!this.validateCardNumber(cardNumber)) {
                Utils.showNotification('Неверный номер карты', 'error');
                return false;
            }

            if (!this.validateCardExpiry(cardExpiry)) {
                Utils.showNotification('Неверный срок действия карты', 'error');
                return false;
            }

            if (!this.validateCVC(cardCVC)) {
                Utils.showNotification('Неверный CVC код', 'error');
                return false;
            }

            if (!cardHolder) {
                Utils.showNotification('Введите имя владельца карты', 'error');
                return false;
            }

            this.orderData.cardDetails = {
                number: cardNumber,
                expiry: cardExpiry,
                cvc: cardCVC,
                holder: cardHolder,
                saveCard: document.getElementById('saveCard').checked
            };
        }

        return true;
    }

    // Валидация шага подтверждения
    validateConfirmationStep() {
        if (!document.getElementById('termsCheck').checked) {
            Utils.showNotification('Необходимо принять условия доставки', 'error');
            return false;
        }

        // Обновляем сводку в подтверждении
        this.updateConfirmationSummary();

        return true;
    }

    // Обновление сводки в подтверждении
    updateConfirmationSummary() {
        // Адрес
        const address = this.orderData.deliveryAddress;
        document.getElementById('summaryAddress').textContent =
            `${address.address}, ${address.city}${address.apartment ? ', кв. ' + address.apartment : ''}`;

        // Способ оплаты
        const paymentMethods = {
            'card': 'Банковская карта',
            'cash': 'Наличными при получении',
            'online': 'Онлайн-кошелек'
        };
        document.getElementById('summaryPayment').textContent = paymentMethods[this.orderData.paymentMethod];

        // Состав заказа
        const itemsContainer = document.getElementById('orderItemsSummary');
        if (itemsContainer && this.cartData) {
            itemsContainer.innerHTML = this.cartData.items.map(item => `
                <div class="summary-order-item">
                    <span>${item.dishName} × ${item.quantity}</span>
                    <span>${Utils.formatPrice(item.total)}</span>
                </div>
            `).join('');
        }

        // Итого
        document.getElementById('summaryTotal').textContent = Utils.formatPrice(this.cartData.total);
    }

    // Валидация номера карты (простая)
    validateCardNumber(number) {
        // Удаляем пробелы
        const cleanNumber = number.replace(/\s/g, '');

        // Проверяем, что это 16 цифр
        if (!/^\d{16}$/.test(cleanNumber)) {
            return false;
        }

        // Алгоритм Луна
        let sum = 0;
        let isEven = false;

        for (let i = cleanNumber.length - 1; i >= 0; i--) {
            let digit = parseInt(cleanNumber.charAt(i), 10);

            if (isEven) {
                digit *= 2;
                if (digit > 9) {
                    digit -= 9;
                }
            }

            sum += digit;
            isEven = !isEven;
        }

        return sum % 10 === 0;
    }

    // Валидация срока действия карты
    validateCardExpiry(expiry) {
        const match = expiry.match(/^(\d{2})\/(\d{2})$/);
        if (!match) return false;

        const month = parseInt(match[1], 10);
        const year = parseInt('20' + match[2], 10);

        const now = new Date();
        const currentYear = now.getFullYear();
        const currentMonth = now.getMonth() + 1;

        if (year < currentYear || (year === currentYear && month < currentMonth)) {
            return false; // Карта просрочена
        }

        if (month < 1 || month > 12) {
            return false; // Неверный месяц
        }

        return true;
    }

    // Валидация CVC
    validateCVC(cvc) {
        return /^\d{3,4}$/.test(cvc);
    }

    // Форматирование номера карты
    formatCardNumber(input) {
        let value = input.value.replace(/\D/g, '');
        value = value.replace(/(\d{4})/g, '$1 ').trim();
        input.value = value.substring(0, 19); // Максимум 16 цифр + 3 пробела
    }

    // Форматирование срока действия карты
    formatCardExpiry(input) {
        let value = input.value.replace(/\D/g, '');
        if (value.length >= 2) {
            value = value.substring(0, 2) + '/' + value.substring(2, 4);
        }
        input.value = value.substring(0, 5);
    }

    // Обновление метода оплаты
    updatePaymentMethod(method) {
        const cardForm = document.getElementById('cardForm');
        if (cardForm) {
            cardForm.style.display = method === 'card' ? 'block' : 'none';
        }
    }

    // Подтверждение заказа
    async confirmOrder() {
        const confirmBtn = document.getElementById('confirmOrderBtn');
        Utils.showLoading(confirmBtn);

        try {
            // Подготавливаем данные заказа
            const orderData = {
                deliveryAddress: `${this.orderData.deliveryAddress.address}, ${this.orderData.deliveryAddress.city}`,
                specialInstructions: this.orderData.specialInstructions
            };

            // Создаем заказ
            const orderResponse = await ApiClient.createOrder(orderData);

            if (orderResponse.success) {
                const orderId = orderResponse.data.id;

                // Обрабатываем оплату
                if (this.orderData.paymentMethod !== 'cash') {
                    const paymentData = {
                        orderId: orderId,
                        amount: this.cartData.total,
                        cardId: null // В реальном приложении здесь будет ID сохраненной карты
                    };

                    const paymentResponse = await ApiClient.processPayment(paymentData);

                    if (!paymentResponse.success) {
                        throw new Error('Ошибка оплаты: ' + paymentResponse.message);
                    }
                }

                // Показываем успешное сообщение
                this.showSuccessModal(orderResponse.data);

                // Очищаем корзину
                await ApiClient.clearCart().catch(console.error);

            } else {
                throw new Error(orderResponse.message);
            }

        } catch (error) {
            console.error('Order confirmation error:', error);
            Utils.showNotification(`Ошибка оформления заказа: ${error.message}`, 'error');

        } finally {
            Utils.hideLoading(confirmBtn);
        }
    }

    // Показ модального окна успеха
    showSuccessModal(orderData) {
        // Генерируем номер заказа
        const orderNumber = 'FD' + Date.now().toString().substring(5);

        // Устанавливаем данные в модальное окно
        document.getElementById('orderNumber').textContent = orderNumber;
        document.getElementById('modalOrderNumber').textContent = orderNumber;
        document.getElementById('modalOrderTotal').textContent = Utils.formatPrice(this.cartData.total);

        // Время доставки
        const now = new Date();
        const deliveryTime = new Date(now.getTime() + 45 * 60000);
        const timeString = deliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });
        document.getElementById('modalDeliveryTime').textContent = timeString;

        // Показываем модальное окно
        document.getElementById('successModal').style.display = 'flex';
    }
}

// Инициализация менеджера оформления заказа
const checkoutManager = new CheckoutManager();

// Глобальные функции для использования в HTML
function loadCheckoutData() {
    checkoutManager.loadCheckoutData();
}

function goToStep1() {
    checkoutManager.goToStep(1);
}

function goToStep2() {
    checkoutManager.goToStep(2);
}

function goToStep3() {
    checkoutManager.goToStep(3);
}

function updatePaymentMethod(method) {
    checkoutManager.updatePaymentMethod(method);
}

function formatCardNumber(input) {
    checkoutManager.formatCardNumber(input);
}

function formatCardExpiry(input) {
    checkoutManager.formatCardExpiry(input);
}

function confirmOrder() {
    checkoutManager.confirmOrder();
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        CheckoutManager,
        checkoutManager,
        loadCheckoutData,
        goToStep1,
        goToStep2,
        goToStep3,
        updatePaymentMethod,
        formatCardNumber,
        formatCardExpiry,
        confirmOrder
    };
}
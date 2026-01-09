// cards.js - Управление сохраненными картами
class CardsManager {
    constructor() {
        this.cards = [];
        this.defaultCard = null;
        this.initialized = false;
    }

    async loadUserCards() {
        try {
            // Проверяем, авторизован ли пользователь
            const token = Utils.getToken();
            if (!token) {
                console.log('Пользователь не авторизован для загрузки карт');
                return [];
            }
            
            const response = await ApiClient.getPaymentCards();
            
            if (response && response.success) {
                this.cards = response.data || [];
                
                // Находим карту по умолчанию
                this.defaultCard = this.cards.find(card => card.isActive) || null;
                
                this.initialized = true;
                console.log('Загружено карт:', this.cards.length);
                return this.cards;
            } else {
                console.log('API не вернул успешный ответ:', response);
                return [];
            }
            
        } catch (error) {
            console.error('Error loading user cards:', error);
            this.cards = [];
            return [];
        }
    }

    getCardIcon(cardType) {
        const icons = {
            'visa': 'fab fa-cc-visa',
            'mastercard': 'fab fa-cc-mastercard',
            'mir': 'fab fa-cc-mir',
            'amex': 'fab fa-cc-amex',
            'default': 'far fa-credit-card'
        };
        return icons[cardType?.toLowerCase()] || icons.default;
    }
}

// Логика оформления заказа
class CheckoutManager {
    constructor() {
        this.currentStep = 1;
        this.orderData = {
            deliveryAddress: {},
            paymentMethod: 'card',
            cardDetails: {},
            specialInstructions: '',
            selectedCardId: null
        };
        this.cartData = null;
        
        // Инициализируем менеджер карт
        this.cardsManager = new CardsManager();
    }

    async initPaymentCards() {
        try {
            // Загружаем сохраненные карты пользователя
            await this.cardsManager.loadUserCards();
            
            // Обновляем UI с картами
            this.updatePaymentUIWithSavedCards();
            
        } catch (error) {
            console.log('User has no saved cards or not logged in:', error);
            // Показываем только форму для новой карты
            this.showNewCardFormOnly();
        }
    }
    
    updatePaymentUIWithSavedCards() {
        const container = document.getElementById('savedCardsContainer');
        if (!container) return;
        
        if (this.cardsManager.cards.length === 0) {
            container.innerHTML = `
                <div class="no-saved-cards-message">
                    <i class="fas fa-credit-card"></i>
                    <p>У вас нет сохраненных карт</p>
                    <p>Добавьте карту, и она появится здесь в будущем</p>
                </div>
            `;
            
            // Показываем форму для новой карты
            const newCardForm = document.getElementById('newCardForm');
            const savedCardCvv = document.getElementById('savedCardCvv');
            if (newCardForm) newCardForm.style.display = 'block';
            if (savedCardCvv) savedCardCvv.style.display = 'none';
            
            return;
        }
        
        // Показываем список карт
        container.innerHTML = `
            <div class="saved-cards-section">
                <h4><i class="fas fa-credit-card"></i> Сохраненные карты</h4>
                <div class="saved-cards-list" id="savedCardsRadio">
                    ${this.cardsManager.cards.map(card => `
                        <div class="saved-card-option">
                            <label class="radio-label">
                                <input type="radio" name="savedCard" 
                                       value="${card.id}" 
                                       ${card.isActive ? 'checked' : ''}
                                       class="saved-card-radio">
                                <div class="card-option-content">
                                    <div class="card-option-icon">
                                        <i class="${this.cardsManager.getCardIcon(card.cardType)}"></i>
                                    </div>
                                    <div class="card-option-details">
                                        <h4>${card.cardHolderName || 'Владелец карты'}</h4>
                                        <p>Карта •••• ${card.cardLastFourDigits || '****'}</p>
                                        <p>Действует до: ${card.expiry || 'ММ/ГГ'}</p>
                                    </div>
                                    ${card.isActive ? 
                                        '<span class="default-badge-sm">Основная</span>' : ''}
                                </div>
                            </label>
                        </div>
                    `).join('')}
                    
                    <div class="saved-card-option">
                        <label class="radio-label">
                            <input type="radio" name="savedCard" 
                                   value="new" 
                                   class="saved-card-radio"
                                   ${!this.cardsManager.defaultCard ? 'checked' : ''}>
                            <div class="card-option-content">
                                <div class="card-option-icon">
                                    <i class="fas fa-plus-circle"></i>
                                </div>
                                <div class="card-option-details">
                                    <h4>Использовать новую карту</h4>
                                    <p>Ввести данные новой карты</p>
                                </div>
                            </div>
                        </label>
                    </div>
                </div>
            </div>
        `;
        
        // Добавляем обработчики событий для радиокнопок
        document.querySelectorAll('.saved-card-radio').forEach(radio => {
            radio.addEventListener('change', (e) => {
                this.handleCardSelection(e.target.value);
            });
        });
        
        // Устанавливаем начальное состояние
        const defaultCardId = this.cardsManager.defaultCard ? 
            this.cardsManager.defaultCard.id : 'new';
        this.handleCardSelection(defaultCardId);
    }
    
    showNewCardFormOnly() {
        const container = document.getElementById('savedCardsContainer');
        if (container) {
            container.innerHTML = `
                <div class="new-card-only-message">
                    <p>Введите данные карты для оплаты</p>
                </div>
            `;
        }
        
        const newCardForm = document.getElementById('newCardForm');
        const savedCardCvv = document.getElementById('savedCardCvv');
        const selectedCardInfo = document.getElementById('selectedCardInfo');
        
        if (newCardForm) newCardForm.style.display = 'block';
        if (savedCardCvv) savedCardCvv.style.display = 'none';
        if (selectedCardInfo) selectedCardInfo.style.display = 'none';
        
        this.orderData.selectedCardId = 'new';
    }
    
    handleCardSelection(cardId) {
    console.log('Selected card:', cardId);
    
    const newCardForm = document.getElementById('newCardForm');
    const savedCardCvv = document.getElementById('savedCardCvv');
    const selectedCardInfo = document.getElementById('selectedCardInfo');
    
    this.orderData.selectedCardId = cardId;
    
    // Сбрасываем состояние CVV при любом изменении выбора карты
    const savedCardCvvInput = document.getElementById('savedCardCvvInput');
    if (savedCardCvvInput) {
        savedCardCvvInput.value = '';
    }
    
    if (cardId === 'new') {
        // Показать форму для новой карты
        if (newCardForm) newCardForm.style.display = 'block';
        if (savedCardCvv) savedCardCvv.style.display = 'none';
        if (selectedCardInfo) selectedCardInfo.style.display = 'none';
        
        // Сбросить форму новой карты
        this.resetNewCardForm();
    } else {
        // Использовать сохраненную карту
        if (newCardForm) newCardForm.style.display = 'none';
        if (savedCardCvv) savedCardCvv.style.display = 'block';
        
        // Показать информацию о выбранной карте
        this.showSelectedCardInfo(cardId);
    }
}
    
    resetNewCardForm() {
        const fields = ['cardNumber', 'cardExpiry', 'cardCVC', 'cardHolder'];
        fields.forEach(fieldId => {
            const field = document.getElementById(fieldId);
            if (field) field.value = '';
        });
        
        const saveCard = document.getElementById('saveCard');
        if (saveCard) saveCard.checked = true;
    }
    
    showSelectedCardInfo(cardId) {
        const card = this.cardsManager.cards.find(c => c.id === cardId);
        if (!card) return;
        
        const selectedCardInfo = document.getElementById('selectedCardInfo');
        if (selectedCardInfo) {
            selectedCardInfo.innerHTML = `
                <div class="selected-card-info">
                    <div class="card-icon">
                        <i class="${this.cardsManager.getCardIcon(card.cardType)} fa-2x"></i>
                    </div>
                    <div class="card-details">
                        <p class="card-name"><strong>${card.cardHolderName || 'Владелец карты'}</strong></p>
                        <p class="card-number">Карта •••• ${card.cardLastFourDigits || '****'}</p>
                        <p class="card-expiry">Действует до: ${card.expiry || 'ММ/ГГ'}</p>
                    </div>
                </div>
            `;
            selectedCardInfo.style.display = 'block';
        }
    }

    async getCartCheckoutData() {
        try {
            const response = await ApiClient.getCart();
            if (response.success && response.data) {
                const cart = response.data.cartItems || [];
                const subtotal = cart.reduce((total, item) => {
                    const price = item.dish?.price || 0;
                    return total + (price * item.quantity);
                }, 0);
                
                return {
                    items: cart.map(item => ({
                        dishId: item.dishId,
                        dishName: item.dish?.name || 'Блюдо',
                        quantity: item.quantity,
                        price: item.dish?.price || 0,
                        total: (item.dish?.price || 0) * item.quantity,
                        imageUrl: item.dish?.imageUrl || 'https://via.placeholder.com/50x50?text=Dish'
                    })),
                    subtotal: subtotal,
                    delivery: subtotal >= 500 ? 0 : 150,
                    total: subtotal + (subtotal >= 500 ? 0 : 150)
                };
            }
        } catch (error) {
            console.error('Error getting cart data:', error);
        }
        return { items: [], subtotal: 0, delivery: 0, total: 0 };
    }

    async loadCheckoutData() {
        try {
            console.log('Loading checkout data...');
            
            const response = await ApiClient.getCart();
            console.log('Cart API response:', response);
            
            if (!response.success) {
                console.error('Failed to load cart:', response);
                Utils.showNotification('Не удалось загрузить корзину', 'error');
                setTimeout(() => window.location.href = 'cart.html', 2000);
                return;
            }
            
            const cartData = response.data;
            
            if (!cartData || !cartData.cartItems || cartData.cartItems.length === 0) {
                console.warn('Cart is empty or invalid');
                Utils.showNotification('Корзина пуста', 'error');
                setTimeout(() => window.location.href = 'cart.html', 2000);
                return;
            }
            
            this.cartData = this.prepareCheckoutData(cartData.cartItems);
            console.log('Cart data prepared:', this.cartData);
            
            if (!this.cartData || this.cartData.total === 0) {
                console.error('Cart data is invalid:', this.cartData);
                Utils.showNotification('Ошибка расчета суммы заказа', 'error');
                return;
            }
            
            this.renderOrderPreview();
            this.updateDeliveryTime();
            
            // Инициализируем карты оплаты
            this.initPaymentCards();
            
        } catch (error) {
            console.error('Error loading checkout data:', error);
            Utils.showNotification('Ошибка загрузки данных для оформления', 'error');
            setTimeout(() => window.location.href = 'cart.html', 2000);
        }
    }
    
    prepareCheckoutData(cartItems) {
        const subtotal = cartItems.reduce((total, item) => {
            const dish = item.dish || {};
            const price = dish.price || item.price || 0;
            const quantity = item.quantity || 1;
            return total + (price * quantity);
        }, 0);
        
        const deliveryCost = subtotal >= 500 ? 0 : 150;
        const total = subtotal + deliveryCost;
        
        return {
            items: cartItems.map(item => {
                const dish = item.dish || {};
                const price = dish.price || item.price || 0;
                const quantity = item.quantity || 1;
                
                return {
                    id: item.id,
                    dishId: item.dishId || dish.id,
                    dishName: dish.name || item.dishName || 'Блюдо',
                    quantity: quantity,
                    price: price,
                    total: price * quantity,
                    imageUrl: dish.imageUrl || item.imageUrl
                };
            }),
            subtotal: subtotal,
            delivery: deliveryCost,
            total: total,
            itemCount: cartItems.reduce((total, item) => total + (item.quantity || 1), 0)
        };
    }

    renderOrderPreview() {
        if (!this.cartData) {
            console.error('No cart data to render');
            return;
        }
        
        const previewContainer = document.getElementById('orderItemsPreview');
        if (previewContainer) {
            previewContainer.innerHTML = this.cartData.items.map(item => `
                <div class="preview-order-item">
                    <div class="preview-item-image">
                        <img src="${item.imageUrl}" 
                             alt="${item.dishName}"
                             onerror="this.src='https://via.placeholder.com/50x50?text=Dish'">
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
        }
        
        this.updateOrderSummary();
    }

    updateOrderSummary() {
        if (!this.cartData) return;
        
        const subtotalElement = document.getElementById('checkoutSubtotal');
        const deliveryElement = document.getElementById('checkoutDelivery');
        const totalElement = document.getElementById('checkoutTotal');
        
        if (subtotalElement) {
            subtotalElement.textContent = Utils.formatPrice(this.cartData.subtotal);
        }
        
        if (deliveryElement) {
            deliveryElement.textContent = this.cartData.delivery === 0 ? 'Бесплатно' : Utils.formatPrice(this.cartData.delivery);
        }
        
        if (totalElement) {
            totalElement.textContent = Utils.formatPrice(this.cartData.total);
        }
    }

    updateDeliveryTime() {
    if (!this.cartData || !this.cartData.items || this.cartData.items.length === 0) {
        document.getElementById('deliveryTime').textContent = '30-45 минут';
        return;
    }

    // Рассчитываем общее время
    // Базовое время доставки: 15-45 минут
    const baseDeliveryTime = 15 + Math.floor(Math.random() * 31); // 15-45 минут
    
    // Время приготовления (в реальном приложении получаем с сервера)
    // Для демо берем 20 минут на блюдо, максимум 40 минут
    const maxPrepTime = Math.min(this.cartData.items.length * 20, 40);
    
    // Общее время
    const totalTime = baseDeliveryTime + maxPrepTime;
    
    // Рассчитываем время доставки
    const now = new Date();
    const deliveryTime = new Date(now.getTime() + totalTime * 60000);
    
    // Форматируем время
    const timeString = deliveryTime.toLocaleTimeString('ru-RU', {
        hour: '2-digit',
        minute: '2-digit'
    });
    
    //document.getElementById('deliveryTime').textContent = `${timeString} (${totalTime} мин)`;
}

    goToStep(step) {
        if (step < 1 || step > 3) return;

        if (step > this.currentStep) {
            if (!this.validateCurrentStep()) {
                return;
            }
        }

        document.querySelectorAll('.checkout-step').forEach(el => {
            el.classList.remove('active');
        });

        const stepElement = document.getElementById(`step${step}`);
        if (stepElement) {
            stepElement.classList.add('active');
        }

        document.querySelectorAll('.step').forEach(el => {
            el.classList.remove('active');
        });

        for (let i = 1; i <= step; i++) {
            const stepEl = document.querySelector(`.step:nth-child(${i})`);
            if (stepEl) stepEl.classList.add('active');
        }

        this.currentStep = step;

        if (step === 3) {
            this.updateConfirmationSummary();
        }

        const currentStepElement = document.getElementById(`step${step}`);
        if (currentStepElement) {
            currentStepElement.scrollIntoView({ behavior: 'smooth' });
        }
    }

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

    validateAddressStep() {
        const address = document.getElementById('address')?.value.trim();
        const city = document.getElementById('city')?.value.trim();

        if (!address || !city) {
            Utils.showNotification('Заполните обязательные поля адреса', 'error');
            return false;
        }

        this.orderData.deliveryAddress = {
            address: address,
            city: city,
            entrance: document.getElementById('entrance')?.value.trim() || '',
            floor: document.getElementById('floor')?.value.trim() || '',
            intercom: document.getElementById('intercom')?.value.trim() || ''
        };

        this.orderData.specialInstructions = document.getElementById('deliveryInstructions')?.value.trim() || '';

        return true;
    }

    validatePaymentStep() {
        const paymentMethod = document.querySelector('.payment-method.active')?.dataset?.method || 'card';
        this.orderData.paymentMethod = paymentMethod;
        
        if (paymentMethod === 'card') {
            const selectedCardId = this.orderData.selectedCardId || 'new';
            
            if (selectedCardId === 'new') {
                // Валидация новой карты
                const cardNumber = document.getElementById('cardNumber')?.value.replace(/\s/g, '') || '';
                const cardExpiry = document.getElementById('cardExpiry')?.value || '';
                const cardCVC = document.getElementById('cardCVC')?.value || '';
                const cardHolder = document.getElementById('cardHolder')?.value.trim() || '';
                const saveCard = document.getElementById('saveCard')?.checked || false;
                
                // Валидация
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
                    saveCard: saveCard,
                    cardId: null
                };
                
            } else {
                // Валидация сохраненной карты
                const savedCardCvv = document.getElementById('savedCardCvvInput')?.value;
                if (!savedCardCvv || !this.validateCVC(savedCardCvv)) {
                    Utils.showNotification('Введите CVC код для сохраненной карты', 'error');
                    return false;
                }
                
                this.orderData.cardDetails = {
                    cvv: savedCardCvv,
                    cardId: selectedCardId
                };
            }
        }
        
        return true;
    }

    validateConfirmationStep() {
        const termsCheck = document.getElementById('termsCheck');
        if (termsCheck && !termsCheck.checked) {
            Utils.showNotification('Необходимо принять условия доставки', 'error');
            return false;
        }

        this.updateConfirmationSummary();
        return true;
    }

    updateConfirmationSummary() {
        const address = this.orderData.deliveryAddress;
        const addressElement = document.getElementById('summaryAddress');
        if (addressElement) {
            addressElement.textContent =
                `${address.address}, ${address.city}` + 
                (address.entrance ? `, подъезд ${address.entrance}` : '') +
                (address.floor ? `, этаж ${address.floor}` : '');
        }

        const paymentMethods = {
            'card': 'Банковская карта',
            'cash': 'Наличными при получении',
            'online': 'Онлайн-кошелек'
        };
        
        let paymentMethodText = paymentMethods[this.orderData.paymentMethod] || 'Не выбран';
        if (this.orderData.paymentMethod === 'card' && this.orderData.selectedCardId !== 'new') {
            const card = this.cardsManager.cards.find(c => c.id === this.orderData.selectedCardId);
            if (card) {
                paymentMethodText += ` (•••• ${card.cardLastFourDigits})`;
            }
        }
        
        const paymentElement = document.getElementById('summaryPayment');
        if (paymentElement) {
            paymentElement.textContent = paymentMethodText;
        }

        const itemsContainer = document.getElementById('orderItemsSummary');
        if (itemsContainer && this.cartData) {
            itemsContainer.innerHTML = this.cartData.items.map(item => `
                <div class="summary-order-item">
                    <span>${item.dishName} × ${item.quantity}</span>
                    <span>${Utils.formatPrice(item.total)}</span>
                </div>
            `).join('');
        }

        const totalElement = document.getElementById('summaryTotal');
        if (totalElement) {
            totalElement.textContent = Utils.formatPrice(this.cartData?.total || 0);
        }
    }

    validateCardNumber(cardNumber) {
    if (!cardNumber) return false;
    
    try {
        const cleanNumber = cardNumber.toString().replace(/\D/g, '');
        
        console.log('Validating card number:', cleanNumber);
        
        // Упрощенная валидация для демо
        // Принимаем любые номера определенной длины
        
        // Проверка длины
        if (cleanNumber.length < 13 || cleanNumber.length > 19) {
            console.log('Invalid length:', cleanNumber.length);
            return false;
        }
        
        // Проверка, что все символы цифры
        if (!/^\d+$/.test(cleanNumber)) {
            console.log('Contains non-digit characters');
            return false;
        }
        
        // Демо-валидация: принимаем карты, начинающиеся на популярные префиксы
        const firstDigit = cleanNumber.charAt(0);
        const firstTwoDigits = cleanNumber.substring(0, 2);
        
        const validPrefixes = [
            '4',    // Visa
            '51', '52', '53', '54', '55', '22', '23', '24', '25', '26', '27',  // MasterCard
            '34', '37',  // American Express
            '60', '62', '64', '65',  // Discover
            '35',  // JCB
            '30', '36', '38', '39'  // Diners Club
        ];
        
        const isValidPrefix = validPrefixes.some(prefix => 
            cleanNumber.startsWith(prefix)
        );
        
        if (!isValidPrefix) {
            console.log('Invalid prefix:', firstDigit, firstTwoDigits);
            // Но для демо все равно принимаем
            console.log('Demo mode: accepting anyway');
        }
        
        console.log('Card validation passed (demo mode)');
        return true;
        
    } catch (error) {
        console.error('Error validating card number:', error);
        return false;
    }
}

    validateCardExpiry(expiry) {
        if (!expiry) return false;
        
        try {
            const match = expiry.toString().match(/^(\d{2})\/(\d{2})$/);
            if (!match) return false;
            
            const month = parseInt(match[1], 10);
            const year = parseInt('20' + match[2], 10);
            
            const now = new Date();
            const currentYear = now.getFullYear();
            const currentMonth = now.getMonth() + 1;
            
            if (year < currentYear || (year === currentYear && month < currentMonth)) {
                return false;
            }
            
            if (month < 1 || month > 12) {
                return false;
            }
            
            if (year > currentYear + 10) {
                return false;
            }
            
            return true;
        } catch (error) {
            console.error('Error validating card expiry:', error);
            return false;
        }
    }

    validateCVC(cvc) {
        if (!cvc) return false;
        
        try {
            return /^\d{3,4}$/.test(cvc.toString());
        } catch (error) {
            console.error('Error validating CVC:', error);
            return false;
        }
    }

    formatCardNumber(inputElement) {
        if (!inputElement || !inputElement.value) return;
        
        try {
            let value = inputElement.value.replace(/\D/g, '');
            value = value.replace(/(\d{4})/g, '$1 ').trim();
            inputElement.value = value.substring(0, 19);
        } catch (error) {
            console.error('Error formatting card number:', error);
        }
    }

    formatCardExpiry(inputElement) {
        if (!inputElement || !inputElement.value) return;
        
        try {
            let value = inputElement.value.replace(/\D/g, '');
            if (value.length >= 2) {
                value = value.substring(0, 2) + '/' + value.substring(2, 4);
            }
            inputElement.value = value.substring(0, 5);
        } catch (error) {
            console.error('Error formatting card expiry:', error);
        }
    }

    updatePaymentMethod(method) {
    const cardForm = document.getElementById('cardForm');
    const savedCardsContainer = document.getElementById('savedCardsContainer');
    const savedCardCvv = document.getElementById('savedCardCvv');
    const selectedCardInfo = document.getElementById('selectedCardInfo');
    
    if (method === 'card') {
        // Показываем блоки, связанные с оплатой картой
        if (cardForm) cardForm.style.display = 'block';
        if (savedCardsContainer) savedCardsContainer.style.display = 'block';
        
        // Скрываем дополнительные блоки, пока не выбран способ оплаты
        if (savedCardCvv) savedCardCvv.style.display = 'none';
        if (selectedCardInfo) selectedCardInfo.style.display = 'none';
        
        // Если есть сохраненные карты, показываем выбор
        if (this.cardsManager.cards.length > 0) {
            this.updatePaymentUIWithSavedCards();
        } else {
            this.showNewCardFormOnly();
        }
    } else {
        // При выборе других способов оплаты скрываем все блоки карт
        if (cardForm) cardForm.style.display = 'none';
        if (savedCardsContainer) savedCardsContainer.style.display = 'none';
        if (savedCardCvv) savedCardCvv.style.display = 'none';
        if (selectedCardInfo) selectedCardInfo.style.display = 'none';
    }
}

    async confirmOrder() {
        const confirmBtn = document.getElementById('confirmOrderBtn');
        if (!confirmBtn) return;
        
        Utils.showLoading(confirmBtn);

        try {
            console.log('Starting order confirmation...');
            
            if (!this.orderData.deliveryAddress || !this.orderData.deliveryAddress.address) {
                throw new Error('Не заполнен адрес доставки');
            }

            if (!this.cartData || !this.cartData.items || this.cartData.items.length === 0) {
                throw new Error('Корзина пуста');
            }

            const orderData = {
                deliveryAddress: `${this.orderData.deliveryAddress.address}, ${this.orderData.deliveryAddress.city}`,
                specialInstructions: this.orderData.specialInstructions || ''
            };

            console.log('Sending order data to API:', orderData);

            const orderResponse = await ApiClient.createOrder(orderData);
            console.log('Order API response:', orderResponse);

            if (orderResponse.success) {
                const orderId = orderResponse.data.id;
                const orderAmount = this.cartData.total;
                
                console.log('Order created successfully. Order ID:', orderId);

                if (this.orderData.paymentMethod === 'card') {
                    await this.processCardPayment(orderId, orderAmount);
                }

                this.showSuccessModal(orderResponse.data);
                await ApiClient.clearCart().catch(console.error);

            } else {
                throw new Error(orderResponse.message || 'Неизвестная ошибка при создании заказа');
            }

        } catch (error) {
            console.error('Order confirmation error details:', error);
            Utils.showNotification(`Ошибка оформления заказа: ${error.message}`, 'error');

        } finally {
            Utils.hideLoading(confirmBtn);
        }
    }
    
    async processCardPayment(orderId, amount) {
    const selectedCardId = this.orderData.selectedCardId || 'new';
    
    if (selectedCardId === 'new') {
        // Оплата новой картой
        const cardData = this.orderData.cardDetails;
        
        // Сохраняем карту если нужно
        if (cardData.saveCard && cardData.number && cardData.holder) {
            await this.saveNewCard(cardData);
        }
        
        // Процесс оплаты
        const paymentResponse = await ApiClient.processPayment({
            orderId: orderId,
            amount: amount,
            cardId: null  // Новая карта, поэтому null
        });
        
        if (!paymentResponse.success) {
            throw new Error('Ошибка оплаты: ' + paymentResponse.message);
        }
        
    } else {
        // Оплата сохраненной картой
        const cardId = selectedCardId;
        
        // Используем существующий эндпоинт с cardId
        const paymentResponse = await ApiClient.processPayment({
            orderId: orderId,
            amount: amount,
            cardId: cardId
        });
        
        if (!paymentResponse.success) {
            throw new Error('Ошибка оплаты: ' + paymentResponse.message);
        }
    }
}
    
    async saveNewCard(cardData) {
    try {
        // Сначала проверяем, действительно ли пользователь хочет сохранить карту
        if (!cardData.saveCard) {
            console.log('User chose not to save the card');
            return;
        }
        
        const expiryParts = cardData.expiry.split('/');
        if (expiryParts.length !== 2) {
            console.error('Invalid expiry format:', cardData.expiry);
            return;
        }
        
        const expiryMonth = parseInt(expiryParts[0]);
        const expiryYear = parseInt('20' + expiryParts[1]);
        
        // Очищаем номер карты от пробелов
        const cleanCardNumber = cardData.number.replace(/\D/g, '');
        
        const cardToSave = {
            cardNumber: cleanCardNumber,
            cardHolderName: cardData.holder,
            expiryMonth: expiryMonth,
            expiryYear: expiryYear,
            cvv: cardData.cvc || '000', // Добавляем CVC (можно использовать заглушку)
            // Либо отправляем без CVC, если бэкенд позволяет
        };
        
        console.log('Sending card data to save...');
        const response = await ApiClient.addPaymentCard(cardToSave);
        
        if (response.success) {
            console.log('Card saved successfully');
            // Перезагружаем список карт
            await this.cardsManager.loadUserCards();
            Utils.showNotification('Карта сохранена для будущих покупок', 'success');
        } else {
            console.warn('Card not saved:', response.message);
            // Не показываем ошибку пользователю - это не критично
        }
    } catch (error) {
        console.error('Error saving card:', error);
        // Игнорируем ошибку сохранения карты - это не должно мешать оплате
    }
}
    
    detectCardType(cardNumber) {
        cardNumber = cardNumber.replace(/\D/g, '');
        
        if (/^4/.test(cardNumber)) return 'visa';
        if (/^5[1-5]/.test(cardNumber)) return 'mastercard';
        if (/^2/.test(cardNumber)) return 'mastercard';
        if (/^220[0-4]/.test(cardNumber)) return 'mir';
        if (/^6/.test(cardNumber)) return 'discover';
        if (/^3[47]/.test(cardNumber)) return 'amex';
        
        return 'unknown';
    }

    showSuccessModal(orderData) {
        const orderNumber = 'FD' + Date.now().toString().substring(5);
        
        const orderNumberElement = document.getElementById('orderNumber');
        const modalOrderNumberElement = document.getElementById('modalOrderNumber');
        const modalOrderTotalElement = document.getElementById('modalOrderTotal');
        
        if (orderNumberElement) orderNumberElement.textContent = orderNumber;
        if (modalOrderNumberElement) modalOrderNumberElement.textContent = orderNumber;
        if (modalOrderTotalElement) modalOrderTotalElement.textContent = Utils.formatPrice(this.cartData.total);

        const now = new Date();
        const deliveryTime = new Date(now.getTime() + 45 * 60000);
        const timeString = deliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });
        
        const modalDeliveryTimeElement = document.getElementById('modalDeliveryTime');
        if (modalDeliveryTimeElement) {
            modalDeliveryTimeElement.textContent = timeString;
        }

        const modal = document.getElementById('successModal');
        if (modal) {
            modal.style.display = 'flex';
        }
    }
}

// Создаем глобальный экземпляр
const checkoutManager = new CheckoutManager();

// Глобальные функции для использования в HTML
function loadCheckoutData() {
    if (checkoutManager) {
        checkoutManager.loadCheckoutData();
    }
}

function goToStep1() {
    if (checkoutManager) {
        checkoutManager.goToStep(1);
    }
}

function goToStep2() {
    if (checkoutManager) {
        checkoutManager.goToStep(2);
    }
}

function goToStep3() {
    if (checkoutManager) {
        checkoutManager.goToStep(3);
    }
}

function updatePaymentMethod(method) {
    if (checkoutManager) {
        checkoutManager.updatePaymentMethod(method);
    }
}

function formatCardNumber(input) {
    if (checkoutManager) {
        checkoutManager.formatCardNumber(input);
    }
}

function formatCardExpiry(input) {
    if (checkoutManager) {
        checkoutManager.formatCardExpiry(input);
    }
}

function confirmOrder() {
    if (checkoutManager) {
        checkoutManager.confirmOrder();
    }
}

function handleCardSelection(cardId) {
    if (checkoutManager) {
        checkoutManager.handleCardSelection(cardId);
    }
}
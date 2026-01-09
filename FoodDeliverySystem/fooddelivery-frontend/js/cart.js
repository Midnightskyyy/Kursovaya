// Логика работы с корзиной

class CartManager {
    constructor() {
        this.cart = [];
        this.cartTotal = 0;
        this.deliveryCost = 0;
        this.minimumOrder = 300;
    }

    // Загрузка корзины
    async loadCart() {
    try {
        // Блокируем повторные вызовы
        if (this.isLoading) return;
        this.isLoading = true;
        
        const response = await ApiClient.getCart();
        console.log('Cart API Response:', response);

        if (response.success && response.data) {
            // Очищаем текущую корзину
            this.cart = [];
            
            // Используем cartItems из data
            if (response.data.cartItems && Array.isArray(response.data.cartItems)) {
                this.cart = response.data.cartItems;
            } else if (Array.isArray(response.data)) {
                this.cart = response.data;
            }
            
            // Обновляем счетчик
            updateCartCountGlobal();
            
            // Обновляем отображение
            this.updateCartDisplay();
        } else {
            console.warn('Cart response not successful:', response);
            this.cart = [];
            updateCartCountGlobal();
            this.updateCartDisplay();
        }
    } catch (error) {
        console.error('Error loading cart:', error);
        this.cart = [];
        updateCartCountGlobal();
        this.updateCartDisplay();
    } finally {
        this.isLoading = false;
    }
}

    calculateDeliveryTime() {
    if (this.cart.length === 0) return 30; // базовое время

    // Находим блюдо с максимальным временем приготовления
    let maxPrepTime = 0;
    this.cart.forEach(item => {
        const prepTime = item.dish.preparationTime || 15;
        maxPrepTime = Math.max(maxPrepTime, prepTime);
    });

    // Базовое время доставки + время приготовления самого долгого блюда
    return maxPrepTime + 15; // 15 минут на доставку
    }

    // Обновление отображения корзины
    updateCartDisplay() {
        this.calculateTotals();
        this.renderCartItems();
        this.updateSummary();
        updateCartCount();
    }

    // Расчет итогов
    calculateTotals() {
    // Сумма товаров
    this.cartTotal = ApiClient.calculateCartTotal(this.cart);

    // Стоимость доставки (бесплатно от 500 руб)
    this.deliveryCost = this.cartTotal >= 500 ? 0 : 150;

    // Время доставки
    this.deliveryTime = this.calculateDeliveryTime();

    // Обновляем отображение времени доставки
    const deliveryTimeElement = document.getElementById('deliveryTime');
    if (deliveryTimeElement) {
        deliveryTimeElement.textContent = `${this.deliveryTime} минут`;
    }

    // Проверка минимального заказа
    if (this.cartTotal > 0 && this.cartTotal < this.minimumOrder) {
        this.showMinimumOrderWarning();
    }
    }

    // Отображение товаров в корзине
    renderCartItems() {
    const cartContent = document.getElementById('cartContent');
    const hasItems = this.cart && this.cart.length > 0;
    
    if (hasItems) {
        // Рендерим список товаров
        cartContent.innerHTML = `
            <div class="cart-items">
                ${this.cart.map(item => {
                    const dish = item.dish || {};
                    const imageUrl = dish.imageUrl || 'https://via.placeholder.com/100x100?text=Dish';
                    const price = dish.price || 0;
                    const totalPrice = price * item.quantity;
                    
                    return `
                        <div class="cart-item" data-id="${item.id}">
                            <div class="cart-item-image">
                                <img src="${imageUrl}" 
                                     alt="${dish.name || 'Блюдо'}"
                                     onerror="this.src='https://via.placeholder.com/100x100?text=Dish'">
                            </div>
                            <div class="cart-item-info">
                                <div class="cart-item-header">
                                    <div class="cart-item-title">
                                        <h4>${dish.name || 'Неизвестное блюдо'}</h4>
                                        <p class="cart-item-description">${dish.description || ''}</p>
                                    </div>
                                    <div class="cart-item-price">
                                        ${Utils.formatPrice(totalPrice)}
                                    </div>
                                </div>
                                
                                <div class="cart-item-actions">
                                    <div class="quantity-control">
                                        <button type="button" class="quantity-btn minus" data-item-id="${item.id}">
                                            <i class="fas fa-minus"></i>
                                        </button>
                                        <span class="quantity-value" id="quantity-${item.id}">${item.quantity}</span>
                                        <button type="button" class="quantity-btn plus" data-item-id="${item.id}">
                                            <i class="fas fa-plus"></i>
                                        </button>
                                    </div>
                                    <button type="button" class="remove-item-btn" data-item-id="${item.id}">
                                        <i class="fas fa-trash"></i> Удалить
                                    </button>
                                </div>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;
        
        this.addCartEventListeners();
        
        // Обновляем кнопку очистки
        const clearCartBtn = document.getElementById('clearCartBtn');
        if (clearCartBtn) {
            clearCartBtn.style.display = 'inline-flex';
        }
        
    } else {
        // Рендерим сообщение о пустой корзине
        cartContent.innerHTML = `
            <div class="empty-cart">
                <i class="fas fa-shopping-cart fa-3x"></i>
                <h3>Корзина пуста</h3>
                <p>Добавьте товары из меню</p>
                <a href="menu.html" class="btn btn-primary">Перейти в меню</a>
            </div>
        `;
        
        // Скрываем кнопку очистки
        const clearCartBtn = document.getElementById('clearCartBtn');
        if (clearCartBtn) {
            clearCartBtn.style.display = 'none';
        }
    }
}
    // Обновление итоговой информации
updateSummary() {
    try {
        const subtotal = document.getElementById('subtotal');
        const deliveryCost = document.getElementById('deliveryCost');
        const totalAmount = document.getElementById('totalAmount');

        if (subtotal) subtotal.textContent = Utils.formatPrice(this.cartTotal);
        if (deliveryCost) {
            deliveryCost.textContent = this.deliveryCost === 0 ? 'Бесплатно' : Utils.formatPrice(this.deliveryCost);
            deliveryCost.style.color = this.deliveryCost === 0 ? '#00b894' : '#636e72';
        }
        if (totalAmount) {
            totalAmount.textContent = Utils.formatPrice(this.cartTotal + this.deliveryCost);
        }

        // Активируем/деактивируем кнопку оформления
        const checkoutBtn = document.getElementById('checkoutBtn');
        if (checkoutBtn) {
            const canCheckout = this.cartTotal >= this.minimumOrder && this.cart.length > 0;
            checkoutBtn.disabled = !canCheckout;
            checkoutBtn.style.opacity = canCheckout ? '1' : '0.5';
            checkoutBtn.style.cursor = canCheckout ? 'pointer' : 'not-allowed';
        }
    } catch (error) {
        console.error('Error updating summary:', error);
    }
    }

    // Добавление обработчиков событий
    addCartEventListeners() {
    // Используем делегирование и флаг для предотвращения множественных кликов
    document.addEventListener('click', async (e) => {
        // Проверяем, не обрабатываем ли мы уже клик
        if (window.isProcessingCartClick) return;
        
        const target = e.target;
        
        // Кнопка "минус"
        if (target.closest('.quantity-btn.minus')) {
            window.isProcessingCartClick = true;
            const button = target.closest('.quantity-btn.minus');
            const itemId = button.dataset.itemId;
            const item = this.cart.find(i => i.id === itemId);
            
            if (item) {
                if (item.quantity > 1) {
                    await this.updateQuantity(itemId, item.quantity - 1);
                } else {
                    await this.removeItem(itemId);
                }
            }
            window.isProcessingCartClick = false;
        }
        
        // Кнопка "плюс"
        if (target.closest('.quantity-btn.plus')) {
            window.isProcessingCartClick = true;
            const button = target.closest('.quantity-btn.plus');
            const itemId = button.dataset.itemId;
            const item = this.cart.find(i => i.id === itemId);
            
            if (item) {
                await this.updateQuantity(itemId, item.quantity + 1);
            }
            window.isProcessingCartClick = false;
        }
        
        // Кнопка удаления
        if (target.closest('.remove-item-btn')) {
            window.isProcessingCartClick = true;
            const button = target.closest('.remove-item-btn');
            const itemId = button.dataset.itemId;
            await this.removeItem(itemId);
            window.isProcessingCartClick = false;
        }
    });
}

    // Обновление количества товара
    async updateQuantity(itemId, newQuantity) {
    try {
        // Находим товар в корзине
        const itemIndex = this.cart.findIndex(i => i.id === itemId);
        if (itemIndex === -1) {
            console.error('Item not found in cart:', itemId);
            await this.loadCart(); // Перезагружаем корзину
            return;
        }
        
        // Обновляем локально для мгновенной обратной связи
        const oldQuantity = this.cart[itemIndex].quantity;
        this.cart[itemIndex].quantity = newQuantity;
        
        // Обновляем отображение
        const quantityElement = document.getElementById(`quantity-${itemId}`);
        if (quantityElement) {
            quantityElement.textContent = newQuantity;
        }
        
        // Пересчитываем итоги
        this.calculateTotals();
        this.updateSummary();
        updateCartCountGlobal();
        
        // Отправляем обновление на сервер
        await ApiClient.updateCartItem(itemId, newQuantity);
        
        Utils.showNotification('Количество обновлено', 'success');
        
    } catch (error) {
        console.error('Error updating cart item:', error);
        
        // Если ошибка, перезагружаем корзину с сервера
        await this.loadCart();
        
        Utils.showNotification('Ошибка обновления количества', 'error');
    }
}

    // Удаление товара
    async removeItem(itemId) {
        if (!confirm('Удалить товар из корзины?')) return;

        try {
            await ApiClient.removeFromCart(itemId);
            await this.loadCart();
            Utils.showNotification('Товар удален из корзины', 'success');
        } catch (error) {
            console.error('Error removing from cart:', error);

            // Fallback на локальную корзину
            ApiClient.removeFromLocalCart(itemId);
            await this.loadCart();
            Utils.showNotification('Товар удален из корзины', 'success');
        }
    }

    // Очистка корзины
    async clearCart() {
        if (!confirm('Очистить всю корзину?')) return;

        try {
            await ApiClient.clearCart();
            await this.loadCart();
            Utils.showNotification('Корзина очищена', 'success');
        } catch (error) {
            console.error('Error clearing cart:', error);

            // Fallback на локальную корзину
            ApiClient.clearLocalCart();
            await this.loadCart();
            Utils.showNotification('Корзина очищена', 'success');
        }
    }

    // Применение промокода
    applyPromoCode() {
        const promoInput = document.getElementById('promoCode');
        const promoCode = promoInput.value.trim();

        if (!promoCode) {
            Utils.showNotification('Введите промокод', 'error');
            return;
        }


        if (validPromoCodes[promoCode]) {
            const discount = validPromoCodes[promoCode];

            if (discount === 'free-delivery') {
                this.deliveryCost = 0;
                Utils.showNotification('Бесплатная доставка активирована!', 'success');
            } else if (typeof discount === 'number') {
                if (discount < 1) {
                    // Процентная скидка
                    const discountAmount = this.cartTotal * discount;
                    this.cartTotal -= discountAmount;
                    Utils.showNotification(`Скидка ${discount * 100}% применена!`, 'success');
                } else {
                    // Фиксированная скидка
                    this.cartTotal = Math.max(0, this.cartTotal - discount);
                    Utils.showNotification(`Скидка ${Utils.formatPrice(discount)} применена!`, 'success');
                }
            }

            this.updateSummary();
            promoInput.value = '';
            promoInput.disabled = true;

            // Сохраняем примененный промокод
            localStorage.setItem('appliedPromo', promoCode);

        } else {
            Utils.showNotification('Недействительный промокод', 'error');
        }
    }

    // Предупреждение о минимальном заказе
    showMinimumOrderWarning() {
        const remaining = this.minimumOrder - this.cartTotal;
        Utils.showNotification(
            `Добавьте еще товаров на ${Utils.formatPrice(remaining)} для оформления заказа`,
            'error'
        );
    }

    // Получение данных корзины для оформления заказа
    getCheckoutData() {
        return {
            items: this.cart.map(item => ({
                dishId: item.dishId,
                dishName: item.dish.name,
                quantity: item.quantity,
                price: item.dish.price,
                total: item.dish.price * item.quantity
            })),
            subtotal: this.cartTotal,
            delivery: this.deliveryCost,
            total: this.cartTotal + this.deliveryCost,
            restaurantId: this.cart[0]?.dish?.restaurantId
        };
    }
}

// Инициализация менеджера корзины
const cartManager = new CartManager();

// Глобальные функции для использования в HTML
function loadCart() {
    cartManager.loadCart();
}

function clearCart() {
    cartManager.clearCart();
}

function applyPromoCode() {
    cartManager.applyPromoCode();
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        CartManager,
        cartManager,
        loadCart,
        clearCart,
        applyPromoCode
    };
}
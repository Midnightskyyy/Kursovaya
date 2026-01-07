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
            const response = await ApiClient.getCart();

            if (response.success) {
                this.cart = response.data?.cartItems || [];
                this.cart.forEach(item => {
                    // Добавляем информацию о блюде, если её нет
                    if (item.dish && !item.dish.imageUrl) {
                        item.dish.imageUrl = 'images/dishes/default.jpg';
                    }
                });
                this.updateCartDisplay();
            } else {
                throw new Error(response.message);
            }
        } catch (error) {
            console.error('Error loading cart:', error);

            // Fallback на локальную корзину
            this.cart = ApiClient.getLocalCart();
            this.updateCartDisplay();
        }
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

        // Проверка минимального заказа
        if (this.cartTotal > 0 && this.cartTotal < this.minimumOrder) {
            this.showMinimumOrderWarning();
        }
    }

    // Отображение товаров в корзине
    renderCartItems() {
        const container = document.getElementById('cartItems');
        if (!container) return;

        if (this.cart.length === 0) {
            document.getElementById('emptyCart').style.display = 'block';
            container.innerHTML = '';
            return;
        }

        document.getElementById('emptyCart').style.display = 'none';

        container.innerHTML = this.cart.map(item => `
            <div class="cart-item" data-id="${item.id}">
                <div class="cart-item-image">
                    <img src="${item.dish.imageUrl || 'images/dishes/default.jpg'}" 
                         alt="${item.dish.name}"
                         onerror="this.src='images/dishes/default.jpg'">
                </div>
                <div class="cart-item-info">
                    <div class="cart-item-header">
                        <div class="cart-item-title">
                            <h4>${item.dish.name}</h4>
                            <p class="cart-item-description">${item.dish.description || ''}</p>
                        </div>
                        <div class="cart-item-price">
                            ${Utils.formatPrice(item.dish.price * item.quantity)}
                        </div>
                    </div>
                    
                    <div class="cart-item-actions">
                        <div class="quantity-control">
                            <button class="quantity-btn minus">
                                <i class="fas fa-minus"></i>
                            </button>
                            <span class="quantity-value">${item.quantity}</span>
                            <button class="quantity-btn plus">
                                <i class="fas fa-plus"></i>
                            </button>
                        </div>
                        <button class="remove-item-btn">
                            <i class="fas fa-trash"></i> Удалить
                        </button>
                    </div>
                </div>
            </div>
        `).join('');

        // Добавляем обработчики
        this.addCartEventListeners();
    }

    // Обновление итоговой информации
    updateSummary() {
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
    }

    // Добавление обработчиков событий
    addCartEventListeners() {
        // Кнопки изменения количества
        document.querySelectorAll('.quantity-btn.minus').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const itemId = e.target.closest('.cart-item').dataset.id;
                const item = this.cart.find(i => i.id === itemId);
                if (item && item.quantity > 1) {
                    await this.updateQuantity(itemId, item.quantity - 1);
                }
            });
        });

        document.querySelectorAll('.quantity-btn.plus').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const itemId = e.target.closest('.cart-item').dataset.id;
                const item = this.cart.find(i => i.id === itemId);
                if (item) {
                    await this.updateQuantity(itemId, item.quantity + 1);
                }
            });
        });

        // Кнопки удаления
        document.querySelectorAll('.remove-item-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const itemId = e.target.closest('.cart-item').dataset.id;
                await this.removeItem(itemId);
            });
        });
    }

    // Обновление количества товара
    async updateQuantity(itemId, newQuantity) {
        try {
            await ApiClient.updateCartItem(itemId, newQuantity);
            await this.loadCart();
            Utils.showNotification('Корзина обновлена', 'success');
        } catch (error) {
            console.error('Error updating cart item:', error);

            // Fallback на локальную корзину
            ApiClient.updateLocalCartItem(itemId, newQuantity);
            await this.loadCart();
            Utils.showNotification('Корзина обновлена', 'success');
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

        // Заглушка для проверки промокодов
        const validPromoCodes = {
            'WELCOME10': 0.1,   // 10% скидка
            'FREE150': 150,     // 150 руб скидки
            'DELIVERYFREE': 'free-delivery' // Бесплатная доставка
        };

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
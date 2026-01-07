// API ������ ��� �������������� � ��������

class ApiClient {
    static BASE_URL = 'http://localhost:5000/api';

    // ����� ����� ��� ���������� ��������
    static async request(endpoint, options = {}) {
        const url = `${this.BASE_URL}${endpoint}`;
        const token = Utils.getToken();

        const defaultHeaders = {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
        };

        if (token) {
            defaultHeaders['Authorization'] = `Bearer ${token}`;
        }

        const config = {
            ...options,
            headers: {
                ...defaultHeaders,
                ...options.headers
            }
        };

        try {
            const response = await fetch(url, config);

            // ���� ������ 401 (Unauthorized), �������������� �� �������� �����������
            if (response.status === 401) {
                Utils.clearAuth();
                window.location.href = 'auth.html';
                throw new Error('��������� �����������');
            }

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.message || `HTTP error! status: ${response.status}`);
            }

            return data;
        } catch (error) {
            console.error('API Error:', error);
            throw error;
        }
    }

    // Auth API
    static async login(email, password) {
        return this.request('/auth/login', {
            method: 'POST',
            body: JSON.stringify({ email, password })
        });
    }

    static async register(userData) {
        return this.request('/auth/register', {
            method: 'POST',
            body: JSON.stringify(userData)
        });
    }

    static async getProfile() {
        const user = Utils.getUser();
        if (!user || !user.id) {
            throw new Error('User not found in local storage');
        }
        return this.request(`/auth/profile/${user.id}`);
    }

    static async logout() {
        return this.request('/auth/logout', {
            method: 'POST'
        });
    }

    // Menu API
    static async getRestaurants() {
        return this.request('/menu/restaurants');
    }

    static async getRestaurantDishes(restaurantId) {
        return this.request(`/menu/restaurants/${restaurantId}/dishes`);
    }

    // Cart API
    static async getCart() {
        return this.request('/cart');
    }

    static async addToCart(dishId, quantity = 1) {
        return this.request('/cart/items', {
            method: 'POST',
            body: JSON.stringify({ dishId, quantity })
        });
    }

    static async updateCartItem(itemId, quantity) {
        return this.request(`/cart/items/${itemId}`, {
            method: 'PUT',
            body: JSON.stringify({ quantity })
        });
    }

    static async removeFromCart(itemId) {
        return this.request(`/cart/items/${itemId}`, {
            method: 'DELETE'
        });
    }

    static async clearCart() {
        const cart = await this.getCart();
        if (cart.data && cart.data.cartItems) {
            const promises = cart.data.cartItems.map(item =>
                this.removeFromCart(item.id)
            );
            await Promise.all(promises);
        }
    }

    // Orders API
    static async createOrder(orderData) {
        return this.request('/orders', {
            method: 'POST',
            body: JSON.stringify(orderData)
        });
    }

    static async getOrders() {
        return this.request('/orders');
    }

    static async getOrder(orderId) {
        return this.request(`/orders/${orderId}`);
    }

    static async cancelOrder(orderId) {
        return this.request(`/orders/${orderId}/cancel`, {
            method: 'POST'
        });
    }

    // Payment API
    static async getPaymentCards() {
        return this.request('/payment/cards');
    }

    static async addPaymentCard(cardData) {
        return this.request('/payment/cards', {
            method: 'POST',
            body: JSON.stringify(cardData)
        });
    }

    static async processPayment(paymentData) {
        return this.request('/payment/pay', {
            method: 'POST',
            body: JSON.stringify(paymentData)
        });
    }

    static async getTransactions() {
        return this.request('/payment/transactions');
    }

    // Delivery API
    static async getDeliveryStatus(orderId) {
        return this.request(`/delivery/order/${orderId}`);
    }

    static async trackDelivery(deliveryId) {
        return this.request(`/delivery/track/${deliveryId}`);
    }

    static async getActiveDeliveries() {
        return this.request('/delivery/active');
    }

    // ��������������� ������ ��� ������ � ��������� ���������� (�� ���������� ��������� API)
    static getLocalCart() {
        return JSON.parse(localStorage.getItem('cart') || '[]');
    }

    static saveLocalCart(cart) {
        localStorage.setItem('cart', JSON.stringify(cart));
    }

    static addToLocalCart(dish, quantity = 1) {
        const cart = this.getLocalCart();
        const existingItem = cart.find(item => item.dishId === dish.id);

        if (existingItem) {
            existingItem.quantity += quantity;
        } else {
            cart.push({
                id: Utils.generateId(),
                dishId: dish.id,
                dish: dish,
                quantity: quantity,
                addedAt: new Date().toISOString()
            });
        }

        this.saveLocalCart(cart);
        updateCartCount();
        return cart;
    }

    static updateLocalCartItem(itemId, quantity) {
        const cart = this.getLocalCart();
        const itemIndex = cart.findIndex(item => item.id === itemId);

        if (itemIndex > -1) {
            if (quantity <= 0) {
                cart.splice(itemIndex, 1);
            } else {
                cart[itemIndex].quantity = quantity;
            }
        }

        this.saveLocalCart(cart);
        updateCartCount();
        return cart;
    }

    static removeFromLocalCart(itemId) {
        const cart = this.getLocalCart();
        const filteredCart = cart.filter(item => item.id !== itemId);
        this.saveLocalCart(filteredCart);
        updateCartCount();
        return filteredCart;
    }

    static clearLocalCart() {
        localStorage.removeItem('cart');
        updateCartCount();
        return [];
    }

    static calculateCartTotal(cart) {
        return cart.reduce((total, item) => {
            return total + (item.dish.price * item.quantity);
        }, 0);
    }
}

// ������� ��� ������������� � ������ ������
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ApiClient;
}
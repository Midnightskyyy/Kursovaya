// Утилитарные функции для работы приложения

class Utils {
    static API_BASE_URL = 'http://localhost:5000/api';

    // Форматирование цены
    static formatPrice(price) {
        return new Intl.NumberFormat('ru-RU', {
            style: 'currency',
            currency: 'RUB',
            minimumFractionDigits: 0
        }).format(price);
    }

    // Безопасный поиск элемента
    static safeQuerySelector(selector) {
        try {
            return document.querySelector(selector);
        } catch (error) {
            console.warn(`Cannot find element with selector: ${selector}`, error);
            return null;
        }
    }

    // Безопасное обновление элемента
    static safeUpdateElement(id, value, defaultValue = '') {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value !== undefined && value !== null ? value : defaultValue;
        } else {
            console.warn(`Element #${id} not found`);
        }
    }

    // Форматирование даты
    static formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('ru-RU', {
            day: 'numeric',
            month: 'long',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    // Получение токена из localStorage
    static getToken() {
        return localStorage.getItem('token');
    }

    // Получение данных пользователя
    static getUser() {
        const user = localStorage.getItem('user');
        return user ? JSON.parse(user) : null;
    }

    // Сохранение данных пользователя
    static saveUser(user) {
        localStorage.setItem('user', JSON.stringify(user));
    }

    // Сохранение токена
    static saveToken(token) {
        localStorage.setItem('token', token);
    }

    // Очистка данных авторизации
    static clearAuth() {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('cart');
    }

    // Проверка авторизации
    static checkAuth() {
        const token = this.getToken();
        if (!token) {
            window.location.href = 'auth.html';
            return false;
        }
        return true;
    }

    // Валидация email
    static isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    // Валидация номера телефона (базовая)
    static isValidPhone(phone) {
        const re = /^[\+]?[0-9\s\-\(\)]+$/;
        return re.test(phone);
    }

    // Показ ошибки
    static showError(elementId, message) {
        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = message;
            element.style.display = 'block';
        }
    }

    // Очистка ошибки
    static clearError(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = '';
            element.style.display = 'none';
        }
    }

    // Показ индикатора загрузки
    static showLoading(button) {
        const text = button.querySelector('.btn-text') || button;
        const spinner = button.querySelector('.spinner');

        if (text) text.style.display = 'none';
        if (spinner) spinner.classList.remove('hidden');
        button.disabled = true;
    }

    // Скрытие индикатора загрузки
    static hideLoading(button) {
        const text = button.querySelector('.btn-text') || button;
        const spinner = button.querySelector('.spinner');

        if (text) text.style.display = 'inline';
        if (spinner) spinner.classList.add('hidden');
        button.disabled = false;
    }

    // Показать уведомление
    static showNotification(message, type = 'success') {
        // Создаем элемент уведомления
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.innerHTML = `
            <div class="notification-content">
                <i class="fas fa-${type === 'success' ? 'check-circle' : 'exclamation-circle'}"></i>
                <span>${message}</span>
            </div>
            <button class="notification-close">&times;</button>
        `;

        // Стили уведомления
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${type === 'success' ? '#00b894' : '#e17055'};
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            display: flex;
            align-items: center;
            justify-content: space-between;
            min-width: 300px;
            max-width: 400px;
            z-index: 9999;
            animation: slideIn 0.3s ease-out;
        `;

        const contentStyle = `
            display: flex;
            align-items: center;
            gap: 10px;
            flex: 1;
        `;

        notification.querySelector('.notification-content').style.cssText = contentStyle;

        // Стили кнопки закрытия
        notification.querySelector('.notification-close').style.cssText = `
            background: none;
            border: none;
            color: white;
            font-size: 1.5rem;
            cursor: pointer;
            margin-left: 10px;
        `;

        // Добавляем анимации
        const style = document.createElement('style');
        style.textContent = `
            @keyframes slideIn {
                from {
                    transform: translateX(100%);
                    opacity: 0;
                }
                to {
                    transform: translateX(0);
                    opacity: 1;
                }
            }
            @keyframes slideOut {
                from {
                    transform: translateX(0);
                    opacity: 1;
                }
                to {
                    transform: translateX(100%);
                    opacity: 0;
                }
            }
        `;
        document.head.appendChild(style);

        // Добавляем в DOM
        document.body.appendChild(notification);

        // Обработчик закрытия
        notification.querySelector('.notification-close').addEventListener('click', () => {
            notification.style.animation = 'slideOut 0.3s ease-out forwards';
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        });

        // Автоматическое закрытие через 5 секунд
        setTimeout(() => {
            if (notification.parentNode) {
                notification.style.animation = 'slideOut 0.3s ease-out forwards';
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.parentNode.removeChild(notification);
                    }
                }, 300);
            }
        }, 5000);
    }

    // Валидация обязательных полей
    static validateRequiredFields(fields) {
        for (const field of fields) {
            if (!field.value.trim()) {
                return {
                    isValid: false,
                    field: field.name,
                    message: 'Это поле обязательно для заполнения'
                };
            }
        }
        return { isValid: true };
    }

    // Получение параметров URL
    static getUrlParams() {
        const params = {};
        const queryString = window.location.search.substring(1);
        const pairs = queryString.split('&');

        for (const pair of pairs) {
            const [key, value] = pair.split('=');
            if (key) {
                params[decodeURIComponent(key)] = decodeURIComponent(value || '');
            }
        }

        return params;
    }

    // Добавьте эту функцию в класс Utils
static parseDate(dateStr) {
    if (!dateStr) return new Date();
    
    // Пробуем разные форматы
    try {
        // PostgreSQL формат: "2026-01-09 22:08:20.918505+03"
        if (dateStr.includes('+')) {
            const cleanDateStr = dateStr.replace(' ', 'T');
            // Преобразуем +03 в +03:00
            const timezoneMatch = cleanDateStr.match(/(\+|\-)(\d{2})(?::?)(\d{2})?$/);
            if (timezoneMatch) {
                const tz = timezoneMatch[0];
                if (tz.length === 3) { // +03
                    dateStr = cleanDateStr.replace(tz, tz + ':00');
                } else {
                    dateStr = cleanDateStr;
                }
            }
            return new Date(dateStr);
        }
        
        // Если есть пробел, заменяем на T
        if (dateStr.includes(' ')) {
            return new Date(dateStr.replace(' ', 'T'));
        }
        
        return new Date(dateStr);
    } catch (error) {
        console.error('Error parsing date:', dateStr, error);
        return new Date();
    }
}

    // Генерация уникального ID
    static generateId() {
        return Date.now().toString(36) + Math.random().toString(36).substr(2);
    }

    // Дополнительные утилиты для работы с корзиной
    static getCartFromLocalStorage() {
        try {
            const cartData = localStorage.getItem('cart');
            return cartData ? JSON.parse(cartData) : [];
        } catch (error) {
            console.error('Error parsing cart from localStorage:', error);
            return [];
        }
    }

    static saveCartToLocalStorage(cart) {
        try {
            localStorage.setItem('cart', JSON.stringify(cart));
        } catch (error) {
            console.error('Error saving cart to localStorage:', error);
        }
    }

    // Проверка роли пользователя
    static hasRole(requiredRole) {
        const user = this.getUser();
        return user && user.role === requiredRole;
    }

    // Проверка, является ли пользователь администратором
    static isAdmin() {
        return this.hasRole('Admin');
    }

    // Форматирование номера телефона
    static formatPhoneNumber(phone) {
        if (!phone) return '';
        // Удаляем все нецифровые символы
        const cleaned = phone.replace(/\D/g, '');
        
        // Форматируем в зависимости от длины
        if (cleaned.length === 11) {
            return `+${cleaned[0]} (${cleaned.substring(1, 4)}) ${cleaned.substring(4, 7)}-${cleaned.substring(7, 9)}-${cleaned.substring(9)}`;
        } else if (cleaned.length === 10) {
            return `+7 (${cleaned.substring(0, 3)}) ${cleaned.substring(3, 6)}-${cleaned.substring(6, 8)}-${cleaned.substring(8)}`;
        }
        
        return phone;
    }
}

// Глобальные функции для использования в HTML

// Проверка авторизации
function checkAuth() {
    if (!Utils.checkAuth()) {
        return false;
    }
    return true;
}

// Выход из системы
function logout() {
    Utils.clearAuth();
    window.location.href = 'index.html';
}

// Обновление счетчика корзины
function updateCartCount() {
    try {
        let totalCount = 0;
        
        // Используем данные из CartManager если доступны и это страница корзины
        if (window.location.pathname.includes('cart.html') && 
            typeof cartManager !== 'undefined' && 
            cartManager.cart && 
            Array.isArray(cartManager.cart)) {
            
            totalCount = cartManager.cart.reduce((total, item) => {
                return total + (parseInt(item.quantity) || 0);
            }, 0);
            
        } else {
            // Для всех других страниц используем API или localStorage
            // Пробуем получить актуальные данные через API
            if (Utils.getToken()) {
                // Асинхронно получаем корзину с сервера
                if (typeof ApiClient !== 'undefined') {
                    ApiClient.getCart().then(response => {
                        if (response.success && response.data) {
                            let count = 0;
                            
                            if (response.data.cartItems && Array.isArray(response.data.cartItems)) {
                                count = response.data.cartItems.reduce((total, item) => {
                                    return total + (parseInt(item.quantity) || 0);
                                }, 0);
                            } else if (response.data.itemCount) {
                                count = response.data.itemCount;
                            }
                            
                            // Обновляем счетчик на всех элементах
                            updateCartCountElements(count);
                        }
                    }).catch(() => {
                        // Fallback на localStorage
                        updateCartCountFromLocalStorage();
                    });
                } else {
                    updateCartCountFromLocalStorage();
                }
            } else {
                // Если пользователь не авторизован, используем localStorage
                updateCartCountFromLocalStorage();
            }
            return; // Выходим из функции, т.к. обновление будет асинхронным
        }
        
        // Обновляем элементы
        updateCartCountElements(totalCount);
        
        return totalCount;
        
    } catch (error) {
        console.error('Error updating cart count:', error);
        updateCartCountElements(0);
        return 0;
    }
}



// Обновление элементов счетчика
function updateCartCountElements(count) {
    // Обновляем все счетчики на странице
    document.querySelectorAll('.cart-count, #mobileCartCount, #cartCounter, .cart-counter').forEach(element => {
        element.textContent = count;
        
        // Скрываем счетчик, если корзина пуста
        if (count === 0 && element.classList.contains('hide-when-empty')) {
            element.style.display = 'none';
        } else {
            element.style.display = 'inline-flex';
        }
    });
    
    // Обновляем значок в заголовке браузера (опционально)
    if (count > 0) {
        document.title = document.title.replace(/^\(\d+\)\s*/, '') + (count > 0 ? ` (${count})` : '');
    }
}

// Обновление счетчика из localStorage
function updateCartCountFromLocalStorage() {
    try {
        const cartData = localStorage.getItem('cart');
        let count = 0;
        
        if (cartData) {
            const cart = JSON.parse(cartData);
            if (Array.isArray(cart)) {
                count = cart.reduce((total, item) => {
                    return total + (parseInt(item.quantity) || 0);
                }, 0);
            }
        }
        
        updateCartCountElements(count);
        return count;
    } catch (error) {
        console.error('Error updating cart count from localStorage:', error);
        updateCartCountElements(0);
        return 0;
    }
}

// Глобальное обновление счетчика корзины
function updateCartCountGlobal(count) {
    if (count !== undefined) {
        updateCartCountElements(count);
    } else {
        updateCartCount();
    }
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function() {
    // Проверяем, существует ли cartManager
    setTimeout(() => {
        updateCartCount();
    }, 500); // Небольшая задержка для инициализации
        
    // Также обновляем при изменении localStorage
    window.addEventListener('storage', function(e) {
        if (e.key === 'cart') {
            updateCartCount();
        }
    });
});

// Экспорт функций для использования в других модулях
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        Utils,
        checkAuth,
        logout,
        updateCartCount,
        updateCartCountGlobal,
        updateCartCountElements,
        updateCartCountFromLocalStorage
    };
}

// Альтернативный экспорт для совместимости
if (typeof window !== 'undefined') {
    window.Utils = Utils;
    window.checkAuth = checkAuth;
    window.logout = logout;
    window.updateCartCount = updateCartCount;
    window.updateCartCountGlobal = updateCartCountGlobal;
}
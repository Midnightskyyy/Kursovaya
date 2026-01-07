// Базовые утилиты для работы приложения

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

    // Проверка валидности email
    static isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    // Проверка валидности телефона (простая)
    static isValidPhone(phone) {
        const re = /^[\+]?[0-9\s\-\(\)]+$/;
        return re.test(phone);
    }

    // Отображение ошибки
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

    // Показать спиннер загрузки
    static showLoading(button) {
        const text = button.querySelector('.btn-text') || button;
        const spinner = button.querySelector('.spinner');

        if (text) text.style.display = 'none';
        if (spinner) spinner.classList.remove('hidden');
        button.disabled = true;
    }

    // Скрыть спиннер загрузки
    static hideLoading(button) {
        const text = button.querySelector('.btn-text') || button;
        const spinner = button.querySelector('.spinner');

        if (text) text.style.display = 'inline';
        if (spinner) spinner.classList.add('hidden');
        button.disabled = false;
    }

    // Отображение уведомления
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

        // Добавляем стили
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

        // Кнопка закрытия
        notification.querySelector('.notification-close').style.cssText = `
            background: none;
            border: none;
            color: white;
            font-size: 1.5rem;
            cursor: pointer;
            margin-left: 10px;
        `;

        // Анимация
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

        // Закрытие по клику
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

    // Проверка обязательных полей формы
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

    // Генерация случайного ID
    static generateId() {
        return Date.now().toString(36) + Math.random().toString(36).substr(2);
    }
}

// Глобальные функции для использования в HTML
function checkAuth() {
    if (!Utils.checkAuth()) {
        return false;
    }
    return true;
}

function logout() {
    Utils.clearAuth();
    window.location.href = 'index.html';
}

function updateCartCount() {
    const cart = JSON.parse(localStorage.getItem('cart') || '[]');
    const count = cart.reduce((total, item) => total + item.quantity, 0);

    document.querySelectorAll('.cart-count').forEach(element => {
        element.textContent = count;
    });

    return count;
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = Utils;
}
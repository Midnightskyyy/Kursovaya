// Управление профилем пользователя

class ProfileManager {
    constructor() {
        this.userProfile = null;
        this.addresses = [];
        this.statistics = {
            totalOrders: 0,
            totalSpent: 0,
            avgOrder: 0,
            deliveryCount: 0
        };
    }

    // Загрузка профиля
    async loadProfile() {
        try {
            const response = await ApiClient.getProfile();

            if (response.success) {
                this.userProfile = response.data;
                this.updateProfileDisplay();
            } else {
                throw new Error(response.message);
            }
        } catch (error) {
            console.error('Error loading profile:', error);
            this.loadMockProfile();
        }
    }

    // Загрузка статистики
    async loadProfileStats() {
        try {
            const ordersResponse = await ApiClient.getOrders();

            if (ordersResponse.success) {
                const orders = ordersResponse.data || [];

                this.statistics.totalOrders = orders.length;
                this.statistics.totalSpent = orders.reduce((sum, order) =>
                    sum + (order.totalAmount || 0), 0
                );
                this.statistics.avgOrder = this.statistics.totalOrders > 0
                    ? this.statistics.totalSpent / this.statistics.totalOrders
                    : 0;
                this.statistics.deliveryCount = orders.filter(order =>
                    order.status === 'Delivered'
                ).length;

                this.updateStatsDisplay();
            }
        } catch (error) {
            console.error('Error loading stats:', error);
            this.loadMockStats();
        }
    }

    // Загрузка сохраненных адресов
    async loadSavedAddresses() {
        try {
            // В реальном приложении здесь будет запрос к API серверу
            // Для демонстрации используем мок данные
            this.addresses = [
                {
                    id: '1',
                    title: 'Дом',
                    address: 'ул. Ленина, д. 10, кв. 25',
                    city: 'Москва',
                    postalCode: '123456',
                    details: 'Подъезд 3, этаж 5, квартира 25',
                    isDefault: true
                },
                {
                    id: '2',
                    title: 'Работа',
                    address: 'ул. Пушкина, д. 15, офис 304',
                    city: 'Москва',
                    postalCode: '123457',
                    details: 'Бизнес-центр "Солнечный"',
                    isDefault: false
                }
            ];

            this.updateAddressesDisplay();
        } catch (error) {
            console.error('Error loading addresses:', error);
        }
    }

    // Обновление отображения профиля
    updateProfileDisplay() {
        if (!this.userProfile) return;
         const avatarContainer = document.querySelector('.profile-avatar');
    
    if (this.userProfile.profile?.avatarUrl) {
        const avatarPath = this.userProfile.profile.avatarUrl.replace(/\\/g, '/');
        
        avatarContainer.innerHTML = `
            <img src="${avatarPath}" 
                 alt="Аватар" 
                 class="profile-avatar-img"
                 onerror="this.onerror=null; this.parentElement.innerHTML='<i class=\"fas fa-user-circle\"></i>
        `;
    } else {
        avatarContainer.innerHTML = '<i class="fas fa-user-circle"></i>';
    }

        // Имя и email
        document.getElementById('profileName').textContent =
            `${this.userProfile.profile?.firstName || ''} ${this.userProfile.profile?.lastName || ''}`.trim() ||
            this.userProfile.email;
        document.getElementById('profileEmail').textContent = this.userProfile.email;

        // Дата регистрации
        const joinDate = Utils.parseDate(this.userProfile.createdAt);
document.getElementById('joinDate').textContent =
    joinDate.toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' });

        // Заполнение полей редактирования
        if (this.userProfile.profile) {
            document.getElementById('editFirstName').value = this.userProfile.profile.firstName || '';
            document.getElementById('editLastName').value = this.userProfile.profile.lastName || '';
            document.getElementById('editEmail').value = this.userProfile.email || '';
            document.getElementById('editPhone').value = this.userProfile.phoneNumber || '';
        }
    }

    // Обновление отображения статистики
    updateStatsDisplay() {
        document.getElementById('totalOrders').textContent = this.statistics.totalOrders;
        document.getElementById('totalSpent').textContent = Utils.formatPrice(this.statistics.totalSpent);
        document.getElementById('avgOrder').textContent = Utils.formatPrice(this.statistics.avgOrder);
        document.getElementById('deliveryCount').textContent = this.statistics.deliveryCount;
    }

    // Обновление отображения адресов
    updateAddressesDisplay() {
        const container = document.getElementById('addressesList');
        if (!container) return;

        if (this.addresses.length === 0) {
            container.innerHTML = `
                <div class="no-addresses">
                    <i class="fas fa-map-marker-alt"></i>
                    <p>У вас нет сохраненных адресов</p>
                </div>
            `;
            return;
        }

        container.innerHTML = this.addresses.map(address => `
            <div class="address-card ${address.isDefault ? 'active' : ''}" data-id="${address.id}">
                <div class="address-header">
                    <span class="address-title">${address.title}</span>
                    ${address.isDefault ? '<span class="address-default">По умолчанию</span>' : ''}
                </div>
                <p class="address-details">${address.address}, ${address.city}</p>
                ${address.details ? `<p class="address-extra">${address.details}</p>` : ''}
                <div class="address-actions">
                    <button class="btn btn-text edit-address-btn">
                        <i class="fas fa-edit"></i> Редактировать
                    </button>
                    ${!address.isDefault ? `
                        <button class="btn btn-text set-default-btn">
                            <i class="fas fa-check-circle"></i> Сделать основным
                        </button>
                        <button class="btn btn-text delete-address-btn">
                            <i class="fas fa-trash"></i> Удалить
                        </button>
                    ` : ''}
                </div>
            </div>
        `).join('');

        // Добавление обработчиков событий для адресов
        this.addAddressEventListeners();
    }

    // Переключение режима редактирования профиля
    toggleEditProfile() {
        const form = document.getElementById('editProfileForm');
        const isVisible = form.style.display !== 'none';

        if (isVisible) {
            form.style.display = 'none';
        } else {
            form.style.display = 'block';
            form.scrollIntoView({ behavior: 'smooth' });
        }
    }

    // Сохранение профиля
    async saveProfile(e) {
        e.preventDefault();

        const form = e.target;
        const firstName = form.editFirstName.value.trim();
        const lastName = form.editLastName.value.trim();
        const email = form.editEmail.value.trim();
        const phone = form.editPhone.value.trim();
        const address = form.editAddress.value.trim();

        // Валидация
        if (!Utils.isValidEmail(email)) {
            Utils.showNotification('Введите корректный email', 'error');
            return;
        }

        if (phone && !Utils.isValidPhone(phone)) {
            Utils.showNotification('Введите корректный номер телефона', 'error');
            return;
        }

        // В реальном приложении здесь будет запрос к API обновления профиля
        // Для демонстрации обновляем локальные данные

        if (this.userProfile) {
            this.userProfile.profile = {
                ...this.userProfile.profile,
                firstName: firstName,
                lastName: lastName
            };
            this.userProfile.email = email;
            this.userProfile.phoneNumber = phone;

            // Сохранение адреса доставки в локальное хранилище
            if (address) {
                localStorage.setItem('deliveryAddress', address);
            }
        }

        this.updateProfileDisplay();
        this.toggleEditProfile();

        Utils.showNotification('Профиль успешно обновлен', 'success');
    }

    // Показ модального окна добавления адреса
    showAddAddressModal() {
        document.getElementById('addressModal').style.display = 'flex';
    }

    // Сохранение нового адреса
    async saveNewAddress(e) {
        e.preventDefault();

        const form = e.target;
        const title = form.newAddressTitle.value.trim();
        const address = form.newAddress.value.trim();
        const city = form.newCity.value.trim();
        const postalCode = form.newPostalCode.value.trim();
        const details = form.newAddressDetails.value.trim();

        if (!title || !address || !city) {
            Utils.showNotification('Заполните обязательные поля', 'error');
            return;
        }

        // Создание нового адреса
        const newAddress = {
            id: Utils.generateId(),
            title: title,
            address: address,
            city: city,
            postalCode: postalCode,
            details: details,
            isDefault: this.addresses.length === 0 // Первый адрес становится основным
        };

        // В реальном приложении здесь будет запрос к API
        this.addresses.push(newAddress);

        // Если новый адрес установлен как основной, снимаем флаг с других
        if (newAddress.isDefault) {
            this.addresses.forEach(addr => {
                if (addr.id !== newAddress.id) {
                    addr.isDefault = false;
                }
            });
        }

        this.updateAddressesDisplay();
        document.getElementById('addressModal').style.display = 'none';
        form.reset();

        Utils.showNotification('Новый адрес добавлен', 'success');
    }

    // Добавление обработчиков событий для адресов
    addAddressEventListeners() {
        // Редактирование адреса
        document.querySelectorAll('.edit-address-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const addressId = e.target.closest('.address-card').dataset.id;
                this.editAddress(addressId);
            });
        });

        // Установка адреса по умолчанию
        document.querySelectorAll('.set-default-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const addressId = e.target.closest('.address-card').dataset.id;
                this.setDefaultAddress(addressId);
            });
        });

        // Удаление адреса
        document.querySelectorAll('.delete-address-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const addressId = e.target.closest('.address-card').dataset.id;
                this.deleteAddress(addressId);
            });
        });
    }

    // Редактирование адреса
    editAddress(addressId) {
        const address = this.addresses.find(addr => addr.id === addressId);
        if (!address) return;

        // В реальном приложении здесь будет открытие формы редактирования
        // Для демонстрации показываем алерт
        alert(`Редактирование адреса: ${address.title}\n\nВ реальном приложении здесь будет форма редактирования.`);
    }

    // Установка адреса по умолчанию
    setDefaultAddress(addressId) {
        this.addresses.forEach(addr => {
            addr.isDefault = addr.id === addressId;
        });

        this.updateAddressesDisplay();
        Utils.showNotification('Основной адрес изменен', 'success');
    }

    // Удаление адреса
    deleteAddress(addressId) {
        if (!confirm('Удалить этот адрес?')) return;

        const addressIndex = this.addresses.findIndex(addr => addr.id === addressId);
        if (addressIndex > -1) {
            const wasDefault = this.addresses[addressIndex].isDefault;
            this.addresses.splice(addressIndex, 1);

            // Если удаляемый адрес был основным, назначаем новый (если есть)
            if (wasDefault && this.addresses.length > 0) {
                this.addresses[0].isDefault = true;
            }
        }

        this.updateAddressesDisplay();
        Utils.showNotification('Адрес удален', 'success');
    }

    // Загрузка мок данных для демонстрации
    loadMockProfile() {
        this.userProfile = {
            id: 'user-123',
            email: 'user@example.com',
            phoneNumber: '+7 (999) 123-45-67',
            role: 'Customer',
            createdAt: '2024-01-01T10:00:00Z',
            profile: {
                firstName: 'Иван',
                lastName: 'Иванов',
                avatarUrl: null
            }
        };

        this.updateProfileDisplay();
    }

    // Загрузка мок статистики для демонстрации
    loadMockStats() {
        this.statistics = {
            totalOrders: 15,
            totalSpent: 12500,
            avgOrder: 833,
            deliveryCount: 12
        };

        this.updateStatsDisplay();
    }
}

// Инициализация менеджера профиля
const profileManager = new ProfileManager();

// Функции для интеграции с HTML
function loadProfile() {
    profileManager.loadProfile();
}

function loadProfileStats() {
    profileManager.loadProfileStats();
}

function loadSavedAddresses() {
    profileManager.loadSavedAddresses();
}

function toggleEditProfile() {
    profileManager.toggleEditProfile();
}

function saveProfile(e) {
    profileManager.saveProfile(e);
}

function showAddAddressModal() {
    profileManager.showAddAddressModal();
}

function saveNewAddress(e) {
    profileManager.saveNewAddress(e);
}

// Экспорт для использования в других модулях
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        ProfileManager,
        profileManager,
        loadProfile,
        loadProfileStats,
        loadSavedAddresses,
        toggleEditProfile,
        saveProfile,
        showAddAddressModal,
        saveNewAddress
    };
}
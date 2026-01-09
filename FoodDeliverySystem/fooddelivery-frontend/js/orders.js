// Логика работы с историей заказов

class OrdersManager {
    constructor() {
        this.orders = [];
        this.filteredOrders = [];
        this.currentPage = 1;
        this.ordersPerPage = 10;
        this.currentFilter = 'all';
    }

    // Загрузка заказов
    async loadOrders() {
        try {
            const response = await ApiClient.getOrders();

            if (response.success) {
                this.orders = response.data || [];
                this.filteredOrders = [...this.orders];
                this.displayOrders();
                this.updatePagination();

                if (this.orders.length === 0) {
                    this.showNoOrders();
                }
            } else {
                throw new Error(response.message);
            }
        } catch (error) {
            console.error('Error loading orders:', error);
            this.loadMockOrders();
        }
    }

    // Отображение заказов
    displayOrders() {
        const container = document.getElementById('ordersList');
        if (!container) return;

        // Определяем, какие заказы показывать на текущей странице
        const startIndex = (this.currentPage - 1) * this.ordersPerPage;
        const endIndex = startIndex + this.ordersPerPage;
        const ordersToShow = this.filteredOrders.slice(startIndex, endIndex);

        if (ordersToShow.length === 0) {
            container.innerHTML = `
                <div class="no-orders" id="noOrders">
                    <i class="fas fa-history"></i>
                    <h3>Заказы не найдены</h3>
                    <p>Попробуйте изменить фильтры поиска</p>
                </div>
            `;
            return;
        }

        container.innerHTML = ordersToShow.map(order => `
            <div class="order-card" data-id="${order.id}">
                <div class="order-header">
                    <div class="order-info">
                        <h3>Заказ #${order.id.substring(0, 8)}</h3>
                        <p class="order-date">${Utils.formatDate(order.createdAt)}</p>
                    </div>
                    <span class="order-status ${this.getStatusClass(order.status)}">
                        ${this.getStatusText(order.status)}
                    </span>
                </div>
                
                <div class="order-details-preview">
                    <div class="order-detail">
                        <span>Ресторан:</span>
                        <span>${order.restaurant?.name || 'Не указан'}</span>
                    </div>
                    <div class="order-detail">
                        <span>Адрес доставки:</span>
                        <span>${order.deliveryAddress || 'Не указан'}</span>
                    </div>
                    <div class="order-detail">
                        <span>Сумма заказа:</span>
                        <span>${Utils.formatPrice(order.totalAmount || 0)}</span>
                    </div>
                </div>
                
                <div class="order-items-count">
                    <i class="fas fa-utensils"></i>
                    <span>${order.orderItems?.length || 0} позиций</span>
                </div>
                
                <div class="order-actions">
                    ${order.status !== 'Cancelled' && order.status !== 'Delivered' ? `
                        <button class="btn btn-outline btn-sm cancel-order-btn" data-id="${order.id}">
                            Отменить заказ
                        </button>
                    ` : ''}
                    <button class="btn btn-text btn-sm view-order-btn" data-id="${order.id}">
                        <i class="fas fa-eye"></i> Подробнее
                    </button>
                    
                </div>
            </div>
        `).join('');

        // Добавляем обработчики
        this.addOrderEventListeners();
    }

    // Фильтрация заказов
    filterOrders(filter) {
    this.currentFilter = filter;
    this.currentPage = 1;

    switch (filter) {
        case 'active':
            this.filteredOrders = this.orders.filter(order =>
                order.status === 'Preparing' || 
                order.status === 'PickingUp' || 
                order.status === 'OnTheWay'
            );
            break;
        case 'delivered':
            this.filteredOrders = this.orders.filter(order =>
                order.status === 'Delivered'
            );
            break;
        case 'cancelled':
            this.filteredOrders = this.orders.filter(order =>
                order.status === 'Cancelled'
            );
            break;
        case 'all':
        default:
            this.filteredOrders = [...this.orders];
            break;
    }

    this.displayOrders();
    this.updatePagination();

    if (this.filteredOrders.length === 0) {
        this.showNoOrders();
    }
}

    // Поиск заказов
    searchOrders() {
        const query = document.getElementById('searchOrders').value.trim().toLowerCase();

        if (!query) {
            this.filteredOrders = [...this.orders];
        } else {
            this.filteredOrders = this.orders.filter(order =>
                order.id.toLowerCase().includes(query) ||
                (order.restaurant?.name && order.restaurant.name.toLowerCase().includes(query)) ||
                (order.deliveryAddress && order.deliveryAddress.toLowerCase().includes(query))
            );
        }

        this.currentPage = 1;
        this.displayOrders();
        this.updatePagination();

        if (this.filteredOrders.length === 0) {
            this.showNoOrders();
        }
    }

    // Обновление пагинации
    updatePagination() {
        const container = document.getElementById('pagination');
        if (!container) return;

        const totalPages = Math.ceil(this.filteredOrders.length / this.ordersPerPage);

        if (totalPages <= 1) {
            container.innerHTML = '';
            return;
        }

        let paginationHTML = '';

        // Кнопка "Назад"
        paginationHTML += `
            <button class="pagination-btn prev-btn" ${this.currentPage === 1 ? 'disabled' : ''}>
                <i class="fas fa-chevron-left"></i>
            </button>
        `;

        // Номера страниц
        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= this.currentPage - 2 && i <= this.currentPage + 2)) {
                paginationHTML += `
                    <button class="pagination-btn ${i === this.currentPage ? 'active' : ''}" data-page="${i}">
                        ${i}
                    </button>
                `;
            } else if (i === this.currentPage - 3 || i === this.currentPage + 3) {
                paginationHTML += `<span class="pagination-ellipsis">...</span>`;
            }
        }

        // Кнопка "Вперед"
        paginationHTML += `
            <button class="pagination-btn next-btn" ${this.currentPage === totalPages ? 'disabled' : ''}>
                <i class="fas fa-chevron-right"></i>
            </button>
        `;

        container.innerHTML = paginationHTML;

        // Добавляем обработчики пагинации
        this.addPaginationEventListeners();
    }

    // Переход на страницу
    goToPage(page) {
        this.currentPage = page;
        this.displayOrders();
        this.updatePagination();

        // Прокручиваем к началу списка
        document.getElementById('ordersList').scrollIntoView({ behavior: 'smooth' });
    }

    // Добавление обработчиков для заказов
    addOrderEventListeners() {
        // Просмотр деталей заказа
        document.querySelectorAll('.view-order-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const orderId = btn.dataset.id;
                this.showOrderDetails(orderId);
            });
        });

        // Отмена заказа
        document.querySelectorAll('.cancel-order-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const orderId = btn.dataset.id;
                await this.cancelOrder(orderId);
            });
        });

        // Повтор заказа
        document.querySelectorAll('.repeat-order-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const orderId = btn.dataset.id;
                this.repeatOrder(orderId);
            });
        });
    }

    // Добавление обработчиков пагинации
    addPaginationEventListeners() {
        // Кнопка "Назад"
        const prevBtn = document.querySelector('.prev-btn');
        if (prevBtn) {
            prevBtn.addEventListener('click', () => {
                if (this.currentPage > 1) {
                    this.goToPage(this.currentPage - 1);
                }
            });
        }

        // Кнопка "Вперед"
        const nextBtn = document.querySelector('.next-btn');
        if (nextBtn) {
            nextBtn.addEventListener('click', () => {
                const totalPages = Math.ceil(this.filteredOrders.length / this.ordersPerPage);
                if (this.currentPage < totalPages) {
                    this.goToPage(this.currentPage + 1);
                }
            });
        }

        // Номера страниц
        document.querySelectorAll('.pagination-btn[data-page]').forEach(btn => {
            btn.addEventListener('click', () => {
                const page = parseInt(btn.dataset.page);
                this.goToPage(page);
            });
        });
    }

    // Показ деталей заказа
    async showOrderDetails(orderId) {
        try {
            const response = await ApiClient.getOrder(orderId);

            if (response.success) {
                const order = response.data;
                this.displayOrderModal(order);
            }
        } catch (error) {
            console.error('Error loading order details:', error);

            // Ищем заказ в локальном списке
            const order = this.orders.find(o => o.id === orderId);
            if (order) {
                this.displayOrderModal(order);
            }
        }
    }

    // Отображение модального окна с деталями заказа
    displayOrderModal(order) {
        const modalBody = document.getElementById('orderModalBody');
        if (!modalBody) return;

        modalBody.innerHTML = `
            <div class="order-details-modal">
                <div class="order-details-header">
                    <h2>Заказ #${order.id.substring(0, 8)}</h2>
                    <span class="order-status ${this.getStatusClass(order.status)}">
                        ${this.getStatusText(order.status)}
                    </span>
                </div>
                
                <div class="order-details-info">
                    <div class="detail-section">
                        <h3><i class="fas fa-info-circle"></i> Информация о заказе</h3>
                        <div class="detail-grid">
                            <div class="detail-item">
                                <span class="detail-label">Дата заказа:</span>
                                <span class="detail-value">${Utils.formatDate(order.createdAt)}</span>
                            </div>
                            <div class="detail-item">
                                <span class="detail-label">Ресторан:</span>
                                <span class="detail-value">${order.restaurant?.name || 'Не указан'}</span>
                            </div>
                            <div class="detail-item">
                                <span class="detail-label">Адрес доставки:</span>
                                <span class="detail-value">${order.deliveryAddress || 'Не указан'}</span>
                            </div>
                            <div class="detail-item">
                                <span class="detail-label">Комментарий:</span>
                                <span class="detail-value">${order.specialInstructions || 'Нет комментария'}</span>
                            </div>
                        </div>
                    </div>
                    
                    <div class="detail-section">
                        <h3><i class="fas fa-utensils"></i> Состав заказа</h3>
                        <div class="order-items-details">
                            ${order.orderItems && order.orderItems.length > 0 ?
                order.orderItems.map(item => `
                                    <div class="order-item-detail">
                                        <div class="item-info">
                                            <h4>${item.dishName}</h4>
                                            <p>${item.quantity} × ${Utils.formatPrice(item.unitPrice)}</p>
                                        </div>
                                        <div class="item-total">
                                            ${Utils.formatPrice(item.unitPrice * item.quantity)}
                                        </div>
                                    </div>
                                `).join('') :
                '<p>Информация о товарах отсутствует</p>'
            }
                        </div>
                    </div>
                    
                    <div class="detail-section">
                        <h3><i class="fas fa-receipt"></i> Итоговая сумма</h3>
                        <div class="order-summary">
                            <div class="summary-row">
                                <span>Сумма заказа:</span>
                                <span>${Utils.formatPrice(order.totalAmount || 0)}</span>
                            </div>
                            <div class="summary-row">
                                <span>Доставка:</span>
                                <span>Бесплатно</span>
                            </div>
                            <div class="summary-divider"></div>
                            <div class="summary-total">
                                <span>Итого:</span>
                                <span>${Utils.formatPrice(order.totalAmount || 0)}</span>
                            </div>
                        </div>
                    </div>
                    
                    
                </div>
                
                <div class="order-details-actions">
                    ${order.status !== 'Delivered' && order.status !== 'Cancelled' ? `
                        <button class="btn btn-outline cancel-order-modal-btn" data-id="${order.id}">
                            Отменить заказ
                        </button>
                    ` : ''}
                    <button class="btn btn-primary" id="closeOrderDetailsBtn">
                        Закрыть
                    </button>
                </div>
            </div>
        `;

        // Показываем модальное окно
        document.getElementById('orderModal').style.display = 'flex';

        // Обработчики для модального окна
        const closeBtn = document.getElementById('closeOrderDetailsBtn');
        if (closeBtn) {
            closeBtn.addEventListener('click', () => {
                document.getElementById('orderModal').style.display = 'none';
            });
        }

        // Отмена заказа из модального окна
        const cancelBtn = modalBody.querySelector('.cancel-order-modal-btn');
        if (cancelBtn) {
            cancelBtn.addEventListener('click', async () => {
                await this.cancelOrder(order.id);
                document.getElementById('orderModal').style.display = 'none';
            });
        }

        // Рейтинг заказа
        if (order.status === 'Delivered') {
            const stars = modalBody.querySelectorAll('.stars i');
            let selectedRating = 0;

            stars.forEach(star => {
                star.addEventListener('mouseover', (e) => {
                    const rating = parseInt(e.target.dataset.rating);
                    this.updateStars(stars, rating);
                });

                star.addEventListener('click', (e) => {
                    selectedRating = parseInt(e.target.dataset.rating);
                    this.updateStars(stars, selectedRating);
                });
            });


            // Отправка рейтинга
            const submitBtn = modalBody.querySelector('.submit-rating-btn');
            if (submitBtn) {
                submitBtn.addEventListener('click', () => {
                    if (selectedRating > 0) {
                        Utils.showNotification(`Спасибо за оценку ${selectedRating} звезд!`, 'success');
                    } else {
                        Utils.showNotification('Выберите оценку', 'error');
                    }
                });
            }
        }
    }

    // Обновление звезд рейтинга
    updateStars(stars, rating) {
        stars.forEach((star, index) => {
            if (index < rating) {
                star.className = 'fas fa-star';
                star.style.color = '#fdcb6e';
            } else {
                star.className = 'far fa-star';
                star.style.color = '#dfe6e9';
            }
        });
    }

    // Отмена заказа
    async cancelOrder(orderId) {
        if (!confirm('Вы уверены, что хотите отменить заказ?')) return;

        try {
            const response = await ApiClient.cancelOrder(orderId);

            if (response.success) {
                Utils.showNotification('Заказ успешно отменен', 'success');

                // Обновляем список заказов
                await this.loadOrders();
            } else {
                throw new Error(response.message);
            }
        } catch (error) {
            console.error('Error cancelling order:', error);
            Utils.showNotification('Не удалось отменить заказ', 'error');
        }
    }


    // Показ сообщения об отсутствии заказов
    showNoOrders() {
        document.getElementById('ordersList').innerHTML = '';
        document.getElementById('noOrders').style.display = 'block';
        document.getElementById('pagination').innerHTML = '';
    }

    // Получение текста статуса
    getStatusText(status) {
    const statusMap = {
        'Preparing': 'Готовится',
        'PickingUp': 'Ожидает курьера',
        'OnTheWay': 'В пути',
        'Delivered': 'Доставлен',
        'Cancelled': 'Отменен'
    };

    return statusMap[status] || status;
}

    // Получение класса статуса
    getStatusClass(status) {
    const classMap = {
        'Preparing': 'status-preparing',
        'PickingUp': 'status-pickingup',
        'OnTheWay': 'status-ontheway',
        'Delivered': 'status-delivered',
        'Cancelled': 'status-cancelled'
    };

    return classMap[status] || 'status-preparing';
}

    // Заглушка заказов для тестирования
    loadMockOrders() {
        this.orders = [
            {
                id: 'order-' + Date.now(),
                restaurant: { name: 'Пиццерия "Италия"' },
                deliveryAddress: 'ул. Ленина, д. 10, кв. 25',
                totalAmount: 1250,
                status: 'Delivered',
                createdAt: '2024-01-15T18:30:00Z',
                orderItems: [
                    { dishName: 'Пицца Маргарита', quantity: 1, unitPrice: 450 },
                    { dishName: 'Салат Цезарь', quantity: 2, unitPrice: 320 }
                ]
            },
            {
                id: 'order-' + (Date.now() - 86400000),
                restaurant: { name: 'Бургерная "Американская"' },
                deliveryAddress: 'ул. Пушкина, д. 15',
                totalAmount: 850,
                status: 'OnDelivery',
                createdAt: '2024-01-16T12:45:00Z',
                orderItems: [
                    { dishName: 'Чизбургер', quantity: 2, unitPrice: 350 },
                    { dishName: 'Картофель фри', quantity: 1, unitPrice: 150 }
                ]
            }
        ];

        this.filteredOrders = [...this.orders];
        this.displayOrders();
        this.updatePagination();
    }
}

// Инициализация менеджера заказов
const ordersManager = new OrdersManager();

// Глобальные функции для использования в HTML
function loadOrders() {
    ordersManager.loadOrders();
}

function filterOrders(filter) {
    ordersManager.filterOrders(filter);
}

function searchOrders() {
    ordersManager.searchOrders();
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        OrdersManager,
        ordersManager,
        loadOrders,
        filterOrders,
        searchOrders
    };
}
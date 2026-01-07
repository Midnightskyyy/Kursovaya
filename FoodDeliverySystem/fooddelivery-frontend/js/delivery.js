// Логика отслеживания доставки

class DeliveryManager {
    constructor() {
        this.orders = [];
        this.currentOrder = null;
        this.deliveryStatus = null;
        this.courier = null;
        this.updateInterval = null;
    }

    // Загрузка заказов для отслеживания
    async loadOrdersForTracking() {
        try {
            const response = await ApiClient.getOrders();

            if (response.success) {
                this.orders = response.data || [];
                this.populateOrderSelect();

                // Если есть активные заказы, выбираем первый
                const activeOrder = this.orders.find(order =>
                    order.status === 'Cooking' ||
                    order.status === 'ReadyForPickup' ||
                    order.status === 'OnDelivery'
                );

                if (activeOrder) {
                    document.getElementById('orderSelect').value = activeOrder.id;
                    await this.trackOrder(activeOrder.id);
                } else {
                    this.showNoActiveDeliveries();
                }
            }
        } catch (error) {
            console.error('Error loading orders:', error);
            this.showNoActiveDeliveries();
        }
    }

    // Заполнение выпадающего списка заказов
    populateOrderSelect() {
        const select = document.getElementById('orderSelect');
        if (!select) return;

        // Очищаем старые опции
        select.innerHTML = '<option value="">Выберите заказ для отслеживания</option>';

        // Добавляем активные заказы
        this.orders.forEach(order => {
            const option = document.createElement('option');
            option.value = order.id;
            option.textContent = `Заказ #${order.id.substring(0, 8)} - ${Utils.formatDate(order.createdAt)}`;
            select.appendChild(option);
        });
    }

    // Отслеживание заказа
    async trackOrder(orderId) {
        try {
            // Получаем статус доставки
            const deliveryResponse = await ApiClient.getDeliveryStatus(orderId);

            if (deliveryResponse.success && deliveryResponse.data) {
                this.deliveryStatus = deliveryResponse.data;
                this.currentOrder = this.orders.find(o => o.id === orderId);

                // Получаем информацию о курьере
                if (this.deliveryStatus.courierId) {
                    await this.loadCourierInfo(this.deliveryStatus.courierId);
                }

                this.updateDeliveryDisplay();
                this.showDeliveryCard();
            } else {
                // Если доставка еще не создана, показываем статус заказа
                this.currentOrder = this.orders.find(o => o.id === orderId);
                this.updateOrderStatusDisplay();
                this.showDeliveryCard();
            }

        } catch (error) {
            console.error('Error tracking order:', error);
            Utils.showNotification('Не удалось загрузить информацию о доставке', 'error');
        }
    }

    // Загрузка информации о курьере
    async loadCourierInfo(courierId) {
        try {
            // В реальном приложении здесь будет запрос к API курьеров
            // Для демонстрации используем заглушку
            this.courier = {
                id: courierId,
                name: 'Иван Петров',
                phone: '+7 (999) 123-45-67',
                rating: 4.8,
                vehicleType: 'Мотоцикл',
                deliveriesCompleted: 156
            };
        } catch (error) {
            console.error('Error loading courier info:', error);
            this.courier = null;
        }
    }

    // Обновление отображения статуса доставки
    updateDeliveryDisplay() {
        if (!this.deliveryStatus || !this.currentOrder) return;

        // Заголовок
        document.getElementById('deliveryOrderNumber').textContent =
            `Заказ #${this.currentOrder.id.substring(0, 8)}`;

        // Статус
        const statusElement = document.getElementById('deliveryStatus');
        const statusMap = {
            'Pending': { text: 'Ожидает подтверждения', class: 'status-pending' },
            'Assigned': { text: 'Курьер назначен', class: 'status-assigned' },
            'PickedUp': { text: 'Заказ забран', class: 'status-pickedup' },
            'OnTheWay': { text: 'В пути', class: 'status-ontheway' },
            'Delivered': { text: 'Доставлен', class: 'status-delivered' },
            'Cancelled': { text: 'Отменен', class: 'status-cancelled' }
        };

        const statusInfo = statusMap[this.deliveryStatus.status] || { text: this.deliveryStatus.status, class: 'status-pending' };
        statusElement.textContent = statusInfo.text;
        statusElement.className = 'status-badge ' + statusInfo.class;

        // Обновляем timeline
        this.updateTimeline();

        // Обновляем информацию о курьере
        this.updateCourierInfo();

        // Обновляем детали заказа
        this.updateOrderDetails();
    }

    // Обновление статуса заказа (если доставка еще не создана)
    updateOrderStatusDisplay() {
        if (!this.currentOrder) return;

        document.getElementById('deliveryOrderNumber').textContent =
            `Заказ #${this.currentOrder.id.substring(0, 8)}`;

        const statusMap = {
            'Pending': { text: 'Ожидает подтверждения', class: 'status-pending' },
            'Cooking': { text: 'Готовится', class: 'status-cooking' },
            'ReadyForPickup': { text: 'Готов к выдаче', class: 'status-ready' },
            'OnDelivery': { text: 'В доставке', class: 'status-ontheway' },
            'Delivered': { text: 'Доставлен', class: 'status-delivered' },
            'Cancelled': { text: 'Отменен', class: 'status-cancelled' }
        };

        const statusInfo = statusMap[this.currentOrder.status] || { text: this.currentOrder.status, class: 'status-pending' };
        document.getElementById('deliveryStatus').textContent = statusInfo.text;
        document.getElementById('deliveryStatus').className = 'status-badge ' + statusInfo.class;

        // Обновляем timeline на основе статуса заказа
        this.updateOrderTimeline();

        // Обновляем детали заказа
        this.updateOrderDetails();
    }

    // Обновление timeline доставки
    updateTimeline() {
        if (!this.deliveryStatus) return;

        const timelineSteps = [
            { id: 'step1', time: this.deliveryStatus.createdAt, label: 'Заказ принят' },
            { id: 'step2', time: null, label: 'Готовится' },
            { id: 'step3', time: this.deliveryStatus.assignedAt, label: 'Передан курьеру' },
            { id: 'step4', time: this.deliveryStatus.pickedUpAt, label: 'В пути' },
            { id: 'step5', time: this.deliveryStatus.deliveredAt, label: 'Доставлен' }
        ];

        // Определяем активный шаг на основе статуса
        let activeStep = 1;
        switch (this.deliveryStatus.status) {
            case 'Assigned':
                activeStep = 3;
                break;
            case 'PickedUp':
                activeStep = 4;
                break;
            case 'OnTheWay':
                activeStep = 4;
                break;
            case 'Delivered':
                activeStep = 5;
                break;
        }

        // Обновляем каждый шаг
        timelineSteps.forEach((step, index) => {
            const stepNumber = index + 1;
            const stepElement = document.querySelector(`.timeline-step:nth-child(${stepNumber})`);

            if (stepElement) {
                // Обновляем иконку
                const icon = stepElement.querySelector('.step-icon i');
                if (stepNumber <= activeStep) {
                    stepElement.classList.add('active');
                    if (icon) icon.className = 'fas fa-check-circle';
                } else {
                    stepElement.classList.remove('active');
                    if (icon) icon.className = 'fas fa-circle';
                }

                // Обновляем время
                const timeElement = stepElement.querySelector('.step-time');
                if (timeElement && step.time) {
                    const date = new Date(step.time);
                    timeElement.textContent = date.toLocaleTimeString('ru-RU', {
                        hour: '2-digit',
                        minute: '2-digit'
                    });
                } else if (timeElement && stepNumber === 2 && this.currentOrder) {
                    // Время приготовления
                    const createdAt = new Date(this.currentOrder.createdAt);
                    const readyTime = new Date(createdAt.getTime() + 20 * 60000); // +20 минут
                    timeElement.textContent = readyTime.toLocaleTimeString('ru-RU', {
                        hour: '2-digit',
                        minute: '2-digit'
                    });
                }
            }
        });
    }

    // Обновление timeline заказа
    updateOrderTimeline() {
        if (!this.currentOrder) return;

        let activeStep = 1;
        switch (this.currentOrder.status) {
            case 'Cooking':
                activeStep = 2;
                break;
            case 'ReadyForPickup':
                activeStep = 3;
                break;
            case 'OnDelivery':
                activeStep = 4;
                break;
            case 'Delivered':
                activeStep = 5;
                break;
        }

        // Обновляем каждый шаг
        for (let i = 1; i <= 5; i++) {
            const stepElement = document.querySelector(`.timeline-step:nth-child(${i})`);
            if (stepElement) {
                const icon = stepElement.querySelector('.step-icon i');
                if (i <= activeStep) {
                    stepElement.classList.add('active');
                    if (icon) icon.className = 'fas fa-check-circle';
                } else {
                    stepElement.classList.remove('active');
                    if (icon) icon.className = 'fas fa-circle';
                }
            }
        }

        // Устанавливаем время для шагов
        const createdAt = new Date(this.currentOrder.createdAt);

        // Шаг 1: Заказ принят
        const step1Time = document.getElementById('step1Time');
        if (step1Time) {
            step1Time.textContent = createdAt.toLocaleTimeString('ru-RU', {
                hour: '2-digit',
                minute: '2-digit'
            });
        }

        // Шаг 2: Готовится
        const step2Time = document.getElementById('step2Time');
        if (step2Time) {
            if (activeStep >= 2) {
                const cookingTime = new Date(createdAt.getTime() + 10 * 60000);
                step2Time.textContent = cookingTime.toLocaleTimeString('ru-RU', {
                    hour: '2-digit',
                    minute: '2-digit'
                });
            }
        }

        // Шаг 3: Передан курьеру
        const step3Time = document.getElementById('step3Time');
        if (step3Time && activeStep >= 3) {
            const courierTime = new Date(createdAt.getTime() + 20 * 60000);
            step3Time.textContent = courierTime.toLocaleTimeString('ru-RU', {
                hour: '2-digit',
                minute: '2-digit'
            });
        }
    }

    // Обновление информации о курьере
    updateCourierInfo() {
        const courierInfo = document.getElementById('courierInfo');

        if (this.courier && this.deliveryStatus &&
            (this.deliveryStatus.status === 'Assigned' ||
                this.deliveryStatus.status === 'PickedUp' ||
                this.deliveryStatus.status === 'OnTheWay')) {

            courierInfo.style.display = 'block';

            document.getElementById('courierName').textContent = this.courier.name;
            document.getElementById('courierRating').textContent = this.courier.rating;
            document.getElementById('courierPhone').textContent = this.courier.phone;

            // Обновляем расстояние (заглушка)
            const distance = (Math.random() * 2 + 0.5).toFixed(1);
            document.getElementById('distance').textContent = distance;

        } else {
            courierInfo.style.display = 'none';
        }
    }

    // Обновление деталей заказа
    updateOrderDetails() {
        if (!this.currentOrder) return;

        // Основные детали
        document.getElementById('detailOrderNumber').textContent =
            this.currentOrder.id.substring(0, 8);
        document.getElementById('detailRestaurant').textContent =
            this.currentOrder.restaurant?.name || 'Ресторан';
        document.getElementById('detailAddress').textContent =
            this.currentOrder.deliveryAddress || 'Адрес не указан';
        document.getElementById('detailOrderTime').textContent =
            new Date(this.currentOrder.createdAt).toLocaleTimeString('ru-RU', {
                hour: '2-digit',
                minute: '2-digit'
            });
        document.getElementById('detailAmount').textContent =
            Utils.formatPrice(this.currentOrder.totalAmount || 0);

        // Время доставки
        const createdAt = new Date(this.currentOrder.createdAt);
        const deliveryTime = new Date(createdAt.getTime() + 45 * 60000);
        document.getElementById('detailDeliveryTime').textContent =
            deliveryTime.toLocaleTimeString('ru-RU', {
                hour: '2-digit',
                minute: '2-digit'
            });

        // Способ оплаты
        document.getElementById('detailPayment').textContent = 'Карта онлайн'; // Заглушка

        // Состав заказа
        this.updateOrderItems();
    }

    // Обновление состава заказа
    updateOrderItems() {
        const container = document.getElementById('deliveryOrderItems');
        if (!container || !this.currentOrder) return;

        if (this.currentOrder.orderItems && this.currentOrder.orderItems.length > 0) {
            container.innerHTML = this.currentOrder.orderItems.map(item => `
                <div class="delivery-order-item">
                    <span>${item.dishName} × ${item.quantity}</span>
                    <span>${Utils.formatPrice(item.unitPrice * item.quantity)}</span>
                </div>
            `).join('');
        } else {
            container.innerHTML = '<p>Информация о заказе отсутствует</p>';
        }
    }

    // Показ карточки доставки
    showDeliveryCard() {
        document.getElementById('deliveryCard').style.display = 'block';
        document.getElementById('noDelivery').style.display = 'none';
    }

    // Показ сообщения об отсутствии активных доставок
    showNoActiveDeliveries() {
        document.getElementById('deliveryCard').style.display = 'none';
        document.getElementById('noDelivery').style.display = 'block';
    }

    // Симуляция обновления статуса (для демонстрации)
    simulateStatusUpdate() {
        if (!this.deliveryStatus) return;

        const statusFlow = ['Pending', 'Assigned', 'PickedUp', 'OnTheWay', 'Delivered'];
        const currentIndex = statusFlow.indexOf(this.deliveryStatus.status);

        if (currentIndex < statusFlow.length - 1) {
            // С вероятностью 20% переходим к следующему статусу
            if (Math.random() < 0.2) {
                this.deliveryStatus.status = statusFlow[currentIndex + 1];

                // Устанавливаем временные метки
                const now = new Date();
                switch (this.deliveryStatus.status) {
                    case 'Assigned':
                        this.deliveryStatus.assignedAt = now;
                        // Создаем курьера
                        this.loadCourierInfo('courier-' + Date.now());
                        break;
                    case 'PickedUp':
                        this.deliveryStatus.pickedUpAt = now;
                        break;
                    case 'Delivered':
                        this.deliveryStatus.deliveredAt = now;
                        break;
                }

                this.updateDeliveryDisplay();
                Utils.showNotification('Статус доставки обновлен!', 'success');
            }
        }
    }

    // Начало автоматического обновления
    startAutoUpdate() {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
        }

        this.updateInterval = setInterval(() => {
            if (this.deliveryStatus &&
                this.deliveryStatus.status !== 'Delivered' &&
                this.deliveryStatus.status !== 'Cancelled') {

                this.simulateStatusUpdate();
            }
        }, 30000); // Обновление каждые 30 секунд
    }

    // Остановка автоматического обновления
    stopAutoUpdate() {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
            this.updateInterval = null;
        }
    }
}

// Инициализация менеджера доставки
const deliveryManager = new DeliveryManager();

// Глобальные функции для использования в HTML
function loadOrdersForTracking() {
    deliveryManager.loadOrdersForTracking();
}

function trackOrder(orderId) {
    deliveryManager.trackOrder(orderId);
    deliveryManager.startAutoUpdate();
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        DeliveryManager,
        deliveryManager,
        loadOrdersForTracking,
        trackOrder
    };
}
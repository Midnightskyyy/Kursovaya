// Логика отслеживания доставки
const { DateTime } = luxon;
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
        // Получаем статус доставки со всей информацией
        const deliveryResponse = await ApiClient.getDeliveryStatus(orderId);

        if (deliveryResponse.success && deliveryResponse.data) {
            this.deliveryStatus = deliveryResponse.data;
            this.currentOrder = this.orders.find(o => o.id === orderId);

            // Теперь deliveryStatus содержит все нужные поля:
            // totalMinutes, preparationMinutes, deliveryMinutes, remainingMinutes и т.д.
            console.log('📊 API Response:', {
                totalMinutes: this.deliveryStatus.totalMinutes,
                preparationMinutes: this.deliveryStatus.preparationMinutes,
                deliveryMinutes: this.deliveryStatus.deliveryMinutes,
                estimatedDurationMinutes: this.deliveryStatus.estimatedDurationMinutes,
                status: this.deliveryStatus.status,
                preparationTimeMinutes: this.deliveryStatus.preparationTimeMinutes,
                deliveryTimeMinutes: this.deliveryStatus.deliveryTimeMinutes
            });
            

            if (this.deliveryStatus.preparationTimeMinutes !== undefined) {
                this.deliveryStatus.preparationMinutes = this.deliveryStatus.preparationTimeMinutes;
            }
            if (this.deliveryStatus.deliveryTimeMinutes !== undefined) {
                this.deliveryStatus.deliveryMinutes = this.deliveryStatus.deliveryTimeMinutes;
            }
            if (this.deliveryStatus.estimatedDurationMinutes !== undefined) {
                this.deliveryStatus.totalMinutes = this.deliveryStatus.estimatedDurationMinutes;
            }
            // Отображаем информацию


            this.updateDeliveryDisplay();
            this.updateDeliveryTimer();
            this.updateCourierInfo();
            this.updateTimeline();

            this.showDeliveryCard();
            
            // Запускаем обновление таймера
            this.startTimerUpdates();

        } else {
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
        'Preparing': { text: 'Готовится', class: 'status-preparing' }, // ДОБАВЛЕНО
        'Assigned': { text: 'Курьер назначен', class: 'status-assigned' },
        'PickedUp': { text: 'Заказ забран', class: 'status-pickedup' },
        'OnTheWay': { text: 'В пути', class: 'status-ontheway' },
        'Delivered': { text: 'Доставлен', class: 'status-delivered' },
        'Cancelled': { text: 'Отменен', class: 'status-cancelled' },
        'ReadyForPickup': { text: 'Готов к выдаче', class: 'status-ready' }
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
            'Preparing': { text: 'Готовится', class: 'status-preparing' },
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
        { id: 'step2', time: this.deliveryStatus.preparationStartedAt, label: 'Готовится' },
        { id: 'step3', time: this.deliveryStatus.pickedUpAt, label: 'Ожидает курьера' },
        { id: 'step4', time: this.deliveryStatus.deliveryStartedAt, label: 'В пути' },
        { id: 'step5', time: this.deliveryStatus.deliveredAt, label: 'Доставлен' }
    ];

    let activeStep = 1;
    switch (this.deliveryStatus.status) {
        case 'Preparing':
            activeStep = 2; // Готовится
            break;
        case 'PickingUp':
            activeStep = 3; // Ожидает курьера
            break;
        case 'OnTheWay':
            activeStep = 4; // В пути
            break;
        case 'Delivered':
            activeStep = 5; // Доставлен
            break;
        case 'Cancelled':
            // Для отмененных показываем все шаги, но с серым цветом
            activeStep = 0;
            break;
    }

    timelineSteps.forEach((step, index) => {
        const stepNumber = index + 1;
        const stepElement = document.querySelector(`.timeline-step:nth-child(${stepNumber})`);

        if (stepElement) {
            const icon = stepElement.querySelector('.step-icon i');
            const timeElement = stepElement.querySelector('.step-time');
            
            if (this.deliveryStatus.status === 'Cancelled') {
                // Для отмененных все шаги серые
                stepElement.classList.remove('active');
                stepElement.classList.add('cancelled');
                if (icon) icon.className = 'fas fa-times-circle';
                if (timeElement) timeElement.textContent = 'Отменено';
            } else if (stepNumber <= activeStep) {
                stepElement.classList.add('active');
                stepElement.classList.remove('cancelled');
                if (icon) icon.className = 'fas fa-check-circle';
                
                if (timeElement && step.time) {
                    const date = DateTime.fromISO(step.time).toLocal();
                    timeElement.textContent = date.toLocaleString(DateTime.TIME_SIMPLE);
                } else if (timeElement && stepNumber === 2 && this.deliveryStatus.status === 'Preparing') {
                    // Для шага "Готовится" показываем прогресс
                    const now = DateTime.now().toUTC();
                    const prepStarted = this.deliveryStatus.preparationStartedAt ? 
                        DateTime.fromISO(this.deliveryStatus.preparationStartedAt).toUTC() : now;
                    
                    const elapsedPrep = now.diff(prepStarted, 'minutes').toObject().minutes;
                    const remainingPrep = Math.max(0, this.deliveryStatus.preparationMinutes - elapsedPrep);
                    
                    if (remainingPrep > 0) {
                        timeElement.textContent = `${Math.ceil(remainingPrep)} мин`;
                    } else {
                        timeElement.textContent = 'Готово';
                    }
                }
            } else {
                stepElement.classList.remove('active');
                stepElement.classList.remove('cancelled');
                if (icon) icon.className = 'fas fa-circle';
                
                if (timeElement) {
                    timeElement.textContent = 'Ожидается';
                }
            }
        }
    });

    // Обновляем таймер доставки
    this.updateDeliveryTimer();
}

updateDeliveryTimer() {
    if (!this.deliveryStatus) {
        this.hideTimer();
        return;
    }

    // ДЕБАГ
    console.log('⏰ Timer data:', {
        preparationMinutes: this.deliveryStatus.preparationMinutes,
        deliveryMinutes: this.deliveryStatus.deliveryMinutes,
        totalMinutes: this.deliveryStatus.totalMinutes,
        estimatedDurationMinutes: this.deliveryStatus.estimatedDurationMinutes,
        preparationTimeMinutes: this.deliveryStatus.preparationTimeMinutes,
        deliveryTimeMinutes: this.deliveryStatus.deliveryTimeMinutes,
        remainingMinutes: this.deliveryStatus.remainingMinutes,
        currentPhase: this.deliveryStatus.currentPhase
    });

    // Используем правильные поля
    const remainingMinutes = this.deliveryStatus.remainingMinutes || 0;
    const currentPhase = this.deliveryStatus.currentPhase || 'preparation';
    
    // Пробуем разные варианты имен полей
    let preparationMinutes = this.deliveryStatus.preparationMinutes || 
                            this.deliveryStatus.preparationTimeMinutes || 0;
    let deliveryMinutes = this.deliveryStatus.deliveryMinutes || 
                         this.deliveryStatus.deliveryTimeMinutes || 0;
    let totalMinutes = this.deliveryStatus.totalMinutes || 
                      this.deliveryStatus.estimatedDurationMinutes || 45;

    // Если значения не пришли, используем разумные дефолты
    if (preparationMinutes === 0 && deliveryMinutes === 0 && totalMinutes > 0) {
        // Предполагаем 60%/40% разделение
        preparationMinutes = Math.round(totalMinutes * 0.6);
        deliveryMinutes = totalMinutes - preparationMinutes;
    }

    console.log('🎯 Final timer values:', {
        preparationMinutes,
        deliveryMinutes,
        totalMinutes,
        remainingMinutes,
        currentPhase
    });

    // Обновляем отображение таймера
    this.updateTimerDisplay(currentPhase, remainingMinutes, preparationMinutes, deliveryMinutes, totalMinutes);
    
    // Показываем таймер
    this.showTimer();
}

updateTimerDisplay() {
    if (!this.deliveryStatus) {
        this.hideTimer();
        return;
    }

    // Используем правильные поля
    const remainingMinutes = this.deliveryStatus.remainingMinutes || 0;
    const preparationMinutes = this.deliveryStatus.preparationMinutes || 
                              this.deliveryStatus.preparationTimeMinutes || 0;
    let deliveryMinutes = this.deliveryStatus.deliveryMinutes || 
                         this.deliveryStatus.deliveryTimeMinutes || 0;
    let totalMinutes = this.deliveryStatus.totalMinutes || 
                      this.deliveryStatus.estimatedDurationMinutes || 45;

    console.log('🎯 Timer values:', {
        status: this.deliveryStatus.status,
        preparationMinutes,
        deliveryMinutes,
        totalMinutes,
        remainingMinutes
    });

    // Вызываем соответствующий метод отображения
    const status = this.deliveryStatus.status;
    
    if (status === 'Preparing') {
        this.showPreparationTimer(preparationMinutes, deliveryMinutes, totalMinutes);
    } else if (status === 'PickingUp') {
        this.showPickingUpTimer(deliveryMinutes, totalMinutes);
    } else if (status === 'OnTheWay') {
        this.showDeliveryTimer(deliveryMinutes, totalMinutes);
    } else if (status === 'Delivered') {
        this.showCompletedTimer();
    } else if (status === 'Cancelled') {
        this.showCancelledTimer();
    } else {
        // Статус по умолчанию
        this.showPreparationTimer(preparationMinutes, deliveryMinutes, totalMinutes);
    }
    
    // Обновляем статус
    this.updateTimerStatus(this.deliveryStatus.status, remainingMinutes);
}

showCancelledTimer() {
    // Заказ отменен
    const prepTimer = document.getElementById('preparationTimer');
    const deliveryTimer = document.getElementById('deliveryTimer');
    const totalTimer = document.getElementById('totalTimer');
    
    if (prepTimer) prepTimer.style.display = 'block';
    if (deliveryTimer) deliveryTimer.style.display = 'block';
    if (totalTimer) totalTimer.style.display = 'block';

    // Все прогрессы красные
    const prepCircle = document.querySelector('#preparationTimer .timer-progress');
    const deliveryCircle = document.querySelector('#deliveryTimer .timer-progress');
    const totalCircle = document.querySelector('#totalTimer .timer-progress');
    
    if (prepCircle) {
        prepCircle.style.background = `conic-gradient(#e74c3c 0% 100%, #e0e0e0 100% 100%)`;
    }
    
    if (deliveryCircle) {
        deliveryCircle.style.background = `conic-gradient(#e74c3c 0% 100%, #e0e0e0 100% 100%)`;
    }
    
    if (totalCircle) {
        totalCircle.style.background = `conic-gradient(#e74c3c 0% 100%, #e0e0e0 100% 100%)`;
    }

    // Обновляем время
    const prepTimeElement = document.getElementById('preparationTime');
    const deliveryTimeElement = document.getElementById('deliveryTime');
    const totalTimeElement = document.getElementById('totalTime');
    
    if (prepTimeElement) prepTimeElement.textContent = 'Отменено';
    if (deliveryTimeElement) deliveryTimeElement.textContent = 'Отменено';
    if (totalTimeElement) totalTimeElement.textContent = 'Отменено';
}

showCompletedTimer() {
    // Все завершено
    const prepTimer = document.getElementById('preparationTimer');
    const deliveryTimer = document.getElementById('deliveryTimer');
    const totalTimer = document.getElementById('totalTimer');
    
    if (prepTimer) prepTimer.style.display = 'block';
    if (deliveryTimer) deliveryTimer.style.display = 'block';
    if (totalTimer) totalTimer.style.display = 'block';

    // Все прогрессы на 100%
    const prepCircle = document.querySelector('#preparationTimer .timer-progress');
    const deliveryCircle = document.querySelector('#deliveryTimer .timer-progress');
    const totalCircle = document.querySelector('#totalTimer .timer-progress');
    
    if (prepCircle) {
        prepCircle.style.background = `conic-gradient(#2ecc71 0% 100%, #e0e0e0 100% 100%)`;
    }
    
    if (deliveryCircle) {
        deliveryCircle.style.background = `conic-gradient(#2ecc71 0% 100%, #e0e0e0 100% 100%)`;
    }
    
    if (totalCircle) {
        totalCircle.style.background = `conic-gradient(#2ecc71 0% 100%, #e0e0e0 100% 100%)`;
    }

    // Обновляем время
    const prepTimeElement = document.getElementById('preparationTime');
    const deliveryTimeElement = document.getElementById('deliveryTime');
    const totalTimeElement = document.getElementById('totalTime');
    
    if (prepTimeElement) prepTimeElement.textContent = 'Готово';
    if (deliveryTimeElement) deliveryTimeElement.textContent = 'Доставлено';
    if (totalTimeElement) totalTimeElement.textContent = 'Завершено';
}

showPickingUpTimer(deliveryMinutes, totalMinutes) {
    // Показываем, что готовка завершена, ожидаем курьера
    const prepTimer = document.getElementById('preparationTimer');
    const deliveryTimer = document.getElementById('deliveryTimer');
    const totalTimer = document.getElementById('totalTimer');
    
    if (prepTimer) prepTimer.style.display = 'block';
    if (deliveryTimer) deliveryTimer.style.display = 'none';
    if (totalTimer) totalTimer.style.display = 'block';

    // Готовка завершена - 100%
    const prepCircle = document.querySelector('#preparationTimer .timer-progress');
    if (prepCircle) {
        prepCircle.style.background = `conic-gradient(#2ecc71 0% 100%, #e0e0e0 100% 100%)`;
    }

    // Обновляем время приготовления
    const prepTimeElement = document.getElementById('preparationTime');
    if (prepTimeElement) {
        prepTimeElement.textContent = 'Готово';
    }

    const totalTimeElement = document.getElementById('totalTime');
    if (totalTimeElement) {
        totalTimeElement.textContent = `${deliveryMinutes} мин`;
    }
}

showDeliveryTimer(deliveryMinutes, totalMinutes) {
    const now = DateTime.now().toUTC();
    const deliveryStarted = this.deliveryStatus.deliveryStartedAt ? 
        DateTime.fromISO(this.deliveryStatus.deliveryStartedAt).toUTC() : now;
    
    const elapsedDelivery = now.diff(deliveryStarted, 'minutes').toObject().minutes;
    const remainingDelivery = Math.max(0, deliveryMinutes - elapsedDelivery);
    const deliveryProgress = deliveryMinutes > 0 ? (elapsedDelivery / deliveryMinutes) * 100 : 0;

    // Показываем все таймеры
    const prepTimer = document.getElementById('preparationTimer');
    const deliveryTimer = document.getElementById('deliveryTimer');
    const totalTimer = document.getElementById('totalTimer');
    
    if (prepTimer) prepTimer.style.display = 'block';
    if (deliveryTimer) deliveryTimer.style.display = 'block';
    if (totalTimer) totalTimer.style.display = 'block';

    // Готовка завершена
    const prepCircle = document.querySelector('#preparationTimer .timer-progress');
    if (prepCircle) {
        prepCircle.style.background = `conic-gradient(#2ecc71 0% 100%, #e0e0e0 100% 100%)`;
    }

    // Прогресс доставки
    const deliveryCircle = document.querySelector('#deliveryTimer .timer-progress');
    if (deliveryCircle) {
        deliveryCircle.style.background = `conic-gradient(#9b59b6 0% ${deliveryProgress}%, #e0e0e0 ${deliveryProgress}% 100%)`;
    }

    // Общее время
    const totalRemaining = remainingDelivery;
    const totalProgress = totalMinutes > 0 ? 
        ((totalMinutes - totalRemaining) / totalMinutes) * 100 : 0;
    
    const totalCircle = document.querySelector('#totalTimer .timer-progress');
    if (totalCircle) {
        totalCircle.style.background = `conic-gradient(#f39c12 0% ${totalProgress}%, #e0e0e0 ${totalProgress}% 100%)`;
    }

    // Обновляем время
    const prepTimeElement = document.getElementById('preparationTime');
    if (prepTimeElement) {
        prepTimeElement.textContent = 'Готово';
    }

    const deliveryTimeElement = document.getElementById('deliveryTime');
    if (deliveryTimeElement) {
        deliveryTimeElement.textContent = `${Math.ceil(remainingDelivery)} мин`;
    }

    const totalTimeElement = document.getElementById('totalTime');
    if (totalTimeElement) {
        totalTimeElement.textContent = `${Math.ceil(totalRemaining)} мин`;
    }
}

showPreparationTimer(prepMinutes, deliveryMinutes, totalMinutes) {
    const now = DateTime.now().toUTC();
    const prepStarted = this.deliveryStatus.preparationStartedAt ? 
        DateTime.fromISO(this.deliveryStatus.preparationStartedAt).toUTC() : now;
    
    const elapsedPrep = now.diff(prepStarted, 'minutes').toObject().minutes;
    const remainingPrep = Math.max(0, prepMinutes - elapsedPrep);
    const prepProgress = prepMinutes > 0 ? (elapsedPrep / prepMinutes) * 100 : 0;

    // Показываем только таймер готовки
    const prepTimer = document.getElementById('preparationTimer');
    const deliveryTimer = document.getElementById('deliveryTimer');
    const totalTimer = document.getElementById('totalTimer');
    
    if (prepTimer) prepTimer.style.display = 'block';
    if (deliveryTimer) deliveryTimer.style.display = 'none';
    if (totalTimer) totalTimer.style.display = 'block';

    // Обновляем прогресс готовки
    const prepCircle = document.querySelector('#preparationTimer .timer-progress');
    if (prepCircle) {
        prepCircle.style.background = `conic-gradient(#3498db 0% ${prepProgress}%, #e0e0e0 ${prepProgress}% 100%)`;
    }

    // Обновляем время
    const prepTimeElement = document.getElementById('preparationTime');
    if (prepTimeElement) {
        prepTimeElement.textContent = `${Math.ceil(remainingPrep)} мин`;
    }

    const totalTimeElement = document.getElementById('totalTime');
    if (totalTimeElement) {
        totalTimeElement.textContent = `${totalMinutes} мин`;
    }
}


    updateTimerStatus(status, remainingMinutes) {
        const statusElement = document.getElementById('timerStatus');
        if (!statusElement) return;
        
        const statusMap = {
            'Preparing': 'Заказ готовится',
            'ReadyForPickup': 'Готов к выдаче курьеру',
            'Assigned': 'Курьер назначен',
            'PickedUp': 'Курьер забрал заказ',
            'OnTheWay': 'В пути к вам',
            'Delivered': 'Заказ доставлен!',
            'Cancelled': 'Заказ отменен'
        };
        
        let statusText = statusMap[status] || status;
        
        // Добавляем время если есть
        if (remainingMinutes > 0 && status !== 'Delivered' && status !== 'Cancelled') {
            statusText += ` • Заказ прибудет через ${remainingMinutes} мин`;
        }
        
        statusElement.textContent = statusText;
    }

    updateTimerSteps(currentStatus) {
        // Сбрасываем все шаги
        ['Preparing', 'Pickup', 'Delivering', 'Delivered'].forEach(step => {
            const element = document.getElementById(`step${step}`);
            if (element) {
                element.classList.remove('active');
            }
        });
        
        // Активируем шаги в зависимости от статуса
        const statusSteps = {
            'Preparing': ['Preparing'],
            'ReadyForPickup': ['Preparing'],
            'Assigned': ['Preparing', 'Pickup'],
            'PickedUp': ['Preparing', 'Pickup'],
            'OnTheWay': ['Preparing', 'Pickup', 'Delivering'],
            'Delivered': ['Preparing', 'Pickup', 'Delivering', 'Delivered']
        };
        
        const activeSteps = statusSteps[currentStatus] || [];
        activeSteps.forEach(step => {
            const element = document.getElementById(`step${step}`);
            if (element) {
                element.classList.add('active');
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
    // Обновление информации о курьере
updateCourierInfo() {
    console.log('🔄 Updating courier info...');
    
    // Получаем блок информации о курьере
    const courierInfo = document.getElementById('courierInfo');
    if (!courierInfo) {
        console.error('❌ Element #courierInfo not found in HTML');
        return;
    }
    
    // Проверяем, есть ли данные о курьере
    const hasCourierData = this.deliveryStatus && 
                          this.deliveryStatus.courier && 
                          this.deliveryStatus.courier.name;
    
    console.log('📦 Courier data check:', {
        hasDeliveryStatus: !!this.deliveryStatus,
        hasCourier: !!(this.deliveryStatus && this.deliveryStatus.courier),
        courierData: this.deliveryStatus?.courier,
        status: this.deliveryStatus?.status
    });
    
    // Показываем или скрываем блок в зависимости от статуса
    if (hasCourierData && 
        (this.deliveryStatus.status === 'Assigned' || 
         this.deliveryStatus.status === 'PickingUp' ||
         this.deliveryStatus.status === 'OnTheWay')) {
        
        courierInfo.style.display = 'block';
        console.log('✅ Showing courier info');
        
        // Безопасно обновляем все элементы
        this.updateCourierElement('courierName', this.deliveryStatus.courier.name, 'Курьер');
        this.updateCourierElement('courierPhone', this.deliveryStatus.courier.phoneNumber, 'Номер не указан');
        this.updateCourierElement('courierRating', this.deliveryStatus.courier.rating || '4.5', '4.5');
        this.updateCourierElement('courierVehicle', this.deliveryStatus.courier.vehicleType || 'Транспорт', 'Транспорт');
        
        // Обновляем статус курьера
        const courierStatus = document.getElementById('courierStatus');
        if (courierStatus) {
            courierStatus.textContent = this.deliveryStatus.status === 'OnTheWay' ? 'В пути' : 'Ожидает';
            courierStatus.className = this.deliveryStatus.status === 'OnTheWay' ? 
                'courier-status-active' : 'courier-status-waiting';
        }
        
        // Обновляем аватар
        const courierAvatar = document.getElementById('courierAvatar');
        if (courierAvatar) {
            const iconClass = this.getCourierVehicleIcon(this.deliveryStatus.courier.vehicleType);
            courierAvatar.innerHTML = `<i class="${iconClass}"></i>`;
        }
        
    } else {
        // Скрываем блок, если нет курьера или неподходящий статус
        courierInfo.style.display = 'none';
        console.log('📭 Hiding courier info (no courier data or wrong status)');
    }
}

// Вспомогательный метод для безопасного обновления элементов
updateCourierElement(elementId, value, defaultValue = '') {
    const element = document.getElementById(elementId);
    if (element) {
        element.textContent = value !== undefined && value !== null ? value : defaultValue;
    } else {
        console.warn(`⚠️ Element #${elementId} not found in HTML`);
    }
}


// Получение иконки для типа транспорта
getCourierVehicleIcon(vehicleType) {
    if (!vehicleType) return 'fas fa-user';
    
    const iconMap = {
        'Bicycle': 'fas fa-bicycle',
        'Motorcycle': 'fas fa-motorcycle',
        'Car': 'fas fa-car',
        'Scooter': 'fas fa-scooter',
        'Walking': 'fas fa-walking'
    };
    
    return iconMap[vehicleType] || 'fas fa-user';
}

    // Обновление деталей заказа
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

    // Время доставки - берем из доставки или рассчитываем
    let deliveryTimeElement = document.getElementById('detailDeliveryTime');
    
    if (this.deliveryStatus && this.deliveryStatus.estimatedDeliveryTime) {
        // Используем время доставки из БД
        const estimatedDeliveryTime = new Date(this.deliveryStatus.estimatedDeliveryTime);
        deliveryTimeElement.textContent = estimatedDeliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });
    } else if (this.deliveryStatus && this.deliveryStatus.totalMinutes) {
        // Рассчитываем: время заказа + общее время доставки
        const createdAt = new Date(this.currentOrder.createdAt);
        const deliveryTime = new Date(createdAt.getTime() + this.deliveryStatus.totalMinutes * 60000);
        deliveryTimeElement.textContent = deliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });
    } else {
        // Используем дефолтное время (45 минут)
        const createdAt = new Date(this.currentOrder.createdAt);
        const deliveryTime = new Date(createdAt.getTime() + 45 * 60000);
        deliveryTimeElement.textContent = deliveryTime.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit'
        });
    }

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


    
    showTimer() {
        const timerSection = document.getElementById('deliveryTimerSection');
        if (timerSection) {
            timerSection.style.display = 'block';
        }
    }

    hideTimer() {
        const timerSection = document.getElementById('deliveryTimerSection');
        if (timerSection) {
            timerSection.style.display = 'none';
        }
    }

    startTimerUpdates() {
        // Останавливаем предыдущий интервал если есть
        if (this.timerUpdateInterval) {
            clearInterval(this.timerUpdateInterval);
        }
        
        // Обновляем таймер каждую минуту
        this.timerUpdateInterval = setInterval(() => {
            if (this.deliveryStatus && this.deliveryStatus.status !== 'Delivered') {
                this.updateDeliveryTimer();
            }
        }, 60000); // 1 минута
    }

    stopTimerUpdates() {
        if (this.timerUpdateInterval) {
            clearInterval(this.timerUpdateInterval);
            this.timerUpdateInterval = null;
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